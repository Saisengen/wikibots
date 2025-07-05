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
using System.Linq;
public enum type { ores, lwa, lwm, replace, addition, tag, deletion }
public enum lang { ru, uk, be, c, d }
public class color { public byte r, g, b; public color(byte r, byte g, byte b) { this.r = r; this.b = b; this.g = g; } public long convert() { return 256 * 256 * r + 256 * g + b; } }
public class model { public string longname; public double limit; }
public class Author { public string name, url; }
public class Embed { public Author author; public string title, description, url; public long color; public List<Field> fields; }
public class Field { public string name, value; }
public class discordjson { public List<Embed> embeds; }
public class Continue { public string rccontinue; public string @continue; }
public class Query { public List<Recentchange> recentchanges; }
public class Recentchange { public string type, title, user, comment; public Int64 revid, old_revid, rcid; public int oldlen, newlen, ns, pageid; public DateTime timestamp; public List<string> tags;
    public object oresscores; public bool? anon; }
public class rchanges { public bool batchcomplete; public Continue @continue; public Query query; }
public class replace_pair { public Regex one, two; public string replacement; }
public class langdata_element { public string last_checked_edit_time, notifying_page_name, domain; public Int64 last_checked_id; }
public class pattern_info { public Regex regex; public bool only_content, not_uk; public int stringnumber; }
class Program
{
    static string user, title, comment, liftwing_token, discord_token, swviewer_token, authors_token, diff_text, comment_diff, discord_diff, lw_raw, strings_with_changes, edit_id_of_first_another_author,
        all_ins, all_del, reason, default_time = DateTime.UtcNow.AddMinutes(-2).ToString("yyyy-MM-ddTHH:mm:ss.000Z"); static string[] settings; static lang lang;
    static Dictionary<lang, HttpClient> site = new Dictionary<lang, HttpClient>(); static HttpClient client = new HttpClient(); static double ores_value, lw_value, ores_limit = 1;
    static Dictionary<type, model> liftwing = new Dictionary<type, model>() { { type.lwa, new model() { longname = "language-agnostic", limit = 1 } }, { type.lwm, new model() { longname = 
        "multilingual", limit = 1 } } }; static HashSet<string> suspicious_users = new HashSet<string>(), trusted_users = new HashSet<string>();
    static Regex lw_rgx = new Regex(@"""true"":(0.\d+)"), reportedusers_rgx = new Regex(@"\| вопрос = u/(.*)"), ins_del_rgx = new Regex(@"<(ins|del)[^>]*>(.*?)<[^>]*>"), ins_rgx = new Regex
        (@"<ins[^>]*>(.*?)</ins>"), del_rgx = new Regex(@"<del[^>]*>(.*?)</del>"), editcount_rgx = new Regex(@"editcount=""(\d*)"""), rev_rgx = new Regex(@"<rev "), revid_rgx = new Regex
        (@"revid=""(\d*)"""), damage_rgx = new Regex(@"damaging"":\s*\{\s*""true"":\s*(0.\d{3})", RegexOptions.Singleline), empty_ins_rgx = new Regex(@"<ins[^>]*>\s*</ins>"), empty_del_rgx = new Regex
        (@"<del[^>]*>\s*</del>"), trash_tags_rgx = new Regex(@"</?(a|b|span|div|table|th|tr|td)[^>]*>"), suspicious_tags_rgx, deletions_rgx, whitelist_text_rgx, whitelist_title_rgx, talk_ns_rgx = 
        new Regex("2|4|100|104|106"), wd_label_rgx = new Regex("<label language=\"([^\"]*)\" value=\"([^\"]*)\"");
    static Dictionary<lang, langdata_element> langdata = new Dictionary<lang, langdata_element>() {
        { global::lang.ru, new langdata_element() { last_checked_edit_time = default_time, last_checked_id = 0, notifying_page_name = "user:Рейму_Хакурей/Проблемные_правки", domain = "ru.wikipedia" } },
        { global::lang.uk, new langdata_element() { last_checked_edit_time = default_time, last_checked_id = 0, notifying_page_name = "user:Рейму_Хакурей/Підозрілі_редагування", domain = "uk.wikipedia" } },
        { global::lang.be, new langdata_element() { last_checked_edit_time = default_time, last_checked_id = 0, notifying_page_name = "", domain = "be.wikipedia" } },
        { global::lang.c, new langdata_element() { last_checked_edit_time = default_time, last_checked_id = 0, notifying_page_name = "", domain = "commons.wikimedia" } },
        { global::lang.d, new langdata_element() { last_checked_edit_time = default_time, last_checked_id = 0, notifying_page_name = "", domain = "www.wikidata" } } };
    static List<pattern_info> patterns = new List<pattern_info>(); static List<replace_pair> replaces = new List<replace_pair>();
    static int currminute = -1, diff_size, num_of_surrounding_chars = 25, num_of_revs_to_check = 20, startpos, endpos, editcount, pageid, ns; static Int64 newid, oldid;
    static Dictionary<type, color> colors = new Dictionary<type, color>() { { type.addition, new color(255, 0, 0) }, { type.lwa, new color(255, 255, 0) }, { type.ores, new color(255, 0, 255) },
        { type.tag, new color(0, 255, 0) }, { type.lwm, new color(255, 128, 0) }, { type.replace, new color(0, 255, 255) }, { type.deletion, new color(255, 255, 255) } };
    static void Main() { initialize_bot(); while (true) { if (currminute != DateTime.UtcNow.Minute / 10) { currminute = DateTime.UtcNow.Minute / 10; read_config(); read_patterns(); }
            foreach (var lang in langdata.Keys) check(lang); Thread.Sleep(2000); } }
    static void initialize_bot()
    {
        settings = new StreamReader("./reimu/config.txt").ReadToEnd().Split('\n');
        liftwing_token = settings[1].Split(':')[1]; swviewer_token = settings[2].Split(':')[1]; discord_token = settings[3].Split(':')[1]; authors_token = settings[4].Split(':')[1];

        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + liftwing_token); client.DefaultRequestHeaders.Add("User-Agent", "vandalism_detection_tool_by_user_MBH");
        var swviewer_trusted_users = client.GetStringAsync("https://swviewer.toolforge.org/php/getGlobals.php?ext_token=" + swviewer_token + "&user=Рейму").Result.Split('|');
        foreach (var g in swviewer_trusted_users)
            trusted_users.Add(g);
        foreach (lang lang in langdata.Keys)
        {
            site.Add(lang, Site(lang, settings[0].Split(':')[0], settings[0].Split(':')[1]));
            foreach (string flag in new string[] { "editor", "autoreview", "bot" })
            {
                if ((lang == lang.d || lang == lang.c) && (flag == "editor" || flag == "autoreview"))
                    continue;
                string apiout, cont = "", request = "https://" + langdata[lang].domain + ".org/w/api.php?action=query&format=xml&list=allusers&augroup=" + flag + "&aulimit=max";
                while (cont != null)
                {
                    apiout = (cont == "" ? site[lang].GetStringAsync(request).Result : site[lang].GetStringAsync(request + "&aufrom=" + e(cont)).Result);
                    using (var r = new XmlTextReader(new StringReader(apiout)))
                    {
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
    static HttpClient Site(lang lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        var result = client.GetAsync("https://" + langdata[lang].domain + ".org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        client.PostAsync("https://" + langdata[lang].domain + ".org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login },
            { "lgpassword", password }, { "lgtoken", logintoken } })); return client;
    }
    static string Save(lang lang, HttpClient site, string title, string appendtext, string comment)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://" + langdata[lang].domain + ".org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var request = new MultipartFormDataContent{{ new StringContent("edit"),"action" },{ new StringContent(title),"title" },
            { new StringContent(comment), "summary" },{ new StringContent(doc.SelectSingleNode("//tokens/@csrftoken").Value), "token" }, { new StringContent(appendtext), "appendtext" } };
        return site.PostAsync("https://" + langdata[lang].domain + ".org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static void read_config()
    {
        settings = new StreamReader("./reimu/config.txt").ReadToEnd().Split('\n');
        var limits = settings[5].Split(':')[1].Split('|');
        ores_limit = Convert.ToDouble(limits[0]);
        liftwing[type.lwa].limit = Convert.ToDouble(limits[1]);
        liftwing[type.lwm].limit = Convert.ToDouble(limits[2]);
        if (!trusted_users.Contains(settings[6].Split(':')[1]))
            trusted_users.Add(settings[6].Split(':')[1]);
        suspicious_tags_rgx = new Regex(settings[7].Split(':')[1], RegexOptions.IgnoreCase);
        deletions_rgx = new Regex(settings[8].Split(':')[1], RegexOptions.IgnoreCase);
        whitelist_text_rgx = new Regex(settings[9].Split(':')[1], RegexOptions.IgnoreCase);
        whitelist_title_rgx = new Regex(settings[10].Split(':')[1], RegexOptions.IgnoreCase);
        replaces.Clear();
        var pairs_list = settings[11].Split(':')[1].Split('|');
        foreach (var pair in pairs_list) { var components = pair.Split('/');
            replaces.Add(new replace_pair() { one = new Regex(components[0], RegexOptions.IgnoreCase), two = new Regex(components[1], RegexOptions.IgnoreCase), replacement = pair }); }
    }
    static void read_patterns()
    {
        patterns.Clear();
        var patterns_list = new StreamReader("./reimu/patterns.txt").ReadToEnd().Split('\n');
        int c = 0;
        foreach (var pattern in patterns_list)
        {
            if (pattern == "")
                continue;
            else try
                {
                    if (pattern.Contains('☯'))
                    {
                        string pattern_body = pattern.Split('☯')[0];
                        string flags = pattern.Split('☯')[1];
                        bool ignorecase = flags[0] == '0';
                        bool not_uk = flags[1] == '1';
                        bool only_content = flags[2] == '1';
                        patterns.Add(new pattern_info() { regex = ignorecase ? new Regex(pattern_body, RegexOptions.IgnoreCase) : new Regex(pattern_body), only_content = only_content, not_uk = not_uk, stringnumber = c });
                    }
                    else
                        patterns.Add(new pattern_info() { regex = new Regex(pattern, RegexOptions.IgnoreCase), only_content = false, not_uk = false, stringnumber = c });
                }
                catch { }
            c++;
        }   
    }
    static void check(lang lang)
    {
        Program.lang = lang;
        var edits = JsonConvert.DeserializeObject<rchanges>(site[lang].GetStringAsync("https://" + langdata[lang].domain + ".org/w/api.php?action=query&format=json&list=recentchanges&formatversion=2" +
            "&rcstart=" + langdata[lang].last_checked_edit_time + "&rcdir=newer&rcprop=title|timestamp|ids|oresscores|comment|user|sizes|tags&rctype=edit|new|log&rclimit=max").Result);
        foreach (var edit in edits.query.recentchanges)
            if (edit.revid > langdata[lang].last_checked_id && !trusted_users.Contains(edit.user) && !read_edit_data_and_exclude_some_cases(edit) && !whitelist_title_rgx.IsMatch(edit.title) &&
                !ores_is_triggered(edit) && !tags_is_triggered(edit) && !addition_is_triggered(edit.comment) && !addition_is_triggered(edit.title) && !lw_is_triggered(edit))
                {
                    generate_all_ins_del();
                    if (!deletion_is_triggered() && !addition_is_triggered(all_ins))
                        check_replaces(edit);
                }
    }
    static bool read_edit_data_and_exclude_some_cases(Recentchange edit)
    {
        user = edit.user;
        title = edit.title.Replace('_', ' ');
        ns = edit.ns;
        if (ns == 2 && title.Contains('/') && title.Substring(0, title.IndexOf('/')) == user)
            return true;
        newid = edit.revid;
        oldid = edit.old_revid;
        comment = edit.comment;
        diff_size = edit.newlen - edit.oldlen;
        pageid = edit.pageid;
        try
        {
            editcount = Convert.ToInt32(editcount_rgx.Match(site[lang].GetStringAsync("https://" + langdata[lang].domain + ".org/w/api.php?action=query&format=xml&prop=&list=users&usprop=editcount" +
                "&ususers=" + e(user)).Result).Groups[1].Value);
        }
        catch { editcount = 0; }
        if (editcount > 500)
        {
            trusted_users.Add(user);
            return true;
        }
        langdata[lang].last_checked_edit_time = edit.timestamp.ToString("yyyy-MM-ddTHH:mm:ss.000Z");
        langdata[lang].last_checked_id = newid;
        return false;
    }
    static bool tags_is_triggered(Recentchange edit)
    {
        foreach (string edit_tag in edit.tags)
            if (suspicious_tags_rgx.IsMatch(edit_tag) && !((suspicious_tags_rgx.Match(edit_tag).Value.Contains("replace") || suspicious_tags_rgx.Match(edit_tag).Value.Contains("blank")) &&
                (lang == lang.d || lang == lang.c))) { post_suspicious_edit(edit_tag, type.tag); return true; } return false;
    }
    static bool ores_is_triggered(Recentchange edit)
    {
        if (lang == lang.d) return false;
        ores_value = 0;
        try /*даже при проверках на формат строки вылетает*/ { ores_value = Convert.ToDouble(damage_rgx.Match(edit.oresscores.ToString()).Groups[1].Value); } catch { return false; }
        if (ores_value >= ores_limit) { post_suspicious_edit("ores:" + ores_value.ToString(), type.ores); return true; }
        else return false;
    }
    static bool lw_is_triggered(Recentchange edit)
    {
        if (!(lang == lang.d || lang == lang.c))
            foreach (type shortname in liftwing.Keys)
            {
                try
                {
                    lw_raw = lw_rgx.Match(client.PostAsync("https://api.wikimedia.org/service/lw/inference/v1/models/revertrisk-" + liftwing[shortname].longname + ":predict", new StringContent
                        ("{\"lang\":\"" + lang + "\",\"rev_id\":" + newid + "}", Encoding.UTF8, "application/json")).Result.Content.ReadAsStringAsync().Result).Groups[1].Value;
                    if (lw_raw != null && lw_raw != "")
                        lw_value = Math.Round(Convert.ToDouble(lw_raw), 3);
                }
                catch { return false; }
                if (lw_value > liftwing[shortname].limit)
                {
                    post_suspicious_edit(shortname + ":" + lw_value.ToString(), shortname);
                    return true;
                }
            }
        return false;
    }
    static void generate_all_ins_del()
    {
        var alldiff = site[lang].GetStringAsync("https://" + langdata[lang].domain + ".org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff" +
            "&difftype=inline&uselang=ru").Result;
        var ins_array = ins_rgx.Matches(alldiff); var del_array = del_rgx.Matches(alldiff); all_ins = ""; all_del = "";
        foreach (var elem in ins_array) all_ins += "\n" + elem; foreach (var elem in del_array) all_del += "\n" + elem;
        if (all_ins != "") all_ins = all_ins.Substring(1); if (all_del != "") all_del = all_del.Substring(1);
    }
    static bool addition_is_triggered(string text)
    {
        //return false;
        if (text == null)
            return false;
        foreach (var pattern in patterns)
        {
            bool edit_is_on_the_discussion_page = ns % 2 == 1 || talk_ns_rgx.IsMatch(ns.ToString());
            if ((pattern.not_uk && lang == lang.uk) || (pattern.only_content && edit_is_on_the_discussion_page)) continue;
            else if (pattern.regex.IsMatch(text) && !whitelist_text_rgx.IsMatch(pattern.regex.Match(text).Value))
            {
                post_suspicious_edit(pattern.regex.Match(text).Value + ", line" + pattern.stringnumber, type.addition); return true;
            }
        } return false;
    }
    static bool deletion_is_triggered() { if (deletions_rgx.IsMatch(all_del)) { post_suspicious_edit(deletions_rgx.Match(all_del).Value, type.deletion); return true; } return false; }
    static void check_replaces(Recentchange edit)
    {
        foreach (var rgxpair in replaces)
            if ((rgxpair.one.IsMatch(all_ins) && rgxpair.two.IsMatch(all_del) && !rgxpair.two.IsMatch(all_ins) && !rgxpair.one.IsMatch(all_del)) ||
            (rgxpair.one.IsMatch(all_del) && rgxpair.two.IsMatch(all_ins) && !rgxpair.one.IsMatch(all_ins) && !rgxpair.two.IsMatch(all_del)))
            {
                post_suspicious_edit(rgxpair.replacement, type.replace); return;
            }
    }
    static string e(string input) { return Uri.EscapeUriString(input); }
    static void post_suspicious_edit(string reason, type type)
    {
        Program.reason = reason; check_if_author_is_recidivist(); generate_visible_diff(); post_edit_to_discord(type);
        if (langdata[lang].notifying_page_name != "") Save(lang, site[lang], langdata[lang].notifying_page_name, ".", "[[special:diff/" + newid + "|" + title + "]] ([[special:history/" + title +
            "|history]]), [[special:contribs/" + e(user) + "|" + user + "]], " + reason + ", " + comment_diff);
    }
    static void check_if_author_is_recidivist()
    {
        if (suspicious_users.Contains(user))
        {
            client.PostAsync("https://discord.com/api/webhooks/" + authors_token, new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + user + "](<https://" + langdata[lang].domain +
                ".org/wiki/special:contribs/" + e(user) + ">), " + title} }));
            if (lang == lang.ru && reason.StartsWith("ores:")) zkab_report();
        }
        else suspicious_users.Add(user);
    }
    static void zkab_report()
    {
        string zkab = site[lang.ru].GetStringAsync("https://ru.wikipedia.org/wiki/ВП:Запросы_к_администраторам/Быстрые?action=raw").Result;
        var reportedusers = reportedusers_rgx.Matches(zkab); bool reportedyet = false;
        foreach (Match r in reportedusers) if (user == r.Groups[1].Value) reportedyet = true;
        if (!reportedyet)
            Save(lang.ru, site[lang.ru], "ВП:Запросы к администраторам/Быстрые", "\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}", "[[special:contribs/" + user + "]] - новый запрос");
    }
    static void generate_visible_diff()
    {
        string diff_request = "https://" + langdata[lang].domain + ".org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline&uselang=ru";
        diff_text = trash_tags_rgx.Replace(empty_del_rgx.Replace(empty_ins_rgx.Replace(site[lang].GetStringAsync(diff_request).Result.Replace("&#160;", ""), ""), ""), "")
            .Replace("\\n", "\n").Replace("\\\"", "\"").Replace("&#9650;", "↕").Replace("&#9660;", "↕");
        strings_with_changes = "";
        foreach (string str in diff_text.Split('\n'))
            if (ins_del_rgx.IsMatch(str))
            {
                var matches = ins_del_rgx.Matches(str);
                startpos = matches[0].Index - num_of_surrounding_chars;
                if (startpos < 0) startpos = 0;
                endpos = matches[matches.Count - 1].Index + matches[matches.Count - 1].Length + num_of_surrounding_chars;
                if (endpos >= str.Length) endpos = str.Length - 1;
                strings_with_changes += str.Substring(startpos, endpos - startpos + 1) + "<...>";
            }
        string revs = site[lang].GetStringAsync("https://" + langdata[lang].domain + ".org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + pageid + "&rvprop=ids&rvlimit=" + num_of_revs_to_check).Result;
        int num_of_revs = rev_rgx.Matches(revs).Count;
        string revisions_info = num_of_revs == num_of_revs_to_check ? "" : ", revs: " + num_of_revs;
        reason += ", dsize:" + diff_size + revisions_info;
        comment_diff = ins_rgx.Replace(del_rgx.Replace(strings_with_changes, "-$1 "), "+$1 ").Replace("&lt;", "<").Replace("&gt;", ">");
        discord_diff = ins_rgx.Replace(del_rgx.Replace(strings_with_changes, "~~$1~~ "), "`$1` ").Replace("&lt;", "<").Replace("&gt;", ">");
    }
    static void post_edit_to_discord(type type)
    {
        if (discord_diff.Length > 1022)
            discord_diff = discord_diff.Substring(0, 1022);
        if (comment.Length > 254)
            comment = comment.Substring(0, 254);
        bool single_author = false;
        string revs1 = site[lang].GetStringAsync("https://" + langdata[lang].domain + ".org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + pageid + "&rvprop=ids&rvlimit=1&rvexcludeuser=" + e(user)).Result;
        if (revid_rgx.IsMatch(revs1))
            edit_id_of_first_another_author = revid_rgx.Match(revs1).Groups[1].Value;
        else single_author = true;
        string visible_wd_title = title;
        if (lang == lang.d)
        {
            var labels = site[lang].GetStringAsync("https://www.wikidata.org/w/api.php?action=wbgetentities&ids=" + title + "&format=xml&props=labels").Result;
            foreach (Match lang in wd_label_rgx.Matches(labels))
                if (lang.Groups[1].Value == "ru")
                    visible_wd_title = lang.Groups[2].Value;
                else if (lang.Groups[1].Value == "en")
                    visible_wd_title = lang.Groups[2].Value;
                else if (lang.Groups[1].Value == "mul")
                    visible_wd_title = lang.Groups[2].Value;
        }

        string curr_link = single_author ? "" : ", [curr](<https://" + langdata[lang].domain + ".org/wiki/" + e(title) + ">)";
        string main_link = single_author ? "https://" + langdata[lang].domain + ".org/wiki/" + e(title) : "https://" + langdata[lang].domain + ".org/w/index.php?oldid=" + edit_id_of_first_another_author +
            "&diff=curr&ilu=" + newid;
        var json = new discordjson()
        {
            embeds = new List<Embed>() { new Embed() { color = colors[type].convert(), title = visible_wd_title, url = main_link, description = reason + ", [hist](<https://" + langdata[lang].domain +
            ".org/wiki/special:history/" + e(title) + ">)" + curr_link, fields = new List<Field>(){ new Field(){ name = comment, value = discord_diff }}, author = new Author(){ name = editcount == 0 ?
            user : user + ", " + editcount + " edits", url = "https://" + langdata[lang].domain + ".org/wiki/special:contribs/" + e(user) } } }
        };
        var res = client.PostAsync("https://discord.com/api/webhooks/" + discord_token, new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json")).Result;
        if (res.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine(res.StatusCode + " " + JsonConvert.SerializeObject(json));
    }
}
