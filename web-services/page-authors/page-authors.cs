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
    static void Sendresponse(string type, string source, int notless, string result)
    {
        string template = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "page-authors.html")).ReadToEnd();
        if (type == "category")
            template = template.Replace("%checked_category%", "checked");
        else if (type == "template")
            template = template.Replace("%checked_template%", "checked");
        else if (type == "talktemplate")
            template = template.Replace("%checked_talktemplate%", "checked");
        else if (type == "links")
            template = template.Replace("%checked_links%", "checked");
        Console.WriteLine(template.Replace("%result%", result).Replace("%source%", source).Replace("%notless%", notless.ToString()));
    }
    static void Main()
    {
        var cl = new WebClient();
        var srcpages = new List<string>();
        //Environment.SetEnvironmentVariable("QUERY_STRING", "type=category&source=Статьи+проекта+Вымысел+III+уровня+низкой+важности&notless=2");
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
        {
            Sendresponse("category", "", 2, "");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        string type = parameters[0];
        var rawsource = parameters[1];
        var source = rawsource.Replace(" ", "_").Replace("\u200E", "").Replace("\r", "").Split('\n');//удаляем пробел нулевой ширины
        foreach (var s in source)
        {
            string upcased = char.ToUpper(s[0]) + s.Substring(1);
            if (!srcpages.Contains(upcased))
                srcpages.Add(upcased);
        }
        int notless = Convert.ToInt32(parameters[2]);
        string result = "";
        var pageids = new HashSet<int>();
        var pagenames = new HashSet<string>();
        var stats = new Dictionary<string, int>();
        var connect = new MySqlConnection(Environment.GetEnvironmentVariable("CONN_STRING").Replace("%project%", "ruwiki"));
        connect.Open();
        MySqlCommand command;
        MySqlDataReader r;
        int c = 0;
        result = "<table border=\"1\" cellspacing=\"0\"><tr><th>№</th><th>Участник</th><th>Создал статей</th></tr>\n";
        //----------------------------------------------------------------------------
        if (type == "category")
        {
            foreach (var s in srcpages)
            {
                command = new MySqlCommand("select cl_from from categorylinks where cl_to=\"" + s + "\";", connect) { CommandTimeout = 99999 };
                r = command.ExecuteReader();
                while (r.Read())
                    if (!pageids.Contains(r.GetInt32(0)))
                        pageids.Add(r.GetInt32(0));
                r.Close();
            }
        }
        //--------------------------------------------------------------------------
        else if (type == "template")
        {
            foreach (var s in srcpages)
            {
                command = new MySqlCommand("select tl_from from templatelinks where tl_title=\"" + s + "\" and tl_namespace=10;", connect) { CommandTimeout = 99999 };
                r = command.ExecuteReader();
                while (r.Read())
                    if (!pageids.Contains(r.GetInt32(0)))
                        pageids.Add(r.GetInt32(0));
                r.Close();
            }
        }
        //------------------------------------------------------------------------
        else if (type == "talktemplate")
        {

        }
        else if (type == "links")
        {
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
                                pagenames.Add(xr.GetAttribute("title"));
                    }
                }
            }

            foreach (var p in pagenames)
            {
                try
                {
                    using (var rr = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(cl.DownloadData("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&rvprop=user&rvlimit=1&rvdir=newer&titles=" + Uri.EscapeDataString(p))))))
                        while (rr.Read())
                            if (rr.Name == "rev")
                            {
                                string user = rr.GetAttribute("user");
                                if (stats.ContainsKey(user))
                                    stats[user]++;
                                else stats.Add(user, 1);
                            }
                }
                catch (Exception e) { continue; }
            }
        }
        else
            Sendresponse("category", "", 2, "Incorrect list type");

        var inputstrings = new List<string>();
        string apiout = "";
        if (type == "category" || type == "template")
        {
            string collector = "";
            foreach (var p in pageids)
                if (++c % 50 == 0)
                {
                    inputstrings.Add(collector.Substring(1));
                    collector = "";
                }
                else
                    collector += "|" + p;
            if (collector != "")
                inputstrings.Add(collector);

            foreach (var i in inputstrings)
            {
                apiout = Encoding.UTF8.GetString(cl.DownloadData("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&rvprop=user&rvlimit=1&rvdir=newer&pageids=" + i));
                using (var rr = new XmlTextReader(new StringReader(apiout)))
                    while (rr.Read())
                        if (rr.Name == "rev")
                        {
                            string user = rr.GetAttribute("user");
                            if (stats.ContainsKey(user))
                                stats[user]++;
                            else stats.Add(user, 1);
                        }
            }
        }

        c = 0;
        foreach (var u in stats.OrderByDescending(u => u.Value))
        {
            if (u.Value < notless)
                break;
            result += "<tr><td>" + ++c + "</td><td><a href=\"https://ru.wikipedia.org/wiki/User:" + Uri.EscapeDataString(u.Key) + "\">" + u.Key + "</a></td><td>" + u.Value + "</td></tr>\n";
        }
        string astr = "";
        foreach (var i in inputstrings)
            astr += i + "\n";
        Sendresponse(type, rawsource, notless, result + "</table><br>" + inputstrings[0] + apiout);
    }
}
