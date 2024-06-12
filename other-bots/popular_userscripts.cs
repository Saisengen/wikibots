using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;

class data
{
    public int active, inactive;
}
class Program
{
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + g_lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        client.PostAsync("https://" + g_lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password },
            { "lgtoken", logintoken }, { "format", "xml" } }));
        return client;
    }
    static void Save(HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + g_lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action");
        request.Add(new StringContent(title), "title");
        request.Add(new StringContent(text), "text");
        request.Add(new StringContent(comment), "summary");
        request.Add(new StringContent(token), "token");
        request.Add(new StringContent("xml"), "format");
        result = site.PostAsync("https://" + g_lang + ".wikipedia.org/w/api.php", request).Result;
        if (result.ToString().Contains("uccess"))
            Console.WriteLine(DateTime.Now.ToString() + " written " + title);
        else
            Console.WriteLine(result);
    }
    static bool user_is_active()
    {
        if (users_activity[g_lang].ContainsKey(g_username))
            return users_activity[g_lang][g_username];
        else
        {
            DateTime edit_ts = new DateTime(), log_ts = new DateTime();
            using (var r = new XmlTextReader(new StringReader(site[g_lang].GetStringAsync("https://" + g_lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucprop=timestamp&ucuser=" + Uri.EscapeUriString(g_username)).Result)))
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string raw_ts = r.GetAttribute("timestamp");
                        edit_ts = new DateTime(Convert.ToInt16(raw_ts.Substring(0, 4)), Convert.ToInt16(raw_ts.Substring(5, 2)), Convert.ToInt16(raw_ts.Substring(8, 2)));
                    }
            using (var r = new XmlTextReader(new StringReader(site[g_lang].GetStringAsync("https://" + g_lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=timestamp&lelimit=1&leuser=" + Uri.EscapeUriString(g_username)).Result)))
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string raw_ts = r.GetAttribute("timestamp");
                        log_ts = new DateTime(Convert.ToInt16(raw_ts.Substring(0, 4)), Convert.ToInt16(raw_ts.Substring(5, 2)), Convert.ToInt16(raw_ts.Substring(8, 2)));
                    }

            if (edit_ts < DateTime.Now.AddMonths(-1) && log_ts < DateTime.Now.AddMonths(-1))
            {
                users_activity[g_lang].Add(g_username, false); return false;
            }
            else
            {
                users_activity[g_lang].Add(g_username, true); return true;
            }

        }
    }
    static void add_script(string scriptname)
    {
        if (scriptname.StartsWith(g_lang + ":"))
            scriptname = scriptname.Substring(3);
        scriptname = Uri.UnescapeDataString(scriptname).Replace("_", " ").Replace("у:", "user:").Replace("участник:", "user:").Replace("участница:", "user:").Replace("вп:", "project:")
            .Replace("википедия:", "project:").Replace("U:", "user:").Replace("У:", "user:").Replace("Участник:", "user:").Replace("Участница:", "user:").Replace("ВП:", "project:")
            .Replace("Википедия:", "project:").Replace("User:", "user:").Replace("Project:", "project:");
        debug_result += "\n|-\n|" + Uri.UnescapeDataString(g_url).Replace(' ', '_') + "||[[:" + scriptname + "]]";
        if (user_is_active() && scripts[g_lang].ContainsKey(scriptname))
            scripts[g_lang][scriptname].active++;
        else if (user_is_active() && !scripts[g_lang].ContainsKey(scriptname))
            scripts[g_lang].Add(scriptname, new data() { active = 1, inactive = 0 });
        else if (!user_is_active() && scripts[g_lang].ContainsKey(scriptname))
            scripts[g_lang][scriptname].inactive++;
        else
            scripts[g_lang].Add(scriptname, new data() { active = 0, inactive = 1 });
    }
    static void process_site(string url)
    {
        g_url = url;
        string content = "";
        try
        {
            content = site[g_lang].GetStringAsync(url).Result;
        }
        catch (Exception e)
        {
            if (e.InnerException.ToString().Contains("404"))
                return;
            else
                Console.WriteLine(e.ToString());
        }
        Thread.Sleep(900);
        content = Uri.UnescapeDataString(multiline_comment.Replace(content, "")).Replace("(\n", "(");
        foreach (var s in content.Split('\n'))
            if (!s.TrimStart(' ').StartsWith("//"))
            {
                if (r1.IsMatch(s) && !(is_ext_rgx.IsMatch(s) || is_foreign_rgx.IsMatch(s) || is_rgx.IsMatch(s) || is2_rgx.IsMatch(s)))
                    Console.WriteLine(s);
                if (r2.IsMatch(s) && !(loader_foreign_rgx.IsMatch(s) || loader_rgx.IsMatch(s)) || loader_foreign2_rgx.IsMatch(s))
                    Console.WriteLine(s);

                if (is_foreign_rgx.IsMatch(s))
                    foreach (Match m in is_foreign_rgx.Matches(s))
                        add_script(m.Groups[2].Value + ":" + m.Groups[1].Value);
                else
                {
                    foreach (Match m in is_rgx.Matches(s))
                        add_script(m.Groups[1].Value);
                    foreach (Match m in is2_rgx.Matches(s))
                        add_script(m.Groups[1].Value);
                    foreach (Match m in is_ext_rgx.Matches(s))
                        if (m.Groups[3].Value.EndsWith("edia"))
                            add_script(m.Groups[2].Value + ":" + m.Groups[4].Value);
                        else if (m.Groups[3].Value == "wikidata" || m.Groups[3].Value == "mediawiki")
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value);
                        else
                            add_script(m.Groups[2].Value + ":" + m.Groups[3].Value + ":" + m.Groups[4].Value);
                    foreach (Match m in loader_rgx.Matches(s))
                        add_script(m.Groups[2].Value);
                    foreach (Match m in loader_foreign_rgx.Matches(s))
                        if (m.Groups[4].Value.EndsWith("edia"))
                            add_script(m.Groups[3].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "wikidata" || m.Groups[4].Value == "mediawiki")
                            add_script(m.Groups[4].Value + ":" + m.Groups[5].Value);
                        else
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value + ":" + m.Groups[5].Value);
                    foreach (Match m in loader_foreign2_rgx.Matches(s))
                        if (m.Groups[4].Value.EndsWith("edia"))
                            add_script(m.Groups[3].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "wikidata" || m.Groups[4].Value == "mediawiki")
                            add_script(m.Groups[4].Value + ":" + m.Groups[5].Value);
                        else
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value + ":" + m.Groups[5].Value);
                }
            }
    }

    static Dictionary<string, HttpClient> site = new Dictionary<string, HttpClient>();
    static Dictionary<string, HashSet<string>> invoking_pages = new Dictionary<string, HashSet<string>>(), script_users = new Dictionary<string, HashSet<string>>();
    static Dictionary<string, Dictionary<string, bool>> users_activity = new Dictionary<string, Dictionary<string, bool>>();
    static Dictionary<string, Dictionary<string, data>> scripts = new Dictionary<string, Dictionary<string, data>>();
    static Regex is_rgx = new Regex(@"importscript\s*\(\s*['""]([^h/].*?)\s*['""]\s*\)", RegexOptions.IgnoreCase), is2_rgx = new Regex(@"importscript\s*\(\s*['""]/wiki/(.*?)\s*['""]\s*\)", RegexOptions.IgnoreCase),
    is_foreign_rgx = new Regex(@"importscript\s*\(\s*['""]([^h].*?)\s*['""],\s*['""]([^""']*)\s*['""]", RegexOptions.IgnoreCase),
    is_ext_rgx = new Regex(@"importscript\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/wiki/(.*?\.js)", RegexOptions.IgnoreCase),
    loader_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""]/w/index\.php\?title=(.*?\.js)", RegexOptions.IgnoreCase),
    loader_foreign_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/w/index\.php\?title=(.*?\.js)", RegexOptions.IgnoreCase),
        loader_foreign2_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/wiki/([^?]*)\?", RegexOptions.IgnoreCase),
        r1 = new Regex("importscript", RegexOptions.IgnoreCase), r2 = new Regex(@"\.(load|getscript|using)\b", RegexOptions.IgnoreCase), multiline_comment = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
    static string g_username, g_url, g_lang, debug_result = "<center>\n{|class=\"standard sortable\"\n!Страница вызова!!Скрипт";
    static void Main()
    {
        var result = new Dictionary<string, string>() { { "ru", "[[К:Википедия:Статистика и прогнозы]]{{shortcut|ВП:СИС}}<center>Статистика собирается по незакомментированным (//, /**/) включениям " +
                "importScript и разнообразным методам load/using/getscript на скриптовых страницах участников рувики, а также их global.js-файлах на Мете. Отсортировано по числу активных участников - " +
                "сделавших хоть одно действие за последний месяц.\n{|class=\"standard sortable\"\n!Скрипт!!Активных!!Неактивных!!Всего" }, { "en", "<center>\n{|class=\"wikitable sortable\"\n!Script!!Active" +
                "!!Inactive!!Total"} };
        var resultpage = new Dictionary<string, string>() { { "ru", "ВП:Самые используемые скрипты" }, { "en", "User:MBH/sandbox" } };
        var w = new StreamWriter("result.txt");
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        foreach (string lang in new string[] { "ru", "en" })
        {
            invoking_pages.Add(lang, new HashSet<string>()); script_users.Add(lang, new HashSet<string>());
            users_activity.Add(lang, new Dictionary<string, bool>()); scripts.Add(lang, new Dictionary<string, data>());
            g_lang = lang;
            site[lang] = Site(creds[0], creds[1]);
            foreach (string skin in new string[] { "common", "monobook", "vector", "cologneblue", "minerva", "timeless", "simple", "myskin", "modern" })
                using (var r = new XmlTextReader(new StringReader(site[g_lang].GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=search&srsearch=" + skin + ".js&srnamespace=2&srlimit=max&srprop=").Result)))
                    while (r.Read())
                        if (r.Name == "p" && r.GetAttribute("title").EndsWith(skin + ".js") && !invoking_pages[lang].Contains(r.GetAttribute("title")))
                            invoking_pages[lang].Add(r.GetAttribute("title"));

            foreach (var invoking_page in invoking_pages[lang])
            {
                g_username = invoking_page.Substring(invoking_page.IndexOf(':') + 1, invoking_page.IndexOf('/') - 1 - invoking_page.IndexOf(':'));
                process_site("https://" + g_lang + ".wikipedia.org/wiki/" + Uri.EscapeUriString(invoking_page) + "?action=raw");
                if (!script_users[g_lang].Contains(g_username))
                    script_users[g_lang].Add(g_username);
            }

            foreach (var username in script_users[lang])
            {
                g_username = username;
                process_site("https://meta.wikimedia.org/wiki/user:" + Uri.EscapeUriString(username) + "/global.js?action=raw");
            }

            foreach (var s in scripts[lang].OrderByDescending(s => s.Value.active))
                result[lang] += "\n|-\n|[[:" + s.Key + "]]||" + s.Value.active + "||" + s.Value.inactive + "||" + (s.Value.active + s.Value.inactive);
            //Save(site, "ВП:Самые используемые скрипты", result + "\n|}", "update");
            if (lang == "ru")
                Save(site[lang], "ВП:Самые используемые скрипты/details", debug_result + "\n|}", "update");
            w.WriteLine(result[lang]);
        }
        w.Close();
    }
}
