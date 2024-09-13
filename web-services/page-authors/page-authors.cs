using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;
using MySql.Data.MySqlClient;
using System.Web;
using System.Xml;
using System.Text;

class Program
{
    static WebClient cl = new WebClient();
    static int notless = -1, size = 0, c = 0, hidden = 0;
    static Dictionary<string, int> stats = new Dictionary<string, int>();
    static void Sendresponse(string type, string project, string source, int notless, string result, string sizetype, int size)
    {
        string template = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "page-authors.html")).ReadToEnd();
        if (type == "cat")
            template = template.Replace("%checked_cat%", "checked");
        else if (type == "tmplt")
            template = template.Replace("%checked_tmplt%", "checked");
        else if (type == "talktmplt")
            template = template.Replace("%checked_talktmplt%", "checked");
        else if (type == "links")
            template = template.Replace("%checked_links%", "checked");
        else if (type == "talkcat")
            template = template.Replace("%checked_talkcat%", "checked");
        if (sizetype == "more")
            template = template.Replace("%checked_more%", "checked");
        else
            template = template.Replace("%checked_less%", "checked");
        Console.WriteLine(template.Replace("%result%", result).Replace("%source%", source).Replace("%wiki%", project).Replace("%size%", size.ToString()).Replace("%notless%", notless.ToString()));
    }
    static void count(string request)
    {
        try//обращение к БД гораздо медленнее
        {
            using (var rr = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cl.DownloadData(request)))))
                while (rr.Read())
                    if (rr.Name == "rev")
                    {
                        if (rr.GetAttribute("userhidden") == "")
                            hidden++;
                        else
                        {
                            string user = rr.GetAttribute("user");
                            if (stats.ContainsKey(user))
                                stats[user]++;
                            else stats.Add(user, 1);
                        }
                    }
        }
        catch { }
    }
    static void Main()
    {
        var srcpages = new List<string>();
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
        {
            Sendresponse("cat", "ru.wikipedia", "", 2, "", "more", 0);
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        string type = parameters["type"];
        string project = parameters["wiki"];
        string sign = parameters["sizetype"] == "more" ? ">" : "<";
        var rawsource = parameters["source"];
        var source = rawsource.Replace(" ", "_").Replace("\u200E", "").Replace("\r", "").Split('\n');//удаляем пробел нулевой ширины
        foreach (var s in source)
        {
            string upcased = char.ToUpper(s[0]) + s.Substring(1).Replace(" ", "_");
            if (!srcpages.Contains(upcased))
                srcpages.Add(upcased);
        }
        try
        {
            notless = Convert.ToInt32(parameters["notless"]);
            size = Convert.ToInt32(parameters["size"]);
        }
        catch { Sendresponse(type, project, rawsource, 2, "Введено не целое число.", parameters["sizetype"], 0); }
        string size_condition = size == 0 ? "" : " page_len" + sign + size * 1024 + " and ";
        var pageids = new HashSet<int>();
        var pagenames = new HashSet<string>();
        var connect = new MySqlConnection(Environment.GetEnvironmentVariable("CONN_STRING").Replace("%project%", project.Replace(".", "").Replace("wikipedia", "wiki")));
        connect.Open();
        MySqlCommand command;
        MySqlDataReader r;
        if (type == "cat")
            foreach (var s in srcpages)
            {
                command = new MySqlCommand("select cl_from from categorylinks " + (size == 0 ? "" : "join page on page_id=cl_from") + " where " + size_condition + " cl_to=\"" + s + "\";", connect) { CommandTimeout = 99999 };
                r = command.ExecuteReader();
                while (r.Read())
                    if (!pageids.Contains(r.GetInt32(0)))
                        pageids.Add(r.GetInt32(0));
                r.Close();
            }
        else if (type == "tmplt")
            foreach (var s in srcpages)
            {
                command = new MySqlCommand("select tl_from from templatelinks " + (size == 0 ? "" : "join page on page_id=tl_from") + " join linktarget on lt_id=tl_target_id where " + size_condition + " lt_title=\"" + s + "\" and lt_namespace=10;", connect) { CommandTimeout = 99999 };
                r = command.ExecuteReader();
                while (r.Read())
                    if (!pageids.Contains(r.GetInt32(0)))
                        pageids.Add(r.GetInt32(0));
                r.Close();
            }
        else if (type == "talkcat")
            foreach (var s in srcpages)
            {
                command = new MySqlCommand("select cast(page_title as char) title from categorylinks join page on page_id=cl_from where " + size_condition + " cl_to=\"" + s + "\";", connect) { CommandTimeout = 99999 };
                r = command.ExecuteReader();
                while (r.Read())
                    if (!pagenames.Contains(r.GetString(0)))
                        pagenames.Add(r.GetString(0));
                r.Close();
            }
        else if (type == "talktmplt")
            foreach (var s in srcpages)
            {
                command = new MySqlCommand("select cast(page_title as char) title from templatelinks join page on page_id=tl_from join linktarget on lt_id=tl_target_id where " + size_condition + " lt_title=\"" + s + "\" and lt_namespace=10;", connect) { CommandTimeout = 99999 };
                r = command.ExecuteReader();
                while (r.Read())
                    if (!pagenames.Contains(r.GetString(0)))
                        pagenames.Add(r.GetString(0));
                r.Close();
            }
        else if (type == "links")
            foreach (var s in srcpages)
            {
                string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=links&titles=" + s + "&pllimit=max";
                while (cont != null)
                {
                    var rawapiout = (cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&plcontinue=" + cont));
                    using (var xr = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(rawapiout))))
                    {
                        xr.WhitespaceHandling = WhitespaceHandling.None;
                        xr.Read(); xr.Read(); xr.Read();
                        cont = xr.GetAttribute("plcontinue");
                        while (xr.Read())
                            if (xr.Name == "pl")
                                pagenames.Add(xr.GetAttribute("title").Replace(" ", "_"));
                    }
                }
            }

        if (type == "cat" || type == "tmplt")
            foreach (var id in pageids)
                count("https://" + project + ".org/w/api.php?action=query&format=xml&prop=revisions&rvprop=user&rvlimit=1&rvdir=newer&pageids=" + id);

        else if (type == "talkcat" || type == "talktmplt" || type == "links")
            foreach (var name in pagenames)
                count("https://" + project + ".org/w/api.php?action=query&format=xml&prop=revisions&rvprop=user&rvlimit=1&rvdir=newer&titles=" + Uri.EscapeDataString(name));

        string result = "Всего страниц: " + (type == "cat" || type == "tmplt" ? pageids.Count : pagenames.Count) + "." + (hidden > 0 ? " Автор первой правки скрыт у " + hidden + " страниц." : "") + "<br><br><table border=\"1\" cellspacing=\"0\"><tr><th>№</th><th>Участник</th><th>Создал(а) статей</th></tr>\n";
        foreach (var u in stats.OrderByDescending(u => u.Value))
        {
            if (u.Value < notless)
                break;
            result += "<tr><td>" + ++c + "</td><td><a href=\"https://ru.wikipedia.org/wiki/User:" + Uri.EscapeDataString(u.Key) + "\">" + u.Key + "</a></td><td>" + u.Value + "</td></tr>\n";
        }
        Sendresponse(type, project, rawsource, notless, result + "</table>", parameters["sizetype"], size);
    }
}
