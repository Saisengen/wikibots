using System.Xml;
using System.IO;
using System;
using System.Collections.Generic;
using System.Net;
using PCRE;
using System.Net.Http;
using System.Linq;
class Program
{
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login.Contains("@") ? login.Substring(0, login.IndexOf('@')) : login);
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        var doc = new XmlDocument(); doc.LoadXml(result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password },
            { "lgtoken", logintoken }, { "format", "xml" } })).Result; return client;
    }
    static string Save(HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value; var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action"); request.Add(new StringContent(title), "title"); request.Add(new StringContent(text), "text"); request.Add(new StringContent(comment), "summary");
        request.Add(new StringContent(token), "token"); return site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static void Main()
    {
        var new_spamlinks_on_page = new HashSet<string>(); var pagenames = new Dictionary<string, string>(); var requeststrings = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var bot = Site(creds[0], creds[1]); var nonbot = Site(creds[3], creds[4]); string rawblacklist = bot.GetStringAsync("https://meta.wikimedia.org/wiki/Spam_blacklist?action=raw").Result;
        rawblacklist += bot.GetStringAsync("https://ru.wikipedia.org/wiki/MediaWiki:Spam-blacklist?action=raw").Result;
        string rawwhitelist = bot.GetStringAsync("https://ru.wikipedia.org/wiki/MediaWiki:Spam-whitelist?action=raw").Result;
        var blacklist = rawblacklist.Split('\n'); var whitelist = rawwhitelist.Split('\n'); var blackrgx = new HashSet<PcreRegex>(); var whitergx = new HashSet<PcreRegex>();
        var spam_template_rgx = new PcreRegex(@"\n*\{\{спам-ссылки\|1?=?([^}]*)\|?2?=?1?\}\}"); var too_many_stars_rgx = new PcreRegex(@"^\*{2,}"); var start = new StreamReader("spamstart.txt").ReadLine();
        foreach (string b in blacklist.OrderBy(b => b)) {
            string current = b;
            if (current.Contains("#")) current = current.Substring(0, current.IndexOf("#")).Trim();
            if (current != "") blackrgx.Add(new PcreRegex(current, PcreOptions.IgnoreCase));
        }
        foreach (var w in whitelist) {
            string current = w;
            if (current.Contains("#")) current = current.Substring(0, current.IndexOf("#")).Trim();
            if (current != "") whitergx.Add(new PcreRegex(current, PcreOptions.IgnoreCase));
        }
        string apiout, cont = "", id="", idset="", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allpages&format=xml&apnamespace=0&apfilterredir=nonredirects&aplimit=max&apfrom=" + start;
        while (cont != null) {
            apiout = (cont == "" ? bot.GetStringAsync(query).Result : bot.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout))) {
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                while (r.Read())
                    if (r.Name == "p") {
                        string pid = r.GetAttribute("pageid"); try { pagenames.Add(pid, r.GetAttribute("title")); } catch { Console.WriteLine(pagenames[pid]); Console.WriteLine(r.GetAttribute("title")); }
                    }
            }
        }
        int c = 0; foreach (var p in pagenames.Keys) { idset += "|" + p; if (++c % 500 == 0) { requeststrings.Add(idset.Substring(1)); idset = ""; } }
        if (idset.Length > 0) requeststrings.Add(idset.Substring(1));

        query = "https://ru.wikipedia.org/w/api.php?action=query&prop=extlinks&format=xml&ellimit=max&pageids="; foreach (var q in requeststrings) {
            string title = ""; cont = ""; while (cont != null) {
                apiout = (cont == "" ? bot.GetStringAsync(query + q).Result : bot.GetStringAsync(query + q + "&eloffset=" + cont).Result);
                using (var r = new XmlTextReader(new StringReader(apiout))) {
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eloffset"); while (r.Read()) {
                        if (r.Name == "page") {
                            var domains = new HashSet<string>();
                            title = r.GetAttribute("title");
                            if (r.NodeType == XmlNodeType.EndElement && new_spamlinks_on_page.Count != 0) {
                                string summary = "[[ВП:Форум/Архив/Общий/2020/03#Решение проблемы со спам-ссылками в статьях|спам-ссылки]]: ";
                                string page_text = bot.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(pagenames[id]) + "?action=raw").Result;
                                string newtemplate = "{{спам-ссылки|1=";
                                var newstrings = new HashSet<string>();
                                if (spam_template_rgx.IsMatch(page_text)) {
                                    string oldtemplate = spam_template_rgx.Match(page_text).Groups[0].ToString();
                                    var old_link_strings_raw = spam_template_rgx.Match(page_text).Groups[1].ToString().Split('\n');
                                    page_text = page_text.Replace(oldtemplate, "");
                                    foreach (var oldstring in old_link_strings_raw)
                                        if (oldstring != "") {
                                            string newstring = oldstring;
                                            if (newstring.EndsWith("/")) newstring = newstring.Substring(0, newstring.Length - 1);
                                            if (newstring.StartsWith("http://")) newstring = newstring.Substring(7);
                                            if (newstring.StartsWith("https://")) newstring = newstring.Substring(8);
                                            if (too_many_stars_rgx.IsMatch(newstring)) newstring = too_many_stars_rgx.Replace(newstring, "*");
                                            if (!newstrings.Contains(newstring)) newstrings.Add(newstring);
                                        }
                                    foreach(var newstring in newstrings)
                                        newtemplate += "\n" + newstring;
                                }
                                foreach (var link in new_spamlinks_on_page)
                                    if (page_text.Contains(link)) {//there are links from WD in infoboxes
                                        string brokenlink = link.Replace("http://", "").Replace("https://", "");
                                        if (brokenlink.EndsWith("/")) brokenlink = brokenlink.Substring(0, brokenlink.Length - 1);
                                        page_text = page_text.Replace(link, brokenlink);
                                        bool same = false; foreach (var newstring in newstrings) if (newstring == brokenlink) same = true;
                                        if (!same) {
                                            newtemplate += "\n* " + brokenlink; string domain = brokenlink.Contains("/") ? brokenlink.Substring(0, brokenlink.IndexOf('/')) : brokenlink;
                                            if (!domains.Contains(domain)) domains.Add(domain);
                                        }
                                    }
                                foreach (var domain in domains)
                                    summary += domain + ", ";
                                if (new_spamlinks_on_page.Count > 0 && domains.Count > 0)
                                    try { Save(bot, pagenames[id], page_text + "\n" + newtemplate + "}}", summary.Substring(0, summary.Length - 2)); }
                                    catch { Console.WriteLine(pagenames[id] + newtemplate); }
                                new_spamlinks_on_page.Clear();
                            }
                            if (r.NodeType == XmlNodeType.Element && r.GetAttribute("missing") == null) id = r.GetAttribute("pageid");
                        }
                        if (r.NodeType == XmlNodeType.Element && r.Name == "el") {
                            r.Read(); bool match = false; string link = r.Value;
                            foreach (var br in blackrgx)
                                if (br.IsMatch(link)) { match = true; break; }
                            if (match)
                                foreach (var wr in whitergx)
                                    if (wr.IsMatch(link)) { match = false; break; }
                            if (match && !new_spamlinks_on_page.Contains(r.Value)) {
                                string answer = Save(nonbot, "u:MBH/test", "[[" + title + "]] " + r.Value, "[[" + title + "]] " + r.Value);
                                if (answer.Contains("spamblacklist") || answer.Contains("abusefilter-blocked-domains-attempted"))
                                    new_spamlinks_on_page.Add(r.Value);
                            }
                        }
                    }
                }
            }
        }
    }
}
