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
    ores, lwa, lwm, replace, addition, tag, deletion
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
    static string user, title, comment, liftwing_token, discord_token, swviewer_token, authors_token, diff_text, comment_diff, discord_diff, lw_raw, strings_with_changes, lang, first_another_author_edit_id,
        all_ins, all_del, default_time = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-ddTHH:mm:ss.000Z");
    static string[] settings;
    static Dictionary<string, HttpClient> site = new Dictionary<string, HttpClient>();
    static HttpClient client = new HttpClient();
    static double ores_value, lw_value, ores_limit = 1;
    static Dictionary<type, model> liftwing = new Dictionary<type, model>() { { type.lwa, new model() { longname = "language-agnostic", limit = 1 } }, { type.lwm, new model() { longname = 
        "multilingual", limit = 1 } } };
    static Regex lw_rgx = new Regex(@"""true"":(0.\d+)"), reportedusers_rgx = new Regex(@"\| вопрос = u/(.*)"), div_rgx = new Regex(@"</?div[^>]*>"),ins_del_rgx = new Regex(@"<(ins|del)[^>]*>(.*?)<[^>]*>"),
        ins_rgx = new Regex(@"<ins[^>]*>(.*?)</ins>"), del_rgx = new Regex(@"<del[^>]*>(.*?)</del>"), editcount_rgx = new Regex(@"editcount=""(\d*)"""), rev_rgx = new Regex(@"<rev "), revid_rgx = new Regex
        (@"revid=""(\d*)"""), damage_rgx = new Regex(@"damaging"":\s*\{\s*""true"":\s*(0.\d{3})", RegexOptions.Singleline), empty_ins_rgx = new Regex(@"<ins[^>]*>\s*</ins>"), empty_del_rgx = new Regex
        (@"<del[^>]*>\s*</del>"), a_rgx = new Regex(@"</?a[^>]*>"), span_rgx = new Regex(@"</?span[^>]*>"), suspicious_tags_rgx, deletions_rgx, whitelist_text_rgx, whitelist_title_rgx;
    static Dictionary<string, string> notifying_page_name = new Dictionary<string, string>() { { "ru", "user:Рейму_Хакурей/Проблемные_правки" }, { "uk", "user:Рейму_Хакурей/Підозрілі_редагування" },
        { "be", "user:Рейму_Хакурей/Падазроныя праўкі" } };
    static Dictionary<string, string> last_checked_edit_time = new Dictionary<string, string>() { { "ru", default_time }, { "uk", default_time }, { "be", default_time } };
    static Dictionary<string, int> last_checked_id = new Dictionary<string, int>() { { "ru", 0 }, { "uk", 0 }, { "be", 0 } };
    static HashSet<string> suspicious_users = new HashSet<string>(), trusted_users = new HashSet<string>(), langs = new HashSet<string>() { { "ru" }, { "uk" }, { "be" } };
    static Dictionary<string, List<Regex>> patterns = new Dictionary<string, List<Regex>>();
    static List<rgxpair> replaces = new List<rgxpair>();
    static bool new_timestamp_saved, new_id_saved;
    static int currminute = -1, diff_size, num_of_surrounding_chars = 25, num_of_revs_to_check = 20, startpos, endpos, editcount, oldid, newid, pageid;
    static Dictionary<type, color> colors = new Dictionary<type, color>() { { type.addition, new color(255, 0, 0) }, { type.lwa, new color(255, 255, 0) }, { type.ores, new color(255, 0, 255) },
        { type.tag, new color(0, 255, 0) }, { type.lwm, new color(255, 128, 0) }, { type.replace, new color(0, 255, 255) }, { type.deletion, new color(255, 255, 255) } };
    static string e(string input)
    {
        return Uri.EscapeUriString(input);
    }
    static void update_patterns()
    {
        foreach (string lang in langs)
        {
            patterns[lang].Clear();
            foreach (string patterns_file_name in new string[] { "patterns-common", "patterns-" + lang })
                try
                {
                    var pattern_source = new StreamReader(patterns_file_name + ".txt").ReadToEnd().Split('\n');
                    bool ignorecase = false;
                    foreach (var pattern in pattern_source)
                        if (pattern == "--------")
                            ignorecase = true;
                        else if (pattern != "")
                            patterns[lang].Add(ignorecase ? new Regex(pattern, RegexOptions.IgnoreCase) : new Regex(pattern));
                }
                catch { continue; }
        }
    }
    static void read_config()
    {
        settings = new StreamReader("reimu-config.txt").ReadToEnd().Split('\n');
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
        foreach (var pair in pairs_list)
        {
            var components = pair.Split('/');
            replaces.Add(new rgxpair() { one = new Regex(components[0], RegexOptions.IgnoreCase), two = new Regex(components[1], RegexOptions.IgnoreCase) });
        }
    }
    static bool tags_is_triggered(Recentchange edit)
    {
        foreach (string edit_tag in edit.tags)
            if (suspicious_tags_rgx.IsMatch(edit_tag))
            {
                post_suspicious_edit(edit_tag, type.tag);
                return true;
            }
        return false;
    }
    static bool ores_is_triggered(Recentchange edit)
    {
        ores_value = 0;
        try //даже при проверках на формат строки вылетает
        {
            ores_value = Convert.ToDouble(damage_rgx.Match(edit.oresscores.ToString()).Groups[1].Value);
        }
        catch { return false; }
        if (ores_value >= ores_limit)
        {
            post_suspicious_edit("ores:" + ores_value.ToString(), type.ores);
            return true;
        }
        else return false;
    }
    static bool lw_is_triggered(Recentchange edit)
    {
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
    static bool addition_is_triggered(string text)
    {
        try
        {
            foreach (var pattern in patterns[lang])
                if (pattern.IsMatch(text) && !whitelist_text_rgx.IsMatch(pattern.Match(text).Value))
                {
                    post_suspicious_edit(pattern.Match(text).Value, type.addition);
                    return true;
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine(text + ex.ToString());
        }
        return false;
    }
    static bool deletion_is_triggered()
    {
        if (deletions_rgx.IsMatch(all_del))
        {
            post_suspicious_edit(deletions_rgx.Match(all_del).Value, type.deletion);
            return true;
        }
        return false;
    }
    static void check_replaces(Recentchange edit)
    {
        foreach (var rgxpair in replaces)
            if ((rgxpair.one.IsMatch(all_ins) && rgxpair.two.IsMatch(all_del) && !rgxpair.two.IsMatch(all_ins) && !rgxpair.one.IsMatch(all_del)) ||
            (rgxpair.one.IsMatch(all_del) && rgxpair.two.IsMatch(all_ins) && !rgxpair.one.IsMatch(all_ins) && !rgxpair.two.IsMatch(all_del)))
            {
                post_suspicious_edit("замена", type.replace);
                return;
            }
    }
    static void initialize_edit_data(Recentchange edit)
    {
        user = edit.user;
        title = edit.title.Replace('_', ' ');
        newid = edit.revid;
        oldid = edit.old_revid;
        comment = edit.comment;
        diff_size = edit.newlen - edit.oldlen;
        pageid = edit.pageid;

        try
        {
            editcount = Convert.ToInt32(editcount_rgx.Match(site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=&list=users&usprop=editcount" +
                "&ususers=" + e(user)).Result).Groups[1].Value);
        }
        catch { editcount = 0; }
        if (editcount > 1000)
            trusted_users.Add(user);

        if (!new_timestamp_saved)
        {
            last_checked_edit_time[lang] = edit.timestamp.ToString("yyyy-MM-ddTHH:mm:ss.000Z");
            new_timestamp_saved = true;
        }
        if (!new_id_saved)
        {
            last_checked_id[lang] = newid;
            new_id_saved = true;
        }
    }
    static void read_diff_text()
    {
        var alldiff = site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline").Result;
        var ins_array = ins_rgx.Matches(alldiff);
        all_ins = "";
        foreach (var elem in ins_array)
            all_ins += elem;
        var del_array = del_rgx.Matches(alldiff);
        all_del = "";
        foreach (var elem in del_array)
            all_del += elem;
    }
    static void zkab_report()
    {
        string zkab = site["ru"].GetStringAsync("https://ru.wikipedia.org/wiki/ВП:Запросы_к_администраторам/Быстрые?action=raw").Result;
        var reportedusers = reportedusers_rgx.Matches(zkab);
        bool reportedyet = false;
        foreach (Match r in reportedusers)
            if (user == r.Groups[1].Value)
                reportedyet = true;
        if (!reportedyet)
            Save("ru", site["ru"], "ВП:Запросы к администраторам/Быстрые", "\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}", "[[special:contribs/" + user + "]] - новый запрос");
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
    static string Save(string lang, HttpClient site, string title, string appendtext, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var request = new MultipartFormDataContent{{ new StringContent("edit"),"action" },{ new StringContent(title),"title" },{ new StringContent(comment), "summary" },{ new StringContent("xml"),"format" },
            { new StringContent(doc.SelectSingleNode("//tokens/@csrftoken").Value), "token" }, { new StringContent(appendtext), "appendtext" } };
        return site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result.Content.ReadAsStringAsync().Result;
    }
    static void post_suspicious_edit(string reason, type type)
    {
        reason += ", dsize:" + diff_size;
        if (suspicious_users.Contains(user))
        {
            client.PostAsync("https://discord.com/api/webhooks/" + authors_token, new FormUrlEncodedContent(new Dictionary<string, string>{ { "content", "[" + user + "](<https://" + lang +
                ".wikipedia.org/wiki/special:contribs/" + e(user) + ">), " + title} }));
            if (lang == "ru")
                zkab_report();
        }
        else
            suspicious_users.Add(user);
        string diff_request = "https://" + lang + ".wikipedia.org/w/api.php?action=compare&format=json&formatversion=2&fromrev=" + oldid + "&torev=" + newid + "&prop=diff&difftype=inline";
        diff_text = empty_ins_rgx.Replace(empty_del_rgx.Replace(div_rgx.Replace(a_rgx.Replace(span_rgx.Replace(site[lang].GetStringAsync(diff_request).Result, ""), ""), ""), ""), "").Replace("\\n", "\n")
            .Replace("\\\"", "\"").Replace("&#160;", "(nb)").Replace("&#9650;", "↕").Replace("&#9660;", "↕");
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
        comment_diff = ins_rgx.Replace(del_rgx.Replace(strings_with_changes, "-$1 "), "+$1 ").Replace("&lt;", "<").Replace("&gt;", ">");
        discord_diff = ins_rgx.Replace(del_rgx.Replace(strings_with_changes, "~~$1~~ "), "`$1` ").Replace("&lt;", "<").Replace("&gt;", ">");

        if (discord_diff.Length > 1022)
            discord_diff = discord_diff.Substring(0, 1022);
        if (comment.Length > 254)
            comment = comment.Substring(0, 254);

        string revs = site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + pageid + "&rvprop=ids&rvlimit=" + num_of_revs_to_check).Result;
        int num_of_revs = rev_rgx.Matches(revs).Count;
        string revisions_info = num_of_revs == num_of_revs_to_check ? "" : ", revs: " + num_of_revs;

        if (lang != "be")
            Save(lang, site[lang], notifying_page_name[lang], ".", "[[toollabs:rv/r.php/" + newid + "|[rollback] ]] [[special:diff/" + newid + "|" + title + "]] ([[special:history/" + title +
                "|history]]), [[special:contribs/" + e(user) + "|" + user + "]], " + reason + ", " + comment_diff);

        bool single_author = false;
        string revs1 = site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + pageid + "&rvprop=ids&rvlimit=1&rvexcludeuser=" + e(user)).Result;
        if (revid_rgx.IsMatch(revs1))
            first_another_author_edit_id = revid_rgx.Match(revs1).Groups[1].Value;
        else
            single_author = true;

        var json = new discordjson()
        {
            embeds = new List<Embed>() { new Embed() { color = colors[type].convert(), title = title, url = "https://" + lang + ".wikipedia.org/w/index.php?" + (single_author ? "diff=" + newid : 
            "oldid=" + first_another_author_edit_id + "&diff=curr&ilu=" + newid), description = reason + revisions_info + ", [hist](<https://" + lang + ".wikipedia.org/wiki/special:history/" + e(title) +
            ">), " + "[curr](<https://" + lang + ".wikipedia.org/wiki/" + e(title) + ">)", fields = new List<Field>(){ new Field(){ name = comment, value = discord_diff }}, author = new Author(){ name = 
            editcount == 0 ? user : user + ", " + editcount + " edits", url = "https://" + lang + ".wikipedia.org/wiki/special:contribs/" + e(user) } } }
        };
        var res = client.PostAsync("https://discord.com/api/webhooks/" + discord_token, new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json")).Result;
        if (res.StatusCode != HttpStatusCode.NoContent)
            Console.WriteLine(res.StatusCode + " " + JsonConvert.SerializeObject(json));

    }
    static void check(string lang)
    {
        Program.lang = lang;
        new_timestamp_saved = false; new_id_saved = false;
        var edits = JsonConvert.DeserializeObject<rchanges>(site[lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=json&list=recentchanges&formatversion=2&rcend=" +
            last_checked_edit_time[lang] + "&rcprop=title|timestamp|ids|oresscores|comment|user|sizes|tags&rctype=edit|new&rclimit=max").Result);
        foreach(var edit in edits.query.recentchanges)
            if (edit.revid > last_checked_id[lang] && !trusted_users.Contains(edit.user))
            {
                initialize_edit_data(edit);
                if (!whitelist_title_rgx.IsMatch(edit.title) && !ores_is_triggered(edit) && !tags_is_triggered(edit) && !addition_is_triggered(edit.comment) && !lw_is_triggered(edit))
                {
                    read_diff_text();
                    if (!deletion_is_triggered() && !addition_is_triggered(all_ins))
                        check_replaces(edit);
                }
            }
    }
    static void initialize_bot()
    {
        settings = new StreamReader("reimu-config.txt").ReadToEnd().Split('\n');
        liftwing_token = settings[1].Split(':')[1]; swviewer_token = settings[2].Split(':')[1]; discord_token = settings[3].Split(':')[1]; authors_token = settings[4].Split(':')[1];

        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + liftwing_token); client.DefaultRequestHeaders.Add("User-Agent", "vandalism_detection_tool_by_user_MBH");
        var swviewer_trusted_users = client.GetStringAsync("https://swviewer.toolforge.org/php/getGlobals.php?ext_token=" + swviewer_token + "&user=Рейму").Result.Split('|');
        foreach (var g in swviewer_trusted_users)
            trusted_users.Add(g);
        foreach (string lang in langs)
        {
            site.Add(lang, Site(lang, settings[0].Split(':')[0], settings[0].Split(':')[1]));
            patterns.Add(lang, new List<Regex>());
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
        initialize_bot();
        while (true)
        {
            if (currminute != DateTime.UtcNow.Minute / 10)
            {
                currminute = DateTime.UtcNow.Minute / 10;
                read_config();
                update_patterns();
            }
            foreach (var lang in langs)
                check(lang);
            Thread.Sleep(2000);
        }
    }
}
