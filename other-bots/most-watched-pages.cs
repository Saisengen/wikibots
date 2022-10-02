using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using DotNetWikiBot;
using System.Web.UI;

class Program
{
    static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var nss = new Dictionary<int, string>();
        string cont, query, apiout, result = "<center>\n";

        apiout = site.GetWebPage("/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces");
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ns")
                {
                    int ns = Convert.ToInt16(r.GetAttribute("id"));
                    if (ns % 2 == 0 || ns == 3)
                    {
                        r.Read();
                        nss.Add(ns, r.Value);
                    }
                }
        }
        nss.Remove(2);
        nss.Remove(-2);

        foreach (var n in nss.Keys)
        {
            var pageids = new HashSet<string>();
            var pagecounts = new Dictionary<string, Pair>();
            cont = ""; query = "/w/api.php?action=query&list=allpages&format=xml&aplimit=max&apfilterredir=nonredirects&apnamespace=";
            while (cont != null)
            {
                apiout = (cont == "" ? site.GetWebPage(query + n) : site.GetWebPage(query + n + "&apcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                            pageids.Add(r.GetAttribute("pageid"));
                }
            }

            var requeststrings = new HashSet<string>();
            string idset = ""; int c = 0;
            foreach (var p in pageids)
            {
                idset += "|" + p;
                if (++c % 500 == 0)
                {
                    requeststrings.Add(idset.Substring(1));
                    idset = "";
                }
            }
            if (idset.Length != 0)
                requeststrings.Add(idset.Substring(1));

            foreach (var q in requeststrings)
                using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&prop=info&format=xml&inprop=visitingwatchers%7Cwatchers&pageids=" + q))))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    while (r.Read())
                        if (r.GetAttribute("watchers") != null)
                        {
                            string title = r.GetAttribute("title");
                            if (n == 3)
                            {
                                if (title.Contains("/Архив"))
                                    continue;
                                title = title.Replace("Обсуждение участника:", "Участник:").Replace("Обсуждение участницы:", "Участница:");
                            }
                            int watchers = Convert.ToInt16(r.GetAttribute("watchers"));
                            string activewatchers;
                            if (r.GetAttribute("visitingwatchers") != null)
                                activewatchers = r.GetAttribute("visitingwatchers");
                            else
                                activewatchers = "";
                            if (n == 0 && watchers >= 50 || n != 0)
                                pagecounts.Add(title, new Pair() { First = watchers, Second = activewatchers});
                        }
                }
            
            if (pagecounts.Count != 0)
            {
                result += "==" + (nss[n] == "" ? "Статьи" : (nss[n] == "Обсуждение участника" ? "Участник" : nss[n])) + "==\n{|class=\"standard sortable\"\n!Страница!!Всего следящих!!Активных\n";
                foreach (var p in pagecounts.OrderByDescending(p => p.Value.First))
                    result += "|-\n|[[:" + p.Key + "]]||" + p.Value.First + "||" + p.Value.Second + "\n";
                result += "|}\n";
            }
        }
        var page = new DotNetWikiBot.Page("u:MBH/most watched pages");
        page.Save(result);
    }
}
