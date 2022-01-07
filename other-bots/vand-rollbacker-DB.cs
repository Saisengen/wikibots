using DotNetWikiBot;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

class Program
{
    static int Main()
    {
        var goodanons = new HashSet<string>();
        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        Site site = new Site("https://ru.wikipedia.org", creds[4], creds[5]);
        var reportedusersrx = new Regex(@"\| вопрос = u/(.*)");
        var rowrx = new Regex(@"\|-");
        string lasteditid = "", newlasteditid = "", csrftoken = "", rollbacktoken = "", lowlimit = "";
        double mediumlimit = 1;
        int currminute = -1;
        var badusers = new HashSet<string>();
        var connect = new MySqlConnection("Server=ruwiki.labsdb;Database=ruwiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
        connect.Open();
        MySqlCommand command;
        MySqlDataReader sqlrdr;
        while (true)
        {
            try
            {
                if (currminute != DateTime.UtcNow.Minute / 10)
                {
                    currminute = DateTime.UtcNow.Minute / 10;
                    string apiout = site.GetWebPage("/w/api.php?action=query&format=xml&meta=tokens&type=csrf%7Crollback");
                    using (var r = new XmlTextReader(new StringReader(apiout)))
                        while (r.Read())
                            if (r.Name == "tokens")
                            {
                                rollbacktoken = Uri.EscapeDataString(r.GetAttribute("rollbacktoken"));
                                csrftoken = Uri.EscapeDataString(r.GetAttribute("csrftoken"));
                            }
                    var limits = site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=user:MBH/limits.css&action=raw").Split('\n');
                    lowlimit = limits[0];
                    mediumlimit = Convert.ToDouble(limits[1]);
                    foreach (var g in site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=user:MBH/goodanons.css&action=raw").Split('\n'))
                        goodanons.Add(g);
                }
                bool newle = false;
                command = new MySqlCommand("select cast(rc_title as char) title, max(case when oresm_name=\"damaging\" then oresc_probability else 0 end) damaging, max(case when oresm_name=" +
                    "\"goodfaith\" then oresc_probability else 0 end) goodfaith, cast(actor_name as char) user, rc_this_oldid, actor_user from recentchanges join ores_classification on " +
                    "oresc_rev=rc_this_oldid join actor on actor_id=rc_actor join ores_model on oresc_model=oresm_id where rc_timestamp>" + DateTime.UtcNow.AddMinutes(-1).ToString("yyyyMMddHHmm") +
                    "00 and rc_type=0 group by rc_this_oldid having max(case when oresm_name=\"damaging\" then oresc_probability else 0 end)>=" + lowlimit + "order by rc_this_oldid desc;", connect);
                sqlrdr = command.ExecuteReader();
                while (sqlrdr.Read())
                {
                    string user = sqlrdr.GetString("user");
                    if (goodanons.Contains(user))
                        continue;
                    bool user_is_anon = sqlrdr.IsDBNull(5);
                    double damaging = Math.Round(sqlrdr.GetDouble("damaging"), 3);
                    double goodfaith = Math.Round(sqlrdr.GetDouble("goodfaith"), 3);
                    string title = sqlrdr.GetString("title").Replace('_', ' ');
                    string editid = sqlrdr.GetString("rc_this_oldid");
                    if (!newle)
                    {
                        newlasteditid = editid;
                        newle = true;
                    }
                    if (editid == lasteditid)
                        break;

                    if (badusers.Contains(user))
                    {
                        string zkab = site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=ВП:Запросы_к_администраторам/Быстрые&action=raw");
                        var reportedusers = reportedusersrx.Matches(zkab);
                        bool reportedyet = false;
                        foreach (Match r in reportedusers)
                            if (user == r.Groups[1].Value)
                                reportedyet = true;
                        if (!reportedyet)
                            site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=ВП:Запросы к администраторам/Быстрые&summary=[[special:contribs/" + user +
                            "]] - новый запрос&appendtext=\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}&token=" + csrftoken);
                    }
                    else badusers.Add(user);

                    if (damaging < mediumlimit)
                    {
                        string text = site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=user:Рейму_Хакурей/Проблемные_правки&action=raw").Replace("!Дифф!!Статья!!Автор!!damaging!!goodfaith",
                            "!Дифф!!Статья!!Автор!!damaging!!goodfaith\n|-\n|[[special:diff/" + editid + "|diff]]||[//ru.wikipedia.org/w/index.php?title=" + title.Replace(' ', '_') + "&action=history " +
                            title + "]||[[special:contribs/" + user + "|" + user + "]]||" + damaging + "||" + goodfaith);
                        var rows = rowrx.Matches(text);
                        text = text.Substring(0, rowrx.Matches(text)[rowrx.Matches(text).Count - 1].Index);
                        string answer = site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=u:Рейму_Хакурей/Проблемные_правки&summary=[[special:diff/" + editid + "|diff]], " +
                            "[[special:history/" + title + "|" + title + "]], [[special:contribs/" + user + "|" + user + "]], " + damaging + "/" + goodfaith + "&text=" + Uri.EscapeDataString(text) +
                            "&token=" + csrftoken);
                    }
                    else
                    {
                        string answer = site.PostDataAndGetResult("/w/api.php?action=rollback&format=xml", "title=" + title + "&user=" + user + "&summary=[[u:Рейму Хакурей|" +
                            "автоматическая отмена]] правки участника [[special:contribs/" + user + "|" + user + "]] (" + damaging + "/" + goodfaith + ")" + "&token=" + rollbacktoken);
                        if (answer.Contains("<rollback title="))
                            site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=user talk:" + user + "&section=new&summary=" + (user_is_anon ? "Правка с вашего IP-адреса" :
                                "Ваша правка") + " в статье [[" + title + "]] " + "автоматически отменена&text={{subst:u:Рейму_Хакурей/Уведомление|" + editid + "|" + title + "|" + damaging + "|" +
                                goodfaith + "|" + (user_is_anon ? "1" : "") + "}}&token=" + csrftoken);
                        else
                        {
                            Console.WriteLine(title);
                            Console.WriteLine(answer);
                        }
                    }
                }
                sqlrdr.Close();
                lasteditid = newlasteditid;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return 1;
            }
            Thread.Sleep(10000);
        }
    }
}
