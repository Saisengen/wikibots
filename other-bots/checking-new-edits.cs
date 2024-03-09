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
    static string user, title, newid, oldid, discord_token;
    static HttpClient discord = new HttpClient(), ru, uk;
    static double damaging;
    static Regex rowrx = new Regex(@"\|-");
    static Dictionary<string,string> notifying_page_name = new Dictionary<string,string>(){{"ru","Рейму_Хакурей/Проблемные_правки"},{"uk","Рейму_Хакурей/Підозрілі_редагування"}};
    static Dictionary<string,string> notifying_header = new Dictionary<string, string>() { { "ru", "!Дифф!!Статья!!Автор!!Причина" }, { "uk", "!Diff!!Стаття!!Автор!!Причина" } };
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
        var request = new MultipartFormDataContent {{ new StringContent(action), "action" }, { new StringContent(title), "title" }, { new StringContent(comment), "summary" }};
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
    static void post_suspicious_edit(string lang, string reason)
    {
        string get_request = "https://" + lang + ".wikipedia.org/w/index.php?title=user:" + notifying_page_name[lang] + "&action=raw";
        string notifying_page_text = (lang == "ru" ? ru.GetStringAsync(get_request).Result : uk.GetStringAsync(get_request).Result);
        notifying_page_text = notifying_page_text.Replace(notifying_header[lang], notifying_header[lang] + "\n|-\n|[[special:diff/" + newid + "|diff]]||[//" + lang + ".wikipedia.org/w/index.php?" +
            "title=" + title.Replace(' ', '_') + "&action=history " + title + "]||[[special:contribs/" + user + "|" + user + "]]||" + reason);
        var rows = rowrx.Matches(notifying_page_text);
        notifying_page_text = notifying_page_text.Substring(0, rows[rows.Count - 1].Index);
        Save(lang, (lang == "ru" ? ru : uk), "edit", "user:" + notifying_page_name[lang], notifying_page_text, "[[special:diff/" + newid + "|diff]], [[special:history/" + title + "|" + title + "]]," +
            "[[special:contribs/" + user + "|" + user + "]], " + reason, edit_type.suspicious_edit);

        discord.PostAsync("https://discord.com/api/webhooks/" + discord_token, new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + title + "](<https://" + lang +
        ".wikipedia.org/w/index.php?diff=" + newid + ">) / [" + user.Replace(' ', '_') + "](<https://" + lang + ".wikipedia.org/wiki/special:contribs/" + user.Replace(' ', '_') + ">) " + reason}}));
    }
    static int Main()
    {
        var goodanons = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var patterns = new List<Regex>();
        var added_string_rgx = new Regex(@"^\+.*", RegexOptions.Multiline);
        discord_token = creds[3];
        ru = Site("ru", creds[4], creds[5]);
        uk = Site("uk", creds[4], creds[5]);
        var reportedusersrx = new Regex(@"\| вопрос = u/(.*)");
        string rulasteditid = "", newrulasteditid = "", ukrlasteditid = "", newukrlasteditid = "";
        double mediumlimit = 1, lowlimit = 1, ukrlimit = 1;
        int currminute = -1;
        var badusers = new HashSet<string>();
        var ruconnect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki").Replace("analytics", "web"));
        ruconnect.Open();
        var ukrconnect = new MySqlConnection(creds[2].Replace("%project%", "ukwiki").Replace("analytics", "web"));
        ukrconnect.Open();
        MySqlDataReader rureader, ukrreader;//попытка решить проблему, что ру-правки постятся в укрвики
        string diff_text;
        while (true)
        {
            try
            {
                if (currminute != DateTime.UtcNow.Minute / 10)
                {
                    currminute = DateTime.UtcNow.Minute / 10;
                    var limits = ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/limits.css&action=raw").Result.Split('\n');
                    lowlimit = Convert.ToDouble(limits[0]);
                    mediumlimit = Convert.ToDouble(limits[1]);
                    ukrlimit = Convert.ToDouble(limits[2]);
                    foreach (var g in ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/goodanons.css&action=raw").Result.Split('\n'))
                        goodanons.Add(g);
                    patterns.Clear();
                    patterns.Add(new Regex(@"\bСВО\b")); //нельзя использовать в ignore case, как остальные
                    patterns.Add(new Regex(@"[хxX][oaо0аАОAO][XХxх][лLлl]\w*")); //исключаем фамилию Хохлов
                    var pattern_source = new StreamReader("patterns.txt").ReadToEnd().Split('\n');
                    foreach (var pattern in pattern_source)
                        patterns.Add(new Regex(pattern, RegexOptions.IgnoreCase));
                }
                bool runewle = false, ukrnewle = false;
                string commandtext = "select actor_user, cast(rc_title as char) title, oresc_probability, cast(actor_name as char) user, rc_this_oldid, rc_last_oldid from recentchanges join " +
                    "ores_classification on oresc_rev=rc_this_oldid join actor on actor_id=rc_actor join ores_model on oresc_model=oresm_id where rc_timestamp>" + DateTime.UtcNow.AddSeconds(-10)
                    .ToString("yyyyMMddHHmmss") + " and (rc_type=0 or rc_type=1) and rc_namespace=0 and oresm_name=\"damaging\" order by rc_this_oldid desc;";
                rureader = new MySqlCommand(commandtext, ruconnect).ExecuteReader();
                while (rureader.Read())
                {
                    user = rureader.GetString("user") ?? "";
                    if (goodanons.Contains(user))
                        continue;
                    bool user_is_anon = rureader.IsDBNull(0);
                    damaging = Math.Round(rureader.GetDouble("oresc_probability"), 3);
                    title = rureader.GetString("title").Replace('_', ' ');
                    newid = rureader.GetString("rc_this_oldid");
                    oldid = rureader.GetString("rc_last_oldid");
                    if (!runewle)
                    {
                        newrulasteditid = newid;
                        runewle = true;
                    }
                    if (newid == rulasteditid)
                        break;

                    if (damaging > lowlimit)
                    {
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
                            post_suspicious_edit("ru", damaging.ToString());

                        else
                        {
                            string answer = Save("ru", ru, "rollback", title, user, "[[u:Рейму Хакурей|автоматическая отмена]] правки участника [[special:contribs/" + user + "|" + user + "]] (" +
                                damaging + ")", edit_type.rollback);
                            if (answer.Contains("<rollback title="))
                                Save("ru", ru, "edit", "ut:" + user, "{{subst:u:Рейму_Хакурей/Уведомление|" + newid + "|" + title + "|" + damaging + "|" + (user_is_anon ? "1" : "") +
                                    "}}", (user_is_anon ? "Правка с вашего IP-адреса" : "Ваша правка") + " в статье [[" + title + "]] " + "автоматически отменена", edit_type.talkpage_warning);
                            else
                                Console.WriteLine(title + "\n" + answer);
                        }
                    }

                    if (user_is_anon)
                    {
                        try { diff_text = ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=compare&format=xml&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=unified").Result; }
                        catch { continue; }
                        foreach (Match added_string in added_string_rgx.Matches(diff_text))
                            foreach (var pattern in patterns)
                                if (pattern.IsMatch(added_string.Value))
                                {
                                    post_suspicious_edit("ru", pattern.Match(added_string.Value).Value);
                                    break;
                                }
                    }
                }
                rureader.Close();
                rulasteditid = newrulasteditid;

                ukrreader = new MySqlCommand(commandtext, ukrconnect).ExecuteReader();
                while (ukrreader.Read())
                {
                    string user = ukrreader.GetString("user");
                    double damaging = Math.Round(ukrreader.GetDouble("oresc_probability"), 3);
                    string title = ukrreader.GetString("title").Replace('_', ' ');
                    string editid = ukrreader.GetString("rc_this_oldid");
                    if (!ukrnewle)
                    {
                        newrulasteditid = editid;
                        ukrnewle = true;
                    }
                    if (editid == ukrlasteditid)
                        break;

                    if (damaging > ukrlimit)
                        post_suspicious_edit("uk", damaging.ToString());
                }
                ukrreader.Close();
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
