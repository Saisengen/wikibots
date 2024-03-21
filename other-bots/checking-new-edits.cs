using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Text;

class Program
{
    static string user, title, newid, oldid, liftwing_token, diff_text, commandtext = "select actor_user, cast(rc_title as char) title, oresc_probability, rc_timestamp, cast(actor_name as char) user, rc_this_oldid, " +
            "rc_last_oldid from recentchanges join ores_classification on oresc_rev=rc_this_oldid join actor on actor_id=rc_actor join ores_model on oresc_model=oresm_id where rc_timestamp>" +
            "%time% and (rc_type=0 or rc_type=1) and rc_namespace=0 and oresm_name=\"damaging\" order by rc_this_oldid desc;";
    static HttpClient discord_client = new HttpClient(), liftwing_client = new HttpClient(), ru, uk;
    static double ores_risk, lw_risk, ru_high = 1, ru_low = 1, uk_low = 1, agnostic_low = 1, agnostic_high = 1;
    static Regex row_rgx = new Regex(@"\|-"), liftwing_rgx = new Regex(@"""true"":(0.9\d+)"), reportedusers_rgx = new Regex(@"\| вопрос = u/(.*)"),
        ins_rgx = new Regex(@"<ins[^>]*>([^<>]*)</ins>"), del_rgx = new Regex(@"<del[^>]*>([^<>]*)</del>");
    static Dictionary<string,string> notifying_page_name = new Dictionary<string,string>(){{"ru", "user:Рейму_Хакурей/Проблемные_правки" },{"uk", "user:Рейму_Хакурей/Підозрілі_редагування" } };
    static Dictionary<string,string> notifying_header = new Dictionary<string, string>() { { "ru", "!Дифф!!Статья!!Автор!!Причина" }, { "uk", "!Diff!!Стаття!!Автор!!Причина" } };
    static Dictionary<string,string> discord_tokens = new Dictionary<string, string>();
    static Dictionary<string,string> last_checked_edit_time = new Dictionary<string, string>();
    static Dictionary<string,string> new_last_checked_edit_time = new Dictionary<string, string>();
    static Dictionary<string,bool> ru_users_by_apat_flag = new Dictionary<string, bool>();
    static Dictionary<string,bool> uk_users_by_apat_flag = new Dictionary<string, bool>();
    static List<string> suspicious_users = new List<string>();
    static List<Regex> patterns = new List<Regex>();
    static bool processed_by_patterns;
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
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login },
            { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
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
        string get_request = "https://" + lang + ".wikipedia.org/w/index.php?title=" + notifying_page_name[lang] + "&action=raw";
        string notifying_page_text = (lang == "ru" ? ru.GetStringAsync(get_request).Result : uk.GetStringAsync(get_request).Result);
        notifying_page_text = notifying_page_text.Replace(notifying_header[lang], notifying_header[lang] + "\n|-\n|[[special:diff/" + newid + "|diff]]||[[special:history/" + title + "|" + title + "]]||" +
            "[[special:contribs/" + user + "|" + user + "]]||" + reason);
        var rows = row_rgx.Matches(notifying_page_text);
        notifying_page_text = notifying_page_text.Substring(0, rows[rows.Count - 1].Index);
        Save(lang, (lang == "ru" ? ru : uk), "edit", notifying_page_name[lang], notifying_page_text, "[[special:diff/" + newid + "|" + title + "]] ([[special:history/" + title + "|" +
            (lang == "ru" ? "история" : "історія") + "]]), [[special:contribs/" + Uri.EscapeDataString(user) + "|" + user + "]], " + reason, edit_type.suspicious_edit);

        discord_client.PostAsync("https://discord.com/api/webhooks/" + discord_tokens[lang], new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + title + "](<https://" + lang +
                ".wikipedia.org/w/index.php?diff=" + newid + ">) ([" + (lang == "ru" ? "история" : "історія") + "](<https://" + lang + ".wikipedia.org/wiki/special:history/" + Uri.EscapeDataString(title) +
                ">)), [" + user.Replace(' ', '_') + "](<https://" + lang + ".wikipedia.org/wiki/special:contribs/" + Uri.EscapeDataString(user) + ">), " + reason}}));
    }
    static void post_suspicious_edit(string lang, string ins, string del)
    {
        string get_request = "https://" + lang + ".wikipedia.org/w/index.php?title=" + notifying_page_name[lang] + "&action=raw";
        string notifying_page_text = (lang == "ru" ? ru.GetStringAsync(get_request).Result : uk.GetStringAsync(get_request).Result);
        notifying_page_text = notifying_page_text.Replace(notifying_header[lang], notifying_header[lang] + "\n|-\n|[[special:diff/" + newid + "|diff]]||[[special:history/" + title + "|" + title + "]]||" +
            "[[special:contribs/" + user + "|" + user + "]]||<b><span class=del><nowiki>" + del + "</nowiki></span><span class=ins><nowiki>" + ins + "</nowiki></span></b>");
        var rows = row_rgx.Matches(notifying_page_text);
        notifying_page_text = notifying_page_text.Substring(0, rows[rows.Count - 1].Index);
        Save(lang, (lang == "ru" ? ru : uk), "edit", notifying_page_name[lang], notifying_page_text, "[[special:diff/" + newid + "|" + title + "]] ([[special:history/" + title + "|" +
            (lang == "ru" ? "история" : "історія") + "]]), [[special:contribs/" + Uri.EscapeDataString(user) + "|" + user + "]], " + del + " => " + ins, edit_type.suspicious_edit);

        discord_client.PostAsync("https://discord.com/api/webhooks/" + discord_tokens[lang], new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + title + "](<https://" + lang +
                ".wikipedia.org/w/index.php?diff=" + newid + ">) ([" + (lang == "ru" ? "история" : "історія") + "](<https://" + lang + ".wikipedia.org/wiki/special:history/" + Uri.EscapeDataString(title) +
                ">)), [" + user.Replace(' ', '_') + "](<https://" + lang + ".wikipedia.org/wiki/special:contribs/" + Uri.EscapeDataString(user) + ">), " + del + " => " + ins}}));
    }
    static void report_suspicious_user_if_needed()
    {
        if (suspicious_users.Contains(user))
        {
            string zkab = ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=ВП:Запросы_к_администраторам/Быстрые&action=raw").Result;
            var reportedusers = reportedusers_rgx.Matches(zkab);
            bool reportedyet = false;
            foreach (Match r in reportedusers)
                if (user == r.Groups[1].Value)
                    reportedyet = true;
            if (!reportedyet)
                Save("ru", ru, "edit", "ВП:Запросы к администраторам/Быстрые", "\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}", "[[special:contribs/" + user +
                    "]] - новый запрос", edit_type.zkab_report);
        }
        else suspicious_users.Add(user);
    }
    static void liftwing_check(string lang)
    {
        string raw = "1";
        try
        {
            raw = liftwing_rgx.Match(liftwing_client.PostAsync("https://api.wikimedia.org/service/lw/inference/v1/models/revertrisk-language-agnostic:predict",
                new StringContent("{\"lang\":\"" + lang + "\",\"rev_id\":" + newid + "}", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result).Groups[1].Value;
            if (raw != null && raw != "")
                lw_risk = Math.Round(Convert.ToDouble(raw), 3);
            else
                return;
        }
        catch (Exception e)
        {
            Console.WriteLine("raw=" + raw + "\n" + e.ToString());
            return;
        }
        if (lw_risk > agnostic_low)
        {
            post_suspicious_edit(lang, "liftwing:" + lw_risk.ToString());
            if (lang == "ru")
                report_suspicious_user_if_needed();
        }
    }
    static void patterns_check(string lang)
    {
        if (processed_by_patterns)
            return;
        try { diff_text = ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff|diffsize&difftype=inline").Result; }
        catch { return; }
        var all_ins = ins_rgx.Matches(diff_text);
        var all_del = del_rgx.Matches(diff_text);
        foreach (Match ins in all_ins)
            foreach (var pattern in patterns)
                if (pattern.IsMatch(ins.Value))
                {
                    if (all_ins.Count < 2 && all_del.Count < 2 && ((all_del.Count == 0 && ins.Value.Length < 30) || (ins.Value.Length + all_del[0].Value.Length < 30)))
                        post_suspicious_edit("ru", ins.Value, all_del[0].Value);
                    else
                        post_suspicious_edit("ru", pattern.Match(ins.Value).Value);
                    processed_by_patterns = true;
                }

    }
    static bool isRuApat(string user)
    {
        if (ru_users_by_apat_flag.ContainsKey(user))
            return ru_users_by_apat_flag[user];
        else
            if (ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=json&list=users&usprop=rights&ususers=" + Uri.EscapeDataString(user)).Result.Contains("review"))
        {
            ru_users_by_apat_flag.Add(user, true);
            return true;
        }
        else
        {
            ru_users_by_apat_flag.Add(user, false);
            return false;
        }
    }
    static bool isUkApat(string user)
    {
        if (uk_users_by_apat_flag.ContainsKey(user))
            return uk_users_by_apat_flag[user];
        else
            if (uk.GetStringAsync("https://uk.wikipedia.org/w/api.php?action=query&format=json&list=users&usprop=rights&ususers=" + Uri.EscapeDataString(user)).Result.Contains("review"))
        {
            uk_users_by_apat_flag.Add(user, true);
            return true;
        }
        else
        {
            uk_users_by_apat_flag.Add(user, false);
            return false;
        }
    }
    static int Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        discord_tokens.Add("ru", creds[10]); discord_tokens.Add("uk", creds[11]);
        liftwing_token = creds[3];
        liftwing_client.DefaultRequestHeaders.Add("Authorization", "Bearer " + liftwing_token);
        liftwing_client.DefaultRequestHeaders.Add("User-Agent", "vandalism_detection_tool_by_user_MBH");
        var goodanons = new List<string>();
        ru = Site("ru", creds[4], creds[5]);
        uk = Site("uk", creds[4], creds[5]);
        last_checked_edit_time.Add("ru", DateTime.UtcNow.AddMinutes(-1).ToString("yyyyMMddHHmmss"));
        last_checked_edit_time.Add("uk", DateTime.UtcNow.AddMinutes(-1).ToString("yyyyMMddHHmmss"));
        int currminute = -1;
        var ruconnect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki").Replace("analytics", "web"));
        ruconnect.Open();
        var ukrconnect = new MySqlConnection(creds[2].Replace("%project%", "ukwiki").Replace("analytics", "web"));
        ukrconnect.Open();
        MySqlDataReader sqlreader;
        while (true)
        {
            if (currminute != DateTime.UtcNow.Minute / 10)
            {
                currminute = DateTime.UtcNow.Minute / 10;
                var settings = ru.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/reimu-config.css&action=raw").Result.Split('\n');
                foreach (var row in settings)
                {
                    var keyvalue = row.Split(':');
                    if (keyvalue[0] == "ru-low")
                        ru_low = Convert.ToDouble(keyvalue[1]);
                    else if (keyvalue[0] == "ru-high")
                        ru_high = Convert.ToDouble(keyvalue[1]);
                    else if (keyvalue[0] == "uk-low")
                        uk_low = Convert.ToDouble(keyvalue[1]);
                    else if (keyvalue[0] == "agnostic-low")
                        agnostic_low = Convert.ToDouble(keyvalue[1]);
                    else if (keyvalue[0] == "agnostic-high")
                        agnostic_high = Convert.ToDouble(keyvalue[1]);
                    else if (keyvalue[0] == "goodanons")
                        foreach (var g in keyvalue[1].Split('|'))
                            goodanons.Add(g);
                }
                patterns.Clear();
                patterns.Add(new Regex(@"\b[СC][ВB][OО]\b")); //нельзя использовать в ignore case, как остальные
                patterns.Add(new Regex(@"[хxX][oaо0аАОAO][XХxх][лLлl]\w*")); //исключаем фамилию Хохлов
                var pattern_source = new StreamReader("patterns.txt").ReadToEnd().Split('\n');
                foreach (var pattern in pattern_source)
                    if (pattern != "")
                        patterns.Add(new Regex(pattern, RegexOptions.IgnoreCase));
            }

            bool new_last_time_saved = false;
            sqlreader = new MySqlCommand(commandtext.Replace("%time%", last_checked_edit_time["uk"]), ukrconnect).ExecuteReader();
            while (sqlreader.Read())
            {
                bool user_is_anon = sqlreader.IsDBNull(0);
                user = sqlreader.GetString("user");
                ores_risk = Math.Round(sqlreader.GetDouble("oresc_probability"), 3);
                title = sqlreader.GetString("title").Replace('_', ' ');
                newid = sqlreader.GetString("rc_this_oldid");
                if (!new_last_time_saved)
                {
                    new_last_checked_edit_time["uk"] = sqlreader.GetString("rc_timestamp");
                    new_last_time_saved = true;
                }

                if (ores_risk > uk_low)
                {
                    post_suspicious_edit("uk", "ores:" + ores_risk.ToString());
                    continue;
                }

                if (user_is_anon || !isUkApat(user))
                    liftwing_check("uk");
            }
            sqlreader.Close();
            last_checked_edit_time["uk"] = new_last_checked_edit_time["uk"];

            new_last_time_saved = false;
            sqlreader = new MySqlCommand(commandtext.Replace("%time%", last_checked_edit_time["ru"]), ruconnect).ExecuteReader();
            while (sqlreader.Read())
            {
                user = sqlreader.GetString("user") ?? "";
                if (goodanons.Contains(user))
                    continue;
                bool user_is_anon = sqlreader.IsDBNull(0);
                ores_risk = Math.Round(sqlreader.GetDouble("oresc_probability"), 3);
                title = sqlreader.GetString("title").Replace('_', ' ');
                newid = sqlreader.GetString("rc_this_oldid");
                oldid = sqlreader.GetString("rc_last_oldid");
                if (!new_last_time_saved)
                {
                    new_last_checked_edit_time["ru"] = sqlreader.GetString("rc_timestamp");
                    new_last_time_saved = true;
                }

                if (ores_risk > ru_low)
                {
                    report_suspicious_user_if_needed();
                    if (ores_risk < ru_high)
                        post_suspicious_edit("ru", "ores:" + ores_risk.ToString());

                    else
                    {
                        string answer = Save("ru", ru, "rollback", title, user, "[[u:Рейму Хакурей|автоматическая отмена]] правки участника [[special:contribs/" + user + "|" + user + "]] (" +
                            ores_risk + ")", edit_type.rollback);
                        if (answer.Contains("<rollback title="))
                            Save("ru", ru, "edit", "ut:" + user, "{{subst:u:Рейму_Хакурей/Уведомление|" + newid + "|" + title + "|" + ores_risk + "|" + (user_is_anon ? "1" : "") +
                                "}}", (user_is_anon ? "Правка с вашего IP-адреса" : "Ваша правка") + " в статье [[" + title + "]] " + "автоматически отменена", edit_type.talkpage_warning);
                        else
                            Console.WriteLine(title + "\n" + answer);
                    }
                    continue;
                }

                if (user_is_anon || !isRuApat(user))
                {
                    processed_by_patterns = false;
                    patterns_check("ru");
                    if (!processed_by_patterns)
                        liftwing_check("ru");
                }
            }
            sqlreader.Close();
            last_checked_edit_time["ru"] = new_last_checked_edit_time["ru"];

            Thread.Sleep(5000);
        }
    }
}
