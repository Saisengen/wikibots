using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;
using System.Text;

class Program
{
    static string wiki, cat;
    static int requireddepth = 0;
    static WebClient cl = new WebClient();
    static Dictionary<string, long> pages = new Dictionary<string, long>();
    static HashSet<string> pagenames = new HashSet<string>();
    static void sendresponse(string wiki, string cat, int depth, string result)
    {
        var sr = new StreamReader("transclusions-count-template.txt");
        string resulttext = sr.ReadToEnd();
        resulttext = resulttext.Replace("%result%", result).Replace("%wiki%", wiki).Replace("%cat%", cat).Replace("%depth%", depth.ToString());
        Console.WriteLine(resulttext);
        return;
    }
    static void searchsubcats(string category, int currentdepth)
    {
        if (currentdepth <= requireddepth)
        {
            string cont = "", query = "https://" + wiki + ".org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Category:" + Uri.EscapeDataString(category) + "&cmprop=title&cmlimit=max&cmnamespace=10|828";
            while (cont != null)
            {
                var apiout = Encoding.UTF8.GetString(cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&cmcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.Name == "cm")
                            if (!pagenames.Contains(r.GetAttribute("title")))
                                pagenames.Add(r.GetAttribute("title"));
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
        //input = "wiki=ru.wikipedia&cat=Шаблоны:Плашки сбоку&depth=0";
        if (input == "" || input == null)
        {
            sendresponse("ru.wikipedia", "", 0, "");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        wiki = parameters["wiki"];
        cat = parameters["cat"] ?? "";
        requireddepth = Convert.ToInt16(parameters["depth"]);
        if (requireddepth < 0)
        {
            sendresponse(wiki, cat, 0, "Use non-negative depth value");
            return;
        }
        if (cat == "")
        {
            sendresponse(wiki, cat, requireddepth, "Input category name");
            return;
        }

        using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cl.DownloadData("https://" + wiki + ".org/w/api.php?action=query&prop=pageprops&format=xml&titles=category:" + Uri.EscapeDataString(cat))))))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("_idx") == "-1")
                {
                    sendresponse(wiki, cat, requireddepth, "There is no Category:" + cat + " in this wiki, you probably misspelled the page name");
                    return;
                }

        if (cat != "")
            searchsubcats(cat, 0);

        foreach (var page in pagenames)
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=" + Uri.EscapeUriString(page) + "&eilimit=max";
            long counter = 0;
            while (cont != null)
            {
                var apiout = Encoding.UTF8.GetString(cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&eicontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                    while (r.Read())
                        if (r.Name == "ei")
                            counter++;
                }
            }
            pages.Add(page, counter);
        }


        if (pages.Count == 0)
            sendresponse(wiki, cat, requireddepth, "There are no pages in this category or using this template");
        else
        {
            string result = "<table border=\"1\" cellspacing=\"0\"><tr><th>Page</th><th>Transclusions</th></tr>\n";
            foreach (var p in pages.OrderByDescending(p => p.Value))
                result += "<tr><td><a target=\"_blank\" href=\"https://" + wiki + ".org/wiki/" + Uri.EscapeDataString(p.Key) + "\">" + p.Key + "</a></td><td>" + p.Value + "</td></tr>\n";

            result += "</table></center>";
            sendresponse(wiki, cat, requireddepth, result);
        }
    }
}
