using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using DotNetWikiBot;

class Program
{
    static void Main()
    {
        var links = new Dictionary<string, int>();
        var shortenedlinks = new Dictionary<string, int>();
        var pages = new HashSet<string>();
        var pagecounts = new Dictionary<string, int>();
        var pagenames = new Dictionary<string, string>();
        var requeststrings = new HashSet<string>();
        int c = 0; string testurl;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);

        string cont = "", query = "/w/api.php?action=query&list=allpages&format=xml&apnamespace=0&apfilterredir=nonredirects&aplimit=max&rawcontinue=";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&apcontinue=" + Uri.EscapeDataString(cont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                while (r.Read())
                    if (r.Name == "p")
                    {
                        string pid = r.GetAttribute("pageid");
                        pages.Add(pid);
                        pagenames.Add(pid, r.GetAttribute("title"));
                        pagecounts.Add(pid, 0);
                    }
            }
        }

        string idset = "", id = "";
        query = "/w/api.php?action=query&prop=extlinks&format=xml&ellimit=5000&pageids=";
        foreach (var p in pages)
        {
            idset += "|" + p;
            if (++c % 500 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        requeststrings.Add(idset.Substring(1));

        c = 0;
        foreach (var q in requeststrings)
        {
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetWebPage(query + q) : site.GetWebPage(query + q + "&eloffset=" + cont));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eloffset");
                    while (r.Read())
                    {
                        if (r.NodeType == XmlNodeType.Element && r.Name == "page" && r.GetAttribute("missing") == null)
                            id = r.GetAttribute("pageid");
                        if (r.NodeType == XmlNodeType.Element && r.Name == "el")
                        {
                            r.Read();
                            string str = r.Value;
                            str = str.Substring(str.IndexOf("//") + 2);
                            str = str.IndexOf("/") == -1 ? str : str.Substring(0, str.LastIndexOf("/"));
                            if (!links.ContainsKey(str))
                                links.Add(str, 1);
                            else
                                links[str]++;
                            pagecounts[id]++;
                        }
                    }
                }
            }
        }

        foreach (var l in links.OrderByDescending(l => l.Value).ToArray())
        {
            testurl = (l.Key.StartsWith("www.") ? l.Key.Substring(4) : "www." + l.Key);
            if (shortenedlinks.ContainsKey(testurl))
                shortenedlinks[testurl] += l.Value;
            else
                shortenedlinks.Add(l.Key, l.Value);
        }
        //links = links.GroupBy(x => x.Key.StartsWith("www.") ? x.Key.Substring(4) : x.Key).ToDictionary(gr => gr.Select(x => x.Key).OrderByDescending(x => x.Length).First(), gr => gr.Sum(x => x.Value));

        string result = "<center>\n{|class=\"standard\"\n!Место!!Число&nbsp;ссылок&nbsp;из&nbsp;рувики&nbsp;на!!style=\"text-align:left\"|данный сайт или его раздел";
        int counter = 0;
        foreach (var l in shortenedlinks.OrderByDescending(l => l.Value))
            if (l.Value >= 70)
                result += "\n|-\n|" + ++counter + "||" + l.Value + "||" + l.Key;
        result += "|}";
        var page = new Page("user:MBH/most linked sites");
        page.Save(result);

        //result = "<center>\n{|class=\"standard\"\n!Место!!Статья!!Внешних ссылок из неё";
        //counter = 0;
        //foreach (var p in pagecounts.OrderByDescending(p => p.Value))
        //    if (p.Value >= 100)
        //        result += "\n|-\n| " + ++counter + "||[[" + pagenames[p.Key] + "]]||" + p.Value;
        //result += "|}";
        //page = new Page("user:MBH/most extlinked pages");
        //page.Save(result);
    }
}
