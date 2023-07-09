using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;
using System.Text;
using MySql.Data.MySqlClient;

class pageinfo
{
    public string id, status;
    public int numofiwiki;
}
class Program
{
    static string sourcewiki, category, template, pagetype, type, targetwiki, sort, requestedwiki;
    static bool wikilist, wikitable;
    static int requireddepth = 0, miniwiki = 0;
    static WebClient cl = new WebClient();
    static List<string> iterationlist = new List<string>();
    static Dictionary<string, pageinfo> processedpages = new Dictionary<string, pageinfo>();
    static List<string> FAs = new List<string>();
    static List<string> GAs = new List<string>();
    static List<string> RAs = new List<string>();
    static List<string> FLs = new List<string>();
    static void gather_quality_pages(List<string> list_of_quality_pages, string wd_item)
    {
        string quality_template_name = "";
        using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cl.DownloadData("https://www.wikidata.org/w/api.php?action=wbgetentities&format=xml&ids=" + wd_item + "&props=sitelinks")))))
            while (r.Read())
                if (r.Name == "sitelink" && r.GetAttribute("site") == url2db(requestedwiki))
                    quality_template_name = r.GetAttribute("title");

        if (quality_template_name != "")
        {
            string cont = "", query = "https://" + requestedwiki + ".org/w/api.php?action=query&format=xml&list=embeddedin&eititle=" + Uri.EscapeDataString(quality_template_name) + "&eilimit=max";
            while (cont != null)
            {
                using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&eicontinue=" + Uri.EscapeDataString(cont))))))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                    while (r.Read())
                        if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                            list_of_quality_pages.Add(r.GetAttribute("title"));
                }
            }
        }
    }
    static void sendresponse(string sourcewiki, string category, string template, string targetwiki, string type, string pagetype, string sort, bool wikilist, bool wikitable, int depth, int miniwiki, string result)
    {
        var sr = new StreamReader("pages-wo-iwiki.html");
        string resulttext;

        if (type == "exist")
            resulttext = sr.ReadToEnd().Replace("%selected_exist%", "selected");
        else
            resulttext = sr.ReadToEnd().Replace("%selected_nonexist%", "selected");

        if (pagetype == "articles")
            resulttext = resulttext.Replace("%selected_articles%", "selected");
        else
            resulttext = resulttext.Replace("%selected_allpages%", "selected");

        if (sort == "iwiki")
            resulttext = resulttext.Replace("%selected_iwiki%", "selected");
        else
            resulttext = resulttext.Replace("%selected_status%", "selected");

        if (wikilist)
            resulttext = resulttext.Replace("%checked_wikilist%", "checked");
        if (wikitable)
            resulttext = resulttext.Replace("%checked_wikitable%", "checked");

        string title = "";
        if (category != "" && template != "")
            title = " (" + category + ", " + template + ")";
        else if (category != "")
            title = " (" + category + ")";
        else if (template != "")
            title = " (" + template + ")";
        resulttext = resulttext.Replace("%result%", result).Replace("%sourcewiki%", sourcewiki).Replace("%category%", category).Replace("%template%", template).Replace("%targetwiki%", targetwiki).Replace("%title%", title).Replace("%depth%", depth.ToString()).Replace("%miniwiki%", miniwiki.ToString());
        Console.WriteLine(resulttext);
        Console.WriteLine();
    }
    static void searchsubcats(string category, int currentdepth)
    {
        if (currentdepth <= requireddepth)
        {
            string nstag = (pagetype == "articles" ? "&cmnamespace=0" : ""); //собираем страницы
            string cont = "", query = "https://" + sourcewiki + ".org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Category:" + Uri.EscapeDataString(category) + nstag + "&cmprop=title&cmlimit=max";
            while (cont != null)
            {
                var rawapiout = (cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&cmcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(rawapiout))))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                            if (!processedpages.ContainsKey(r.GetAttribute("title")))
                            {
                                processedpages.Add(r.GetAttribute("title"), new pageinfo());
                                iterationlist.Add(r.GetAttribute("title"));
                            }
                }
            }

            cont = ""; //собираем категории
            query = "https://" + sourcewiki + ".org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Category:" + Uri.EscapeDataString(category) + "&cmnamespace=14&cmprop=title&cmlimit=max";
            while (cont != null)
            {
                var rawapiout = (cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&cmcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(rawapiout))))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                        {
                            string fullcategoryname = r.GetAttribute("title");
                            string shortcategoryname = fullcategoryname.Substring(fullcategoryname.IndexOf(':') + 1);
                            searchsubcats(shortcategoryname, currentdepth + 1);
                        }
                }
            }
        }
    }
    static string url2db(string url)
    {
        return url.Replace(".", "").Replace("wikipedia", "wiki");
    }
    static string GetStatusOnRequestedWiki(string page)
    {
        string status = "";
        if (FAs.Contains(page))
            status = "Featured";
        else if (GAs.Contains(page))
            status = "Good";
        else if (FLs.Contains(page))
            status = "Featured list";
        else if (RAs.Contains(page))
            status = "Recommended";
        return status;
    }
    static void Main()
    {
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        //get = "sourcewiki=ru.wikipedia&category=NEC&subcats=on&template=&pagetype=articles&type=exist&targetwiki=ru.wikipedia&sort=iwiki";
        if (input == "" || input == null)
        {
            sendresponse("en.wikipedia", "", "", "ru.wikipedia", "nonexist", "articles", "iwiki", false, false, 0, 5, "");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        sourcewiki = parameters["sourcewiki"];
        category = parameters["category"];
        template = parameters["template"] ?? "";
        pagetype = parameters["pagetype"];
        type = parameters["type"];
        targetwiki = parameters["targetwiki"];
        sort = parameters["sort"] ?? "iwiki";
        wikilist = parameters["wikilist"] == "on";
        wikitable = parameters["wikitable"] == "on";
        requireddepth = Convert.ToInt16(parameters["depth"]);
        miniwiki = parameters["miniwiki"] == null ? 1 : Convert.ToInt16(parameters["miniwiki"]);
        if (requireddepth < 0)
        {
            sendresponse(sourcewiki, category, template, targetwiki, type, pagetype, sort, wikilist, wikitable, requireddepth, miniwiki, "Введите неотрицательную глубину");
            return;
        }
        if (category == "" && template == "")
        {
            sendresponse(sourcewiki, category, template, targetwiki, type, pagetype, sort, wikilist, wikitable, requireddepth, miniwiki, "Укажите категорию, шаблон или оба значения");
            return;
        }
        var targetpages = new Dictionary<string, pageinfo>();
        var existentpageids = new List<string>();

        if (category != "")
            searchsubcats(category, 0);

        if (template != "")
        {
            string nstag = (pagetype == "articles" ? "&einamespace=0" : "");
            string cont = "", query = "https://" + sourcewiki + ".org/w/api.php?action=query&format=xml&list=embeddedin&eititle=" + Uri.EscapeDataString(template) + nstag + "&eilimit=max";
            while (cont != null)
            {
                string apiout = Encoding.UTF8.GetString(cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&eicontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                    while (r.Read())
                        if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                            if (!processedpages.ContainsKey(r.GetAttribute("title")))
                            {
                                processedpages.Add(r.GetAttribute("title"), new pageinfo());
                                iterationlist.Add(r.GetAttribute("title"));
                            }
                }
            }
        }

        if (iterationlist.Count == 0)
        {
            sendresponse(sourcewiki, category, template, targetwiki, type, pagetype, sort, wikilist, wikitable, requireddepth, miniwiki, "Нет страниц в такой категории или с таким шаблоном");
            return;
        }
        else
        {
            requestedwiki = (type == "exist" ? targetwiki : sourcewiki);
            gather_quality_pages(FAs, "Q5626124");
            gather_quality_pages(GAs, "Q5303");
            gather_quality_pages(FLs, "Q5857568");
            gather_quality_pages(RAs, "Q13402307");

            var creds = new StreamReader("../p").ReadToEnd().Split('\n');
            var connect = new MySqlConnection("Server=wikidatawiki.labsdb;Database=wikidatawiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
            connect.Open();
            foreach (var pagename_on_sourcewiki in iterationlist)
            {
                string itemid;
                int numofiwiki = 0;
                var query = new MySqlCommand("select ips_item_id from wb_items_per_site where ips_site_id=\"" + url2db(sourcewiki) + "\" and ips_site_page=\"" + pagename_on_sourcewiki.Replace("\"", "\\\"") + "\";", connect);
                MySqlDataReader r = query.ExecuteReader();
                if (r.Read())
                {
                    itemid = r.GetString(0);
                    processedpages[pagename_on_sourcewiki].id = itemid;
                    r.Close();
                }
                else
                {
                    r.Close();
                    continue;
                }

                query = new MySqlCommand("select count(*) c from wb_items_per_site where ips_item_id=\"" + itemid + "\";", connect);
                r = query.ExecuteReader();
                r.Read();
                numofiwiki = Convert.ToInt32(r.GetString(0));
                processedpages[pagename_on_sourcewiki].numofiwiki = numofiwiki;
                r.Close();

                processedpages[pagename_on_sourcewiki].status = GetStatusOnRequestedWiki(pagename_on_sourcewiki);

                if (type == "exist")
                {
                    query = new MySqlCommand("select cast(ips_site_page as char) from wb_items_per_site where ips_site_id=\"" + url2db(targetwiki) + "\" and ips_item_id=\"" + itemid + "\";", connect);
                    r = query.ExecuteReader();
                    if (r.Read())
                    {
                        string pagename_on_targetwiki = r.GetString(0);
                        targetpages.Add(pagename_on_targetwiki, new pageinfo() { numofiwiki = numofiwiki, status = GetStatusOnRequestedWiki(pagename_on_targetwiki) });
                    }
                    r.Close();
                }

                else
                {
                    query = new MySqlCommand("select cast(ips_site_page as char) from wb_items_per_site where ips_site_id=\"" + url2db(targetwiki) + "\" and ips_item_id=\"" + itemid + "\";", connect);
                    r = query.ExecuteReader();
                    if (r.Read())
                        existentpageids.Add(itemid);
                    r.Close();
                }
            }

            string result = "<table border=\"1\" cellspacing=\"0\"><tr><th>Page</th><th># of interwikis</th><th>Status</th></tr>\n";

            if (type == "exist")
            {
                foreach (var p in sort == "iwiki" ? targetpages.OrderByDescending(p => p.Value.numofiwiki) : targetpages.OrderByDescending(p => p.Value.status))
                    if (p.Value.numofiwiki >= miniwiki)
                        result += "<tr><td><a href=\"https://" + targetwiki + ".org/wiki/" + Uri.EscapeDataString(p.Key) + "\">" + p.Key + "</a></td><td>" + p.Value.numofiwiki + "</td><td>" + p.Value.status + "</td></tr>\n";
            }

            else
            {
                foreach (var p in sort == "iwiki" ? processedpages.OrderByDescending(p => p.Value.numofiwiki) : processedpages.OrderByDescending(p => p.Value.status))
                    if (!existentpageids.Contains(p.Value.id) && p.Value.numofiwiki >= miniwiki)
                        result += "<tr><td><a href=\"https://" + sourcewiki + ".org/wiki/" + Uri.EscapeDataString(p.Key) + "\">" + p.Key + "</a></td><td>" + p.Value.numofiwiki + "</td><td>" + p.Value.status + "</td></tr>\n";
            }

            result += "</table></center>";
            if (wikilist && type == "nonexist")
                foreach (var p in sort == "iwiki" ? processedpages.OrderByDescending(p => p.Value.numofiwiki) : processedpages.OrderByDescending(p => p.Value.status))
                    if (!existentpageids.Contains(p.Value.id) && p.Value.numofiwiki >= miniwiki)
                        result += "\n<br>#{{iw|||" + sourcewiki.Substring(0, sourcewiki.IndexOf('.')) + "|" + p.Key + "}}";
            if (wikitable)
            {
                result += "\n<br>{|class=\"standard sortable\"<br>! Страница !! Интервик !! Статус";
                foreach (var p in sort == "iwiki" ? processedpages.OrderByDescending(p => p.Value.numofiwiki) : processedpages.OrderByDescending(p => p.Value.status))
                    if (!existentpageids.Contains(p.Value.id) && p.Value.numofiwiki >= miniwiki)
                        result += "<br>|-<br>| [[:" + sourcewiki.Substring(0, sourcewiki.IndexOf('.')) + ":" + p.Key + "|]] || " + p.Value.numofiwiki + " || " + p.Value.status;
                result += "<br>|}";
            }

            sendresponse(sourcewiki, category, template, targetwiki, type, pagetype, sort, wikilist, wikitable, requireddepth, miniwiki, result);
        }
    }
}
