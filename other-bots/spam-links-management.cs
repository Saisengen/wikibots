using System.Xml;
using System.IO;
using DotNetWikiBot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using PCRE;
using System.Threading;

class Program
{
    static void Main(string[] args)
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
        //var links = new Dictionary<string, int>();
        //var regexes = new Dictionary<string, int>();
        var spamlinksonpage = new HashSet<string>();
        var pageids = new HashSet<string>();
        var pagenames = new Dictionary<string, string>();
        var requeststrings = new HashSet<string>();
        int c = 0, numofarts;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        string token = "";
        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&meta=tokens&type=csrf%7Crollback"))))
            while (r.Read())
                if (r.Name == "tokens")
                    token = Uri.EscapeDataString(r.GetAttribute("csrftoken"));
        using (var rd = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&meta=siteinfo&format=xml&siprop=statistics"))))
            while (rd.Read())
                if (rd.Name == "statistics")
                    numofarts = Convert.ToInt32(rd.GetAttribute("articles"));

        string apiout, cont = "", query = "/w/api.php?action=query&list=allpages&format=xml&apfrom=Green_Day&apnamespace=0&apfilterredir=nonredirects&aplimit=max";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&apcontinue=" + Uri.EscapeDataString(cont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                while (r.Read())
                    if (r.Name == "p")
                    {
                        string pid = r.GetAttribute("pageid");
                        pageids.Add(pid);
                        pagenames.Add(pid, r.GetAttribute("title"));
                    }
            }
        }

        string idset = "", id = "";
        query = "/w/api.php?action=query&prop=extlinks&format=xml&ellimit=max&pageids=";
        foreach (var p in pageids)
        {
            idset += "|" + p;
            if (++c % 500 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        requeststrings.Add(idset.Substring(1));

        //var result1 = new StreamWriter("result1.txt");
        c = 0;
        foreach (var q in requeststrings)
        {
            cont = "";
            while (cont != null)
            {
                apiout = (cont == "" ? site.GetWebPage(query + q) : site.GetWebPage(query + q + "&eloffset=" + cont));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eloffset");
                    while (r.Read())
                    {
                        if (r.Name == "page")
                        {
                            if (r.NodeType == XmlNodeType.EndElement && spamlinksonpage.Count != 0)
                            {
                                string starttext = Encoding.UTF8.GetString(cl.DownloadData("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(pagenames[id]) + "?action=raw"));//page.text;
                                string text = starttext;
                                string newtemplate = "{{спам-ссылки|1=";
                                if (spamtemplatergx.IsMatch(starttext))
                                {
                                    string oldtemplate = spamtemplatergx.Match(starttext).Groups[0].ToString();
                                    text = text.Replace(oldtemplate, "");
                                    var links = spamtemplatergx.Match(starttext).Groups[1].ToString().Split('\n');
                                    foreach (var l in links)
                                        newtemplate += "\n*" + l;
                                }
                                foreach (var link in spamlinksonpage)
                                {
                                    string brokenlink = link.Substring(link.IndexOf("//") + 2);
                                    text = text.Replace(link, brokenlink);
                                    newtemplate += "\n* " + brokenlink;
                                }
                                if (starttext != text)
                                    try
                                    {
                                        site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "pageid=" + id + "&summary=[[ВП:Форум/Архив/Общий/2020/03#Решение проблемы со спам-ссылками в статьях|уведомление о проблемных ссылках в статье]]" +
                                            "&text=" + Uri.EscapeDataString(text + "\n\n" + newtemplate + "\n}}") + "&token=" + token);
                                    }
                                    catch { }
                                spamlinksonpage.Clear();
                            }
                            if (r.NodeType == XmlNodeType.Element && r.GetAttribute("missing") == null)
                                id = r.GetAttribute("pageid");
                        }
                        if (r.NodeType == XmlNodeType.Element && r.Name == "el")
                        {
                            r.Read();
                            bool match = false;
                            var rgx = new PcreRegex("");
                            string shortlink = r.Value;
                            shortlink = shortlink.Substring(shortlink.IndexOf("//") + 2);
                            shortlink = shortlink.IndexOf("/") == -1 ? shortlink : shortlink.Substring(0, shortlink.IndexOf("/"));
                            foreach (var br in blackrgx)
                                if (br.IsMatch(shortlink))
                                {
                                    match = true;
                                    rgx = br;
                                    break;
                                }
                            if (match)
                                foreach (var wr in whitergx)
                                    if (wr.IsMatch(shortlink))
                                    {
                                        match = false;
                                        rgx = null;
                                        break;
                                    }
                            if (match && r.Value.Contains("goo.gl"))
                                match = false;
                            if (match && !spamlinksonpage.Contains(r.Value) && site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=u:MBH/test&text=" + Uri.EscapeDataString(r.Value) + "&token=" + token).Contains("spamblacklist"))
                                spamlinksonpage.Add(r.Value);
                            //{
                            //    string longlink = r.Value;
                            //    longlink = longlink.Substring(longlink.IndexOf("//") + 2);
                            //    longlink = longlink.IndexOf("/") == -1 ? longlink : longlink.Substring(0, longlink.LastIndexOf("/"));
                            //    if (links.ContainsKey(longlink))
                            //        links[longlink]++;
                            //    else
                            //        links.Add(longlink, 1);
                            //    string rgxs = rgx.ToString();
                            //    if (regexes.ContainsKey(rgxs))
                            //        regexes[rgxs]++;
                            //    else
                            //        regexes.Add(rgxs, 1);
                            //    result1.WriteLine(pagenames[id] + "\t" + r.Value + "\t" + rgxs);
                            //}
                        }
                    }
                }
            }
        }
        //result1.Close();

        //var result2 = new StreamWriter("result2.txt");
        //foreach (var l in links.OrderByDescending(l => l.Value))
        //    result2.WriteLine(l.Key + "\t" + l.Value);
        //result2.Close();

        //var result3 = new StreamWriter("result3.txt");
        //foreach (var r in regexes.OrderByDescending(l => l.Value))
        //    result3.WriteLine(r.Key + "\t" + r.Value);
        //result3.Close();
    }
}
