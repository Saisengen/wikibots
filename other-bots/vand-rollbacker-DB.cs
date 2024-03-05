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
    enum edit_type { zkab_report, talkpage_warning, suspicious_edit, rollback }
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static string Save(string lang, HttpClient site, string action, string title, string customparam, string comment, edit_type type)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf|rollback").Result;
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
        return site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static int Main()
    {
        var discord = new HttpClient();
        var goodanons = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        string discord_token = creds[3];

        var ru = Site("ru", creds[4], creds[5]);
        var uk = Site("uk", creds[4], creds[5]);
        var reportedusersrx = new Regex(@"\| вопрос = u/(.*)");
        var rowrx = new Regex(@"\|-");
        string rulasteditid = "", newrulasteditid = "", ukrlasteditid = "", newukrlasteditid = "", lowlimit = "", ukrlimit = "";
        double mediumlimit = 1;
        int currminute = -1;
        var badusers = new HashSet<string>();
        var ruconnect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki").Replace("analytics", "web"));
        ruconnect.Open();
        var ukrconnect = new MySqlConnection(creds[2].Replace("%project%", "ukwiki").Replace("analytics", "web"));
        ukrconnect.Open();
        MySqlDataReader sqlreader;
        while (true)
        {
            try
            {
                if (currminute != DateTime.UtcNow.Minute / 10)
                {
                    currminute = DateTime.UtcNow.Minute / 10;
                    var limits = ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/limits.css&action=raw").Result.Split('\n');
                    lowlimit = limits[0];
                    mediumlimit = Convert.ToDouble(limits[1]);
                    ukrlimit = limits[2];
                    foreach (var g in ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/goodanons.css&action=raw").Result.Split('\n'))
                        goodanons.Add(g);
                }
                bool runewle = false, ukrnewle = false;
                string commandtext = "select cast(rc_title as char) title, max(case when oresm_name=\"damaging\" then oresc_probability else 0 end) damaging, max(case when oresm_name=\"goodfaith\" " +
                    "then oresc_probability else 0 end) goodfaith, cast(actor_name as char) user, rc_this_oldid, actor_user from recentchanges join ores_classification on oresc_rev=rc_this_oldid join " +
                    "actor on actor_id=rc_actor join ores_model on oresc_model=oresm_id where rc_timestamp>" + DateTime.UtcNow.AddSeconds(-10).ToString("yyyyMMddHHmmss") + " and rc_type=0 group by " +
                    "rc_this_oldid having max(case when oresm_name=\"damaging\" then oresc_probability else 0 end)>=lowlimit order by rc_this_oldid desc;";
                sqlreader = new MySqlCommand(commandtext.Replace("lowlimit", lowlimit), ruconnect).ExecuteReader();
                while (sqlreader.Read())
                {
                    string user = sqlreader.GetString("user");
                    if (goodanons.Contains(user))
                        continue;
                    bool user_is_anon = sqlreader.IsDBNull(5);
                    double damaging = Math.Round(sqlreader.GetDouble("damaging"), 3);
                    double goodfaith = Math.Round(sqlreader.GetDouble("goodfaith"), 3);
                    string title = sqlreader.GetString("title").Replace('_', ' ');
                    string editid = sqlreader.GetString("rc_this_oldid");
                    if (!runewle)
                    {
                        newrulasteditid = editid;
                        runewle = true;
                    }
                    if (editid == rulasteditid)
                        break;

                    if (badusers.Contains(user))
                    {
                        string zkab = ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=ВП:Запросы_к_администраторам/Быстрые&action=raw").Result;
                        var reportedusers = reportedusersrx.Matches(zkab);
                        bool reportedyet = false;
                        foreach (Match r in reportedusers)
                            if (user == r.Groups[1].Value)
                                reportedyet = true;
                        if (!reportedyet)
                            Save("ru", ru, "edit", "ВП:Запросы к администраторам/Быстрые", "\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}", "[[special:contribs/" + user +
                                "]] - новый запрос", edit_type.zkab_report);
                    }
                    else badusers.Add(user);

                    if (damaging < mediumlimit)
                    {
                        string text = ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:Рейму_Хакурей/Проблемные_правки&action=raw").Result.Replace(
                            "!Дифф!!Статья!!Автор!!damaging!!goodfaith", "!Дифф!!Статья!!Автор!!damaging!!goodfaith\n|-\n|[[special:diff/" + editid + "|diff]]||[//ru.wikipedia.org" +
                            "/w/index.php?title=" + title.Replace(' ', '_') + "&action=history " + title + "]||[[special:contribs/" + user + "|" + user + "]]||" + damaging + "||" + goodfaith);
                        var rows = rowrx.Matches(text);
                        text = text.Substring(0, rowrx.Matches(text)[rowrx.Matches(text).Count - 1].Index);
                        Save("ru", ru, "edit", "u:Рейму_Хакурей/Проблемные_правки", text, "[[special:diff/" + editid + "|diff]], [[special:history/" + title + "|" + title + "]]," +
                            "[[special:contribs/" + user + "|" + user + "]], " + damaging + "/" + goodfaith, edit_type.suspicious_edit);

                        var result = discord.PostAsync("https://discord.com/api/webhooks/" + discord_token, new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + title +
                                "](<https://ru.wikipedia.org/w/index.php?diff=" + editid + ">) / [" + user.Replace(' ', '_') + "](<https://ru.wikipedia.org/wiki/special:contribs/" +
                                user.Replace(' ', '_') + ">) (dmg:" + damaging + " gdf:" + goodfaith + ")"}})).Result;
                    }
                    else
                    {
                        string answer = Save("ru", ru, "rollback", title, user, "[[u:Рейму Хакурей|автоматическая отмена]] правки участника [[special:contribs/" + user + "|" + user + "]] (" +
                            damaging + "/" + goodfaith + ")", edit_type.rollback);
                        if (answer.Contains("<rollback title="))
                        Save("ru", ru, "edit", "ut:" + user, "{{subst:u:Рейму_Хакурей/Уведомление|" + editid + "|" + title + "|" + damaging + "|" + goodfaith + "|" + (user_is_anon ? "1" : "") +
                            "}}", (user_is_anon ? "Правка с вашего IP-адреса" : "Ваша правка") + " в статье [[" + title + "]] " + "автоматически отменена", edit_type.talkpage_warning);
                        else
                        {
                            Console.WriteLine(title);
                            Console.WriteLine(answer);
                        }
                    }
                }
                sqlreader.Close();
                rulasteditid = newrulasteditid;

                sqlreader = new MySqlCommand(commandtext.Replace("lowlimit", ukrlimit), ukrconnect).ExecuteReader();
                while (sqlreader.Read())
                {
                    string user = sqlreader.GetString("user");
                    if (goodanons.Contains(user))
                        continue;
                    bool user_is_anon = sqlreader.IsDBNull(5);
                    double damaging = Math.Round(sqlreader.GetDouble("damaging"), 3);
                    double goodfaith = Math.Round(sqlreader.GetDouble("goodfaith"), 3);
                    string title = sqlreader.GetString("title").Replace('_', ' ');
                    string editid = sqlreader.GetString("rc_this_oldid");
                    if (!ukrnewle)
                    {
                        newrulasteditid = editid;
                        ukrnewle = true;
                    }
                    if (editid == ukrlasteditid)
                        break;

                    string text = uk.GetStringAsync("https://uk.wikipedia.org/w/index.php?title=user:Рейму_Хакурей/Підозрілі редагування&action=raw").Result.Replace(
                            "!Diff!!Стаття!!Автор!!damaging!!goodfaith", "!Diff!!Стаття!!Автор!!damaging!!goodfaith\n|-\n|[[special:diff/" + editid + "|diff]]||[//uk.wikipedia.org" +
                            "/w/index.php?title=" + title.Replace(' ', '_') + "&action=history " + title + "]||[[special:contribs/" + user + "|" + user + "]]||" + damaging + "||" + goodfaith);
                    var rows = rowrx.Matches(text);
                    text = text.Substring(0, rowrx.Matches(text)[rowrx.Matches(text).Count - 1].Index);
                    Save("uk", uk, "edit", "user:Рейму Хакурей/Підозрілі редагування", text, "[[special:diff/" + editid + "|diff]], [[special:history/" + title + "|" + title + "]]," +
                        "[[special:contribs/" + user + "|" + user + "]], " + damaging + "/" + goodfaith, edit_type.suspicious_edit);

                }
                sqlreader.Close();
                ukrlasteditid = newukrlasteditid;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return 1;
            }
            Thread.Sleep(5000);
        }
    }
}
