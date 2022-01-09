using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using DotNetWikiBot;
using MySql.Data.MySqlClient;

class Program
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    static Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
    static Dictionary<string, bool> users = new Dictionary<string, bool>();
    static bool isPatroller(string user)
    {
        if (user is null)
            return false;
        if (users.ContainsKey(user))
            return (users[user]);
        bool patroller = false;
        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=users&usprop=groups&ususers=" + Uri.EscapeDataString(user)))))
            while (r.Read())
                if (r.Name == "g" && r.NodeType == XmlNodeType.Element)
                {
                    r.Read();
                    if (r.Value == "editor" || r.Value == "autoreview" || r.Value == "bot")
                        patroller = true;
                }
        users.Add(user, patroller);
        return patroller;
    }
    static void Main()
    {
        var connect = new MySqlConnection("Server=ruwiki.labsdb;Database=ruwiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
        connect.Open();
        MySqlCommand command = new MySqlCommand("select distinct cast(log_title as char) title from logging where log_type=\"rights\" and log_params like \"%bot%\";", connect) { CommandTimeout = 9999 };
        MySqlDataReader rdr;
        rdr = command.ExecuteReader();
        while (rdr.Read())
            users.Add(rdr.GetString("title").Replace("_", " "), true);
        rdr.Close();
        var processedpages = new Dictionary<string, string>();
        DateTime starttime, endtime;
        string endstring = "", cont, query;
        bool hourly = false;
        if (hourly)
        {
            starttime = DateTime.UtcNow.AddHours(-1).AddMinutes(-1);
        }
        else
        {
            starttime = DateTime.UtcNow.AddMonths(-72);
            endtime = DateTime.UtcNow.AddMonths(-48);
            endstring = endtime.ToString("yyyy-MM-dd") + "T" + endtime.ToString("HH:mm:ss") + "Z";
        }
        string startstring = starttime.ToString("yyyy-MM-dd") + "T" + starttime.ToString("HH:mm:ss") + ".000Z";

        //recently created pages
        foreach (int i in new int[] { 0, 6, 10, 14, 100, 828 })
        {
            cont = ""; query = "/w/api.php?action=query&format=xml&list=logevents&leprop=title&letype=create&lelimit=max&lenamespace=" + i + "&leend=" + startstring + (hourly ? "" : "&lestart=" + endstring);
            while (cont != null)
            {
                var apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&lecontinue=" + cont));
                using (var xr = new XmlTextReader(new StringReader(apiout)))
                {
                    xr.WhitespaceHandling = WhitespaceHandling.None;
                    xr.Read(); xr.Read(); xr.Read();
                    cont = xr.GetAttribute("lecontinue");
                    Console.WriteLine(cont);
                    while (xr.Read())
                        if (xr.Name == "item")
                        {
                            bool nonexist = false;
                            string title = xr.GetAttribute("title");
                            using (var xr2 = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=flagged&titles=" + title))))
                                while (xr2.Read())
                                    if (xr2.Name == "page" && xr2.GetAttribute("_idx") == "-1")
                                        nonexist = true;
                            if (!nonexist && !processedpages.ContainsKey(title))
                                processedpages.Add(title, "");
                        }
                }
            }
        }

        //oldreviewed pages from some date to now
        while (startstring != null)
        {
            using (var xr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=oldreviewedpages&ordir=newer&ornamespace=*&orlimit=max&orstart=" + startstring))))
            {
                xr.WhitespaceHandling = WhitespaceHandling.None;
                xr.Read(); xr.Read(); xr.Read();
                startstring = xr.GetAttribute("orstart");
                //for (int i = 0; i < startstring.Length; i++)
                //    if (startstring[i] > endstring[i])
                //        goto end;
                Console.WriteLine(startstring);
                while (xr.Read())
                    if (xr.Name == "p" && !processedpages.ContainsKey(xr.GetAttribute("title")))
                        processedpages.Add(xr.GetAttribute("title"), xr.GetAttribute("pending_since"));
            }
        }
    end:

        //all unreviewed pages
        cont = ""; query = "/w/api.php?action=query&format=xml&list=unreviewedpages&urfilterlevel=0&urlimit=max";
        while (cont != null)
        {
            var apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&urstart=" + cont));
            using (var xr = new XmlTextReader(new StringReader(apiout)))
            {
                xr.WhitespaceHandling = WhitespaceHandling.None;
                xr.Read(); xr.Read(); xr.Read();
                cont = xr.GetAttribute("urstart");
                Console.WriteLine(cont);
                while (xr.Read())
                    if (xr.Name == "p")
                    {
                        bool nonexist = false;
                        string title = xr.GetAttribute("title");
                        using (var xr2 = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=flagged&titles=" + title))))
                            while (xr2.Read())
                                if (xr2.Name == "page" && xr2.GetAttribute("_idx") == "-1")
                                    nonexist = true;
                        if (!nonexist && !processedpages.ContainsKey(title))
                            processedpages.Add(title, "");
                    }
            }
        }

        site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        foreach (var p in processedpages)
        {
            bool should_be_patrolled = false;
            string id_to_patrol = "";
            using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=revisions&titles=" + Uri.EscapeDataString(p.Key) +
                "&rvprop=timestamp%7Cuser%7Cids&rvlimit=max&rvdir=newer" + (p.Value == "" ? "" : "&rvstart=" + Uri.EscapeDataString(p.Value))))))
                while (r.Read())
                    if (r.Name == "rev")
                    {
                        if (!isPatroller(r.GetAttribute("user")))
                            break;
                        should_be_patrolled = true;
                        id_to_patrol = r.GetAttribute("revid");
                    }
            if (should_be_patrolled)
            {
                string token = "";
                using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&meta=tokens&type=csrf"))))
                    while (r.Read())
                        if (r.Name == "tokens")
                            token = r.GetAttribute("csrftoken");
                string result = site.PostDataAndGetResult("/w/api.php?action=review&format=xml", "revid=" + id_to_patrol + "&token=" + Uri.EscapeDataString(token) + 
                    "&flag_accuracy=1&comment=патрулирование версий, не отпатрулированных из-за [[phab:T233561]] " + (hourly ? "(hourly)" : "(manual)"));
                if (!result.Contains("result=\"Success\""))
                    Console.WriteLine(p.Key + " " + result);
            }
        }
    }
}
