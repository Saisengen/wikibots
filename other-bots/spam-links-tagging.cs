using System.Xml;
using System.IO;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using PCRE;
using System.Net.Http;

class Program
{
    static string[] creds;
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login.Contains("@") ? login.Substring(0, login.IndexOf('@')) : login);
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static string Save(HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return "";
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action");
        request.Add(new StringContent("1"), "bot");
        request.Add(new StringContent(title), "title");
        request.Add(new StringContent(text), "text");
        request.Add(new StringContent(comment), "summary");
        request.Add(new StringContent(token), "token");
        request.Add(new StringContent("xml"), "format");
        return site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static void Main()
    {
        var cl = new WebClient();
        string rawblacklist = Encoding.UTF8.GetString(cl.DownloadData("https://meta.wikimedia.org/wiki/Spam_blacklist?action=raw"));
        rawblacklist += Encoding.UTF8.GetString(cl.DownloadData("https://ru.wikipedia.org/wiki/MediaWiki:Spam-blacklist?action=raw"));
        string rawwhitelist = Encoding.UTF8.GetString(cl.DownloadData("https://ru.wikipedia.org/wiki/MediaWiki:Spam-whitelist?action=raw"));
        var blacklist = rawblacklist.Split('\n');
        var whitelist = rawwhitelist.Split('\n');
        var blackrgx = new HashSet<PcreRegex>();
        var whitergx = new HashSet<PcreRegex>();
        var spamtemplatergx = new PcreRegex(@"\{\{спам-ссылки\|1?=?([^}]*)\|?2?=?1?\}\}");
        foreach (string b in blacklist)
        {
            string current = b;
            if (current.Contains("#"))
                current = current.Substring(0, current.IndexOf("#")).Trim();
            if (current != "")
                blackrgx.Add(new PcreRegex(current, PcreOptions.IgnoreCase));
        }
        foreach (var w in whitelist)
        {
            string current = w;
            if (current.Contains("#"))
                current = current.Substring(0, current.IndexOf("#")).Trim();
            if (current != "")
                whitergx.Add(new PcreRegex(current, PcreOptions.IgnoreCase));
        }
        var spamlinksonpage = new HashSet<string>();
        var pageids = new HashSet<string>();
        var pagenames = new Dictionary<string, string>();
        var requeststrings = new HashSet<string>();
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var bot = Site(creds[0], creds[1]);
        var nonbot = Site(creds[6], creds[7]);

        string dir = DateTime.Now.Month % 2 == 0 ? "ascending" : "descending";
        string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allpages&format=xml&apnamespace=0&apfilterredir=nonredirects&aplimit=max&apdir=" + dir;//&apfrom=Гладков, Фёдор Васильевич
        while (cont != null)
        {
            apiout = (cont == "" ? bot.GetStringAsync(query).Result : bot.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                while (r.Read())
                    if (r.Name == "p")
                    {
                        string pid = r.GetAttribute("pageid");
                        pageids.Add(pid);
                        try
                        {
                            pagenames.Add(pid, r.GetAttribute("title"));
                        }
                        catch
                        {
                            Console.WriteLine(pagenames[pid]);
                            Console.WriteLine(r.GetAttribute("title"));
                        }
                    }
            }
        }

        string idset = "", id = "";
        int c = 0;
        query = "https://ru.wikipedia.org/w/api.php?action=query&prop=extlinks&format=xml&ellimit=max&pageids=";
        foreach (var p in pageids)
        {
            idset += "|" + p;
            if (++c % 500 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        if (idset.Length > 0)
            requeststrings.Add(idset.Substring(1));

        foreach (var q in requeststrings)
        {
            string title = "";
            cont = "";
            while (cont != null)
            {
                apiout = (cont == "" ? bot.GetStringAsync(query + q).Result : bot.GetStringAsync(query + q + "&eloffset=" + cont).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eloffset");
                    while (r.Read())
                    {
                        if (r.Name == "page")
                        {
                            var domains = new HashSet<string>();
                            title = r.GetAttribute("title");
                            if (r.NodeType == XmlNodeType.EndElement && spamlinksonpage.Count != 0)
                            {
                                string summary = "[[ВП:Форум/Архив/Общий/2020/03#Решение проблемы со спам-ссылками в статьях|спам-ссылки]]: ";
                                string starttext = bot.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(pagenames[id]) + "?action=raw").Result;
                                string text = starttext;
                                string newtemplate = "{{спам-ссылки|1=";
                                if (spamtemplatergx.IsMatch(starttext))
                                {
                                    string oldtemplate = spamtemplatergx.Match(starttext).Groups[0].ToString();
                                    text = text.Replace(oldtemplate, "");
                                    var links = spamtemplatergx.Match(starttext).Groups[1].ToString().Split('\n');
                                    foreach (var l in links)
                                        newtemplate += "\n" + l;
                                }
                                foreach (var link in spamlinksonpage)
                                    if (text.Contains(link))//there are links from WD in infoboxes
                                    {
                                        string brokenlink = link.Replace("http://", "").Replace("https://", "");
                                        text = text.Replace(link, brokenlink);
                                        newtemplate += "\n* " + link;
                                        string domain = brokenlink.Contains("/") ? brokenlink.Substring(0, brokenlink.IndexOf('/')) : brokenlink;
                                        if (!domains.Contains(domain))
                                            domains.Add(domain);
                                    }
                                foreach (var domain in domains)
                                    summary += domain + ", ";
                                if (starttext != text)
                                    try
                                    {
                                        Save(bot, pagenames[id], text + "\n\n" + newtemplate + "\n}}", summary.Substring(0, summary.Length - 2));
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(pagenames[id] + newtemplate);
                                    }
                                spamlinksonpage.Clear();
                            }
                            if (r.NodeType == XmlNodeType.Element && r.GetAttribute("missing") == null)
                                id = r.GetAttribute("pageid");
                        }
                        if (r.NodeType == XmlNodeType.Element && r.Name == "el")
                        {
                            r.Read();
                            bool match = false;
                            string link = r.Value;
                            foreach (var br in blackrgx)
                                if (br.IsMatch(link))
                                {
                                    match = true;
                                    break;
                                }
                            if (match)
                                foreach (var wr in whitergx)
                                    if (wr.IsMatch(link))
                                    {
                                        match = false;
                                        break;
                                    }
                            if (match && !spamlinksonpage.Contains(r.Value) && !r.Value.Contains("youtu.be") && Save(nonbot, "u:MBH/test", "[[" + title + "]] " + r.Value, "[[" + title + "]] " + r.Value).Contains("spamblacklist"))
                                spamlinksonpage.Add(r.Value);
                        }
                    }
                }
            }
        }
    }
}
