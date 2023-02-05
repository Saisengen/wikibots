using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

class Program
{
    enum edit_type
    {
        zkab_report, talkpage_warning, suspicious_edit, rollback
    }
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static string Save(HttpClient site, string action, string title, string customparam, string comment, edit_type type)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf|rollback").Result;
        if (!result.IsSuccessStatusCode)
            return "";
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var request = new MultipartFormDataContent();
        request.Add(new StringContent(action), "action");
        request.Add(new StringContent(title), "title");
        request.Add(new StringContent(comment), "summary");
        if (type == edit_type.rollback)
            request.Add(new StringContent(doc.SelectSingleNode("//tokens/@rollbacktoken").Value), "token");
        else
            request.Add(new StringContent(doc.SelectSingleNode("//tokens/@csrftoken").Value), "token");
        request.Add(new StringContent("xml"), "format");
        if (type == edit_type.zkab_report)
            request.Add(new StringContent(customparam), "appendtext");
        else if (type == edit_type.talkpage_warning)
        {
            request.Add(new StringContent(customparam), "text");
            request.Add(new StringContent("new"), "section");
        }
        else if (type == edit_type.suspicious_edit)
            request.Add(new StringContent(customparam), "text");
        else if (type == edit_type.rollback)
            request.Add(new StringContent(customparam), "user");
        return site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static int Main()
    {
        var goodanons = new HashSet<string>();
        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        var site = Site(creds[4], creds[5]);
        var reportedusersrx = new Regex(@"\| вопрос = u/(.*)");
        var rowrx = new Regex(@"\|-");
        string lasteditid = "", newlasteditid = "", lowlimit = "";
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
                    var limits = site.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/limits.css&action=raw").Result.Split('\n');
                    lowlimit = limits[0];
                    mediumlimit = Convert.ToDouble(limits[1]);
                    foreach (var g in site.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/goodanons.css&action=raw").Result.Split('\n'))
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
                        string zkab = site.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=ВП:Запросы_к_администраторам/Быстрые&action=raw").Result;
                        var reportedusers = reportedusersrx.Matches(zkab);
                        bool reportedyet = false;
                        foreach (Match r in reportedusers)
                            if (user == r.Groups[1].Value)
                                reportedyet = true;
                        if (!reportedyet)
                            Save(site, "edit", "ВП:Запросы к администраторам/Быстрые", "\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}", "[[special:contribs/" + user + "]] - новый запрос", edit_type.zkab_report);
                    }
                    else badusers.Add(user);

                    if (damaging < mediumlimit)
                    {
                        string text = site.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:Рейму_Хакурей/Проблемные_правки&action=raw").Result.Replace("!Дифф!!Статья!!Автор!!damaging!!goodfaith", "!Дифф!!Статья!!Автор!!damaging!!goodfaith\n|-\n|[[special:diff/" + editid +
                            "|diff]]||[//ru.wikipedia.org/w/index.php?title=" + title.Replace(' ', '_') + "&action=history " + title + "]||[[special:contribs/" + user + "|" + user + "]]||" + damaging + "||" + goodfaith);
                        var rows = rowrx.Matches(text);
                        text = text.Substring(0, rowrx.Matches(text)[rowrx.Matches(text).Count - 1].Index);
                        Save(site, "edit", "u:Рейму_Хакурей/Проблемные_правки", text, "[[special:diff/" + editid + "|diff]], [[special:history/" + title + "|" + title + "]], [[special:contribs/" + user + "|" + user + "]], " + damaging + "/" + goodfaith, edit_type.suspicious_edit);
                    }
                    else
                    {
                        string answer = Save(site, "rollback", title, user, "[[u:Рейму Хакурей|автоматическая отмена]] правки участника [[special:contribs/" + user + "|" + user + "]] (" + damaging + "/" + goodfaith + ")", edit_type.rollback);
                        if (answer.Contains("<rollback title="))
                        Save(site, "edit", "ut:" + user, "{{subst:u:Рейму_Хакурей/Уведомление|" + editid + "|" + title + "|" + damaging + "|" + goodfaith + "|" + (user_is_anon ? "1" : "") + "}}", (user_is_anon ? "Правка с вашего IP-адреса" : "Ваша правка") + " в статье [[" + title + "]] " + 
                            "автоматически отменена", edit_type.talkpage_warning);
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
