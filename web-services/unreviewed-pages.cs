using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;
using System.Text;

class pageinfo
{
    public string pending_since, stable_revid;
}
class Program
{
    static string wiki, cat, template;
    static int requireddepth = 0;
    static WebClient cl = new WebClient();
    static HashSet<string> candidates = new HashSet<string>();
    static Dictionary<string, pageinfo> pages = new Dictionary<string, pageinfo>();
    static void sendresponse(string wiki, string cat, string template, int depth, string result)
    {
        var sr = new StreamReader("unreviewed-pages-template.txt");
        string resulttext = sr.ReadToEnd();
        string title = "";
        if (cat != "" && template != "")
            title = " (" + cat + ", " + template + ")";
        else if (cat != "")
            title = " (" + cat + ")";
        else if (template != "")
            title = " (" + template + ")";
        resulttext = resulttext.Replace("%result%", result).Replace("%wiki%", wiki).Replace("%cat%", cat).Replace("%template%", template).Replace("%depth%", depth.ToString()).Replace("%title%", title);
        Console.WriteLine(resulttext);
        return;
    }
    static void searchsubcats(string category, int currentdepth)
    {
        if (currentdepth <= requireddepth)
        {
            string cont = "", query = "https://" + wiki + ".org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Category:" + Uri.EscapeDataString(category) + "&cmprop=ids|title&cmlimit=max&cmnamespace=100|102|0|6|10|14";
            while (cont != null)
            {
                var apiout = Encoding.UTF8.GetString(cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&cmcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.Name == "cm")
                            if (!candidates.Contains(r.GetAttribute("pageid")))
                                candidates.Add(r.GetAttribute("pageid"));
                }
            }

            cont = ""; //собираем категории
            query = "https://" + wiki + ".org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Category:" + Uri.EscapeDataString(category) + "&cmnamespace=14&cmprop=title&cmlimit=max";
            while (cont != null)
            {
                var apiout = Encoding.UTF8.GetString(cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&cmcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.Name == "cm")
                        {
                            string fullcategoryname = r.GetAttribute("title");
                            string shortcategoryname = fullcategoryname.Substring(fullcategoryname.IndexOf(':') + 1);
                            searchsubcats(shortcategoryname, currentdepth + 1);
                        }
                }
            }
        }
    }
    static void Main()
    {
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        //input = "wiki=ru.wikipedia&cat=Компьютерные+игры1&depth=0&template=";
        if (input == "" || input == null)
        {
            sendresponse("ru.wikipedia", "", "", 0, "");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        wiki = parameters["wiki"];
        cat = parameters["cat"] ?? "";
        template = parameters["template"] ?? "";
        requireddepth = Convert.ToInt16(parameters["depth"]);
        if (requireddepth < 0)
        {
            sendresponse(wiki, cat, template, 0, "Use non-negative depth value");
            return;
        }
        if (cat == "" && template == "")
        {
            sendresponse(wiki, cat, template, requireddepth, "Input category, template name or both");
            return;
        }

        bool broken_title = false;
        using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cl.DownloadData("https://" + wiki + ".org/w/api.php?action=query&prop=pageprops&format=xml&titles=category:" + Uri.EscapeDataString(cat))))))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("_idx") == "-1")
                    broken_title = true;
        using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cl.DownloadData("https://" + wiki + ".org/w/api.php?action=query&prop=pageprops&format=xml&titles=" + Uri.EscapeDataString(template))))))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("_idx") == "-1")
                    broken_title = true;
        if (broken_title)
        {
            sendresponse(wiki, cat, template, requireddepth, "There is no such category or such template in this wiki, you probably misspelled the page name");
            return;
        }

        if (cat != "")
            searchsubcats(cat, 0);

        if (template != "")
        {
            string cont = "", query = "https://" + wiki + ".org/w/api.php?action=query&format=xml&list=embeddedin&eititle=" + Uri.EscapeDataString(template) + "&eilimit=max&einamespace=100|102|0|6|10|14";
            while (cont != null)
            {
                string apiout = Encoding.UTF8.GetString(cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&eicontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                    while (r.Read())
                        if (r.Name == "ei")
                            if (!candidates.Contains(r.GetAttribute("pageid")))
                                candidates.Add(r.GetAttribute("pageid"));
                }
            }
        }

        var requeststrings = new HashSet<string>();
        int c = 0;
        string idset = "";
        foreach (var id in candidates)
        {
            idset += "|" + id;
            if (++c % 49 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        requeststrings.Add(idset.Substring(1));
        
        foreach(var rstring in requeststrings)
            using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cl.DownloadData("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=flagged&pageids=" + rstring)))))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.Name == "page" && r.NodeType == XmlNodeType.Element)
                    {
                        string title = r.GetAttribute("title");
                        r.Read();
                        if (r.Name != "flagged")
                            pages.Add(title, new pageinfo() { pending_since = "never", stable_revid = "" });
                        else if (r.GetAttribute("pending_since") != null)
                            pages.Add(title, new pageinfo() { pending_since = r.GetAttribute("pending_since").Substring(0, 10), stable_revid = r.GetAttribute("stable_revid") } );
                    }
            }

        if (candidates.Count == 0)
            sendresponse(wiki, cat, template, requireddepth, "There are no pages in this category or using this template");
        else if (pages.Count == 0)
            sendresponse(wiki, cat, template, requireddepth, "All pages in this category or using this template are reviewed in last revision");
        else
        {
            string result = "<table border=\"1\" cellspacing=\"0\"><tr><th>Page</th><th>Date of first unreviewed revision</th></tr>\n";
            foreach (var p in pages.OrderByDescending(p => p.Value.pending_since))
            {
                string link;
                if (p.Value.pending_since == "never")
                    link = "https://" + wiki + ".org/wiki/" + Uri.EscapeDataString(p.Key);
                else
                    link = "https://" + wiki + ".org/w/index.php?title=" + Uri.EscapeDataString(p.Key) + "&type=revision&diff=cur&oldid=" + p.Value.stable_revid;
                result += "<tr><td><a target=\"_blank\" href=\"" + link + "\">" + p.Key + "</a></td><td>" + p.Value.pending_since + "</td></tr>\n";
            }
                
            result += "</table></center>";
            sendresponse(wiki, cat, template, requireddepth, result);
        }
    }
}
