
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
using Newtonsoft.Json;
public class color
{
    public byte r, g, b; public color(byte r, byte g, byte b) { this.r = r; this.b = b; this.g = g; }
    public long convert() { return 256 * 256 * r + 256 * g + b; }
}
public class Author { public string name, url; }
public class Embed { public Author author; public string title, description, url; public long color; public List<Field> fields; }
public class Field { public string name, value; }
public class Root { public List<Embed> embeds; }
class Program
{
    static string commandtext = "select actor_user, cast(rc_title as char) title, cast(comment_text as char) comment, oresc_probability, rc_timestamp, cast(actor_name as char) user, rc_this_oldid, " +
        "rc_last_oldid, rc_old_len, rc_new_len, rc_namespace, rc_cur_id from recentchanges " +
        "join comment on rc_comment_id=comment_id " +
        "join ores_classification on oresc_rev=rc_this_oldid " +
        "join actor on actor_id=rc_actor " +
        "join ores_model on oresc_model=oresm_id " +
        "where rc_timestamp>%time% and rc_this_oldid>%id% and (rc_type=0 or rc_type=1) and (rc_namespace=0 or rc_namespace=6 or rc_namespace=10 or rc_namespace=14 or rc_namespace=100) " +
        "and oresm_name=\"damaging\" order by rc_this_oldid desc;", user, title, comment, liftwing_token, discord_token, swviewer_token, authors_token, diff_text, comment_diff, discord_diff, lw_raw, 
        strings_with_changes, default_time = DateTime.UtcNow.AddMinutes(-1).ToString("yyyyMMddHHmmss");
    static string[] creds;
    static Dictionary<string, HttpClient> site = new Dictionary<string, HttpClient>();
    static HttpClient client = new HttpClient();
    static double ores_risk, lw_risk, ores_limit = 1;
    static Dictionary<string, double> lw_limit = new Dictionary<string, double>() { { "agnostic", 1 }, { "multilang", 1 } };
    static Regex liftwing_rgx = new Regex(@"""true"":(0.\d+)"),
        reportedusers_rgx = new Regex(@"\| вопрос = u/(.*)"),
        ins_rgx = new Regex(@"<ins[^>]*>(.*?)</ins>"),
        tag_rgx = new Regex(@"<tag>([^<>]*)</tag>", RegexOptions.Singleline),
        ins_del_rgx = new Regex(@"<(ins|del)[^>]*>(.*?)<[^>]*>"),
        div_rgx = new Regex(@"</?div[^>]*>"),
        del_rgx = new Regex(@"<del[^>]*>(.*?)</del>"),
        editcount_rgx = new Regex(@"editcount=""(\d*)"""),
        rev_rgx = new Regex(@"<rev ");
    static Dictionary<string, string> notifying_page_name = new Dictionary<string, string>() { { "ru", "user:Рейму_Хакурей/Проблемные_правки" }, { "uk", "user:Рейму_Хакурей/Підозрілі_редагування" } };
    static Dictionary<string, string> last_checked_edit_time = new Dictionary<string, string>() { { "ru", default_time }, { "uk", default_time } };
    static Dictionary<string, int> last_checked_id = new Dictionary<string, int>() { { "ru", 0 }, { "uk", 0 } };
    static Dictionary<int, string> ns_name = new Dictionary<int, string>() { { 0, "" }, { 6, "file:" }, { 10, "template:" }, { 14, "category:" }, { 100, "portal:" } };
    static List<string> suspicious_users = new List<string>(), trusted_users = new List<string>(), goodanons = new List<string>(), suspicious_tags = new List<string>()
    { { "blank" }, { "replace" }, { "emoji" }, { "spam" }, { "спам" }, { "ожлив" }, { "тест" }, { "Тест" } };
    static Dictionary<string, List<Regex>> patterns = new Dictionary<string, List<Regex>>();
    static MySqlDataReader sqlreader;
    static bool user_is_anon, new_timestamp_saved, new_id_saved;
    static int currminute = -1, diff_size, num_of_surrounding_chars = 20, startpos, endpos, editcount, ns, newid, oldid, pageid;
    static Dictionary<string, MySqlConnection> connection = new Dictionary<string, MySqlConnection>();
    static Dictionary<string, color> colors = new Dictionary<string, color>() { { "pattern", new color(255, 0, 0) }, { "liftwing", new color(255, 255, 0) }, { "ores", new color(255, 0, 255) }, { "tag", new color(0, 255, 0) } };
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login },
            { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } }));
        return client;
    }
    static string Save(string lang, HttpClient site, string action, string title, string appendtext, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var request = new MultipartFormDataContent{{ new StringContent(action),"action" },{ new StringContent(title),"title" },{ new StringContent(comment), "summary" },{ new StringContent("xml"),"format" },
            { new StringContent(doc.SelectSingleNode("//tokens/@csrftoken").Value), "token" }, { new StringContent(appendtext), "appendtext" } };
        return site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static void post_suspicious_edit(string lang, string reason, string type)
    {
        if (suspicious_users.Contains(user))
        {
            client.PostAsync("https://discord.com/api/webhooks/" + authors_token, new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + user + "](<https://" + lang +
                ".wikipedia.org/wiki/special:contribs/" + Uri.EscapeUriString(user) + ">), " + ns_name[ns] + title} }));

            if (lang == "ru")
            {
                string zkab = site["ru"].GetStringAsync("https://ru.wikipedia.org/w/index.php?title=ВП:Запросы_к_администраторам/Быстрые&action=raw").Result;
                var reportedusers = reportedusers_rgx.Matches(zkab);
                bool reportedyet = false;
                foreach (Match r in reportedusers)
                    if (user == r.Groups[1].Value)
                        reportedyet = true;
                if (!reportedyet)
                    Save("ru", site["ru"], "edit", "ВП:Запросы к администраторам/Быстрые", "\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}", "[[special:contribs/" + user + "]] - новый запрос");
            }
        }
        else suspicious_users.Add(user);
        string diff_request = "https://" + lang + ".wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline";
        diff_text = site[lang].GetStringAsync(diff_request).Result.Replace("&#160;", " ");

        strings_with_changes = "";
        string prepared_text = div_rgx.Replace(diff_text, "").Replace("\\n", "\n");
        foreach (string str in prepared_text.Split('\n'))
            if (ins_del_rgx.IsMatch(str))
            {
                var matches = ins_del_rgx.Matches(str);
                startpos = matches[0].Index - num_of_surrounding_chars;
                if (startpos < 0) startpos = 0;
                endpos = matches[matches.Count - 1].Index + matches[matches.Count - 1].Length + num_of_surrounding_chars;
                if (endpos >= str.Length) endpos = str.Length - 1;
                strings_with_changes += str.Substring(startpos, endpos - startpos + 1).Replace("&lt;", "<").Replace("&gt;", ">") + "<...>";
            }
        comment_diff = ins_rgx.Replace(del_rgx.Replace(strings_with_changes, "-$1 "), "+$1 ");
        discord_diff = ins_rgx.Replace(del_rgx.Replace(strings_with_changes, "~~$1~~ "), "`$1` ");

        if (discord_diff.Length > 1022)
            discord_diff = discord_diff.Substring(0, 1022);
        if (comment.Length > 254)
            comment = comment.Substring(0, 254);

        string revs = site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + pageid + "&rvprop=ids&rvlimit=10").Result;
        int num_of_revs = rev_rgx.Matches(revs).Count;
        string revisions_info = num_of_revs > 9 ? "" : ", revs: " + num_of_revs;

        Save(lang, site[lang], "edit", notifying_page_name[lang], ".", "[[toollabs:rv/r.php/" + newid + "|[" + (lang == "ru" ? "откат" : "відкат") + "] ]] [[special:diff/" + newid + "|" +
            title + "]] ([[special:history/" + title + "|" + (lang == "ru" ? "история" : "історія") + "]]), [[special:contribs/" + Uri.EscapeDataString(user) + "|" + user + "]], " + reason + ", " + comment_diff);

        var json = new Root()
        {
            embeds = new List<Embed>() { new Embed() { color = colors[type].convert(), title = ns_name[ns] + title, url = "https://" + lang + ".wikipedia.org/w/index.php?diff=" + newid, description =
            "[" + reason + revisions_info + "](<https://" + lang + ".wikipedia.org/wiki/special:history/" + ns_name[ns] + Uri.EscapeDataString(title) + ">)", fields = 
            new List<Field>(){ new Field(){ name = comment, value = discord_diff }}, author = new Author(){ name = user_is_anon ? user : user + ", " + editcount + " edits", url = "https://" + lang + 
                ".wikipedia.org/wiki/special:contribs/" + Uri.EscapeDataString(user) } } }
        };
        var res = client.PostAsync("https://discord.com/api/webhooks/" + discord_token, new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json")).Result;

        

        if (res.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine(res.StatusCode + " " + JsonConvert.SerializeObject(json));

    }
    static void check(string lang)
    {
        connection[lang] = new MySqlConnection(creds[1].Replace("%project%", lang + "wiki").Replace("analytics", "web"));
        connection[lang].Open();
        new_timestamp_saved = false; new_id_saved = false;
        sqlreader = new MySqlCommand(commandtext.Replace("%time%", last_checked_edit_time[lang]).Replace("%id%", last_checked_id[lang].ToString()), connection[lang]).ExecuteReader();
        while (sqlreader.Read())
        {
            user = sqlreader.GetString("user") ?? "";
            if (goodanons.Contains(user))
                continue;
            user_is_anon = sqlreader.IsDBNull(0);
            ores_risk = Math.Round(sqlreader.GetDouble("oresc_probability"), 3);
            title = sqlreader.GetString("title").Replace('_', ' ');
            ns = sqlreader.GetInt16("rc_namespace");
            newid = sqlreader.GetInt32("rc_this_oldid");
            oldid = sqlreader.GetInt32("rc_last_oldid");
            pageid = sqlreader.GetInt32("rc_cur_id");
            comment = sqlreader.GetString("comment");
            diff_size = sqlreader.GetInt32("rc_new_len") - sqlreader.GetInt32("rc_old_len");
            if (!new_timestamp_saved)
            {
                last_checked_edit_time[lang] = sqlreader.GetString("rc_timestamp"); new_timestamp_saved = true;
            }
            if (!new_id_saved)
            {
                last_checked_id[lang] = newid; new_id_saved = true;
            }
            if (user_is_anon || !trusted_users.Contains(user))
            {
                if (!user_is_anon)
                    editcount = Convert.ToInt32(editcount_rgx.Match(site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=&list=users&usprop=editcount&ususers=" +
                        Uri.EscapeDataString(user)).Result).Groups[1].Value);
                if (editcount > 1000)
                {
                    trusted_users.Add(user);
                    continue;
                }

                if (ores_risk > ores_limit)
                {
                    post_suspicious_edit(lang, "ores:" + ores_risk.ToString() + ", diffsize:" + diff_size, "ores");
                    continue;
                }

                foreach (Match edit_tag in tag_rgx.Matches(site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&revids=" + newid + "&rvprop=tags").Result))
                    foreach (string susp_tag in suspicious_tags)
                        if (edit_tag.Groups[1].Value.Contains(susp_tag))
                        {
                            post_suspicious_edit(lang, edit_tag.Groups[1].Value + ", diffsize:" + diff_size, "tag");
                            goto End;
                        }

                var all_ins = ins_rgx.Matches(site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline").Result);
                foreach (Match ins in all_ins)
                    foreach (var pattern in patterns[lang])
                        if (pattern.IsMatch(ins.Groups[1].Value))
                        {
                            post_suspicious_edit(lang, pattern.Match(ins.Groups[1].Value).Value + ", diffsize:" + diff_size, "pattern");
                            goto End;
                        }

                foreach (string type in new string[] { "agnostic", "multilang" })
                {
                    try
                    {
                        lw_raw = liftwing_rgx.Match(client.PostAsync("https://api.wikimedia.org/service/lw/inference/v1/models/revertrisk-" + (type == "agnostic" ? "language-agnostic" : "multilingual") +
                            ":predict", new StringContent("{\"lang\":\"" + lang + "\",\"rev_id\":" + newid + "}", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result).Groups[1].Value;
                        if (lw_raw != null && lw_raw != "")
                            lw_risk = Math.Round(Convert.ToDouble(lw_raw), 3);
                    }
                    catch { goto End; }
                    if (lw_risk > lw_limit[type])
                    {
                        post_suspicious_edit(lang, "lw-" + type + ":" + lw_risk.ToString() + ", diffsize:" + diff_size, "liftwing");
                        goto End;
                    }
                }
            End:;
            }
        }
        sqlreader.Close();
        connection[lang].Close();
    }
    static void update_settings()
    {
        if (currminute != DateTime.UtcNow.Minute / 10)
        {
            goodanons.Clear();
            currminute = DateTime.UtcNow.Minute / 10;
            var settings = site["ru"].GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/reimu-config.css&action=raw").Result.Split('\n');
            foreach (var row in settings)
            {
                var keyvalue = row.Split(':');
                if (keyvalue[0] == "ores")
                    ores_limit = Convert.ToDouble(keyvalue[1]);
                else if (keyvalue[0] == "agnostic")
                    lw_limit["agnostic"] = Convert.ToDouble(keyvalue[1]);
                else if (keyvalue[0] == "multilang")
                    lw_limit["multilang"] = Convert.ToDouble(keyvalue[1]);
                else if (keyvalue[0] == "goodanons")
                    foreach (var g in keyvalue[1].Split('|'))
                        goodanons.Add(g);
            }
            foreach (string lang in new string[] { "ru", "uk" })
            {
                patterns[lang].Clear();
                patterns[lang].Add(new Regex(@"\b[СC][ВB][OО]\b")); //нельзя использовать в ignore case, как остальные
                patterns[lang].Add(new Regex(@"[хxX][oaо0аАОAO][XХxх][лLлl]\w*")); //исключаем фамилию Хохлов
                foreach (string patterns_file_name in new string[] { "patterns-common.txt", "patterns-" + lang + ".txt" } )
                {
                    var pattern_source = new StreamReader(patterns_file_name).ReadToEnd().Split('\n');
                    foreach (var pattern in pattern_source)
                        if (pattern != "")
                            patterns[lang].Add(new Regex(pattern, RegexOptions.IgnoreCase));
                }
            }
        }
    }
    static void gather_trusted_users()
    {
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + liftwing_token);
        client.DefaultRequestHeaders.Add("User-Agent", "vandalism_detection_tool_by_user_MBH");
        var global_flags_bearers = client.GetStringAsync("https://swviewer.toolforge.org/php/getGlobals.php?ext_token=" + swviewer_token + "&user=Рейму").Result.Split('|');
        foreach (var g in global_flags_bearers)
            trusted_users.Add(g);
        foreach (string lang in new string[] { "ru", "uk" })
        {
            site.Add(lang, Site(lang, creds[0].Split(':')[0], creds[0].Split(':')[1]));
            patterns.Add(lang, new List<Regex>());
            connection.Add(lang, new MySqlConnection());
            foreach (string flag in new string[] { "editor", "autoreview", "bot" })
            {
                string apiout, cont = "", request = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allusers&augroup=" + flag + "&aulimit=max";
                while (cont != null)
                {
                    apiout = (cont == "" ? site[lang].GetStringAsync(request).Result : site[lang].GetStringAsync(request + "&aufrom=" + Uri.EscapeDataString(cont)).Result);
                    using (var r = new XmlTextReader(new StringReader(apiout)))
                    {
                        r.WhitespaceHandling = WhitespaceHandling.None;
                        r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("aufrom");
                        while (r.Read())
                            if (r.Name == "u")
                                if (!trusted_users.Contains(r.GetAttribute("name")))
                                    trusted_users.Add(r.GetAttribute("name"));
                    }
                }
            }
        }
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        liftwing_token = creds[2].Split(':')[1]; swviewer_token = creds[3].Split(':')[1]; discord_token = creds[4].Split(':')[1]; authors_token = creds[5].Split(':')[1];

        gather_trusted_users();
        while (true)
        {
            update_settings();
            check("ru");
            check("uk");
            Thread.Sleep(4000);
        }
    }
}
