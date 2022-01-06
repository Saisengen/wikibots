using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using System.IO;
using DotNetWikiBot;
using System.Xml;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

public class UserSet
{
    public List<string> A;
    public List<string> B;
    public List<string> C;
    public List<string> E;
    public List<string> F;
    public List<string> I;
    public List<string> O;
    public List<string> K;
    public List<string> Ar;
    public List<string> Ex_Ar;
}
public class RootObject
{
    public UserSet userSet;
    public List<string> users_talkLinkOnly;
}
class Program
{
    static string mnumber(int number)
    {
        return (number.ToString().Length == 1 ? "0" + number.ToString() : number.ToString());
    }
    static void Main()
    {
        var discussiontypes = new string[] { "К удалению", "К восстановлению" };
        var monthnames = new string[13];
        monthnames[1] = "января"; monthnames[2] = "февраля"; monthnames[3] = "марта"; monthnames[4] = "апреля"; monthnames[5] = "мая"; monthnames[6] = "июня"; monthnames[7] = "июля"; monthnames[8] = "августа"; monthnames[9] = "сентября"; monthnames[10] = "октября"; monthnames[11] = "ноября"; monthnames[12] = "декабря";
        var botnames = new HashSet<string>();
        var statstable = new Dictionary<string, Dictionary<string, int>>();
        var now = DateTime.Now;

        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        var connect = new MySqlConnection("Server=ruwiki.labsdb;Database=ruwiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
        connect.Open();
        MySqlCommand command;
        MySqlDataReader r;

        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"sysop\";", connect) { CommandTimeout = 99999 };
        r = command.ExecuteReader();
        while (r.Read())
            statstable.Add(r.GetString(0), new Dictionary<string, int>() { { "closer", 0 }, { "totalactions", 0}, { "delsum", 0 }, { "restoresum", 0 }, { "contentedits", 0 }, { "totaledits", 0 }, { "del_rev_log", 0 }, { "abusefilter", 0}, { "block", 0}, { "contentmodel", 0}, { "delete", 0}, { "gblblock", 0}, { "managetags", 0}, { "merge", 0}, { "protect", 0},
                { "renameuser", 0}, { "restore", 0}, { "review", 0}, { "rights", 0}, { "stable", 0}, { "mediawiki", 0}, { "massmessage", 0}, { "checkuser", 0}, { "tag", 0}, { "import", 0 }, { "growthexperiments", 0 } });
        r.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"closer\";";
        r = command.ExecuteReader();
        while (r.Read())
            statstable.Add(r.GetString(0), new Dictionary<string, int>() { { "closer", 1 }, { "totalactions", 0}, { "delsum", 0 }, { "restoresum", 0 }, { "contentedits", 0 }, { "totaledits", 0 }, { "del_rev_log", 0 }, { "abusefilter", 0}, { "block", 0}, { "contentmodel", 0}, { "delete", 0}, { "gblblock", 0}, { "managetags", 0}, { "merge", 0}, { "protect", 0},
                { "renameuser", 0}, { "restore", 0}, { "review", 0}, { "rights", 0}, { "stable", 0}, { "mediawiki", 0}, { "massmessage", 0}, { "checkuser", 0}, { "tag", 0}, { "import", 0 }, { "growthexperiments", 0 } });
        r.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"bot\";";
        r = command.ExecuteReader();
        while (r.Read())
            botnames.Add(r.GetString(0));
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, log_type, log_action, COUNT(log_title) count FROM user_groups INNER JOIN actor_logging ON actor_user = ug_user INNER JOIN logging_userindex ON actor_id = log_actor WHERE ug_group IN ('sysop', 'closer') AND log_timestamp BETWEEN " + now.AddMonths(-6).Year + mnumber(now.AddMonths(-6).Month) + "01000000 AND " +
            now.Year + mnumber(now.Month) + "01000000 and log_type = 'delete' and log_action <> 'delete_redir' GROUP BY actor_name, log_type, log_action;";
        r = command.ExecuteReader();
        while (r.Read())
        {
            statstable[r.GetString("user")]["totalactions"] += Convert.ToInt32(r.GetString("count"));
            switch (r.GetString("log_action"))
            {
                case "delete":
                    statstable[r.GetString("user")]["delete"] += Convert.ToInt32(r.GetString("count"));
                    break;
                case "restore":
                    statstable[r.GetString("user")]["restore"] += Convert.ToInt32(r.GetString("count"));
                    break;
                case "revision":
                case "event":
                    statstable[r.GetString("user")]["del_rev_log"] += Convert.ToInt32(r.GetString("count"));
                    break;
            }
        }
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, log_type, COUNT(log_title) count FROM user_groups INNER JOIN actor_logging ON actor_user = ug_user INNER JOIN " + "logging_userindex ON actor_id = log_actor WHERE ug_group IN ('sysop', 'closer') AND log_timestamp BETWEEN " + now.AddMonths(-6).Year + mnumber(now.AddMonths(-6).Month) + "01000000 AND " +
            now.Year + mnumber(now.Month) + "01000000 and log_action not like 'move_%' and log_action not like '%-a' and log_action not like '%-ia' and log_type <> 'spamblacklist' and log_type <> 'thanks' and log_type <> 'upload' and log_type <> 'create' and log_type <> 'move' and log_type <> 'delete' and log_type <> 'newusers' and log_type <> 'timedmediahandler' GROUP BY actor_name, log_type;";
        r = command.ExecuteReader();
        while (r.Read())
            if (r.GetString("log_type") == "review")
                statstable[r.GetString("user")]["review"] += Convert.ToInt32(r.GetString("count"));
            else
            {
                statstable[r.GetString("user")]["totalactions"] += Convert.ToInt32(r.GetString("count"));
                statstable[r.GetString("user")][r.GetString("log_type")] += Convert.ToInt32(r.GetString("count"));
            }
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, page_namespace, COUNT(rev_page) count FROM revision_userindex INNER JOIN page ON rev_page = page_id INNER JOIN actor_revision ON rev_actor = actor_id INNER JOIN user_groups ON ug_user = actor_user WHERE ug_group IN ('sysop', 'closer') AND rev_timestamp BETWEEN " + now.AddMonths(-6).Year + mnumber(now.AddMonths(-6).Month) +
            "01000000 AND " + now.Year + mnumber(now.Month) + "01000000 GROUP BY actor_name, page_namespace;";
        r = command.ExecuteReader();
        while (r.Read())
        {
            statstable[r.GetString("user")]["totaledits"] += Convert.ToInt32(r.GetString("count"));
            switch (r.GetString("page_namespace"))
            {
                case "0":
                case "6":
                case "10":
                case "14":
                case "100":
                case "102":
                    statstable[r.GetString("user")]["contentedits"] += Convert.ToInt32(r.GetString("count"));
                    break;
                case "8":
                    statstable[r.GetString("user")]["totalactions"] += Convert.ToInt32(r.GetString("count"));
                    statstable[r.GetString("user")]["mediawiki"] += Convert.ToInt32(r.GetString("count"));
                    break;
            }
        }
        r.Close();

        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var flagged_users = JsonConvert.DeserializeObject<RootObject>(site.GetWebPage("/w/index.php?title=MediaWiki:Gadget-markadmins.json&action=raw"));
        var newfromabove = new HashSet<string>();
        using (var xr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Категория:Википедия:Участники с добавлением тем сверху&cmprop=title&cmlimit=5000"))))
            while (xr.Read())
                if (xr.Name == "cm")
                {
                    string rawtitle = xr.GetAttribute("title");
                    newfromabove.Add(rawtitle.Substring(rawtitle.IndexOf(":") + 1));
                }
        var lm = DateTime.Now.AddMonths(-1);
        var summaryrgx = new Regex(@"={1,}\s*Итог\s*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(" + monthnames[lm.Month] + "|" + monthnames[lm.AddMonths(-1).Month] + "|" + monthnames[lm.AddMonths(-2).Month] + "|" + monthnames[lm.AddMonths(-3).Month] + "|" +
            monthnames[lm.AddMonths(-4).Month] + "|" + monthnames[lm.AddMonths(-5).Month] + ") (" + lm.Year + "|" + lm.AddMonths(-5).Year + @") \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (var t in discussiontypes)
            using (var xr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=allpages&apprefix=" + t + "/&apnamespace=4&aplimit=max"))))
                while (xr.Read())
                    if (xr.Name == "p")
                    {
                        string page = xr.GetAttribute("title");
                        int year;
                        try
                        {year = Convert.ToInt16(page.Substring(page.Length - 4));}
                        catch
                        {continue;}
                        if (year >= 2017)
                        {
                            string pagetext;
                            try
                            { pagetext = site.GetWebPage("https://ru.wikipedia.org/wiki/" + page + "?action=raw"); }
                            catch
                            { continue; }
                            var results = summaryrgx.Matches(pagetext);
                            foreach (Match m in results)
                            {
                                string user = m.Groups[3].ToString().Replace('_', ' ');
                                if (!statstable.ContainsKey(user))
                                    continue;
                                statstable[user]["totalactions"]++;
                                if (t == "К удалению")
                                    statstable[user]["delsum"]++;
                                else
                                    statstable[user]["restoresum"]++;
                            }
                        }
                    }

        var cupage = new Page("u:BotDR/CU_stats");
        cupage.Load();
        var custats = cupage.text.Split('\n');
        foreach (var s in custats)
            if (s.Contains('='))
            {
                var data = s.Split('=');
                statstable[data[0]]["checkuser"] += Convert.ToInt32(data[1]);
                statstable[data[0]]["totalactions"] += Convert.ToInt32(data[1]);
            }

        string result = "<templatestyles src=\"Википедия:Администраторы/Активность/styles.css\"/>\n{{shortcut|ВП:АДА}}<center>{{Самые активные участники}}\nСтатистика активности администраторов и подводящих итоги Русской Википедии за период с 1 " + monthnames[now.AddMonths(-6).Month] + " " + now.AddMonths(-6).Year + " по 1 " + monthnames[now.Month] + " " + now.Year + " года. " +
            "Первично отсортирована по сумме числа правок и админдействий.\n\nДля подтверждения активности [[ВП:А#Неактивность администратора|администраторы]] должны сделать за полгода минимум 100 правок, из них 50 — в содержательных пространствах имён, а также 25 админдействий, включая подведение итогов на специальных страницах. [[ВП:ПИ#Процедура снятия статуса|Подводящие итоги]] " +
            "должны совершить 10 действий (итоги плюс удаления), из которых не менее двух - именно итоги.\n{|class=\"ts-википедия_администраторы_активность-table standard sortable\"\n!rowspan=2|Участник!!colspan=3|Правки!!colspan=14|Админдействия\n|-\n!{{abbr|Σ∀|все правки|0}}!!{{abbr|Σ|контентные правки|0}}!!{{abbr|✔|патрулирование|0}}!!{{abbr|Σ|все действия|0}}!!{{abbr|<big>🗑</big> " +
            "(📝)|удаление (итоги на КУ)|0}}!!{{abbr|<big>🗑⇧</big> (📝)|восстановление (итоги на ВУС)|0}}!!{{abbr|<big>≡🗑</big>|удаление правок и записей журналов|0}}!!{{abbr|🔨|(раз)блокировки|0}}!!{{abbr|🔒|защита и её снятие|0}}!!{{abbr|1=<big>⚖</big>|2=(де)стабилизация|3=0}}!!{{abbr|👮|изменение прав участников|0}}!!{{abbr|<big>⚙</big>|правка MediaWiki, изменение тегов и" +
            " контентной модели страниц|0}}!!{{abbr|<big>🕸</big>|изменение фильтров правок|0}}!!{{abbr|<big>🔍</big>|чекъюзерские проверки|0}}!!{{abbr|<big>⇨⇦</big>|слияние историй статей|0}}!!{{abbr|<big>📢</big>|рассылка массовых уведомлений|0}}!!{{abbr|<big>⇨</big>👤|переименование участников|0}}";
        foreach (var u in statstable.OrderByDescending(t => t.Value["totalactions"] + t.Value["totaledits"]))
        {
            bool inactivecloser = u.Value["closer"] == 1 && (u.Value["delete"] + u.Value["delsum"] < 10 || u.Value["delsum"] < 2);
            bool lessactions = u.Value["closer"] == 0 && u.Value["totalactions"] < 25;
            bool lesscontent = u.Value["closer"] == 0 && u.Value["contentedits"] + u.Value["review"] < 50;
            bool lesstotal = u.Value["closer"] == 0 && u.Value["totaledits"] + u.Value["review"] < 100;
            string color = "";
            if (!botnames.Contains(u.Key))
            {
                if (inactivecloser || lessactions || lesscontent || lesstotal)
                {
                    color = "style=\"background-color:#fcc\"";
                    if (!flagged_users.userSet.Ex_Ar.Contains(u.Key) && !flagged_users.userSet.Ar.Contains(u.Key) && u.Key != "Фильтр правок")
                        try
                        {
                            var user = new Page("user talk:" + u.Key);
                            user.Load();
                            string common_notif_text = "\n==Уведомление о вероятной неактивности %flagname%==\nСогласно [[ВП:Администраторы/Активность|автоматическому подсчёту вашей активности за " +
                                "последние полгода]], вы подпадаете под определение %flag%. Если в течение %span% вы не восстановите активность, на вас может быть подана заявка о снятии флага по " +
                                "неактивности. ~~~~";
                            if (!newfromabove.Contains(u.Key) || (newfromabove.Contains(u.Key) && user.text.IndexOf("==") == -1)) //если новые снизу
                            {
                                if (u.Value["closer"] == 1)
                                    user.Save(user.text + "\n\n" + common_notif_text.Replace("%flag%", "[[ВП:ПИ#Процедура_снятия_статуса|неактивного ПИ]]").Replace("%span%", "двух недель")
                                        .Replace("%flagname%", "ПИ"), "уведомление о вероятной неактивности ПИ", false);
                                else
                                    user.Save(user.text + "\n\n" + common_notif_text.Replace("%flag%", "[[ВП:А#Неактивность_администратора|неактивного администратора]]")
                                    .Replace("%span%", "трёх месяцев").Replace("%flagname%", "администратора"), "уведомление о вероятной неактивности администратора", false);
                            }
                            else //если новые сверху
                            {
                                int border = user.text.IndexOf("==");
                                string header = user.text.Substring(0, border - 1);
                                string pagebody = user.text.Substring(border);
                                if (u.Value["closer"] == 1)
                                    user.Save(header + common_notif_text.Replace("%flag%", "[[ВП:ПИ#Процедура_снятия_статуса|неактивного ПИ]]").Replace("%span%", "двух недель")
                                    .Replace("%flagname%", "ПИ") + "\n\n" + pagebody, "уведомление о вероятной неактивности ПИ", false);
                                else
                                    user.Save(header + common_notif_text.Replace("%flag%", "[[ВП:А#Неактивность_администратора|неактивного администратора]]").Replace("%span%", "трёх месяцев")
                                    .Replace("%flagname%", "администратора") + "\n\n" + pagebody, "уведомление о вероятной неактивности администратора", false);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(u.Key + "\n" + e.ToString());
                        }
                }
            }
            else
                color = "style=\"background-color:#ccf\"";
            result += "\n|-" + color + "\n|{{u|" + u.Key + "}} ([[special:contribs/" + u.Key + "|вклад]] | [[special:log/" + u.Key + "|журн]])||" +
                (lesstotal ? "'''" + u.Value["totaledits"] + "'''" : u.Value["totaledits"].ToString()) + "||" + (lesscontent ? "'''" + u.Value["contentedits"] + "'''" :
                u.Value["contentedits"].ToString()) + "||" + u.Value["review"] + "||" + (lessactions ? "'''" + u.Value["totalactions"] + "'''" : u.Value["totalactions"].ToString()) + "||" +
                (inactivecloser ? "'''" + u.Value["delete"] + " (" + u.Value["delsum"] + ")'''" : u.Value["delete"] + " (" + u.Value["delsum"] + ")") + "||" + u.Value["restore"] + " (" +
                u.Value["restoresum"] + ")||" + u.Value["del_rev_log"] + "||" + (u.Value["block"] + u.Value["gblblock"]) + "||" + u.Value["protect"] + "||" + u.Value["stable"] + "||" +
                u.Value["rights"] + "||" + (u.Value["managetags"] + u.Value["contentmodel"] + u.Value["mediawiki"] + u.Value["tag"]) + "||" + u.Value["abusefilter"] + "||" + u.Value["checkuser"] +
                "||" + u.Value["merge"] + "||" + u.Value["massmessage"] + "||" + u.Value["renameuser"];
        }
        result += "\n|}";
        var p = new Page("Википедия:Администраторы/Активность");
        p.Save(result, "обновление", false);
    }
}
