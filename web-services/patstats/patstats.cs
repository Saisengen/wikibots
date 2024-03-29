using System;
using System.Collections.Generic;
using System.Web;
using System.Net;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text;
using MySql.Data.MySqlClient;

class Stat { public int main, template, cat, file, portal, unpat, module, sum; }

class Program
{
    static Dictionary<string, Stat> usertable = new Dictionary<string, Stat>();
    static string url2db(string url)
    {
        return url.Replace(".", "").Replace("wikipedia", "wiki");
    }
    static void put_new_action(string user, string type, int ns)
    {
        if (usertable.ContainsKey(user))
        {
            usertable[user].sum++;
            if (type.Contains("un"))
                usertable[user].unpat++;
            if (ns == 0)
                usertable[user].main++;
            else if (ns == 10)
                usertable[user].template++;
            else if (ns == 14)
                usertable[user].cat++;
            else if (ns == 6)
                usertable[user].file++;
            else if (ns == 100)
                usertable[user].portal++;
            else if (ns == 828)
                usertable[user].module++;
        }
        else
        {
            int main, template, file, cat, portal, module, unpat, sum;
            unpat = (type.Contains("un") ? 1 : 0);
            main = (ns == 0 ? 1 : 0);
            file = (ns == 6 ? 1 : 0);
            template = (ns == 10 ? 1 : 0);
            cat = (ns == 14 ? 1 : 0);
            portal = (ns == 100 ? 1 : 0);
            module = (ns == 828 ? 1 : 0);
            sum = 1;
            var stats = new Stat { unpat = unpat, main = main, file = file, template = template, cat = cat, portal = portal, module = module, sum = sum };
            usertable.Add(user, stats);
        }
    }
    static void Sendresponse(string type, string project, string startdate, string enddate, string sort, string result)
    {
        string result1 = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "patstats.html")).ReadToEnd().Replace("%result%", result).Replace("%project%", project).Replace("%startdate%", startdate).Replace("%enddate%", enddate);
        if (type == "db")
            result1 = result1.Replace("%checked_db%", "checked");
        else if (type == "api")
            result1 = result1.Replace("%checked_api%", "checked");
        if (sort == "all")
            result1 = result1.Replace("%checked_all%", "checked");
        else if (sort == "main")
            result1 = result1.Replace("%checked_main%", "checked");
        else if (sort == "template")
            result1 = result1.Replace("%checked_template%", "checked");
        else if (sort == "file")
            result1 = result1.Replace("%checked_file%", "checked");
        else if (sort == "cat")
            result1 = result1.Replace("%checked_cat%", "checked");
        else if (sort == "portal")
            result1 = result1.Replace("%checked_portal%", "checked");
        else if (sort == "module")
            result1 = result1.Replace("%checked_module%", "checked");
        else if (sort == "unpat")
            result1 = result1.Replace("%checked_unpat%", "checked");
        Console.WriteLine(result1);
    }
    static void Main()
    {
        var cl = new WebClient();
        //Environment.SetEnvironmentVariable("QUERY_STRING", "project=ru.wikipedia&startdate=2020-01-01&enddate=2020-12-31&sort=all");
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
        {
            Sendresponse("db", "ru.wikipedia", DateTime.Now.ToString("yyyy-MM-dd"), DateTime.Now.ToString("yyyy-MM-dd"), "all", "");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        string type = parameters["type"];
        string project = parameters["project"];
        string startdate = parameters["startdate"];
        string enddate = parameters["enddate"];
        string sort = parameters["sort"];
        string result = "";

        if (type == "db")
        {
            var connect = new MySqlConnection(Environment.GetEnvironmentVariable("CONN_STRING").Replace("%project%", url2db(project)));
            connect.Open();
            var squery = new MySqlCommand("select log_action, log_namespace, cast(actor_name as char) user from logging join actor on log_actor=actor_id where log_type=\"review\" and " +
                "log_timestamp >" + startdate.Replace("-", "") + "000000 and log_timestamp<" + enddate.Replace("-", "") + "235959", connect);
            var r = squery.ExecuteReader();
            while (r.Read())
            {
                string user = r.GetString("user");
                if (user == null)
                    continue;
                var buffer = new byte[10];
                r.GetBytes(0, 0, buffer, 0, 10);
                int ns = r.GetInt16("log_namespace");
                put_new_action(user, Encoding.UTF8.GetString(buffer, 0, buffer.Length), ns);
            }
        }

        if (type == "api")
        {
            string cont = "", query = "https://" + project + ".org/w/api.php?action=query&format=xml&list=logevents&leprop=title|user|type&letype=review&leend=" + startdate +
                    "T00%3A00%3A00.000Z&lestart=" + enddate + "T23%3A59%3A59.999Z&lelimit=500";
            while (cont != null)
            {
                var rawapiout = (cont == "" ? cl.DownloadData(query) : cl.DownloadData(query + "&lecontinue=" + cont));
                using (var xr = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(rawapiout))))
                {
                    xr.WhitespaceHandling = WhitespaceHandling.None;
                    xr.Read(); xr.Read(); xr.Read();
                    cont = xr.GetAttribute("lecontinue");
                    while (xr.Read())
                        if (xr.Name == "item")
                        {
                            string user = xr.GetAttribute("user");
                            if (user == null)
                                continue;
                            put_new_action(user, xr.GetAttribute("action"), Convert.ToInt16(xr.GetAttribute("ns")));
                        }
                }
            }
        }

        int c = 0;
        result = "<table border=\"1\" cellspacing=\"0\"><tr><th>№</th><th>Участник</th><th>Всего действий</th><th>В статьях</th><th>шаблонах</th><th>категориях</th><th>файлах</th><th>порталах" +
            "</th><th>модулях</th><th>Из них распатрулирований</th></tr>\n";
        foreach (var u in usertable.OrderByDescending(u => sort == "main" ? u.Value.main : (sort == "template" ? u.Value.template : (sort == "cat" ? u.Value.cat : (sort == "file" ? u.Value.file :
        (sort == "portal" ? u.Value.portal : (sort == "module" ? u.Value.module : (sort == "unpat" ? u.Value.unpat : u.Value.sum))))))))
            result += "<tr><td>" + ++c + "</td><td><a href=\"https://" + project + ".org/wiki/special:log?type=review&user=" + Uri.EscapeDataString(u.Key) + "\">" + u.Key + "</a></td><td>" +
                u.Value.sum + "</td><td>" + u.Value.main + "</td><td>" + u.Value.template + "</td><td>" + u.Value.cat + "</td><td>" + u.Value.file + "</td><td>" + u.Value.portal + "</td><td>" +
                u.Value.module + "</td><td>" + u.Value.unpat + "</td></tr>";
        Sendresponse(type, project, startdate, enddate, sort, result + "</table>");
    }
}
