using System.Xml;
using System.IO;
using System;
using System.Collections.Generic;
using System.Net;
using PCRE;
using System.Net.Http;
using System.Linq;
using Newtonsoft.Json;
public class Root { public string domain; public string notes; public string addedBy; }
class Program
{
    static HttpClient login(string login, string password, string ua)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", ua);
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument(); doc.LoadXml(result.Content
            .ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value; result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new
                FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result; return client;
    }
    static string Save(HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value; var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action"); request.Add(new StringContent(title), "title"); request.Add(new StringContent(text), "text"); request.Add(new StringContent(comment), "summary");
        request.Add(new StringContent("xml"), "format"); request.Add(new StringContent(token), "token"); return site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static void Main() {
        var new_spamlinks_on_page = new HashSet<string>(); var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var bot = login(creds[0], creds[1], creds[3]); var nonbot = login(creds[4], creds[5], creds[3]); var blackrgx = new List<PcreRegex>(); var whitergx = new List<PcreRegex>();
        string rawblacklist = bot.GetStringAsync("https://meta.wikimedia.org/wiki/Spam_blacklist?action=raw").Result;
        rawblacklist += bot.GetStringAsync("https://ru.wikipedia.org/wiki/MediaWiki:Spam-blacklist?action=raw").Result;
        foreach (var item in JsonConvert.DeserializeObject<List<Root>>(bot.GetStringAsync("https://ru.wikipedia.org/wiki/MediaWiki:BlockedExternalDomains.json?action=raw").Result))
            rawblacklist += item.domain.Replace(".", "\\.") + "\n";
        string rawwhitelist = bot.GetStringAsync("https://ru.wikipedia.org/wiki/MediaWiki:Spam-whitelist?action=raw").Result; var blacklist = rawblacklist.Split('\n'); var whitelist = rawwhitelist.Split('\n');
        var spam_template_rgx = new PcreRegex(@"\n*\{\{спам-ссылки\|1?=?([^}]*)\|?2?=?1?\}\}"); var too_many_stars_rgx = new PcreRegex(@"^\*{2,}"); //var start = new StreamReader("spamstart.txt").ReadLine();
        foreach (string b in blacklist.OrderBy(b => b)) {
            string current = b; if (current.Contains("#")) current = current.Substring(0, current.IndexOf("#")).Trim();
            if (current != "") blackrgx.Add(new PcreRegex(current, PcreOptions.IgnoreCase));
        }
        foreach (var w in whitelist) {
            string current = w; if (current.Contains("#")) current = current.Substring(0, current.IndexOf("#")).Trim();
            if (current != "") whitergx.Add(new PcreRegex(current, PcreOptions.IgnoreCase));
        }
        string cont = "", id = "", idset = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allpages&format=xml&apfilterredir=nonredirects&aplimit=max",//&apfrom=" + start;
            query2 = "https://ru.wikipedia.org/w/api.php?action=query&prop=extlinks&format=xml&ellimit=max&pageids=";
        while (cont != null) {
            var r = new XmlTextReader(new StringReader(cont == "" ? bot.GetStringAsync(query).Result : bot.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result));
            r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
            while (r.Read())
                if (r.Name == "p") {
                    string pid = r.GetAttribute("pageid"), title = r.GetAttribute("title"), ns = r.GetAttribute("ns"); string cont2 = "";
                    var r2 = new XmlTextReader(new StringReader(cont2 == "" ? bot.GetStringAsync(query2 + pid).Result : bot.GetStringAsync(query2 + pid + "&eloffset=" + cont2).Result));
                    r2.Read(); r2.Read(); r2.Read(); cont2 = r2.GetAttribute("eloffset"); while (r2.Read()) {
                        if (r2.Name == "page") {
                            var domains = new HashSet<string>();
                            if (r2.NodeType == XmlNodeType.EndElement && new_spamlinks_on_page.Count != 0) {
                                string summary = "[[ВП:Форум/Архив/Общий/2020/03#Решение проблемы со спам-ссылками в статьях|спам-ссылки]]: ";
                                string page_text = bot.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(title) + "?action=raw").Result;
                                string newtemplate = "\n{{спам-ссылки|1="; var newstrings = new HashSet<string>(); if (spam_template_rgx.IsMatch(page_text)) {
                                    string oldtemplate = spam_template_rgx.Match(page_text).Groups[0].ToString();
                                    var old_link_strings_raw = spam_template_rgx.Match(page_text).Groups[1].ToString().Split('\n');
                                    page_text = page_text.Replace(oldtemplate, "");
                                    foreach (var oldstring in old_link_strings_raw)
                                        if (oldstring != "") {
                                            string newstring = oldstring;
                                            if (newstring.EndsWith("/")) newstring = newstring.Substring(0, newstring.Length - 1); if (newstring.StartsWith("http://")) newstring = newstring.Substring(7);
                                            if (newstring.StartsWith("https://")) newstring = newstring.Substring(8); if (too_many_stars_rgx.IsMatch(newstring)) newstring = too_many_stars_rgx.Replace(newstring, "*");
                                            if (!newstrings.Contains(newstring)) newstrings.Add(newstring);
                                        }
                                    foreach (var newstring in newstrings)
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
                                    try { Save(bot, title, page_text + (ns == "0" ? newtemplate + "}}" : ""), summary.Substring(0, summary.Length - 2)); } catch { }
                                new_spamlinks_on_page.Clear();
                            }
                            if (r2.NodeType == XmlNodeType.Element && r2.GetAttribute("missing") == null) id = r2.GetAttribute("pageid");
                        }
                        if (r2.NodeType == XmlNodeType.Element && r2.Name == "el") {
                            r2.Read(); bool match = false; string link = r2.Value;
                            foreach (var br in blackrgx)
                                if (br.IsMatch(link)) { match = true; break; }
                            if (match)
                                foreach (var wr in whitergx)
                                    if (wr.IsMatch(link)) { match = false; break; }
                            if (match && !new_spamlinks_on_page.Contains(r2.Value)) {
                                string answer = Save(nonbot, "u:MBH/test", "[[" + title + "]] " + r2.Value, "[[" + title + "]] " + r2.Value);
                                if (answer.Contains("spamblacklist") || answer.Contains("blocked-domains"))
                                    new_spamlinks_on_page.Add(r2.Value);
                            }
                        }
                    }
                }
        }
    }
}
