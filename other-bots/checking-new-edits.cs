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
    static string commandtext = "select actor_user, cast(rc_title as char) title, oresc_probability, rc_timestamp, cast(actor_name as char) user, rc_this_oldid, rc_last_oldid, rc_old_len, rc_new_len from " +
        "recentchanges join ores_classification on oresc_rev=rc_this_oldid join actor on actor_id=rc_actor join ores_model on oresc_model=oresm_id where rc_timestamp>%time% and (rc_type=0 or rc_type=1) and " +
        "rc_namespace=0 and oresm_name=\"damaging\" order by rc_this_oldid desc;", user, title, newid, oldid, liftwing_token, swviewer_token, diff_text, default_time = DateTime.UtcNow.AddMinutes(-1).ToString("yyyyMMddHHmmss");
    static HttpClient client = new HttpClient(), ruwiki, ukwiki;
    static double ores_risk, lw_risk, ores_limit = 1, agnostic_limit = 1, ru_high = 1;
    static Regex row_rgx = new Regex(@"\|-"), liftwing_rgx = new Regex(@"""true"":(0.9\d+)"), reportedusers_rgx = new Regex(@"\| вопрос = u/(.*)"),
        ins_del_rgx = new Regex(@"<(ins|del)[^>]*>([^<>]*)</"), ins_rgx = new Regex(@"<ins[^>]*>([^<>]*)</");
    static Dictionary<string, string> notifying_page_name = new Dictionary<string, string>() { { "ru", "user:Рейму_Хакурей/Проблемные_правки" }, { "uk", "user:Рейму_Хакурей/Підозрілі_редагування" } };
    static Dictionary<string, string> notifying_header = new Dictionary<string, string>() { { "ru", "!Дифф!!Статья!!Автор!!Причина" }, { "uk", "!Diff!!Стаття!!Автор!!Причина" } };
    static Dictionary<string, string> discord_tokens = new Dictionary<string, string>();
    static Dictionary<string, string> last_checked_edit_time = new Dictionary<string, string>() { { "ru", default_time }, { "uk", default_time } };
    static List<string> suspicious_users = new List<string>(), trusted_users = new List<string>();
    static List<Regex> patterns = new List<Regex>();
    static List<string> goodanons = new List<string>();
    static MySqlDataReader sqlreader;
    static MySqlConnection ruconnect, ukrconnect;
    static bool user_is_anon, new_last_time_saved;
    static int max_diff_length_for_show, sum_of_ins_del_lengths, currminute = -1, diff_size;
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
        var request = new MultipartFormDataContent { { new StringContent(action), "action" }, { new StringContent(title), "title" }, { new StringContent(comment), "summary" } };
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
    static void post_suspicious_edit(string lang, string reason, MatchCollection changes)
    {
        string wiki_diff, comment_diff, discord_diff;
        wiki_diff = reason + ", "; comment_diff = reason + ", "; discord_diff = "";
        int increment = 0;
        foreach (Match change in changes)
        {
            string new_addition = change.Groups[2].Value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("\\\"", "\"");
            increment += new_addition.Length;
            if (increment > max_diff_length_for_show)
                break;
            if (change.Groups[1].Value == "ins")
            {
                wiki_diff += "<b><span class=ins><nowiki>" + new_addition + "</nowiki></span></b>";
                comment_diff += "+" + new_addition;
                discord_diff += "`" + new_addition + "`";
            }
            else
            {
                wiki_diff += "<b><span class=del><nowiki>" + new_addition + "</nowiki></span></b>";
                comment_diff += "-" + new_addition;
                discord_diff += "~~" + new_addition + "~~";
            }
        }

        string get_request = "https://" + lang + ".wikipedia.org/w/index.php?title=" + notifying_page_name[lang] + "&action=raw";
        string notifying_page_text = (lang == "ru" ? ruwiki.GetStringAsync(get_request).Result : ukwiki.GetStringAsync(get_request).Result);
        notifying_page_text = notifying_page_text.Replace(notifying_header[lang], notifying_header[lang] + "\n|-\n|[[special:diff/" + newid + "|diff]]||[[special:history/" + title + "|" + title + "]]||" +
            "[[special:contribs/" + user + "|" + user + "]]||" + wiki_diff);
        var rows = row_rgx.Matches(notifying_page_text);
        notifying_page_text = notifying_page_text.Substring(0, rows[rows.Count - 1].Index);
        Save(lang, (lang == "ru" ? ruwiki : ukwiki), "edit", notifying_page_name[lang], notifying_page_text, "[[special:diff/" + newid + "|" + title + "]] ([[special:history/" + title + "|" +
            (lang == "ru" ? "история" : "історія") + "]]), [[special:contribs/" + Uri.EscapeDataString(user) + "|" + user + "]], " + comment_diff, edit_type.suspicious_edit);

        var res = client.PostAsync("https://discord.com/api/webhooks/" + discord_tokens[lang], new StringContent("{\"embeds\":[{\"author\":{\"name\":\"" + user +
            "\",\"url\":\"https://" + lang + ".wikipedia.org/wiki/special:contribs/" + Uri.EscapeDataString(user) + "\"},\"title\":\"" + title + "\",\"description\":" +
            "\"[" + reason + "](<https://" + lang + ".wikipedia.org/wiki/special:history/" + Uri.EscapeDataString(title) + ">)\",\"url\":\"https://" + lang +
            ".wikipedia.org/w/index.php?diff=" + newid + "\",\"fields\":[{\"name\":\"\",\"value\":\"" + discord_diff + "\"}]}]}", Encoding.UTF8, "application/json")).Result;
        if (res.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine(res.StatusCode);
    }
    static void report_suspicious_user_if_needed()
    {
        if (suspicious_users.Contains(user))
        {
            string zkab = ruwiki.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=ВП:Запросы_к_администраторам/Быстрые&action=raw").Result;
            var reportedusers = reportedusers_rgx.Matches(zkab);
            bool reportedyet = false;
            foreach (Match r in reportedusers)
                if (user == r.Groups[1].Value)
                    reportedyet = true;
            if (!reportedyet)
                Save("ru", ruwiki, "edit", "ВП:Запросы к администраторам/Быстрые", "\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}", "[[special:contribs/" + user + "]] - новый запрос", edit_type.zkab_report);
        }
        else suspicious_users.Add(user);
    }
    static void liftwing_check(string lang, int diff_size)
    {
        string raw;
        try
        {
            raw = liftwing_rgx.Match(client.PostAsync("https://api.wikimedia.org/service/lw/inference/v1/models/revertrisk-language-agnostic:predict",
                new StringContent("{\"lang\":\"" + lang + "\",\"rev_id\":" + newid + "}", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result).Groups[1].Value;
            if (raw != null && raw != "")
                lw_risk = Math.Round(Convert.ToDouble(raw), 3);
            else
                return;
        }
        catch { return; }
        if (lw_risk > agnostic_limit)
        {
            process_diff(lang, "liftwing:" + lw_risk.ToString() + ", diffsize:" + diff_size);
            if (lang == "ru")
                report_suspicious_user_if_needed();
        }
    }
    static void process_diff(string lang, string reason)
    {
        string request = "https://" + lang + ".wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline";
        if (lang == "ru")
            diff_text = ruwiki.GetStringAsync(request).Result;
        else
            diff_text = ukwiki.GetStringAsync(request).Result;
        post_suspicious_edit(lang, reason, ins_del_rgx.Matches(diff_text));
    }
    static void update_settings()
    {
        if (currminute != DateTime.UtcNow.Minute / 10)
        {
            goodanons.Clear();
            currminute = DateTime.UtcNow.Minute / 10;
            var settings = ruwiki.GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/reimu-config.css&action=raw").Result.Split('\n');
            foreach (var row in settings)
            {
                var keyvalue = row.Split(':');
                if (keyvalue[0] == "ores")
                    ores_limit = Convert.ToDouble(keyvalue[1]);
                else if (keyvalue[0] == "ru-high")
                    ru_high = Convert.ToDouble(keyvalue[1]);
                else if (keyvalue[0] == "agnostic")
                    agnostic_limit = Convert.ToDouble(keyvalue[1]);
                else if (keyvalue[0] == "max-diff-size")
                    max_diff_length_for_show = Convert.ToInt16(keyvalue[1]);
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
    }
    static void collect_trusted_users()
    {
        var global_flags_bearers = client.GetStringAsync("https://swviewer.toolforge.org/php/getGlobals.php?ext_token=" + swviewer_token + "&user=Рейму").Result.Split('|');
        foreach (var g in global_flags_bearers)
            trusted_users.Add(g);
        foreach (string flag in new string[] { "editor", "autoreview", "bot" })
            using (var r = new XmlTextReader(new StringReader(ruwiki.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allusers&augroup=" + flag + "&aulimit=max").Result)))
                while (r.Read())
                    if (r.Name == "u")
                        if (!trusted_users.Contains(r.GetAttribute("name")))
                            trusted_users.Add(r.GetAttribute("name"));
        foreach (string flag in new string[] { "editor", "autoreview" })
            using (var r = new XmlTextReader(new StringReader(ukwiki.GetStringAsync("https://uk.wikipedia.org/w/api.php?action=query&format=xml&list=allusers&augroup=" + flag + "&aulimit=max").Result)))
                while (r.Read())
                    if (r.Name == "u")
                        if (!trusted_users.Contains(r.GetAttribute("name")))
                            trusted_users.Add(r.GetAttribute("name"));
    }
    static int Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        discord_tokens.Add("ru", creds[10]); discord_tokens.Add("uk", creds[11]); liftwing_token = creds[3]; swviewer_token = creds[12];

        //var res = client.PostAsync("https://discord.com/api/webhooks/" + discord_tokens["ru"], new StringContent("{\"embeds\":[{\"author\":{\"name\":\"reason\",\"url\":\"https://ru.wikipedia.org\"},\"title\":\"статья\",\"description\":\"ores 456\",\"url\":\"https://ru.wikipedia.org\",\"fields\":[{\"name\":\"\",\"value\":\"frgnhtyrj\"}]}]}", Encoding.UTF8, "application/json")).Result;
        
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + liftwing_token); client.DefaultRequestHeaders.Add("User-Agent", "vandalism_detection_tool_by_user_MBH");
        ruwiki = Site("ru", creds[4], creds[5]); ukwiki = Site("uk", creds[4], creds[5]);
        collect_trusted_users();
        ruconnect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki").Replace("analytics", "web")); ruconnect.Open();
        ukrconnect = new MySqlConnection(creds[2].Replace("%project%", "ukwiki").Replace("analytics", "web")); ukrconnect.Open();
        while (true)
        {
            update_settings();

            new_last_time_saved = false;
            sqlreader = new MySqlCommand(commandtext.Replace("%time%", last_checked_edit_time["uk"]), ukrconnect).ExecuteReader();
            while (sqlreader.Read())
            {
                user = sqlreader.GetString("user") ?? "";
                if (goodanons.Contains(user))
                    continue;
                user_is_anon = sqlreader.IsDBNull(0);
                ores_risk = Math.Round(sqlreader.GetDouble("oresc_probability"), 3);
                title = sqlreader.GetString("title").Replace('_', ' ');
                newid = sqlreader.GetString("rc_this_oldid");
                oldid = sqlreader.GetString("rc_last_oldid");
                diff_size = sqlreader.GetInt32("rc_new_len") - sqlreader.GetInt32("rc_old_len");
                if (!new_last_time_saved)
                {
                    last_checked_edit_time["uk"] = sqlreader.GetString("rc_timestamp");
                    new_last_time_saved = true;
                }

                if (ores_risk > ores_limit)
                {
                    process_diff("uk", "ores:" + ores_risk.ToString() + ", diffsize:" + diff_size);
                    continue;
                }

                if (user_is_anon || !trusted_users.Contains(user))
                    liftwing_check("uk", diff_size);
            }
            sqlreader.Close();

            new_last_time_saved = false;
            sqlreader = new MySqlCommand(commandtext.Replace("%time%", last_checked_edit_time["ru"]), ruconnect).ExecuteReader();
            while (sqlreader.Read())
            {
                user = sqlreader.GetString("user") ?? "";
                if (goodanons.Contains(user))
                    continue;
                user_is_anon = sqlreader.IsDBNull(0);
                ores_risk = Math.Round(sqlreader.GetDouble("oresc_probability"), 3);
                title = sqlreader.GetString("title").Replace('_', ' ');
                newid = sqlreader.GetString("rc_this_oldid");
                oldid = sqlreader.GetString("rc_last_oldid");
                diff_size = sqlreader.GetInt32("rc_new_len") - sqlreader.GetInt32("rc_old_len");
                if (!new_last_time_saved)
                {
                    last_checked_edit_time["ru"] = sqlreader.GetString("rc_timestamp");
                    new_last_time_saved = true;
                }

                if (ores_risk > ores_limit)
                {
                    report_suspicious_user_if_needed();
                    if (ores_risk < ru_high)
                        process_diff("ru", "ores:" + ores_risk.ToString() + ", diffsize:" + diff_size);

                    else
                    {
                        string answer = Save("ru", ruwiki, "rollback", title, user, "[[u:Рейму Хакурей|автоматическая отмена]] правки участника [[special:contribs/" + user + "|" + user + "]] (" +
                            ores_risk + ")", edit_type.rollback);
                        if (answer.Contains("<rollback title="))
                            Save("ru", ruwiki, "edit", "ut:" + user, "{{subst:u:Рейму_Хакурей/Уведомление|" + newid + "|" + title + "|" + ores_risk + "|" + (user_is_anon ? "1" : "") +
                                "}}", (user_is_anon ? "Правка с вашего IP-адреса" : "Ваша правка") + " в статье [[" + title + "]] " + "автоматически отменена", edit_type.talkpage_warning);
                        else
                            Console.WriteLine(title + "\n" + answer);
                    }
                    continue;
                }

                if (user_is_anon || !trusted_users.Contains(user))
                {
                    var all_ins = ins_rgx.Matches(ruwiki.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline").Result);
                    foreach (Match ins in all_ins)
                        foreach (var pattern in patterns)
                            if (pattern.IsMatch(ins.Groups[1].Value))
                            {
                                process_diff("ru", pattern.Match(ins.Groups[1].Value).Value + ", diffsize:" + diff_size);
                                goto End;
                            }
                    liftwing_check("ru", diff_size);
                End:;
                }
            }
            sqlreader.Close();

            Thread.Sleep(5000);
        }
    }
}
