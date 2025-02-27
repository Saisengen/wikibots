
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
public enum type
{
    ores, lwa, lwm, replaces, rgx, tag
}
public class color
{
    public byte r, g, b; public color(byte r, byte g, byte b) { this.r = r; this.b = b; this.g = g; }
    public long convert() { return 256 * 256 * r + 256 * g + b; }
}
public class model
{
    public string longname;
    public double limit;
}
public class Author { public string name, url; }
public class Embed { public Author author; public string title, description, url; public long color; public List<Field> fields; }
public class Field { public string name, value; }
public class discordjson { public List<Embed> embeds; }
public class Continue
{
    public string rccontinue;
    public string @continue;
}
public class Query
{
    public List<Recentchange> recentchanges;
}
public class Recentchange
{
    public string type;
    public int ns;
    public string title;    
    public int pageid;  
    public int revid;
    public int old_revid;   
    public int rcid;    
    public string user;
    public int oldlen;
    public int newlen;
    public DateTime timestamp;
    public string comment;
    public List<string> tags;
    public object oresscores;
    public bool? anon;
}
public class rchanges
{
    public bool batchcomplete;
    public Continue @continue;
    public Query query;
}
public class rgxpair
{
    public Regex one, two;
}
class Program
{
    static string commandtext = "select cast(rc_title as char) title, cast(comment_text as char) comment, oresc_probability, rc_timestamp, cast(actor_name as char) user, rc_this_oldid, rc_last_oldid, " +
        "rc_old_len, rc_new_len, rc_namespace, rc_cur_id from recentchanges join comment on rc_comment_id=comment_id join ores_classification on oresc_rev=rc_this_oldid join actor on actor_id=rc_actor " +
        "join ores_model on oresc_model=oresm_id where rc_timestamp>%time% and rc_this_oldid>%id% and (rc_type=0 or rc_type=1) and (rc_namespace=0 or rc_namespace=6 or rc_namespace=10 or rc_namespace=14 " +
        "or rc_namespace=100) and oresm_name=\"damaging\" order by rc_this_oldid desc;", user, title, comment, liftwing_token, discord_token, swviewer_token, authors_token, diff_text, comment_diff,
        discord_diff, lw_raw, strings_with_changes, lang, first_another_author_edit_id, default_time = DateTime.UtcNow.AddMinutes(-1).ToString("yyyyMMddHHmmss");
    static string[] creds;
    static Dictionary<string, HttpClient> site = new Dictionary<string, HttpClient>();
    static HttpClient client = new HttpClient();
    static double ores_risk, lw_risk, ores_limit = 1;
    static Dictionary<type, model> liftwing = new Dictionary<type, model>() { { type.lwa, new model() { longname = "language-agnostic", limit = 1 } }, { type.lwm, new model() { longname = 
        "multilingual", limit = 1 } } };
    static Regex lw_rgx = new Regex(@"""true"":(0.\d+)"), reportedusers_rgx = new Regex(@"\| вопрос = u/(.*)"), div_rgx = new Regex(@"</?div[^>]*>"),ins_del_rgx = new Regex(@"<(ins|del)[^>]*>(.*?)<[^>]*>"),
        ins_rgx = new Regex(@"<ins[^>]*>(.*?)</ins>"), del_rgx = new Regex(@"<del[^>]*>(.*?)</del>"), editcount_rgx = new Regex(@"editcount=""(\d*)"""), rev_rgx = new Regex(@"<rev "), revid_rgx = new Regex
        (@"revid=""(\d*)"""),tag_rgx = new Regex(@"<tag>([^<>]*)</tag>", RegexOptions.Singleline), empty_ins_rgx = new Regex(@"<ins[^>]*>\s*</ins>"), empty_del_rgx = new Regex(@"<del[^>]*>\s*</del>"),
        a_rgx = new Regex(@"</?a[^>]*>"), span_rgx = new Regex(@"</?span[^>]*>");
    static Dictionary<string, string> notifying_page_name = new Dictionary<string, string>() { { "ru", "user:Рейму_Хакурей/Проблемные_правки" }, { "uk", "user:Рейму_Хакурей/Підозрілі_редагування" },
        { "be", "user:Рейму_Хакурей/Падазроныя праўкі" } };
    static Dictionary<string, string> last_checked_edit_time = new Dictionary<string, string>() { { "ru", default_time }, { "uk", default_time }, { "be", default_time } };
    static Dictionary<string, int> last_checked_id = new Dictionary<string, int>() { { "ru", 0 }, { "uk", 0 }, { "be", 0 } };
    static Dictionary<int, string> ns_name = new Dictionary<int, string>() { { 0, "" }, { 6, "file:" }, { 10, "template:" }, { 14, "category:" }, { 100, "portal:" } };
    static List<string> suspicious_users = new List<string>(), trusted_users = new List<string>(), goodanons = new List<string>(), langs = new List<string>() { { "ru" }, { "uk" }/*, { "be" }*/ },
        suspicious_tags = new List<string>() { { "blank" }, { "replace" }, { "emoji" }, { "spam" }, { "спам" }, { "ожлив" }, { "ест" }, { "ASCI" } };
    static Dictionary<string, List<Regex>> patterns = new Dictionary<string, List<Regex>>();
    static List<rgxpair> replaces = new List<rgxpair>();
    static MySqlDataReader sqlreader;
    static bool new_timestamp_saved, new_id_saved;
    static int currminute = -1, diff_size, num_of_surrounding_chars = 20, startpos, endpos, editcount, ns, oldid, newid, pageid;
    static Dictionary<string, MySqlConnection> connection = new Dictionary<string, MySqlConnection>();
    static Dictionary<type, color> colors = new Dictionary<type, color>() { { type.rgx, new color(255, 0, 0) }, { type.lwa, new color(255, 255, 0) }, { type.ores, new color(255, 0, 255) },
        { type.tag, new color(0, 255, 0) }, { type.lwm, new color(255, 128, 0) }, { type.replaces, new color(0, 255, 255) } };
    static string e(string input)
    {
        return Uri.EscapeUriString(input);
    }
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
    static void post_suspicious_edit(string reason, type type)
    {
        reason += ", dsize:" + diff_size;
        if (suspicious_users.Contains(user))
        {
            client.PostAsync("https://discord.com/api/webhooks/" + authors_token, new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + user + "](<https://" + lang +
                ".wikipedia.org/wiki/special:contribs/" + e(user) + ">), " + ns_name[ns] + title} }));

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
        diff_text = empty_ins_rgx.Replace(empty_del_rgx.Replace(div_rgx.Replace(a_rgx.Replace(span_rgx.Replace(diff_text, ""), ""), ""), ""), "").Replace("\\n", "\n").Replace("\\\"", "\"");
        strings_with_changes = "";
        foreach (string str in diff_text.Split('\n'))
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

        if (lang != "be")
            Save(lang, site[lang], "edit", notifying_page_name[lang], ".", "[[toollabs:rv/r.php/" + newid + "|[" + (lang == "ru" ? "откат" : "відкат") + "] ]] [[special:diff/" + newid + "|" +
            title + "]] ([[special:history/" + title + "|" + (lang == "ru" ? "история" : "історія") + "]]), [[special:contribs/" + e(user) + "|" + user + "]], " + reason + ", " + comment_diff);

        bool single_author = false;
        string revs1 = site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + pageid + "&rvprop=ids&rvlimit=1&rvexcludeuser=" + e(user)).Result;
        if (revid_rgx.IsMatch(revs1))
            first_another_author_edit_id = revid_rgx.Match(revs1).Groups[1].Value;
        else
            single_author = true;

        string page_title = ns_name[ns] + title;
        var json = new discordjson()
        {
            embeds = new List<Embed>() { new Embed() { color = colors[type].convert(), title = page_title, url = "https://" + lang + ".wikipedia.org/w/index.php?" + (single_author ? "diff=" + newid : 
            "oldid=" + first_another_author_edit_id + "&diff=curr&ilu=" + newid), description = reason + revisions_info + ", [hist](<https://" + lang + ".wikipedia.org/wiki/special:history/" + e(page_title) +
            ">), " + "[curr](<https://" + lang + ".wikipedia.org/wiki/" + e(page_title) + ">)", fields = new List<Field>(){ new Field(){ name = comment, value =
            discord_diff }}, author = new Author(){ name = editcount == 0 ? user : user + ", " + editcount + " edits", url = "https://" + lang + ".wikipedia.org/wiki/special:contribs/" + e(user) } } }
        };
        var res = client.PostAsync("https://discord.com/api/webhooks/" + discord_token, new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json")).Result;
        if (res.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine(res.StatusCode + " " + JsonConvert.SerializeObject(json));

    }
    static void check(string lang)
    {
        Program.lang = lang;
        connection[lang] = new MySqlConnection(creds[1].Replace("%project%", lang + "wiki").Replace("analytics", "web"));
        connection[lang].Open();
        new_timestamp_saved = false; new_id_saved = false;
        sqlreader = new MySqlCommand(commandtext.Replace("%time%", last_checked_edit_time[lang]).Replace("%id%", last_checked_id[lang].ToString()), connection[lang]).ExecuteReader();
        while (sqlreader.Read())
        {
            user = sqlreader.GetString("user") ?? "";
            if (goodanons.Contains(user))
                continue;
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
            if (!trusted_users.Contains(user))
            {
                try
                {
                    editcount = Convert.ToInt32(editcount_rgx.Match(site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=&list=users&usprop=editcount" +
                        "&ususers=" + e(user)).Result).Groups[1].Value);
                }
                catch
                {
                    editcount = 0;
                }
                if (editcount > 500)
                {
                    trusted_users.Add(user);
                    continue;
                }

                if (ores_risk > ores_limit)
                {
                    post_suspicious_edit("ores:" + ores_risk.ToString(), type.ores);
                    continue;
                }

                foreach (Match edit_tag in tag_rgx.Matches(site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&revids=" + newid + "&rvprop=tags").Result))
                    foreach (string susp_tag in suspicious_tags)
                        if (edit_tag.Groups[1].Value.Contains(susp_tag))
                        {
                            post_suspicious_edit(edit_tag.Groups[1].Value, type.tag);
                            goto End;
                        }

                var alldiff = site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline").Result;
                var ins_array = ins_rgx.Matches(alldiff); var del_array = del_rgx.Matches(alldiff); string all_ins = "", all_del = "";
                foreach(var elem in ins_array)
                    all_ins += elem;
                foreach (var elem in del_array)
                    all_del += elem;
                foreach(var rgxpair in replaces)
                    if ((rgxpair.one.IsMatch(all_ins) && rgxpair.two.IsMatch(all_del) && !rgxpair.two.IsMatch(all_ins) && !rgxpair.one.IsMatch(all_del)) ||
                    (rgxpair.one.IsMatch(all_del) && rgxpair.two.IsMatch(all_ins) && !rgxpair.one.IsMatch(all_ins) && !rgxpair.two.IsMatch(all_del)))
                    {
                        post_suspicious_edit("замена", type.replaces);
                        goto End;
                    }

                foreach (Match ins in ins_array)
                    foreach (var pattern in patterns[lang])
                        if (pattern.IsMatch(ins.Groups[1].Value))
                        {
                            post_suspicious_edit(pattern.Match(ins.Groups[1].Value).Value, type.rgx);
                            goto End;
                        }

                foreach (type shortname in liftwing.Keys)
                {
                    try
                    {
                        lw_raw = lw_rgx.Match(client.PostAsync("https://api.wikimedia.org/service/lw/inference/v1/models/revertrisk-" + liftwing[shortname].longname + ":predict", new StringContent
                            ("{\"lang\":\"" + lang + "\",\"rev_id\":" + newid + "}", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result).Groups[1].Value;
                        if (lw_raw != null && lw_raw != "")
                            lw_risk = Math.Round(Convert.ToDouble(lw_raw), 3);
                    }
                    catch { goto End; }
                    if (lw_risk > liftwing[shortname].limit)
                    {
                        post_suspicious_edit(shortname + ":" + lw_risk.ToString(), shortname);
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
        goodanons.Clear();
        currminute = DateTime.UtcNow.Minute / 10;
        var settings = site["ru"].GetStringAsync("https://ru.wikipedia.org/w/index.php?title=user:MBH/reimu-config.css&action=raw").Result.Split('\n');
        foreach (var row in settings)
        {
            var keyvalue = row.Split(':');
            if (keyvalue[0] == "ores")
                ores_limit = Convert.ToDouble(keyvalue[1]);
            else if (keyvalue[0] == "goodanons")
                foreach (var g in keyvalue[1].Split('|'))
                    goodanons.Add(g);
            foreach (var type in liftwing.Keys)
                if (keyvalue[0] == liftwing[type].longname)
                    liftwing[type].limit = Convert.ToDouble(keyvalue[1]);
        }
        foreach (string lang in langs)
        {
            patterns[lang].Clear();
            foreach (string patterns_file_name in new string[] { "patterns-common.txt", "patterns-" + lang + ".txt" })
                try
                {
                    var pattern_source = new StreamReader(patterns_file_name).ReadToEnd().Split('\n');
                    bool ignorecase = false;
                    foreach (var pattern in pattern_source)
                        if (pattern == "--------")
                            ignorecase = true;
                        else
                            patterns[lang].Add(ignorecase ? new Regex(pattern, RegexOptions.IgnoreCase) : new Regex(pattern));
                }
                catch
                {
                    continue;
                }

            replaces.Clear();
            var pairs_list = new StreamReader("replaces.txt").ReadToEnd().Split('\n');
            foreach (var pair in pairs_list)
            {
                var components = pair.Split('|');
                replaces.Add(new rgxpair() { one = new Regex(components[0], RegexOptions.IgnoreCase), two = new Regex(components[1], RegexOptions.IgnoreCase) });
            }
        }
    }
    static void initialize()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        liftwing_token = creds[2].Split(':')[1]; swviewer_token = creds[3].Split(':')[1]; discord_token = creds[4].Split(':')[1]; authors_token = creds[5].Split(':')[1];

        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + liftwing_token); client.DefaultRequestHeaders.Add("User-Agent", "vandalism_detection_tool_by_user_MBH");
        var global_flags_bearers = client.GetStringAsync("https://swviewer.toolforge.org/php/getGlobals.php?ext_token=" + swviewer_token + "&user=Рейму").Result.Split('|');
        foreach (var g in global_flags_bearers)
            trusted_users.Add(g);
        foreach (string lang in langs)
        {
            site.Add(lang, Site(lang, creds[0].Split(':')[0], creds[0].Split(':')[1]));
            patterns.Add(lang, new List<Regex>());
            connection.Add(lang, new MySqlConnection());
            foreach (string flag in new string[] { "editor", "autoreview", "bot" })
            {
                string apiout, cont = "", request = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allusers&augroup=" + flag + "&aulimit=max";
                while (cont != null)
                {
                    apiout = (cont == "" ? site[lang].GetStringAsync(request).Result : site[lang].GetStringAsync(request + "&aufrom=" + e(cont)).Result);
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
        initialize();
        while (true)
        {
            if (currminute != DateTime.UtcNow.Minute / 10)
                update_settings();
            foreach (var lang in langs)
                check(lang);
            Thread.Sleep(2000);
        }
    }
}
