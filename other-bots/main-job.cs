using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Xml;
using static System.Net.Mime.MediaTypeNames;
class most_edits_record { public int all, main, user, templ, file, cat, portproj, meta, tech, main_edits_index; public bool globalbot; }
class redir { public string src_title, dest_title; public int src_ns, dest_ns; public override string ToString() { return src_ns + ' ' + src_title + ' ' + dest_ns + ' ' + dest_title; } }
class script_usages { public int active, inactive;}
class pair { public string page, file; public logrecord deletion_data; }
class logrecord { public string deleter, comment; }
public class Image { public int ns; public string title; }
public class Limits { public int allpages, images, revisions; }
public class Page { public int pageid, ns; public string title; public List<Image> images; public List<Revision> revisions; }
public class Query { public List<Page> pages; public List<Allpage> allpages; }
public class Root { public bool batchcomplete; public Limits limits; public Query query; public Continue @continue; }
public class Revision { public DateTime timestamp; }
public class Allpage { public int pageid; public int ns; public string title; }
public class Continue { public string apcontinue; public string @continue; }
class Program
{
    static DateTime now; static string[] monthname, prepositional, creds; static HashSet<string> highflags = new HashSet<string>(); static HttpClient site;
    static string resulttext_per_year, resulttext_per_month, resulttext_alltime, user, common_resulttext = "{{самые активные участники}}{{Плавающая шапка таблицы}}{{shortcut|ВП:ИТОГИ}}<center>\nСтатистика" +
        " по числу итогов, подведённых %type%.\n\nСтатистика собирается поиском по тексту страниц обсуждений и потому верна лишь приближённо, нестандартный синтаксис итога или подписи итогоподводящего " +
        "может привести к тому, что такой итог не будет засчитан. Первично отсортировано по сумме всех итогов, кроме итогов на КУЛ и ЗКП(АУ).\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!" +
        "Участник!!Σ!!{{vh|[[ВП:КУ|]]}}!!{{vh|[[ВП:ВУС|]]}}!!{{vh|[[ВП:КПМ|]]}}!!{{vh|[[ВП:ПУЗ|]]}}!!{{vh|[[ВП:КОБ|]]+[[ВП:КРАЗД|РАЗД]]}}!!{{vh|[[ВП:ОБК|]]}}!!{{vh|[[ВП:КУЛ|]]}}!!{{vh|[[ВП:ЗКА|]]}}!!" +
        "{{vh|[[ВП:ОСП|]]+[[ВП:ОАД|]]}}!!{{vh|[[ВП:ЗС|]]}}!!{{vh|[[ВП:ЗС-|]]}}!!{{vh|[[ВП:ЗСП|ЗС]]+[[ВП:ЗСАП|(А)П]]}}!!{{vh|[[ВП:ЗСПИ|]]}}!!{{vh|[[ВП:ЗСФ|]]}}!!{{vh|[[ВП:КОИ|]]}}!!{{vh|[[ВП:ИСЛ|]]}}!!" +
        "{{vh|[[ВП:ЗКП|]][[ВП:ЗКПАУ|(АУ)]]}}!!{{vh|[[ВП:КИС|]]}}!!{{vh|[[ВП:КИСЛ|]]}}!!{{vh|[[ВП:КХС|]]}}!!{{vh|[[ВП:КЛСХС|]]}}!!{{vh|[[ВП:КДС|]]}}!!{{vh|[[ВП:КЛСДС|]]}}!!{{vh|[[ВП:КИСП|]]}}!!{{vh|[[ВП:" +
        "КЛСИСП|]]}}!!{{vh|[[ВП:РДБ|]]}}!!{{vh|[[ВП:ФТ|]]+[[ВП:ТЗ|]]}}!!{{vh|[[ВП:Ф-АП|АП]]}}", debug_result = "<center>\n{|class=\"standard sortable\"\n!Страница вызова!!Скрипт", orphan_article, username,
        invoking_page, file_exclusion_query;
    static Dictionary<string, Dictionary<string, Dictionary<string, int>>> stats = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>
    { { "month", new Dictionary<string, Dictionary<string, int>>() }, { "year", new Dictionary<string, Dictionary<string, int>>() }, { "alltime", new Dictionary<string, Dictionary<string, int>>() } };
    static HashSet<string> invoking_pages = new HashSet<string>(), script_users = new HashSet<string>(); static MySqlCommand command; static MySqlDataReader rdr; static MySqlConnection connect;
    static Dictionary<string, bool> users_activity = new Dictionary<string, bool>(); static Dictionary<string, script_usages> scripts = new Dictionary<string, script_usages>();
    static Regex is_rgx = new Regex(@"importscript\s*\(\s*['""]([^h/].*?)\s*['""]\s*\)", RegexOptions.IgnoreCase),
    is2_rgx = new Regex(@"importscript\s*\(\s*['""]/wiki/(.*?)\s*['""]\s*\)", RegexOptions.IgnoreCase), multiline_comment = new Regex(@"/\*.*?\*/", RegexOptions.Singleline),
    is_foreign_rgx = new Regex(@"importscript\s*\(\s*['""]([^h].*?)\s*['""],\s*['""]([^""']*)\s*['""]", RegexOptions.IgnoreCase),
    is_ext_rgx = new Regex(@"importscript\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/wiki/(.*?\.js)", RegexOptions.IgnoreCase),
    loader_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""]/w/index\.php\?title=(.*?\.js)", RegexOptions.IgnoreCase),
    loader_foreign_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/w/index\.php\?title=(.*?\.js)", RegexOptions.IgnoreCase),
    loader_foreign2_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/wiki/([^?]*)\?", RegexOptions.IgnoreCase),
    r1 = new Regex(@"importscript.*\.js", RegexOptions.IgnoreCase), r2 = new Regex(@"\.(load|getscript|using)\b.*\.js", RegexOptions.IgnoreCase);
    static Dictionary<string, Dictionary<string, int>> users = new Dictionary<string, Dictionary<string, int>>(); static bool legit_link_found; static int position_number = 0;
    static string e(string input) { return Uri.EscapeDataString(input); }
    static int i(Object input) { return Convert.ToInt32(input); }
    static string readpage(string input) { return site.GetStringAsync("https://ru.wikipedia.org/wiki/" + e(input) + "?action=raw").Result; }
    static string serialize(HashSet<string> list) { list.ExceptWith(highflags); return JsonConvert.SerializeObject(list); }
    static string cell(int number) { if (number == 0) return ""; else return number.ToString(); }
    static string escape_comment(string comment)
    {
        string result = comment.Replace("[[К", "[[:К").Replace("[[C", "[[:C"); if (result.Contains("{") || result.Contains("}") || result.Contains("|")) result = "<nowiki>" + result + "</nowiki>";
        return result;
    }
    static HttpClient login(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument(); doc.LoadXml(result.Content
            .ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value; result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new 
                FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result; return client;
    }
    static void save(string lang, string title, string text, string comment)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result; if (!result.IsSuccessStatusCode) return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" },
            { new StringContent(text), "text" }, { new StringContent(comment), "summary" }, { new StringContent(token), "token" } }).Result;
        if (!result.ToString().Contains("uccess")) Console.WriteLine(result.ToString());
    }
    static void rsave(string title, string text) { save("ru", title, text, ""); }
    static void nonfree_files_in_nonmain_ns()
    {
        string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&generator=categorymembers&fuprop=title&fulimit=max&gcmtitle=К:Файлы:Несвободные&gcmtype=file&gcmlimit=1000";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&gcmcontinue=" + e(cont)).Result);
            var r = new XmlTextReader(new StringReader(apiout)); r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("gcmcontinue"); string file = "";
            while (r.Read()) {
                if (r.Name == "page")
                    file = r.GetAttribute("title");
                if (r.Name == "fu" && r.GetAttribute("ns") != "0" && r.GetAttribute("ns") != "102") {
                    string title = r.GetAttribute("title"); if (title == "Википедия:Форум/Авторское право/Файлы Инкубатора") continue;
                    string text = readpage(title); string initialtext = text; string filename = file.Substring(5);
                    filename = "(" + Regex.Escape(filename) + "|" + Regex.Escape(e(filename)) + ")"; filename = filename.Replace(@"\ ", "[ _]+");
                    var r1 = new Regex(@"\[\[\s*(file|image|файл|изображение):\s*" + filename + @"[^[\]]*\]\]", RegexOptions.IgnoreCase);
                    var r2 = new Regex(@"\[\[\s*(file|image|файл|изображение):\s*" + filename + @"[^[]*(\[\[[^\[\]]*\]\][^[\]]*)*\]\]", RegexOptions.IgnoreCase);
                    var r3 = new Regex(@"<\s*gallery[^>]*>\s*(file|image|файл|изображение):\s*" + filename + @"[^\n]*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var r4 = new Regex(@"(<\s*gallery[^>]*>.*)(file|image|файл|изображение):\s*" + filename + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var r5 = new Regex(@"(<\s*gallery[^>]*>.*)" + filename + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var r6 = new Regex(@"<\s*gallery[^>]*>\s*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    var r7 = new Regex(@"\{\{\s*(flagicon image|audio)[^|}]*\|\s*" + filename + @"[^}]*\}\}");
                    var r8 = new Regex(@"([=|]\s*)(file|image|файл|изображение):\s*" + filename, RegexOptions.IgnoreCase);
                    var r9 = new Regex(@"([=|]\s*)" + filename, RegexOptions.IgnoreCase); text = r1.Replace(text, ""); text = r2.Replace(text, ""); text = r3.Replace(text, ""); text = r4.Replace(text, "$1");
                    text = r5.Replace(text, "$1"); text = r6.Replace(text, ""); text = r7.Replace(text, ""); text = r8.Replace(text, "$1"); text = r9.Replace(text, "$1");
                    if (text != initialtext) {
                        save("ru", title, text, "удаление несвободного файла из служебных пространств");
                        if (r.GetAttribute("ns") == "10") {
                            string tracktext = readpage("u:MBH/Шаблоны с удалёнными файлами"); rsave("u:MBH/Шаблоны с удалёнными файлами", tracktext + "\n* [[" + title + "]]"); }
                    }
                }
            }

        }
    }
    static void outdated_templates()
    {
        var rgx = new Regex(@"\{\{\s*(Текущие события|Редактирую|Связь с текущим событием)[^{}]*\}\}", RegexOptions.IgnoreCase);
        foreach (string cat in new string[] { "Категория:Википедия:Статьи с просроченным шаблоном текущих событий", "Категория:Википедия:Просроченные статьи, редактируемые прямо сейчас" })
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=" + cat + "&cmlimit=max").Result)))
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "cm") {
                        string text = readpage(r.GetAttribute("title")); save("ru", r.GetAttribute("title"), rgx.Replace(text, ""), "удалены просроченные шаблоны");
                    }
    }
    static void unlicensed_files()
    {
        var autocatfiles = new HashSet<string>();
        var tagged_files = new HashSet<string>();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Категория:Файлы:Без машиночитаемой лицензии&cmprop=title&cmlimit=50").Result)))
            while (r.Read())
                if (r.Name == "cm")
                    autocatfiles.Add(r.GetAttribute("title"));

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=embeddedin&format=xml&eititle=t:No_license&einamespace=6&eilimit=max").Result)))
            while (r.Read())
                if (r.Name == "ei")
                    tagged_files.Add(r.GetAttribute("title"));

        autocatfiles.ExceptWith(tagged_files);
        foreach (var file in autocatfiles)
        {
            string pagetext = readpage(file);
            save("ru", file, "{{subst:nld}}\n" + pagetext, "вынос на КБУ файла без валидной лицензии");
        }
    }
    static void orphan_nonfree_files()
    {
        string cont, apiout, query, fucont = "", gcmcont = ""; var tagged_files = new HashSet<string>(); var nonfree_files = new HashSet<string>(); var unused_files = new HashSet<string>();
        query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&list=&continue=gcmcontinue%7C%7C&generator=categorymembers&fulimit=max&gcmtitle=Категория:Файлы:Несвободные&gcmnamespace=6&gcmlimit=max";
        do
        {
            apiout = site.GetStringAsync(query + (fucont == "" ? "" : "&fucontinue=" + e(fucont)) + (gcmcont == "" ? "" : "&gcmcontinue=" + e(gcmcont))).Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); fucont = r.GetAttribute("fucontinue"); gcmcont = r.GetAttribute("gcmcontinue");
                if (fucont == null) fucont = ""; if (gcmcont == null) gcmcont = "";
                string filename = "";
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page")
                        filename = r.GetAttribute("title");
                    if (r.Name == "fu" && (r.GetAttribute("ns") == "0" || r.GetAttribute("ns") == "102") && !tagged_files.Contains(filename))
                        tagged_files.Add(filename);
                }
            }
        } while (fucont != "" || gcmcont != "");

        cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Категория:Файлы:Несвободные&cmprop=title&cmnamespace=6&cmlimit=max";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&cmcontinue=" + e(cont)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                        nonfree_files.Add(r.GetAttribute("title"));
            }
        }

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=embeddedin&format=xml&eititle=t:Orphaned-fairuse&einamespace=6&eilimit=max").Result)))
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                    tagged_files.Add(r.GetAttribute("title"));

        nonfree_files.ExceptWith(tagged_files);
        var pagerx = new Regex(@"\|\s*статья\s*=\s*([^|\n]*)\s*\|");
        var redirrx = new Regex(@"#(redirect|перенаправление)\s*\[\[([^\]]*)\]\]", RegexOptions.IgnoreCase);
        foreach (var file in nonfree_files)
        {
            try
            {
                var legal_file_using_pages = new HashSet<string>();
                string file_descr = readpage(file);
                var x = pagerx.Matches(file_descr);
                foreach (Match xx in x)
                    legal_file_using_pages.Add(xx.Groups[1].Value);
                foreach (var page in legal_file_using_pages)
                    try
                    {
                        string using_page_text = readpage(page);
                        if (!redirrx.IsMatch(using_page_text))
                            rsave(page, using_page_text + "\n");
                        else
                        {
                            string redirect_target_page = redirrx.Match(using_page_text).Groups[1].Value;
                            string target_page_text = readpage(redirect_target_page);
                            rsave(redirect_target_page, target_page_text + "\n");
                        }
                    }
                    catch { continue; }
            }
            catch { }
        }
        foreach (var file in nonfree_files)
        {
            apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&titles=" + e(file)).Result;
            if (!apiout.Contains("<fileusage>"))
                unused_files.Add(file);
        }

        foreach (var file in unused_files)
        {
            string uploaddate = "";
            string file_descr = readpage(file);
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&titles=" + e(file) + "&rvprop=timestamp&rvlimit=1&rvdir=newer").Result)))
                while (r.Read())
                    if (r.Name == "rev")
                        uploaddate = r.GetAttribute("timestamp").Substring(0, 10);
            if (now - DateTime.ParseExact(uploaddate, "yyyy-MM-dd", CultureInfo.InvariantCulture) > new TimeSpan(0, 1, 0, 0))
                save("ru", file, "{{subst:ofud}}\n" + file_descr, "вынос на КБУ неиспользуемого в статьях несвободного файла");
        }
    }
    static bool always_redir(string page, bool title)
    {
        var redir_rgx = new Regex(@"#(перенаправление|redirect) *\[\[");
        var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&rvprop=ids|content&rvlimit=max&" + (title ? "titles=" :
            "pageids=") + page).Result));
        while (r.Read())
            if (r.Name == "rev" && r.NodeType == XmlNodeType.Element) {
                r.Read();
                if (!redir_rgx.IsMatch(r.Value))
                    return false;
            }
        return true;
    }
    static void redirs_deletion()
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result; doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value; var legal_redirs = new List<string>();
        var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=К:Википедия:Намеренные перенаправления между СО&cmlimit=max").Result));
        while (r.Read())
            if (r.Name == "cm")
                legal_redirs.Add(r.GetAttribute("pageid"));
        foreach (int ns in new int[] { 1, 3, 5, 7, 9, 11, 13, 15, 101, 103, 105, 107, 829 })
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allredirects&arprop=title|ids&arnamespace=" + ns + "&arlimit=max";
            while (cont != null)
            {
                var r4 = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arcontinue=" + e(cont)).Result));
                r4.Read(); r4.Read(); cont = r4.GetAttribute("arcontinue"); while (r4.Read())
                    if (r4.Name == "r" && (ns != 3 || r4.GetAttribute("title").Contains("/")))
                    {
                        string id = r4.GetAttribute("fromid");
                        if (!legal_redirs.Contains(id) && always_redir(id, false))
                        {
                            var r3 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blpageid=" + id).Result));
                            bool there_are_links = false;
                            while (r3.Read())
                                if (r3.Name == "bl" && !r3.GetAttribute("title").StartsWith("Википедия:Страницы с похожими названиями") && !r3.GetAttribute("title").StartsWith("Участник:DvoreBot/Оставленные перенаправления"))
                                    there_are_links = true;
                            if (!there_are_links)
                                result = site.PostAsync("https://ru.wikipedia.org/w/api.php", new MultipartFormDataContent { { new StringContent("delete"), "action" }, { new StringContent(id), "pageid" },
                                        { new StringContent(token), "token" }, { new StringContent("[[ВП:КБУ#П6|редирект на СО без ссылок]]"), "reason" } }).Result;
                        }
                    }
            }
        }
        var r2 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=querypage&qppage=BrokenRedirects&qplimit=max").Result));
        while (r2.Read())
            if (r2.Name == "page")
            {
                string title = r2.GetAttribute("title");
                string ns = r2.GetAttribute("ns");
                if (ns != "2" || (ns == "2" && title.Contains("/")))//ЛС с меты?
                {
                    var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&titles=" + title + "&rvprop=ids&rvlimit=max").Result));
                    if (always_redir(title, true))
                        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", new MultipartFormDataContent { { new StringContent("delete"), "action" }, { new StringContent(title), "title" },
                            { new StringContent(token), "token" } }).Result;
                }
            }
    }
    static void unreviewed_in_nonmain_ns()
    {
        var nsnames = new Dictionary<int, string>() { { 0, "Статьи" }, { 6, "Файлы" }, { 10, "Шаблоны" }, { 14, "Категории" }, { 100, "Порталы" }, { 828, "Модули" } };
        string result = "";
        foreach (var ns in nsnames.Keys)
            foreach (string type in new string[] { "nonredirects", "redirects" })
                if (!(ns == 0 && type == "nonredirects"))
                {
                    string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=unreviewedpages&urlimit=max&urnamespace=" + ns + "&urfilterredir=" + type, apiout;
                    result += "==" + (type == "nonredirects" ? nsnames[ns] : "=Редиректы=") + "==\n";
                    while (cont != null)
                    {
                        apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&urcontinue=" + e(cont)).Result);
                        using (var r = new XmlTextReader(new StringReader(apiout)))
                        {
                            r.WhitespaceHandling = WhitespaceHandling.None;
                            r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("urcontinue");
                            while (r.Read())
                                if (r.Name == "p")
                                {
                                    string title = r.GetAttribute("title");
                                    result += type == "nonredirects" ? "#[[:" + title + "]]\n" : "#[https://ru.wikipedia.org/w/index.php?title=" + e(title) + "&redirect=no " + title + "]\n";
                                }
                        }
                    }
                }
        rsave("Проект:Патрулирование/Непроверенные вне ОП", result);
    }
    static void user_activity_stats_template()
    {
        var days = new Dictionary<string, int>(); var edits = new Dictionary<string, int>(); var itemrgx = new Regex("<item");
        foreach (string group in new string[] { "sysop", "bot" })
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allusers&aulimit=max&augroup=" + group).Result)))
                while (r.Read())
                    if (r.Name == "u" && !days.ContainsKey(r.GetAttribute("name")))
                        days.Add(r.GetAttribute("name"), 1);
        var initialusers = readpage("Ш:User activity stats/users").Split('\n');
        foreach (var user in initialusers)
            if (!days.ContainsKey(user))
                days.Add(user, 1);
        foreach (string tmplt in new string[] { "Участник покинул проект", "Вики-отпуск" })
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&einamespace=2|3&eilimit=max&eititle=Ш:" + tmplt).Result)))
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "ei") {
                        string user = r.GetAttribute("title");
                        if (!user.Contains("/"))
                            user = user.Substring(user.IndexOf(':') + 1, user.Length - user.IndexOf(':') - 1);
                        else
                            user = user.Substring(user.IndexOf(':') + 1, user.IndexOf("/") - user.IndexOf(':') - 1);
                        if (!edits.ContainsKey(user))
                            edits.Add(user, 0);
                    }
        foreach (var u in days.Keys.ToList())
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucuser=" + e(u)).Result)))
                while (r.Read())
                    if (r.Name == "item") {
                        string ts = r.GetAttribute("timestamp"); int y = i(ts.Substring(0, 4)); int m = i(ts.Substring(5, 2)); int d = i(ts.Substring(8, 2)); days[u] = (now - new DateTime(y, m, d)).Days;
                    }
        foreach (var v in edits.Keys.ToList()) {
            var res = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=max&ucend=" + now.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ss") +
                "&ucprop=&ucuser=" + e(v)).Result;
            edits[v] = itemrgx.Matches(res).Count;
        }

        string result = "{{#switch:{{{1}}}\n";
        foreach (var r in days.OrderBy(r => r.Value))
            result += "|" + r.Key + "=" + r.Value + "\n";
        rsave("Шаблон:User activity stats/days", result + "|}}");

        result = "{{#switch:{{{1}}}\n";
        foreach (var v in edits.OrderByDescending(v => v.Value))
            if (v.Value > 0)
                result += "|" + v.Key + "=" + (v.Value == 0 ? "" : v.Value.ToString()) + "\n";
        rsave("Шаблон:User activity stats/edits", result + "|}}");
    }
    static void trans_namespace_moves()
    {
        var apatusers = new HashSet<string>();
        string result = "<center>{{Плавающая шапка таблицы}}{{shortcut|ВП:TRANSMOVE}}Красным выделены неавтопатрулируемые.{{clear}}\n{|class=\"standard sortable ts-stickytableheader\"\n!Дата!!Источник!!Название в ОП!!Переносчик!!Коммент";
        var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title|type|user|timestamp|comment|details&letype=move&lelimit=max").Result));
        while (r.Read())
            if (r.NodeType == XmlNodeType.Element && r.Name == "item")
            {
                string user = r.GetAttribute("user");
                if (!apatusers.Contains(user))
                    using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=users&usprop=rights&ususers=" + user).Result)))
                        while (rr.Read())
                            if (rr.Value == "autoreview" || rr.Value.Contains("patrol"))
                                apatusers.Add(user);
                string oldns = r.GetAttribute("ns"); if (oldns == "0") continue;
                string oldtitle = r.GetAttribute("title"); string date = r.GetAttribute("timestamp").Substring(5, 5); string comment = escape_comment(r.GetAttribute("comment"));
                r.Read(); string newns = r.GetAttribute("target_ns"); if (newns != "0") continue; string newtitle = r.GetAttribute("target_title");
                result += "\n|-" + (apatusers.Contains(user) ? "" : "style=\"background-color:#fcc\"") + "\n|" + date + "||[[:" + oldtitle + "]]||[[:" + newtitle + "]]||{{u|" + user + "}}||" + comment;
            }
        rsave("ВП:Страницы, перенесённые в пространство статей", result + "\n|}");
    }
    static void zsf_archiving()
    {
        var year = now.Year; string zsftext = readpage("ВП:Заявки на снятие флагов"); string initialtext = zsftext;
        var threadrgx = new Regex(@"\n\n==[^\n]*: флаг [^=]*==[^⇧]*===\s*Итог[^=]*===([^⇧]*)\((апат|пат|откат|загр|ПИ|ПФ|ПбП|инж|АИ|бот)\)\s*—\s*{{(за|против)([^⇧]*)⇧-->", RegexOptions.Singleline);
        var signature = new Regex(@"(\d\d:\d\d, \d{1,2} \w+ \d{4}) \(UTC\)"); var threads = threadrgx.Matches(zsftext);
        foreach (Match thread in threads)
        {
            string archivepage = ""; string threadtext = thread.Groups[0].Value; var summary = signature.Matches(thread.Groups[1].Value); var summary_discuss = signature.Matches(thread.Groups[4].Value);
            bool outdated = true;
            foreach (Match s in summary)
                if (now - DateTime.Parse(s.Groups[1].Value, new CultureInfo("ru-RU")) < new TimeSpan(2, 0, 0, 0))
                    outdated = false;
            foreach (Match s in summary_discuss)
                if (now - DateTime.Parse(s.Groups[1].Value, new CultureInfo("ru-RU")) < new TimeSpan(2, 0, 0, 0))
                    outdated = false;
            if (!outdated) continue;
            switch (thread.Groups[2].Value)
            {
                case "апат":
                case "пат":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Патрулирующие/" + year;
                    break;
                case "откат":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Откатывающие/" + year;
                    break;
                case "загр":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Загружающие";
                    break;
                case "ПИ":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Подводящие итоги/" + year;
                    break;
                case "ПбП":
                case "ПФ":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Переименовывающие";
                    break;
                case "инж":
                case "АИ":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Инженеры и АИ";
                    break;
                case "бот":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Нарушающие боты";
                    break;
                default:
                    continue;
            }
            zsftext = zsftext.Replace(threadtext, "");
            try
            {
                string archivetext = readpage(archivepage);
                rsave(archivepage, archivetext + threadtext);
            }
            catch { rsave(archivepage, threadtext); }

        }
        if (zsftext != initialtext)
            rsave("Википедия:Заявки на снятие флагов", zsftext);
    }
    static void little_flags()
    {
        var ru = new MySqlConnection(creds[2].Replace("%project%", "ruwiki")); var global = new MySqlConnection(creds[2].Replace("%project%", "centralauth")); ru.Open(); global.Open();
        MySqlCommand command; MySqlDataReader rdr; var pats = new HashSet<string>(); var rolls = new HashSet<string>(); var apats = new HashSet<string>(); var fmovers = new HashSet<string>();

        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"editor\";", ru); rdr = command.ExecuteReader();
        while (rdr.Read())
            pats.Add(rdr.GetString(0));
        rdr.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"rollbacker\";"; rdr = command.ExecuteReader();
        while (rdr.Read())
            rolls.Add(rdr.GetString(0));
        rdr.Close(); rolls.Remove("Железный капут");

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"autoreview\";"; rdr = command.ExecuteReader();
        while (rdr.Read())
            apats.Add(rdr.GetString(0));
        rdr.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"filemover\";"; rdr = command.ExecuteReader();
        while (rdr.Read())
            fmovers.Add(rdr.GetString(0));
        rdr.Close();

        foreach (string flag in new string[] { "sysop", "closer", "engineer" }) {
            command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"" + flag + "\";";
            rdr = command.ExecuteReader();
            while (rdr.Read()) {
                string user = rdr.GetString(0);
                if (!highflags.Contains(user))
                    highflags.Add(user);
            }
            rdr.Close();
        }
        command = new MySqlCommand("SELECT cast(gu_name as char) user FROM global_user_groups JOIN globaluser ON gu_id=gug_user WHERE gug_group=\"global-rollbacker\"", global); rdr = command.ExecuteReader();
        while (rdr.Read())
            if (!rolls.Contains(rdr.GetString(0)))
                rolls.Add(rdr.GetString(0));

        var patnotrolls = new HashSet<string>(pats); patnotrolls.ExceptWith(rolls); var rollnotpats = new HashSet<string>(rolls); rollnotpats.ExceptWith(pats);
        var patrolls = new HashSet<string>(pats); patrolls.IntersectWith(rolls);
        string result = "{\"userSet\":{\"p,r\":" + serialize(patrolls) + ",\"ap\":" + serialize(apats) + ",\"p\":" + serialize(patnotrolls) + ",\"r\":" + serialize(rollnotpats) + "," + "\"f\":" + serialize(fmovers) + "}}";
        rsave("MediaWiki:Gadget-markothers.json", result);
    }
    static void catmoves()
    {
        string result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Откуда (страниц в категории)!!Куда (страниц в категории)!!Юзер!!Коммент";
        var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&letype=move&lenamespace=14&lelimit=max").Result));
        while (r.Read())
            if (r.NodeType == XmlNodeType.Element && r.Name == "item") {
                string oldtitle = r.GetAttribute("title");
                var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + e(oldtitle)).Result));
                while (rr.Read())
                    if (rr.NodeType == XmlNodeType.Element && rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0") {
                        string user = r.GetAttribute("user"); string timestamp = r.GetAttribute("timestamp").Substring(0, 10); string comment = escape_comment(r.GetAttribute("comment"));
                        r.Read(); string newtitle = r.GetAttribute("target_title");
                        result += "\n|-\n|" + timestamp + "||[[:" + oldtitle + "]] ({{PAGESINCATEGORY:" + oldtitle.Substring(10) + "}})||[[:" + newtitle + "]] ({{PAGESINCATEGORY:" +
                            newtitle.Substring(10) + "}})||[[u:" + user + "]]||" + comment;
                    }
            }
        rsave("u:MBH/Переименованные категории с недоперенесёнными страницами", result + "\n|}");
        result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Имя (страниц в категории)!!Юзер!!Коммент";
        r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&leaction=delete/delete&lenamespace=14&lelimit=max").Result));
        while (r.Read())
            if (r.NodeType == XmlNodeType.Element && r.Name == "item" && r.GetAttribute("title") != null) {
                string title = r.GetAttribute("title");
                var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + e(title)).Result));
                while (rr.Read())
                    if (rr.NodeType == XmlNodeType.Element && rr.Name == "page" && rr.GetAttribute("missing") != null) {
                        rr.Read();
                        if (rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0") {
                            string user = r.GetAttribute("user"); string timestamp = r.GetAttribute("timestamp").Substring(0, 10); string comment = escape_comment(r.GetAttribute("comment"));
                            result += "\n|-\n|" + timestamp + "||[[:" + title + "]] ({{PAGESINCATEGORY:" + title.Substring(10) + "}})||[[u:" + user + "]]||" + comment;
                        }
                    }
            }
        rsave("u:MBH/Удалённые категории со страницами", result += "\n|}");
    }
    static void orphan_articles()
    {
        var nonlegit_link_pages = new List<string>();
        foreach (string templatename in "Координационный список|Неоднозначность|Навигация для года|Шапка календарной даты".Split('|')) {
            string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eilimit=max&eititle=Ш:" + templatename;
            while (cont != null) {
                apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&eicontinue=" + e(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout))) {
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                    while (r.Read())
                        if (r.Name == "ei" && !nonlegit_link_pages.Contains(r.GetAttribute("title")))
                            nonlegit_link_pages.Add(r.GetAttribute("title"));
                }
            }
        }
        string apiout1, cont1 = "", query1 = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eilimit=max&eititle=ш:изолированная статья";
        while (cont1 != null) {
            apiout1 = (cont1 == "" ? site.GetStringAsync(query1).Result : site.GetStringAsync(query1 + "&eicontinue=" + e(cont1)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout1))) {
                r.Read(); r.Read(); r.Read(); cont1 = r.GetAttribute("eicontinue");
                while (r.Read())
                    if (r.Name == "ei") {
                        orphan_article = r.GetAttribute("title");
                        legit_link_found = false;
                        using (var r2 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blfilterredir=nonredirects" +
                                "&bllimit=max&bltitle=" + e(orphan_article)).Result)))
                            while (r2.Read())
                                if (r2.Name == "bl" && r2.GetAttribute("ns") == "0" && !nonlegit_link_pages.Contains(r2.GetAttribute("title"))) {
                                    remove_template_from_non_orphan_page(); break;
                                }
                        if (!legit_link_found)
                            using (var r3 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blfilterredir=redirects" +
                                "&bllimit=max&bltitle=" + e(orphan_article)).Result)))
                                while (r3.Read())
                                    if (r3.Name == "bl" && !legit_link_found) {
                                        string linked_redirect = r3.GetAttribute("title");
                                        using (var r4 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blfilterredir=redirects" +
                                "&bllimit=max&bltitle=" + e(linked_redirect)).Result)))
                                            while (r4.Read())
                                                if (r4.Name == "bl" && r4.GetAttribute("ns") == "0" && !nonlegit_link_pages.Contains(r4.GetAttribute("title"))) {
                                                    remove_template_from_non_orphan_page(); break;
                                                }
                                    }
                    }
            }
        }
    }
    static void remove_template_from_non_orphan_page()
    {
        try {
            string pagetext = readpage(orphan_article);
            save("ru", orphan_article, pagetext.Replace("{{изолированная статья|", "{{subst:ET|").Replace("{{Изолированная статья|", "{{subst:ET|"), "удаление неактуального шаблона изолированной статьи");
            legit_link_found = true;
        } catch { }
    }
    static void dm89_stats()
    {        
        var cats = new Dictionary<string, int>() { {"Википедия:Статьи для срочного улучшения",0 },{ "Википедия:Незакрытые обсуждения переименования страниц",0 },{ "Википедия:Незакрытые обсуждения статей для" +
                " улучшения", 0 },{ "Википедия:Кандидаты на удаление", 0 },{ "Википедия:Незакрытые обсуждения удаления страниц", 0 },{ "Википедия:Статьи для переименования", 0 }, { "Википедия:Кандидаты на " +
                "объединение", 0 },{ "Википедия:Незакрытые обсуждения объединения страниц", 0 },{ "Википедия:Статьи для разделения", 0 },{ "Википедия:Незакрытые обсуждения разделения страниц", 0 },
            { "Википедия:Незакрытые обсуждения восстановления страниц", 0 },{ "Инкубатор:Все статьи", 0 },{ "Инкубатор:Запросы помощи/проверки", 0 }, { "Википедия:Статьи со спам-ссылками", 0},
            { "Википедия:Страницы на КУ более 5 лет", 0 } };
        foreach (var cat in cats.Keys.ToList()) {
            var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=categoryinfo&titles=К:" + e(cat) + "&format=xml").Result));
            while (rdr.Read())
                if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "categoryinfo")
                    cats[cat] = i(rdr.GetAttribute("pages"));
        }
        string vus_text = readpage("Википедия:К восстановлению");
        var non_summaried_vus = new Regex(@"[^>]\[\[([^\]]*)\]\][^<]");

        string stat_text = readpage("u:MBH/Завалы");
        string result = "\n|-\n|{{subst:#time:j.m.Y}}||" + cats["Википедия:Статьи для срочного улучшения"] + "||" + cats["Википедия:Незакрытые обсуждения статей для улучшения"] + "||" + cats["Википедия:" +
            "Кандидаты на удаление"] + "||" + cats["Википедия:Страницы на КУ более 5 лет"] + "||" + cats["Википедия:Незакрытые обсуждения удаления страниц"] + "||" + cats["Википедия:Статьи для переименования"] +
            "||" + cats["Википедия:Незакрытые обсуждения переименования страниц"] + "||" + cats["Википедия:Кандидаты на объединение"] + "||" + cats["Википедия:Незакрытые обсуждения объединения страниц"] +
            "||" + cats["Википедия:Статьи для разделения"] + "||" + cats["Википедия:Незакрытые обсуждения разделения страниц"] + "||" + non_summaried_vus.Matches(vus_text).Count + "||" + cats["Википедия:" +
            "Незакрытые обсуждения восстановления страниц"] + "||" + cats["Инкубатор:Все статьи"] + "||" + cats ["Инкубатор:Запросы помощи/проверки"] + "||" + cats["Википедия:Статьи со спам-ссылками"];
        rsave("u:MBH/Завалы", stat_text + result);
    }
    static void inc_check_help_requests_img()
    {
        string result = ""; var processed = new HashSet<string>() { "Шаблон:Инкубатор, помочь/проверить" };
        var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmprop=title&cmtitle=К:Инкубатор:Запросы помощи/проверки").Result));
        while (r.Read())
            if (r.Name == "cm" && r.GetAttribute("ns").StartsWith("10")) {
                string title = r.GetAttribute("title"); string shorttitle = title.Substring(title.IndexOf(':') + 1);
                if (!processed.Contains(shorttitle)) { processed.Add(shorttitle); result += ", [[" + title + "|" + shorttitle + "]]"; }
            }
        rsave("ВП:Форум/Общий/Запросы помощи в Инкубаторе", result.Substring(2));
        result = "<gallery mode=packed heights=75px>";
        Root inc_images_json = JsonConvert.DeserializeObject<Root>(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=json&prop=images&generator=allpages&formatversion=2&imlimit=max&gapnamespace=102&gapfilterredir=nonredirects&gaplimit=max").Result);
        foreach (Page page in inc_images_json.query.pages)
        {
            string pagetext = readpage(page.title);
            foreach (var img in page.images) {
                var rgx = new Regex(Regex.Escape(img.title).Replace("\\ ", "[ _]"), RegexOptions.IgnoreCase);
                if (rgx.IsMatch(pagetext))
                    result += "\n" + img.title + "|[[" + page.title + "|" + page.title.Substring(10) + "]]";
            }
        }
        rsave("ВП:Форум/Авторское право/Файлы Инкубатора", result + "\n</gallery>");
    }
    static void main_inc_bot()
    {
        var except_rgx = new Regex(@"#(REDIRECT|перенаправление) \[\[|\{\{ *db-|\{\{ *к удалению|инкубатор, (на доработке|черновик ВУС)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var inc_tmplt_rgx = new Regex(@"\{\{[^{}|]*инкубатор[^{}]*\}\}\n", RegexOptions.IgnoreCase); var suppressed_cats_rgx = new Regex(@"\[\[ *: *(category|категория|к) *:", RegexOptions.IgnoreCase);
        var cats_rgx = new Regex(@"\[\[ *(Category|Категория|К) *:.*?\]\]", RegexOptions.Singleline | RegexOptions.IgnoreCase); int num_of_nominated_pages = 0; string afd_addition = "";
        var index_rgx = new Regex("__(INDEX|ИНДЕКС)__", RegexOptions.IgnoreCase); string afd_pagename = "Википедия:К удалению/" + now.Day + " " + monthname[now.Month] + " " + now.Year; var ts = now;
        var unpatbot = login("ru", creds[3], creds[4]);
        var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=allpages&apnamespace=102&apfilterredir=nonredirects&aplimit=max&format=xml").Result));
        while (rdr.Read())
            if (rdr.Name == "p" && rdr.GetAttribute("title") != "Инкубатор:Песочница") {
                string incname = rdr.GetAttribute("title"); string pagetext = readpage(incname);
                if (!except_rgx.IsMatch(pagetext)) {
                    Root history = JsonConvert.DeserializeObject<Root>(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=json&prop=revisions&formatversion=2&rvprop=timestamp" +
                    "&rvlimit=max&titles=" + e(incname)).Result);
                    if (now - history.query.pages[0].revisions.Last().timestamp > new TimeSpan(14, 0, 0, 0) && now - history.query.pages[0].revisions.First().timestamp > new TimeSpan(7, 0, 0, 0) &&
                        num_of_nominated_pages < 5) {
                        string newname = incname.Substring(10); string article_exist = "";
                        var r1 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(newname)).Result));
                        while (r1.Read())
                            if (r1.Name == "page" && r1.GetAttribute("_idx") != "-1") {
                                article_exist = " ВНИМАНИЕ: статья [[:" + newname + "]] в ОП уже существует."; newname += " (из Инкубатора)"; }
                        string newtext = pagetext;
                        while (newtext.Contains("\n "))
                            newtext = newtext.Replace("\n ", "\n");
                        while (newtext.Contains("\n\n\n"))
                            newtext = newtext.Replace("\n\n\n", "\n\n");

                        save("ru", incname, "{{подст:КУ}}" + inc_tmplt_rgx.Replace(suppressed_cats_rgx.Replace(newtext, "[[К:"), ""), "удаление инк-шаблонов, возврат категорий, [[" + afd_pagename + "#" + 
                            newname + "|вынос на КУ]]");
                        num_of_nominated_pages++;
                        afd_addition += "\n\n==[[" + newname + "]]==\n[[file:Songbird-egg.svg|20px]] Исчерпало срок нахождения в [[ВП:Инкубатор|]]е, нужно оценить допустимость нахождения статьи в основном " +
                            "пространстве." + article_exist + " [[u:MBHbot]] " + ts.ToString("HH:mm, d MMMM yyyy", new CultureInfo("ru-RU")) + " (UTC)"; ts = ts.AddMinutes(1);

                        var doc = new XmlDocument(); var result = unpatbot.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
                        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var unpat_token = doc.SelectSingleNode("//tokens/@csrftoken").Value; var request = new MultipartFormDataContent
                        { { new StringContent("move"), "action" }, { new StringContent(incname), "from" }, { new StringContent(newname), "to" }, { new StringContent("1"), "movetalk" },
                            { new StringContent("1"), "noredirect" }, { new StringContent(unpat_token), "token" } };
                        unpatbot.PostAsync("https://ru.wikipedia.org/w/api.php", request);
                    }
                    else {
                        if (!pagetext.Contains("{{В инкубаторе"))
                            pagetext = "{{В инкубаторе}}\n" + pagetext;
                        foreach (Match m in cats_rgx.Matches(pagetext))
                            pagetext = pagetext.Replace(m.ToString(), m.ToString().Replace("[[", "[[:"));
                        foreach (Match m in index_rgx.Matches(pagetext))
                            pagetext = pagetext.Replace(m.ToString(), "");
                        save("ru", incname, pagetext, "добавлен {{В инкубаторе}}, если не было, и [[U:MBHbot/Подготовка статей|скрыты категории]], если были");
                    }
                }
            }
        if (num_of_nominated_pages > 0)
        {
            string afd_text = ""; var r2 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(afd_pagename)).Result));
            while (r2.Read())
                if (r2.Name == "page" && r2.GetAttribute("_idx") == "-1")
                    afd_text = "{{КУ-Навигация}}\n\n";
                else
                    afd_text = readpage(afd_pagename);
            rsave(afd_pagename, afd_text + afd_addition);
        }
    }
    static void pats_awarding()
    {
        var newfromabove = new HashSet<string>();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Категория:Википедия:Участники с " +
            "добавлением тем сверху&cmprop=title&cmlimit=max").Result)))
            while (r.Read())
                if (r.Name == "cm")
                    newfromabove.Add(r.GetAttribute("title").Substring(r.GetAttribute("title").IndexOf(":") + 1));
        var lastmonth = now.AddMonths(-1);
        var pats = new Dictionary<string, HashSet<string>>();
        string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=title|user&letype=review&leend=" + lastmonth.ToString("yyyy-MM") +
            "-01T00:00:00&lestart=" + now.ToString("yyyy-MM") + "-01T00:00:00&lelimit=max";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&lecontinue=" + cont).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue"); while (r.Read())
                    if (r.Name == "item")
                    {
                        string user = r.GetAttribute("user"); string page = r.GetAttribute("title");
                        if (user != null)
                        {
                            if (!pats.ContainsKey(user))
                                pats.Add(user, new HashSet<string>() { page });
                            else if (!pats[user].Contains(page))
                                pats[user].Add(page);
                        }
                    }
            }
        }
        string addition = "\n|-\n";
        if (lastmonth.Month == 1)
            addition += "|rowspan=\"12\"|" + lastmonth.Year + "||" + monthname[lastmonth.Month];
        else
            addition += "|" + monthname[lastmonth.Month];
        int c = 0;
        pats.Remove("MBHbot");
        foreach (var p in pats.OrderByDescending(p => p.Value.Count))
        {
            if (++c > 10) break;
            addition += "||{{u|" + p.Key + "}} (" + p.Value.Count + ")";
            string usertalk = readpage("ut:" + p.Key);
            string grade = c < 4 ? "I" : (c < 7 ? "II" : "III");
            if (!newfromabove.Contains(p.Key) || (newfromabove.Contains(p.Key) && usertalk.IndexOf("==") == -1))
                save("ru", "ut:" + p.Key, usertalk + "\n\n==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year + ")==\n{{subst:u:Орденоносец/" +
                    "Заслуженному патрулирующему " + grade + "|За " + c + " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года. Поздравляем! ~~~~}}",
                    "орден за патрулирования в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года");
            else
            {
                int border = usertalk.IndexOf("==");
                string header = usertalk.Substring(0, border - 1);
                string pagebody = usertalk.Substring(border);
                save("ru", "ut:" + p.Key, header + "==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year + ")==\n{{subst:u:Орденоносец/" +
                    "Заслуженному патрулирующему " + grade + "|За " + c + " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года. Поздравляем! ~~~~}}\n\n" +
                    pagebody, "орден за патрулирования в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года");
            }
        }
        string pats_order = readpage("ВП:Ордена/Заслуженному патрулирующему");
        rsave("ВП:Ордена/Заслуженному патрулирующему", pats_order + addition);
    }
    static void likes_stats()
    {
        int num_of_rows_in_output_table = 2000;
        var pairs = new Dictionary<string, int>();
        var thankedusers = new Dictionary<string, int>();
        var thankingusers = new Dictionary<string, int>();
        var ratio = new Dictionary<string, double>();
        string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp&letype=thanks&lelimit=max";
        while (cont != null) {
            if (cont == "") apiout = site.GetStringAsync(query).Result; else apiout = site.GetStringAsync(query + "&lecontinue=" + cont).Result;
            using (var rdr = new XmlTextReader(new StringReader(apiout))) {
                rdr.Read(); rdr.Read(); rdr.Read(); cont = rdr.GetAttribute("lecontinue");
                while (rdr.Read())
                    if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "item")
                    {
                        string source = rdr.GetAttribute("user");
                        string target = rdr.GetAttribute("title");
                        if (target != null && source != null) {
                            if (thankingusers.ContainsKey(source))
                                thankingusers[source]++;
                            else
                                thankingusers.Add(source, 1);
                            target = target.Substring(target.IndexOf(":") + 1);
                            if (thankedusers.ContainsKey(target))
                                thankedusers[target]++;
                            else
                                thankedusers.Add(target, 1);
                            string pair = source + " → " + target;
                            if (pairs.ContainsKey(pair))
                                pairs[pair]++;
                            else
                                pairs.Add(pair, 1);
                        }
                    }
            }
        }
        int c1 = 0, c2 = 0, c3 = 0;
        string result = "{{Плавающая шапка таблицы}}<center>См. также [https://mbh.toolforge.org/cgi-bin/likes интерактивную статистику].\n{|style=\"word-break: break-all\"\n|valign=top|\n{|class=" +
            "\"standard ts-stickytableheader\"\n!max-width=300px|Участник!!{{comment|👤⇨👍🏻|место}}";
        foreach (var p in thankingusers.OrderByDescending(p => p.Value))
            if (++c1 <= num_of_rows_in_output_table)
                result += "\n|-\n|{{u|" + p.Key + "}}||{{comment|" + p.Value + "|" + c1 + "}}";
            else
                break;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!max-width=400px|Направление!!Число";
        foreach (var p in pairs.OrderByDescending(p => p.Value))
            if (++c2 <= num_of_rows_in_output_table)
                result += "\n|-\n|" + p.Key + "||{{comment|" + p.Value + "|" + c2 + "}}";
            else
                break;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!max-width=300px|Участник!!{{comment|👍🏻⇨👤|место}}";
        foreach (var p in thankedusers.OrderByDescending(p => p.Value))
            if (++c3 <= num_of_rows_in_output_table)
                result += "\n|-\n|{{u|" + p.Key + "}}||{{comment|" + p.Value + "|" + c3 + "}}";
            else
                break;
        rsave("ВП:Пинг/Статистика лайков", result + "\n|}\n|}");
    }
    static void adminstats()
    {
        var discussiontypes = new string[] { "К удалению", "К восстановлению" }; var bots = new HashSet<string>(); var statstable = new Dictionary<string, Dictionary<string, int>>(); var sixmonths_earlier =
            now.AddMonths(-6); var now_ym = now.ToString("yyyyMM"); var sixmonths_earlier_ym = sixmonths_earlier.ToString("yyyyMM"); var connect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki"));
        connect.Open(); MySqlCommand command; MySqlDataReader r;
        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"sysop\";", connect) { CommandTimeout = 99999 }; r = command.ExecuteReader();
        while (r.Read())
            statstable.Add(r.GetString(0), new Dictionary<string, int>() { { "closer", 0 }, { "totalactions", 0}, { "delsum", 0 }, { "restoresum", 0 }, { "contentedits", 0 }, { "totaledits", 0 }, { "del_rev_log", 0 }, { "abusefilter", 0}, { "block", 0}, { "contentmodel", 0},
                { "delete", 0}, { "gblblock", 0}, { "managetags", 0}, { "merge", 0}, { "protect", 0}, { "renameuser", 0}, { "restore", 0}, { "review", 0}, { "rights", 0}, { "stable", 0}, { "mediawiki", 0}, { "checkuser", 0}, { "tag", 0} });
        r.Close();
        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"closer\";";
        r = command.ExecuteReader();
        while (r.Read())
            statstable.Add(r.GetString(0), new Dictionary<string, int>() { { "closer", 1 }, { "totalactions", 0}, { "delsum", 0 }, { "restoresum", 0 }, { "contentedits", 0 }, { "totaledits", 0 }, { "del_rev_log", 0 }, { "abusefilter", 0}, { "block", 0}, { "contentmodel", 0},
                { "delete", 0}, { "gblblock", 0}, { "managetags", 0}, { "merge", 0}, { "protect", 0}, { "renameuser", 0}, { "restore", 0}, { "review", 0}, { "rights", 0}, { "stable", 0}, { "mediawiki", 0}, { "checkuser", 0}, { "tag", 0} });
        r.Close();
        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"bot\";";
        r = command.ExecuteReader();
        while (r.Read())
            bots.Add(r.GetString(0));
        r.Close();
        command.CommandText = "SELECT cast(actor_name as char) user, log_type, log_action, COUNT(log_title) count FROM user_groups INNER JOIN actor_logging ON actor_user = ug_user INNER JOIN logging_userindex ON actor_id = log_actor WHERE ug_group IN ('sysop', 'closer') AND " +
            "log_timestamp BETWEEN " + sixmonths_earlier_ym + "01000000 AND " + now_ym + "01000000 and log_type = 'delete' and log_action <> 'delete_redir' GROUP BY actor_name, log_type, log_action;";
        r = command.ExecuteReader();
        while (r.Read())
        {
            statstable[r.GetString("user")]["totalactions"] += r.GetInt32("count");
            switch (r.GetString("log_action"))
            {
                case "delete":
                    statstable[r.GetString("user")]["delete"] += r.GetInt32("count");
                    break;
                case "restore":
                    statstable[r.GetString("user")]["restore"] += r.GetInt32("count");
                    break;
                case "revision":
                case "event":
                    statstable[r.GetString("user")]["del_rev_log"] += r.GetInt32("count");
                    break;
            }
        }
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, log_type, COUNT(log_title) count FROM user_groups INNER JOIN actor_logging ON actor_user = ug_user INNER JOIN logging_userindex ON actor_id = log_actor WHERE ug_group IN ('sysop', 'closer') AND log_timestamp " +
            "BETWEEN " + sixmonths_earlier_ym + "01000000 AND " + now_ym + "01000000 and log_action not like 'move_%' and log_action not like '%-a' and log_action not like '%-ia' and log_type <> 'spamblacklist' and log_type <> 'thanks' and log_type <> 'upload' and log_type <> 'create' " +
            "and log_type <> 'move' and log_type <> 'delete' and log_type <> 'newusers' and log_type <> 'timedmediahandler' and log_type <> 'massmessage' and log_type<>'growthexperiments' and log_type<>'import' GROUP BY actor_name, log_type;";
        r = command.ExecuteReader();
        while (r.Read())
            if (r.GetString("log_type") == "review")
                statstable[r.GetString("user")]["review"] += r.GetInt32("count");
            else {
                statstable[r.GetString("user")]["totalactions"] += r.GetInt32("count");
                statstable[r.GetString("user")][r.GetString("log_type")] += r.GetInt32("count");
            }
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, page_namespace, COUNT(rev_page) count FROM revision_userindex INNER JOIN page ON rev_page = page_id INNER JOIN actor_revision ON rev_actor = actor_id INNER JOIN user_groups ON ug_user = actor_user WHERE ug_group IN " +
            "('sysop', 'closer') AND rev_timestamp BETWEEN " + sixmonths_earlier_ym + "01000000 AND " + now_ym + "01000000 GROUP BY actor_name, page_namespace;";
        r = command.ExecuteReader();
        while (r.Read()) {
            statstable[r.GetString("user")]["totaledits"] += r.GetInt32("count");
            switch (r.GetString("page_namespace")) {
                case "0":
                case "6":
                case "10":
                case "14":
                case "100":
                case "102":
                    statstable[r.GetString("user")]["contentedits"] += r.GetInt32("count"); break;
                case "8":
                    statstable[r.GetString("user")]["totalactions"] += r.GetInt32("count"); statstable[r.GetString("user")]["mediawiki"] += r.GetInt32("count"); break;
            }
        }
        r.Close();

        var lm = now.AddMonths(-1);
        var summaryrgx = new Regex(@"={1,}\s*Итог\s*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(" +
            monthname[lm.Month] + "|" + monthname[lm.AddMonths(-1).Month] + "|" + monthname[lm.AddMonths(-2).Month] + "|" + monthname[lm.AddMonths(-3).Month] + "|" + monthname[lm.AddMonths(-4).Month] +
            "|" + monthname[lm.AddMonths(-5).Month] + ") (" + lm.Year + "|" + lm.AddMonths(-5).Year + @") \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (var t in discussiontypes)
            using (var xr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allpages&apprefix=" + t + "/&apnamespace=4&aplimit=max").Result)))
                while (xr.Read())
                    if (xr.Name == "p") {
                        string page = xr.GetAttribute("title"); int year; try { year = i(page.Substring(page.Length - 4)); } catch { continue; }
                        if (year >= 2018) {
                            string pagetext;
                            try { pagetext = readpage(page); } catch { continue; }
                            var results = summaryrgx.Matches(pagetext);
                            foreach (Match m in results) {
                                string user = m.Groups[3].ToString().Replace('_', ' ');
                                if (!statstable.ContainsKey(user))
                                    continue;
                                statstable[user]["totalactions"]++;
                                if (t == "К удалению")
                                    statstable[user]["delsum"]++;
                                else
                                    statstable[user]["restoresum"]++;
                            }
                        }
                    }

        string cutext = readpage("u:BotDR/CU_stats");
        var custats = cutext.Split('\n');
        foreach (var s in custats)
            if (s.Contains('=')) {
                var data = s.Split('='); statstable[data[0]]["checkuser"] += i(data[1]); statstable[data[0]]["totalactions"] += i(data[1]);
            }

        string result = "<templatestyles src=\"Википедия:Администраторы/Активность/styles.css\"/>\n{{Самые активные участники}}{{списки администраторов}}{{shortcut|ВП:АДА}}<center>\nСтатистика активности " +
            "администраторов и подводящих итоги Русской Википедии за период с 1 " + monthname[sixmonths_earlier.Month] + " " + sixmonths_earlier.Year + " по 1 " + monthname[now.Month] + " " + now.Year +
            " года. Первично отсортирована по сумме числа правок и админдействий, нулевые значения не показаны. Включает только участников, имеющих флаг сейчас - после снятия флага строка участника пропадёт " +
            "из таблицы при следующем обновлении.\n\nДля подтверждения активности [[ВП:А#Неактивность администратора|администраторы]] должны сделать за полгода минимум 100 правок, из них 50 — в содержательных " +
            "пространствах имён, а также 25 админдействий, включая подведение итогов на специальных страницах. [[ВП:ПИ#Процедура снятия статуса|Подводящие итоги]] должны совершить 10 действий (итоги плюс удаления)" +
            ", из которых не менее двух — именно итоги.\n{|class=\"ts-википедия_администраторы_активность-table standard sortable\"\n!rowspan=2|Участник!!colspan=3|Правки!!colspan=13|Админдействия\n|-\n!{{abbr" +
            "|Σ∀|все правки|0}}!!{{abbr|Σ|контентные правки|0}}!!{{abbr|✔|патрулирование|0}}!!{{abbr|Σ|все действия|0}}!!{{abbr|<big>🗑</big> (📝)|удаление (итоги на КУ)|0}}!!{{abbr|<big>🗑⇧</big> (📝)|" +
            "восстановление (итоги на ВУС)|0}}!!{{abbr|<big>≡🗑</big>|удаление правок и записей журналов|0}}!!{{abbr|🔨|(раз)блокировки|0}}!!{{abbr|🔒|защита и её снятие|0}}!!{{abbr|1=<big>⚖</big>|2=(де)" +
            "стабилизация|3=0}}!!{{abbr|👮|изменение прав участников|0}}!!{{abbr|<big>⚙</big>|правка MediaWiki, изменение тегов и контентной модели страниц|0}}!!{{abbr|<big>🕸</big>|изменение фильтров " +
            "правок|0}}!!{{abbr|<big>🔍</big>|чекъюзерские проверки|0}}!!{{abbr|<big>⇨</big>👤|переименование участников|0}}";
        foreach (var u in statstable.OrderByDescending(t => t.Value["totalactions"] + t.Value["totaledits"])) {
            bool inactivecloser = u.Value["closer"] == 1 && (u.Value["delete"] + u.Value["delsum"] < 10 || u.Value["delsum"] < 2);
            bool lessactions = u.Value["closer"] == 0 && u.Value["totalactions"] < 25;
            bool lesscontent = u.Value["closer"] == 0 && u.Value["contentedits"] + u.Value["review"] < 50;
            bool lesstotal = u.Value["closer"] == 0 && u.Value["totaledits"] + u.Value["review"] < 100;
            string color = "";
            if (!bots.Contains(u.Key)) {
                if (inactivecloser || lessactions || lesscontent || lesstotal)
                    color = "style=\"background-color:#fcc\"";
            }
            else
                color = "style=\"background-color:#ccf\"";
            string deletetext = u.Value["delete"] + u.Value["delsum"] == 0 ? "" : inactivecloser ? "'''" + u.Value["delete"] + " (" + u.Value["delsum"] + ")'''" : u.Value["delete"] + " (" + u.Value["delsum"] + ")";
            string restoretext = u.Value["restore"] + u.Value["restoresum"] == 0 ? "" : u.Value["restore"] + " (" + u.Value["restoresum"] + ")"; //пробелы после ''' нужны чтоб не было висящих '
            result += "\n|-" + color + "\n|{{u|" + u.Key + "}} ([[special:contribs/" + u.Key + "|вклад]] | [[special:log/" + u.Key + "|журн]])||" + (lesstotal ? "''' " + cell(u.Value["totaledits"]) +
                "'''" : cell(u.Value["totaledits"])) + "||" + (lesscontent ? "''' " + cell(u.Value["contentedits"]) + "'''" : cell(u.Value["contentedits"])) + "||" + cell(u.Value["review"]) + "||" +
                (lessactions ? "''' " + cell(u.Value["totalactions"]) + "'''" : cell(u.Value["totalactions"])) + "||" + deletetext + "||" + restoretext + "||" + cell(u.Value["del_rev_log"]) + "||" +
                cell(u.Value["block"] + u.Value["gblblock"]) + "||" + cell(u.Value["protect"]) + "||" + cell(u.Value["stable"]) + "||" + cell(u.Value["rights"]) + "||" + cell(u.Value["managetags"] +
                u.Value["contentmodel"] + u.Value["mediawiki"] + u.Value["tag"]) + "||" + cell(u.Value["abusefilter"]) + "||" + cell(u.Value["checkuser"]) + "||" + cell(u.Value["renameuser"]);
        }
        rsave("ВП:Администраторы/Активность", result + "\n|}");
    }
    static bool sameuser(string s1, string s2)
    {
        if (s1.Contains(":"))
            s1 = s1.Substring(s1.IndexOf(':'));
        if (s2.Contains(":"))
            s2 = s2.Substring(s2.IndexOf(':'));
        if (s1.Contains("/"))
            s1 = s1.Substring(0, s1.IndexOf('/'));
        if (s2.Contains("/"))
            s2 = s2.Substring(0, s2.IndexOf('/'));
        if (s1 == s2) return true;
        return false;
    }
    static void incorrect_redirects()
    {
        var redirs = new Dictionary<string, redir>();
        var nss = new Dictionary<string, string>();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces").Result)))
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ns" && !r.GetAttribute("id").StartsWith("-")) { string id = r.GetAttribute("id"); r.Read();nss.Add(id, r.Value); }
        foreach (var current_target_ns in nss)
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allredirects&format=xml&arprop=ids|title&arnamespace=" + current_target_ns.Key + "&arlimit=500";//NOT 5000
            while (cont != null) {
                var temp = new Dictionary<string, redir>();
                string idset = "";
                using (var rdr = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arcontinue=" + e(cont)).Result))) {
                    rdr.Read(); rdr.Read(); rdr.Read(); cont = rdr.GetAttribute("arcontinue");
                    while (rdr.Read())
                        if (rdr.Name == "r") {
                            idset += '|' + rdr.GetAttribute("fromid"); temp.Add(rdr.GetAttribute("fromid"), new redir() { dest_title = rdr.GetAttribute("title"), dest_ns = i(rdr.GetAttribute("ns")) });
                        }
                } if (idset.Length != 0)
                    idset = idset.Substring(1);

                using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=info&format=xml&pageids=" + idset).Result)))
                    while (rdr.Read())
                        if (rdr.Name == "page") {
                            var id = rdr.GetAttribute("pageid");
                            int src_ns = i(rdr.GetAttribute("ns"));
                            if (temp[id].dest_ns != src_ns || temp[id].dest_ns == 6 || temp[id].dest_ns == 14)
                                if (!(sameuser(rdr.GetAttribute("title"), temp[id].dest_title) && ((temp[id].dest_ns == 3 && src_ns == 2) || (temp[id].dest_ns == 2 && src_ns == 3))))
                                    redirs.Add(id, new redir() { src_ns = src_ns, src_title = rdr.GetAttribute("title"), dest_ns = temp[id].dest_ns, dest_title = temp[id].dest_title });
                        }
            }
        }
        var result = "<center>\n{| class=\"standard sortable\"\n|-\n!Откуда!!Куда";
        foreach (var r in redirs) { result += "\n|-\n|[[:" + r.Value.src_title + "]]||[[:" + r.Value.dest_title + "]]"; }//var w = new StreamWriter("incorr.redir.txt"); w.Write(result + "\n|}"); w.Close();
        rsave("u:MBH/incorrect redirects", result + "\n|}");
    }
    static void apat_for_filemovers()
    {
        var badusers = new List<string>() { "Шухрат Саъдиев" };
        var globalusers = new HashSet<string>();
        var globalusers_needs_flag = new HashSet<string>();
        var apats = new HashSet<string>();
        var connect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki"));
        connect.Open();
        var command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"editor\" or ug_group = \"autoreview\";", connect) { CommandTimeout = 99999 };
        var r = command.ExecuteReader();
        while (r.Read())
            apats.Add(r.GetString(0));
        r.Close();

        connect = new MySqlConnection(creds[2].Replace("%project%", "commonswiki"));
        connect.Open();
        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"filemover\";", connect) { CommandTimeout = 99999 };
        r = command.ExecuteReader();
        while (r.Read())
            globalusers.Add(r.GetString(0));
        r.Close();

        using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://meta.wikimedia.org/w/api.php?action=query&format=xml&list=globalallusers&agugroup=global-rollbacker&agulimit=max").Result)))
            while (rdr.Read())
                if (rdr.Name == "globaluser")
                    if (!globalusers.Contains(rdr.GetAttribute("name")))
                        globalusers.Add(rdr.GetAttribute("name"));

        globalusers.ExceptWith(apats);

        var lastmonth = now.AddMonths(-1);
        foreach (var mover in globalusers)
            using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=max&ucend=" + lastmonth.ToString
                ("yyyy-MM-dd") + "T00:00:00&ucprop=comment&ucuser=" + e(mover)).Result)))
                while (rdr.Read())
                    if (rdr.Name == "item" && rdr.GetAttribute("comment") != null)
                        if (rdr.GetAttribute("comment").Contains("GR]") && !badusers.Contains(mover))
                        {
                            globalusers_needs_flag.Add(mover);
                            break;
                        }

        if (globalusers_needs_flag.Count > 0)
        {
            string zkatext = readpage("ВП:Запросы к администраторам");
            var header = new Regex(@"(^\{[^\n]*\}\s*<[^>]*>\n)");
            string newmessage = "==Выдать апата глобальным правщикам==\nПеречисленные ниже участники занимаются переименованием файлов на Викискладе с заменой включений во всех разделах. В соответствии с [[ВП:ПАТ#ГЛОБ]] прошу рассмотреть их вклад и выдать им апата, чтобы такие правки не распатрулировали страницы.";
            foreach (var mover in globalusers_needs_flag)
                newmessage += "\n* [[special:contribs/" + mover + "|" + mover + "]]";
            newmessage += "\n~~~~\n\n";
            if (header.IsMatch(zkatext))
                rsave("ВП:Запросы к администраторам", header.Replace(zkatext, "$1" + "\n\n" + newmessage));
            else
                rsave("ВП:Запросы к администраторам", newmessage + zkatext);
        }
    }
    static void writerow_ss(KeyValuePair<string, Dictionary<string, int>> s, string type)
    {
        string newrow = "\n|-\n|" + ++position_number + "||{{u|" + s.Key + "}}||" + cell(s.Value["sum"]) + "||" + cell(s.Value["К удалению"]) + "||" + cell(s.Value["К восстановлению"]) + "||" + cell(
            s.Value["К переименованию"]) + "||" + cell(s.Value["Запросы на переименование учётных записей"]) + "||" + cell(s.Value["К объединению"] + s.Value["К разделению"]) + "||" + cell(s.Value[
                "Обсуждение категорий"]) + "||" + cell(s.Value["К улучшению"]) + "||" + cell(s.Value["Запросы к администраторам"]) + "||" + cell(s.Value["Оспаривание итогов"] + s.Value["Оспаривание " +
                "административных действий"]) + "||" + cell(s.Value["Установка защиты"]) + "||" + cell(s.Value["Снятие защиты"]) + "||" + cell(s.Value["Заявки на статус автопатрулируемого"] + s.Value[
                    "Заявки на статус патрулирующего"]) + "||" + cell(s.Value["Заявки на статус подводящего итоги"]) + "||" + cell(s.Value["Заявки на снятие флагов"]) + "||" + cell(s.Value["К оценке " +
                    "источников"]) + "||" + cell(s.Value["Изменение спам-листа"]) + "||" + cell(s.Value["Запросы к патрулирующим от автоподтверждённых участников"] + s.Value["Запросы к патрулирующим"]) +
                    "||" + cell(s.Value["Избранные статьи/Кандидаты"]) + "||" + cell(s.Value["Избранные статьи/Кандидаты в устаревшие"]) + "||" + cell(s.Value["Хорошие статьи/Кандидаты"]) + "||" + cell(
                        s.Value["Хорошие статьи/К лишению статуса"]) + "||" + cell(s.Value["Добротные статьи/Кандидаты"]) + "||" + cell(s.Value["Добротные статьи/К лишению статуса"]) + "||" + cell(s.Value
                        ["Избранные списки и порталы/Кандидаты"]) + "||" + cell(s.Value["Избранные списки и порталы/К лишению статуса"]) + "||" + cell(s.Value["Запросы к ботоводам"]) + "||" + cell(s.Value
                        ["Форум/Архив/Технический"] + s.Value["Технические запросы"]) + "||" + cell(s.Value["Форум/Архив/Авторское право"]);
        if (type == "month")
            resulttext_per_month += newrow;
        else if (type == "year")
            resulttext_per_year += newrow;
        else
            resulttext_alltime += newrow;
    }
    static void initialize_ss(string type, string pagetype)
    {
        if (!stats[type].ContainsKey(user))
            stats[type].Add(user, new Dictionary<string, int>() { { "К удалению", 0 },{ "К улучшению", 0 },{ "К разделению", 0 },{ "К объединению", 0 },{ "К переименованию", 0 },{ "К восстановлению", 0 },
                { "Обсуждение категорий", 0 }, { "Снятие защиты", 0 }, { "Установка защиты", 0 }, { "sum", 0 }, { "Изменение спам-листа", 0 }, { "Запросы к администраторам", 0 },{ "К оценке источников", 0 },
                { "Заявки на статус патрулирующего", 0 },{ "Заявки на статус автопатрулируемого", 0 },{ "Заявки на статус подводящего итоги", 0 },{ "Оспаривание итогов", 0 },{ "Заявки на снятие флагов", 0 },
                { "Оспаривание административных действий",0}, { "Хорошие статьи/Кандидаты",0 },{ "Добротные статьи/Кандидаты",0},{ "Избранные списки и порталы/Кандидаты",0},{ "Форум/Архив/Технический",0},
                { "Избранные статьи/Кандидаты", 0 }, { "Запросы к ботоводам", 0 }, { "Запросы к патрулирующим", 0 },{ "Технические запросы",0},{ "Запросы к патрулирующим от автоподтверждённых участников",0},
                { "Избранные статьи/Кандидаты в устаревшие", 0 },{ "Добротные статьи/К лишению статуса", 0 }, { "Хорошие статьи/К лишению статуса", 0 },{ "Избранные списки и порталы/К лишению статуса", 0 },
                { "Запросы на переименование учётных записей", 0},{ "Форум/Архив/Авторское право", 0 } });
        stats[type][user]["sum"]++; stats[type][user][pagetype]++;
    }
    static void summary_stats()
    {
        var lastmonthdate = now.AddMonths(-1);
        var lastyear = now.AddYears(-1);
        var first_not_fully_summaried_year = new Dictionary<string, int>
        {
            { "К удалению", 2018 },{ "К улучшению", 2018 },{ "К разделению", 2018 },{ "К объединению", 2015 },{ "К переименованию", 2015 },{ "К восстановлению", 2018 },{ "Обсуждение категорий", 2017 },
            { "Снятие защиты", 0 },{ "Установка защиты", 0 },{ "Оспаривание итогов", 0 },{ "Оспаривание административных действий", 0 },{ "Форум/Архив/Технический", 0 },{ "Технические запросы", 0 },
            { "К оценке источников", 0 },{ "Изменение спам-листа", 0 },{ "Запросы к патрулирующим", 0 },{ "Запросы к патрулирующим от автоподтверждённых участников", 0 },{ "Запросы к ботоводам", 0 },
            { "Заявки на снятие флагов", 0 },{ "Запросы к администраторам", 0 },{ "Хорошие статьи/Кандидаты", 0 },{ "Избранные статьи/Кандидаты", 0 },
            { "Добротные статьи/Кандидаты", 0 },{ "Избранные списки и порталы/Кандидаты", 0 },{ "Заявки на статус патрулирующего", 0 },{ "Заявки на статус подводящего итоги", 0 }, { "Заявки на статус" +
            " автопатрулируемого", 0 },{ "Избранные статьи/Кандидаты в устаревшие", 0 },{ "Хорошие статьи/К лишению статуса", 0 },{ "Добротные статьи/К лишению статуса", 0 }, { "Избранные списки и " +
            "порталы/К лишению статуса", 0 }, {"Запросы на переименование учётных записей", 0},{ "Форум/Архив/Авторское право", 0 }
        };
        var monthnumbers = new Dictionary<string, int>{{ "января", 1 },{ "февраля", 2 },{ "марта", 3 },{ "апреля", 4 },{ "мая", 5 },{ "июня", 6 },{ "июля", 7 },{ "августа", 8 },
            { "сентября", 9 },{ "октября", 10 },{ "ноября", 11 },{ "декабря", 12 }};//НЕ ПЕРЕНОСИТЬ СТРОКУ НИЖЕ, ОНА ЛОМАЕТСЯ
        var summary_rgx = new Regex(@"={1,}\s*(Итог)[^=\n]*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря) (\d{4}) \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var rdb_zkp_summary_rgx = new Regex(@"(done|сделано|отпатрулировано|отклонено)\s*\}\}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря) (\d{4}) \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var yearrgx = new Regex(@"\d{4}");
        foreach (var pagetype in first_not_fully_summaried_year.Keys)
        {
            int ns;
            if (pagetype.Contains("статьи") || pagetype.Contains("списки"))
                ns = 104;
            else ns = 4;
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&list=allpages&apprefix=" + pagetype + "&apnamespace=" + ns + "&aplimit=max";
            while (cont != "-")
            {
                Root response = JsonConvert.DeserializeObject<Root>(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + e(cont)).Result);
                cont = response.@continue == null ? "-" : response.@continue.apcontinue;
                foreach (var pageinfo in response.query.allpages)
                {
                    string pagetitle = pageinfo.title;
                    bool correctpage = false;
                    int startyear = now.Month == 1 ? 2000 : (first_not_fully_summaried_year[pagetype] == 0 ? lastyear.Year : first_not_fully_summaried_year[pagetype]);
                    if (pagetitle.Contains("Избранные"))
                        correctpage = true;
                    else if (yearrgx.IsMatch(pagetitle))
                        if (i(yearrgx.Match(pagetitle).Value) >= startyear)
                            correctpage = true;
                        else if (pagetitle.IndexOf('/') == -1)
                            correctpage = true;
                    if (correctpage)
                    {
                        string pagetext = readpage(pagetitle);
                        var summaries = (pagetype == "Запросы к ботоводам" || pagetype == "Запросы к патрулирующим от автоподтверждённых участников" || pagetype == "Запросы к патрулирующим") ?
                            rdb_zkp_summary_rgx.Matches(pagetext) : summary_rgx.Matches(pagetext);
                        foreach (Match summary in summaries)
                        {
                            int signature_year = i(summary.Groups[7].Value); int signature_month = monthnumbers[summary.Groups[6].Value];
                            user = summary.Groups[4].ToString().Replace('_', ' ');
                            if (user.Contains("/"))
                                user = user.Substring(0, user.IndexOf("/"));
                            if (user == "TextworkerBot")
                                continue;
                            initialize_ss("alltime", pagetype);
                            if (signature_year == lastmonthdate.Year && signature_month == lastmonthdate.Month)
                                initialize_ss("month", pagetype);
                            if (signature_year == lastmonthdate.Year || (signature_year == lastmonthdate.Year - 1 && signature_month > lastmonthdate.Month))
                                initialize_ss("year", pagetype);
                        }
                    }
                }
            }
        }
        if (now.Month == 1)
        {
            resulttext_alltime = common_resulttext.Replace("%type%", "за все годы существования Русской Википедии").Replace("%otherpage%", "итоги за [[ВП:Статистика итогов|последний месяц]] и [[ВП:Статистика итогов/За год|год]]");
            foreach (var s in stats["alltime"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
                writerow_ss(s, "alltime");
            rsave("ВП:Статистика итогов/За всё время", resulttext_alltime + "\n|}");
        }
        else
        {
            resulttext_per_month = common_resulttext.Replace("%type%", "в " + prepositional[lastmonthdate.Month] + " " + lastmonthdate.Year + " года");
            resulttext_per_year = common_resulttext.Replace("%type%", "за последние 12 месяцев");
            foreach (var s in stats["year"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
                writerow_ss(s, "year");
            position_number = 0;
            foreach (var s in stats["month"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
                writerow_ss(s, "month");
            rsave("ВП:Статистика итогов/За год", resulttext_per_year + "\n|}");
            rsave("ВП:Статистика итогов", resulttext_per_month + "\n|}");
        }
    }
    static void popular_wd_items_without_ru()
    {
        int numofitemstoanalyze = 150000; //100k is okay, 1m isn't
        var allitems = new Dictionary<string, int>(); var nonruitems = new Dictionary<string, int>(); string result = "<center>\n{|class=\"standard\"\n!Страница!!Кол-во интервик";
        var connect = new MySqlConnection(creds[2].Replace("%project%", "wikidatawiki")); connect.Open();
        var query = new MySqlCommand("select ips_item_id, count(*) cnt from wb_items_per_site group by ips_item_id order by cnt desc limit " + numofitemstoanalyze + ";", connect); query.CommandTimeout = 99999;
        MySqlDataReader r = query.ExecuteReader();
        while (r.Read())
            allitems.Add(r.GetString("ips_item_id"), r.GetInt16("cnt"));
        r.Close();
        foreach (var i in allitems) {
            query = new MySqlCommand("select ips_site_page from wb_items_per_site where ips_site_id=\"ruwiki\" and ips_item_id=" + i.Key + ";", connect);
            r = query.ExecuteReader();
            if (!r.Read())
                nonruitems.Add(i.Key, i.Value);
            r.Close();
        }
        foreach (var n in nonruitems) {
            query = new MySqlCommand("select cast(ips_site_page as char) title from wb_items_per_site where ips_site_id=\"enwiki\" and ips_item_id=" + n.Key + ";", connect);
            r = query.ExecuteReader();
            if (r.Read()) {
                string title = r.GetString(0);
                if (!title.StartsWith("Template:") && !title.StartsWith("Category:") && !title.StartsWith("Module:") && !title.StartsWith("Wikipedia:") && !title.StartsWith("Help:") && !title.StartsWith("Portal:"))
                    result += "\n|-\n|[[:en:" + title + "]]||" + n.Value;
            }
            r.Close();
        }
        rsave("ВП:К созданию/Статьи с наибольшим числом интервик без русской", result + "\n|}{{Проект:Словники/Шаблон:Списки недостающих статей}}[[Категория:Википедия:Статьи без русских интервик]]");
    }
    static void most_active_users()
    {
        var falsebots = new Dictionary<string, string[]>() { { "ru", new string[] { "Alex Smotrov", "Wind", "Tutaishy" } }, { "be", new string[] { "Maksim L.", "Artsiom91" } }, { "kk", new string[] { "Arystanbek", "Нұрлан Рахымжанов" } } };
        var min_num_of_edits = new Dictionary<string, int>() { { "ru", 10000 }, { "be", 5000 }, { "kk", 500 } };

        var headers = new Dictionary<string, string>() { { "ru", "{{Плавающая шапка таблицы}}{{Самые активные участники}}%shortcut%<center>\nВ каждой колонке приведена сумма правок в указанном пространстве и его обсуждении. Первично отсортировано и пронумеровано по общему числу правок.%specific_text%\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!{{abbr|№ п/с|место по числу правок в статьях|0}}!!Участник!!Всего правок!!В статьях!!шаблонах!!файлах!!категориях!!порталах и проектах!!модулях и MediaWiki!!страницах участников!!метапедических страницах" },
            { "be", "{{Самыя актыўныя ўдзельнікі}}%shortcut%<center>У кожным слупку прыведзена сума правак у адпаведнай прасторы і размовах пра яе. Першасна адсартавана і пранумаравана паводле агульнай колькасці правак.%specific_text%\n{|class=\"standard sortable\"\n!№!!{{abbr|№ п/с|месца па колькасці правак у артыкулах|0}}!!Удзельнік!!Агулам правак!!У артыкулах!!шаблонах!!файлах!!катэгорыях!!парталах і праектах!!модулях і MediaWiki!!старонках удзельнікаў!!метапедычных старонках" },
            { "kk", "%shortcut%<center>Әрбір бағанда көрсетілген кеңістіктегі және оның талқылауындағы өңдеулер саны берілген. Ең алдымен жалпы түзетулер бойынша сұрыпталған және нөмірленген.%specific_text%\n{{StatInfo}}\n{|class=\"standard sortable ts-stickytableheader\"\n!#!!{{abbr|#м/о|мақалалардағы өңдеме саны бойынша орны|0}}!!Қатысушы!!Барлық өңдемесі!!Мақалалар!!Үлгілер!!Файлдар!!Санаттар!!Порталдар + жобалар!!Модулдар + MediaWiki!!Қатысушы беттері!!Метапедиялық (Уикипедия)" } };

        var resultpages = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "ВП:Самые активные боты", Second = "ВП:Участники по числу правок" } },
            { "be", new Pair() { First = "Вікіпедыя:Боты паводле колькасці правак", Second = "Вікіпедыя:Удзельнікі паводле колькасці правак" } },
            { "kk", new Pair() { First = "Уикипедия:Өңдеме саны бойынша боттар", Second = "Уикипедия:Өңдеме саны бойынша қатысушылар" } } };

        var footers = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "[[К:Википедия:Боты]]", Second = "" } },
            { "be", new Pair() { First = "[[Катэгорыя:Вікіпедыя:Боты]][[Катэгорыя:Вікіпедыя:Статыстыка і прагнозы]]", Second = "[[Катэгорыя:Вікіпедыя:Статыстыка і прагнозы]]" } },
            { "kk", new Pair() { First = "{{Wikistats}}[[Санат:Уикипедия:Боттар]]", Second = "{{Wikistats}}[[Санат:Уикипедия:Қатысушылар]]" } } };

        var shortcuts = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "ВП:САБ", Second = "ВП:САУ" } }, { "be", new Pair() { First = "ВП:САБ", Second = "ВП:САУ" } }, { "kk", new Pair() { First = "УП:ӨСБ", Second = "УП:ӨСҚ" } } };

        foreach (var lang in new string[] { "ru", "be", "kk" })
        {
            var hdr_modifications = new Dictionary<string, Pair>() { { "ru", new Pair() { First = " Голубым выделены глобальные боты без локального флага.", Second = " В список включены участники, имеющие не менее " + min_num_of_edits[lang] + " правок, включая удалённые правки (из-за них число живых правок в таблице может быть меньше)." } },
            { "be", new Pair() { First = " Блакітным вылучаныя глабальныя боты без лакальнага сцяга.", Second = " У спіс уключаны ўдзельнікі, якія маюць не менш за " + min_num_of_edits[lang] + " правак." } },
            { "kk", new Pair() { First = " Жергілікті жалаусыз ғаламдық боттар көкпен ерекшеленген.", Second = " Тізімге " + min_num_of_edits[lang] + " өңдемеден кем емес өңдеме жасаған қатысушылар кірістірілген." } } };
            var users = new Dictionary<string, most_edits_record>();
            var bots = new Dictionary<string, most_edits_record>();
            var connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            var command = new MySqlCommand("select distinct cast(log_title as char) bot from logging where log_type=\"rights\" and log_params like \"%bot%\";", connect) { CommandTimeout = 9999 };
            var reader = command.ExecuteReader();
            while (reader.Read()) {
                string bot = reader.GetString("bot").Replace("_", " ");
                if (!falsebots[lang].Contains(bot))
                    bots.Add(bot, new most_edits_record() { globalbot = false });
            }
            reader.Close();

            command.CommandText = "select cast(user_name as char) user from user where user_editcount >= " + min_num_of_edits[lang] + ";"; reader = command.ExecuteReader();
            while (reader.Read()) {
                string user = reader.GetString("user");
                if (!bots.ContainsKey(user))
                    users.Add(user, new most_edits_record());
            }
            reader.Close(); connect.Close();

            connect = new MySqlConnection(creds[2].Replace("%project%", "metawiki")); connect.Open();
            command = new MySqlCommand("select distinct cast(log_title as char) bot from logging where log_type='gblrights' and (log_params like '%lobal-bot%' or log_params like '%lobal_bot%');", connect) { CommandTimeout = 9999 };
            reader = command.ExecuteReader();
            while (reader.Read()) {
                string bot = reader.GetString("bot").Replace("_", " ");
                if (!bots.ContainsKey(bot)) {
                    bots.Add(bot, new most_edits_record() { globalbot = true });
                    users.Remove(bot);
                }
            }
            reader.Close(); connect.Close();
            foreach (var type in new Dictionary<string, most_edits_record>[] { users, bots })
                foreach (var k in type.Keys) {
                    string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=max&ucprop=title&ucuser=" + e(k);
                    while (cont != null) {
                        string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&uccontinue=" + e(cont)).Result);
                        using (var r = new XmlTextReader(new StringReader(apiout))) {
                            r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("uccontinue");
                            while (r.Read())
                                if (r.Name == "item") {
                                    int ns = i(r.GetAttribute("ns"));
                                    type[k].all++;
                                    if (ns == 0 || ns == 1)
                                        type[k].main++;
                                    else if (ns == 2 || ns == 3)
                                        type[k].user++;
                                    else if (ns == 4 || ns == 5 || ns == 12 || ns == 13 || ns == 106 || ns == 107)
                                        type[k].meta++;
                                    else if (ns == 100 || ns == 101 || ns == 104 || ns == 105)
                                        type[k].portproj++;
                                    else if (ns == 10 || ns == 11)
                                        type[k].templ++;
                                    else if (ns == 6 || ns == 7)
                                        type[k].file++;
                                    else if (ns == 8 || ns == 9 || ns == 828 || ns == 829)
                                        type[k].tech++;
                                    else if (ns == 14 || ns == 15)
                                        type[k].cat++;
                                }
                        }
                    }
                }

            string result = headers[lang].Replace("%specific_text%", hdr_modifications[lang].First.ToString()).Replace("%shortcut%", "{{shortcut|" + shortcuts[lang].First + "}}");

            int main_edits_index = 0;
            foreach (var bot in bots.OrderByDescending(bot => bot.Value.main)) {
                if (bot.Value.all == 0)
                    bots.Remove(bot.Key);
                else bot.Value.main_edits_index = ++main_edits_index;
            }
            main_edits_index = 0;
            foreach (var user in users.OrderByDescending(user => user.Value.main))
                user.Value.main_edits_index = ++main_edits_index;

            int all_edits_index = 0;
            foreach (var s in bots.OrderByDescending(s => s.Value.all)) {
                string color = "";
                if (s.Value.globalbot)
                    color = "style=\"background-color:#ccf\"";
                result += "\n|-" + color + "\n|" + ++all_edits_index + "||" + s.Value.main_edits_index + "||{{u|" + s.Key + "}}||" + s.Value.all + "||" + s.Value.main + "||" + s.Value.templ + "||" + 
                    s.Value.file + "||" + s.Value.cat + "||" + s.Value.portproj + "||" + s.Value.tech + "||" + s.Value.user + "||" + s.Value.meta;
            }
            result += "\n|}" + footers[lang].First;
            save(lang, resultpages[lang].First.ToString(), result, "");

            all_edits_index = 0;
            result = headers[lang].Replace("%specific_text%", hdr_modifications[lang].Second.ToString()).Replace("%shortcut%", "{{shortcut|" + shortcuts[lang].Second + "}}");
            foreach (var s in users.OrderByDescending(s => s.Value.all))
                result += "\n|-\n|" + ++all_edits_index + "||" + s.Value.main_edits_index + "||{{u|" + s.Key + "}}||" + s.Value.all + "||" + s.Value.main + "||" + s.Value.templ + "||" + s.Value.file + "||" + 
                    s.Value.cat + "||" + s.Value.portproj + "||" + s.Value.tech + "||" + s.Value.user + "||" + s.Value.meta;
            result += "\n|}" + footers[lang].Second;
            save(lang, resultpages[lang].Second.ToString(), result, "");
        }
    }
    static void most_watched_pages()
    {
        int limit = 30; var nss = new Dictionary<int, string>();
        string cont, query, apiout, result = "<center>Отсортировано сперва по числу активных следящих, когда их меньше " + limit + " - по числу следящих в целом.\n";

        apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces").Result;
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ns") {
                    int ns = i(r.GetAttribute("id")); if (ns % 2 == 0 || ns == 3) { r.Read(); nss.Add(ns, r.Value); }
                }
        }
        nss.Remove(2); nss.Remove(-2);

        foreach (var n in nss.Keys)
        {
            var pageids = new HashSet<string>(); var pagecountswithactive = new Dictionary<string, Pair>(); var pagecountswoactive = new Dictionary<string, int>();
            cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&list=allpages&format=xml&aplimit=max&apfilterredir=nonredirects&apnamespace=";
            while (cont != null)
            {
                apiout = (cont == "" ? site.GetStringAsync(query + n).Result : site.GetStringAsync(query + n + "&apcontinue=" + e(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout))) {
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                            pageids.Add(r.GetAttribute("pageid"));
                }
            }

            var requeststrings = new HashSet<string>();
            string idset = ""; int c = 0;
            foreach (var p in pageids) {
                idset += "|" + p;
                if (++c % 500 == 0) { requeststrings.Add(idset.Substring(1)); idset = ""; }
            }
            if (idset.Length != 0)
                requeststrings.Add(idset.Substring(1));

            foreach (var q in requeststrings)
                using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=info&format=xml&inprop=visitingwatchers%7Cwatchers&pageids=" + q).Result)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    while (r.Read())
                        if (r.GetAttribute("watchers") != null) {
                            string title = r.GetAttribute("title");
                            if (n == 3) {
                                if (title.Contains("/Архив"))
                                    continue;
                                title = title.Replace("Обсуждение участника:", "Участник:").Replace("Обсуждение участницы:", "Участница:");
                            }
                            int watchers = i(r.GetAttribute("watchers"));
                            if (n == 0 && watchers >= 60 || n != 0) {
                                if (r.GetAttribute("visitingwatchers") != null)
                                    pagecountswithactive.Add(title, new Pair() { First = watchers, Second = r.GetAttribute("visitingwatchers") });
                                else
                                    pagecountswoactive.Add(title, watchers);
                            }
                        }
                }

            if (pagecountswoactive.Count != 0) {
                result += "==" + (nss[n] == "" ? "Статьи" : (nss[n] == "Обсуждение участника" ? "Участник" : nss[n])) + "==\n{|class=\"standard sortable\"\n!Страница!!Всего следящих!!Активных\n";
                foreach (var p in pagecountswithactive.OrderByDescending(p => i(p.Value.Second)))
                    result += "|-\n|[[:" + p.Key + "]]||" + p.Value.First + "||" + p.Value.Second + "\n";
                foreach (var p in pagecountswoactive.OrderByDescending(p => p.Value))
                    result += "|-\n|[[:" + p.Key + "]]||" + p.Value + "||<" + limit + "\n";
                result += "|}\n";
            }
        }
        rsave("u:MBH/most watched pages", result);
    }
    static void popular_userscripts()
    {
        var result = "[[К:Википедия:Статистика и прогнозы]]{{shortcut|ВП:СИС}}<center>Статистика собирается по незакомментированным включениям importScript/.load/.using/.getscript на скриптовых страницах " +
            "участников рувики, а также их global.js-файлах на Мете. Отсортировано по числу активных участников - сделавших хоть одно действие за последний месяц. Показаны лишь скрипты, имеющие более " +
            "одного включения. Статистику использования гаджетов см. [[Special:GadgetUsage|тут]]. Подробная разбивка скриптов по страницам - [[/details|тут]]. Обновлено " + now.ToString("dd.MM.yyyy") +
            ". \n{|class=\"standard sortable\"\n!Скрипт!!Активных!!Неактивных!!Всего";
        foreach (string skin in new string[] { "common", "monobook", "vector", "cologneblue", "minerva", "timeless", "simple", "myskin", "modern" })
        {
            string offset = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=search&srsearch=" + skin + ".js&srnamespace=2&srlimit=max&srprop=";
            while (offset != null) {
                string apiout = (offset == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&sroffset=" + e(offset)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout))) {
                    r.Read(); r.Read(); r.Read(); offset = r.GetAttribute("sroffset");
                    while (r.Read())
                        if (r.Name == "p" && r.GetAttribute("title").EndsWith(skin + ".js") && !invoking_pages.Contains(r.GetAttribute("title")))
                            invoking_pages.Add(r.GetAttribute("title"));
                }
            }
        }

        foreach (var invoking_page in invoking_pages) {
            username = invoking_page.Substring(invoking_page.IndexOf(':') + 1, invoking_page.IndexOf('/') - 1 - invoking_page.IndexOf(':')); Program.invoking_page = invoking_page;
            process_site("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&rvprop=content&rvlimit=1&titles=" + e(invoking_page));
            if (!script_users.Contains(username))
                script_users.Add(username);
        }

        foreach (var username in script_users) {
            Program.username = username; invoking_page = "meta:" + username + "/global.js";
            process_site("https://meta.wikimedia.org/w/api.php?action=query&format=xml&prop=revisions&rvprop=content&rvlimit=1&titles=user:" + e(username) + "/global.js");
        }

        foreach (var s in scripts.OrderByDescending(s => s.Value.active))
            if ((s.Value.active + s.Value.inactive) > 1)
                result += "\n|-\n|[[:" + s.Key + "]]||" + s.Value.active + "||" + s.Value.inactive + "||" + (s.Value.active + s.Value.inactive);
        rsave("ВП:Самые используемые скрипты", result + "\n|}"); rsave("ВП:Самые используемые скрипты/details", debug_result + "\n|}");
    }
    static bool user_is_active()
    {
        if (users_activity.ContainsKey(username))
            return users_activity[username];
        else
        {
            DateTime edit_ts = new DateTime(), log_ts = new DateTime();
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucprop=timestamp&ucuser=" + e(username)).Result)))
                while (r.Read())
                    if (r.Name == "item") {
                        string raw_ts = r.GetAttribute("timestamp"); edit_ts = new DateTime(i(raw_ts.Substring(0, 4)), i(raw_ts.Substring(5, 2)), i(raw_ts.Substring(8, 2)));
                    }
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=timestamp&lelimit=1&leuser=" + e(username)).Result)))
                while (r.Read())
                    if (r.Name == "item") {
                        string raw_ts = r.GetAttribute("timestamp"); log_ts = new DateTime(i(raw_ts.Substring(0, 4)), i(raw_ts.Substring(5, 2)), i(raw_ts.Substring(8, 2)));
                    }

            if (edit_ts < now.AddMonths(-1) && log_ts < now.AddMonths(-1))
            { users_activity.Add(username, false); return false; }
            else { users_activity.Add(username, true); return true; }

        }
    }
    static void add_script(string scriptname)
    {
        if (scriptname.StartsWith(":"))
            scriptname = scriptname.Substring(1);
        if (scriptname.StartsWith("ru:"))
            scriptname = scriptname.Substring(3);
        if (scriptname.IndexOf(":") > -1)
            scriptname = scriptname.Substring(0, scriptname.IndexOf(":")).ToLower() + scriptname.Substring(scriptname.IndexOf(":"));
        scriptname = e(scriptname).Replace("_", " ").Replace("у:", "user:").Replace("участник:", "user:").Replace("участница:", "user:").Replace("вп:", "project:")
            .Replace("википедия:", "project:").Replace("вікіпедія:", "project:").Replace("користувач:", "user:").Replace("користувачка:", "user:");
        if (scriptname.StartsWith("u:"))
            scriptname = "user:" + scriptname.Substring(2);
        //if (g_invoking_page.EndsWith("/global.js") && scriptname.ToLower().StartsWith("mediawiki:"))
        //    scriptname = "meta:" + scriptname;
        debug_result += "\n|-\n|[[:" + invoking_page + "]]||[[:" + scriptname + "]]";
        if (user_is_active() && scripts.ContainsKey(scriptname))
            scripts[scriptname].active++;
        else if (user_is_active() && !scripts.ContainsKey(scriptname))
            scripts.Add(scriptname, new script_usages() { active = 1, inactive = 0 });
        else if (!user_is_active() && scripts.ContainsKey(scriptname))
            scripts[scriptname].inactive++;
        else
            scripts.Add(scriptname, new script_usages() { active = 0, inactive = 1 });
    }
    static void process_site(string url)
    {
        string content = "";
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync(url).Result)))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("_idx") != "-1") { r.Read(); r.Read(); r.Read(); content = r.Value; break; }
        content = e(multiline_comment.Replace(content, "")).Replace("(\n", "(").Replace("{\n", "{");
        foreach (var s in content.Split('\n'))
            if (!s.TrimStart(' ').StartsWith("//")) {
                //if (r1.IsMatch(s) && !(is_ext_rgx.IsMatch(s) || is_foreign_rgx.IsMatch(s) || is_rgx.IsMatch(s) || is2_rgx.IsMatch(s)))
                //e.WriteLine(s);
                //if (r2.IsMatch(s) && !(loader_foreign_rgx.IsMatch(s) || loader_rgx.IsMatch(s)) || loader_foreign2_rgx.IsMatch(s))
                //e.WriteLine(s);
                if (is_foreign_rgx.IsMatch(s))
                    foreach (Match m in is_foreign_rgx.Matches(s))
                        add_script(m.Groups[2].Value + ":" + m.Groups[1].Value);
                else {
                    foreach (Match m in is_rgx.Matches(s))
                        add_script(m.Groups[1].Value);
                    foreach (Match m in is2_rgx.Matches(s))
                        add_script(m.Groups[1].Value);
                    foreach (Match m in is_ext_rgx.Matches(s))
                        if (m.Groups[3].Value.EndsWith("edia"))
                            add_script(m.Groups[2].Value + ":" + m.Groups[4].Value);
                        else if (m.Groups[3].Value == "wikidata")
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value);
                        else if (m.Groups[3].Value == "mediawiki")
                            add_script("mw:" + m.Groups[4].Value);
                        else
                            add_script(m.Groups[2].Value + ":" + m.Groups[3].Value + ":" + m.Groups[4].Value);
                    foreach (Match m in loader_rgx.Matches(s))
                        add_script(m.Groups[2].Value);
                    foreach (Match m in loader_foreign_rgx.Matches(s))
                        if (m.Groups[4].Value.EndsWith("edia"))
                            add_script(m.Groups[3].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "wikidata")
                            add_script(m.Groups[4].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "mediawiki")
                            add_script("mw:" + m.Groups[5].Value);
                        else
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value + ":" + m.Groups[5].Value);
                    foreach (Match m in loader_foreign2_rgx.Matches(s))
                        if (m.Groups[4].Value.EndsWith("edia"))
                            add_script(m.Groups[3].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "wikidata")
                            add_script(m.Groups[4].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "mediawiki")
                            add_script("mw:" + m.Groups[5].Value);
                        else
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value + ":" + m.Groups[5].Value);
                }
            }
    }
    static void page_creators()
    {
        var falsebots = new Dictionary<string, string[]>() { { "ru", new string[] { "Alex Smotrov", "Wind", "Tutaishy" } }, { "kk", new string[] { "Arystanbek", "Нұрлан_Рахымжанов" } } };
        var resultpage = new Dictionary<string, string>() { { "ru", "ВП:Участники по числу созданных страниц" }, { "kk", "Уикипедия:Бет бастауы бойынша қатысушылар" } };
        var disambigcategory = new Dictionary<string, string>() { { "ru", "Страницы значений по алфавиту" }, { "kk", "Алфавит бойынша айрық беттер" } };
        var headers = new Dictionary<string, string>() { { "ru", "{{Плавающая шапка таблицы}}{{Самые активные участники}}{{shortcut|ВП:УПЧС}}<center>Бот, генерирующий таблицу, работает так: берёт " +
                "все страницы основного пространства, включая редиректы, и для каждой смотрит имя первого правщика. Таким образом бот не засчитывает создание удалённых статей и статей, авторство в " +
                "которых скрыто. Обновлено " + now.ToString("d.M.yyyy") + ".\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!Участник!!Статьи!!Редиректы!!Дизамбиги!!Шаблоны!!Категории!!Файлы" },
            { "kk", "{{shortcut|УП:ББҚ}}<center>{{StatInfo}}\n{|class=\"standard sortable ts-stickytableheader\"\n!#!!Қатысушы!!Мақалалар!!Бағыттау беттері!!Айрық беттер!!Үлгілер!!Санаттар!!Файлдар" } };
        var footers = new Dictionary<string, string>() { { "ru", "" }, { "kk", "\n{{Wikistats}}[[Санат:Уикипедия:Қатысушылар]]" } }; var limit = new Dictionary<string, int>() { { "ru", 100 }, { "kk", 50 } };
        foreach (var lang in new string[] { "kk", "ru" }) {
            users.Clear();
            Dictionary<string, Dictionary<string, int>> bestusers = new Dictionary<string, Dictionary<string, int>>();
            HashSet<string> bots = new HashSet<string>(), disambs = new HashSet<string>();
            connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            command = new MySqlCommand("select distinct cast(log_title as char) title from logging where log_type=\"rights\" and log_params like \"%bot%\";", connect) { CommandTimeout = 9999 };
            rdr = command.ExecuteReader();
            while (rdr.Read()) {
                string bot = rdr.GetString("title");
                if (!falsebots[lang].Contains(bot) && !bots.Contains(bot))
                    bots.Add(bot.Replace("_", " "));
            }
            rdr.Close();
            connect.Close();
            string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=category:" + disambigcategory[lang] + "&cmprop=ids&cmlimit=max";
            while (cont != null) {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&cmcontinue=" + e(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout))) {
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.Name == "cm")
                            disambs.Add(r.GetAttribute("pageid"));
                }
            }
            foreach (var ns in new string[] { "14", "10", "6", "0" }) {
                cont = ""; query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=json&formatversion=2&list=allpages&aplimit=max&apfilterredir=nonredirects&apnamespace=" + ns;
                while (cont != "-") {
                    Root response = JsonConvert.DeserializeObject<Root>(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + e(cont)).Result);
                    cont = response.@continue == null ? "-" : response.@continue.apcontinue;
                    foreach (var pageinfo in response.query.allpages) {
                        int id = pageinfo.pageid;
                        if (ns != "0")
                            get_page_author(id, ns, lang);
                        else if (disambs.Contains(id.ToString()))
                            get_page_author(id, "d", lang);
                        else
                            get_page_author(id, "0", lang);
                    }
                }
            }
            cont = ""; query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=json&formatversion=2&list=allpages&aplimit=max&apfilterredir=redirects&apnamespace=0";
            while (cont != "-")
            {
                Root response = JsonConvert.DeserializeObject<Root>(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + e(cont)).Result);
                cont = response.@continue == null ? "-" : response.@continue.apcontinue;
                foreach (var pageinfo in response.query.allpages)
                    get_page_author(pageinfo.pageid, "r", lang);
            }
            foreach (var u in users)
                if (u.Value["0"] + u.Value["6"] + u.Value["10"] + u.Value["14"] + u.Value["r"] + u.Value["d"] >= limit[lang])
                    bestusers.Add(u.Key, u.Value);
            string result = headers[lang];
            int c = 0;
            foreach (var u in bestusers.OrderByDescending(u => u.Value["0"])) {
                bool bot = bots.Contains(u.Key);
                string color = (bot ? "style=\"background-color:#ddf\"" : "");
                string number = (bot ? "" : (++c).ToString());
                result += "\n|-" + color + "\n|" + number + "||{{u|" + (u.Key.Contains('=') ? "1=" + u.Key : u.Key) + "}}||" + u.Value["0"] + "||" + u.Value["r"] + "||" + u.Value["d"] + "||" +
                    u.Value["10"] + "||" + u.Value["14"] + "||" + u.Value["6"];
            }
            save(lang, resultpage[lang], result + "\n|}" + footers[lang], "");
        }
    }
    static void get_page_author(int id, string ns, string lang)
    {
        try {
            connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki")); connect.Open();
            command = new MySqlCommand("SELECT cast(actor_name as char) user FROM revision JOIN actor ON rev_actor = actor_id where rev_page=" + id + " order by rev_timestamp asc limit 1;", connect);
            rdr = command.ExecuteReader();
            while (rdr.Read()) {
                string user = rdr.GetString("user");
                if (!users.ContainsKey(user))
                    users.Add(user, new Dictionary<string, int>() { { "0", 0 }, { "6", 0 }, { "10", 0 }, { "14", 0 }, { "r", 0 }, { "d", 0 } });
                users[user][ns]++;
            }
            rdr.Close();
            connect.Close();
        } catch { }
    }
    static void exclude_deleted_files()
    {
        file_exclusion_query = "https://{domain}.org/w/api.php?action=query&format=xml&list=logevents&leprop=title|user|comment&leaction=delete/delete&lenamespace=6&lelimit=max&leend=" + now.AddDays(-1)
            .ToString("yyyy-MM-ddTHH:mm:ss"); run_ru(); run_commons();
    }
    static void delete_transclusion(pair dp, bool isCommons)
    {
        string initial_text = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + e(dp.page) + "?action=raw").Result; string new_page_text = initial_text;
        string filename = dp.file[4] == ':' ? dp.file.Substring(5) : dp.file; string rgxtext = filename.Replace(" ", "[ _]+"); rgxtext = "(" + rgxtext + "|" + e(filename) + ")";
        var r1 = new Regex(@" *\[\[\s*(file|image|файл|изображение):\s*" + rgxtext + @"[^[\]]*\]\]", RegexOptions.IgnoreCase);
        var r2 = new Regex(@" *\[\[\s*(file|image|файл|изображение):\s*" + rgxtext + @"[^[]*(\[\[[^\[\]]*\]\][^[\]]*)*\]\]", RegexOptions.IgnoreCase);
        var r3 = new Regex(@" *<\s*gallery[^>]*>\s*(file|image|файл|изображение):\s*" + rgxtext + @"[^\n]*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r4 = new Regex(@" *(<\s*gallery[^>]*>.*)(file|image|файл|изображение):\s*" + rgxtext + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r5 = new Regex(@" *(<\s*gallery[^>]*>.*)" + rgxtext + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r6 = new Regex(@" *<\s*gallery[^>]*>\s*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r7 = new Regex(@" *\{\{\s*(flagicon image|audio)[^|}]*\|\s*" + rgxtext + @"[^}]*\}\}");
        var r8 = new Regex(@" *([=|]\s*)(file|image|файл|изображение):\s*" + rgxtext, RegexOptions.IgnoreCase);
        var r9 = new Regex(@" *([=|]\s*)" + rgxtext, RegexOptions.IgnoreCase); new_page_text = r1.Replace(new_page_text, "");
        new_page_text = r2.Replace(new_page_text, ""); new_page_text = r3.Replace(new_page_text, ""); new_page_text = r4.Replace(new_page_text, "$1"); new_page_text = r5.Replace(new_page_text, "$1");
        new_page_text = r6.Replace(new_page_text, ""); new_page_text = r7.Replace(new_page_text, ""); new_page_text = r8.Replace(new_page_text, "$1"); new_page_text = r9.Replace(new_page_text, "$1");
        if (new_page_text != initial_text)
            try
            {
                string comment = "[[file:" + filename + "]] удалён [[user:" + dp.deletion_data.deleter + "]] по причине " + dp.deletion_data.comment;
                save("ru", dp.page, new_page_text, isCommons ? comment.Replace("[[", "[[c:") : comment);
                if (dp.page.StartsWith("Шаблон:")) {
                    string logpage_text = site.GetStringAsync("https://ru.wikipedia.org/wiki/u:MBH/Шаблоны с удалёнными файлами?action=raw").Result;
                    rsave("u:MBH/Шаблоны с удалёнными файлами", logpage_text + "\n* [[" + dp.page + "]]");
                }
            }
            catch (Exception e) { Console.WriteLine(e.ToString()); }
    }
    static void find_and_delete_usages(Dictionary<string, logrecord> deletedfiles, bool iscommons)
    {
        foreach (var df in deletedfiles.Keys.ToList()) {
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(df)).Result)))
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("_idx")[0] != '-')
                        deletedfiles.Remove(r.GetAttribute("title"));
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=max&titles=" + e(df)).Result))) {
                bool file_is_used = true; string ru_filename = ""; while (r.Read()) {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page") { ru_filename = r.GetAttribute("title"); file_is_used = r.GetAttribute("_idx")[0] != '-'; }
                    if (r.Name == "fu" && !file_is_used) { int ns = i(r.GetAttribute("ns"));
                        if (ns % 2 == 0 && ns != 4 && ns != 104 && ns != 106)
                            try { delete_transclusion(new pair { file = ru_filename, deletion_data = deletedfiles[ru_filename.Replace("Файл:", "File:")], page = r.GetAttribute("title") }, iscommons); }
                            catch { try { delete_transclusion(new pair { file = ru_filename, deletion_data = deletedfiles[ru_filename], page = r.GetAttribute("title") }, iscommons); } catch { } }
                    }
                }
            }
        }
    }
    static void run_commons()
    {
        var deletedfiles = new Dictionary<string, logrecord>(); string cont = "", query = file_exclusion_query.Replace("{domain}", "commons.wikimedia");
        var invalid_reasons_for_deletion = new Regex("temporary|maintenance|old revision|redirect", RegexOptions.IgnoreCase);
        while (cont != null)
            using (var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&lecontinue=" + cont).Result))) {
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue"); while (r.Read())
                    if (r.Name == "item") {
                        string title = r.GetAttribute("title");
                        if (title == null) continue;
                        string comment = r.GetAttribute("comment") ?? "";
                        if (!deletedfiles.ContainsKey(title) && !invalid_reasons_for_deletion.IsMatch(comment))
                            deletedfiles.Add(title, new logrecord { deleter = r.GetAttribute("user"), comment = comment });
                    }
            }
        find_and_delete_usages(deletedfiles, true);
    }
    static void run_ru()
    {
        var deletedfiles = new Dictionary<string, logrecord>(); var replacedfiles = new Dictionary<string, logrecord>(); var usages_for_deletion = new HashSet<pair>(); var replacingpairs = new HashSet<pair>();
        string cont = "", query = file_exclusion_query.Replace("{domain}", "ru.wikipedia"); var commons_importer_link = new Regex(@"commons.wikimedia.org/wiki/File:([^ ])", RegexOptions.IgnoreCase);
        var file_is_replaced_rgx = new Regex("КБУ#Ф[178]|икисклад|ommons", RegexOptions.IgnoreCase); var inner_link_to_replacement_file = new Regex(@"\[\[(:?c:|:?commons:|)(File|Файл):([^\]]*)\]\]", RegexOptions.IgnoreCase);
        while (cont != null) {
            using (var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&lecontinue=" + cont).Result))) {
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue"); while (r.Read())
                    if (r.Name == "item" && r.GetAttribute("title") != null) {
                        string comm = r.GetAttribute("comment") ?? ""; string filename = r.GetAttribute("title"); bool same_name_on_commons_exist = false;
                        using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=info&titles=file:" +
                            e(filename.Substring(5))).Result)))
                            while (rr.Read())
                                if (rr.Name == "page" && rr.GetAttribute("_idx")[0] != '-')
                                    same_name_on_commons_exist = true;
                        if (!same_name_on_commons_exist)
                            if (file_is_replaced_rgx.IsMatch(comm) && ((inner_link_to_replacement_file.IsMatch(comm) && inner_link_to_replacement_file.Match(comm).Groups[3].Value !=
                            filename.Substring(5)) || (commons_importer_link.IsMatch(comm) && commons_importer_link.Match(comm).Groups[1].Value != filename.Substring(5))) && !replacedfiles.ContainsKey(filename))
                                replacedfiles.Add(filename, new logrecord { deleter = r.GetAttribute("user"), comment = comm });
                            else if (!deletedfiles.ContainsKey(filename))
                                deletedfiles.Add(filename, new logrecord { deleter = r.GetAttribute("user"), comment = comm });
                    }
            }
        }
        foreach (var rf in replacedfiles.Keys.ToList())
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=max&titles=" + e(rf)).Result))) {
                bool file_exists = true; string filename = "";
                while (r.Read()) {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page") { filename = r.GetAttribute("title").Substring(5); file_exists = r.GetAttribute("_idx")[0] != '-'; }
                    if (r.Name == "fu" && !file_exists && i(r.GetAttribute("ns")) % 2 == 0) {
                        var page = r.GetAttribute("title"); string initial_text = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + e(page) + "?action=raw").Result; string newname;
                        try { newname = inner_link_to_replacement_file.Match(replacedfiles[rf].comment).Groups[3].Value; } catch { newname = commons_importer_link.Match(replacedfiles[rf].comment).Groups[1].Value; }
                        string rgxtext = filename.Replace(" ", "[ _]"); rgxtext = "(" + rgxtext + "|" + e(filename) + ")"; var rgx = new Regex(rgxtext, RegexOptions.IgnoreCase);
                        string new_page_text = rgx.Replace(initial_text, newname);
                        if (new_page_text != initial_text)
                            try { save("ru", page, new_page_text, "[[" + rf + "]] удалён [[u:" + replacedfiles[rf].deleter + "]] по причине " + replacedfiles[rf].comment); }
                            catch (Exception e) { Console.WriteLine(e.ToString()); }
                    }
                }
            }
        find_and_delete_usages(deletedfiles, false);
    }
    static void astro_update()
    {
        string github_base_url = "https://raw.githubusercontent.com/Saisengen/wikibots/refs/heads/main/astro-updater/";
        var requests = new Dictionary<string, string> { { "stars-by-cluster", "Википедия:Автоматически формируемые списки звёзд по скоплениям" }, { "exoplanets-by-constellation", "Википедия:Автоматически " +
                "формируемые списки экзопланет по созвездиям" }, { "exoplanetary-systems", "Википедия:Автоматически формируемые шаблоны экзопланетных систем" }, { "astrocatalogs", "Википедия:Автоматически " +
                "формируемые шаблоны по астрокаталогам" }, { "stars-by-constellation", "Навигационные шаблоны:Звёзды по созвездиям" } };
        foreach (var rq in "stars-by-constellation|stars-by-cluster|exoplanets-by-constellation|exoplanetary-systems|astrocatalogs".Split('|')) {
            var query = new StreamReader(site.GetStreamAsync(github_base_url + rq + ".rq").Result).ReadToEnd().Replace("{", "{{").Replace("}", "}}").Replace("{{0}}", "{0}"); var pages = new List<string>();
            var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmprop=title&cmlimit=max&cmtitle=К:" + requests[rq]).Result));
            while (r.Read())
                if (r.Name == "cm")
                    pages.Add(r.GetAttribute("title"));
            foreach (var title in pages) {
                var r2 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=pageprops&ppprop=wikibase_item&format=xml&titles=" + title).Result));
                while (r2.Read())
                    if (r2.Name == "pageprops") {
                        var result = site.PostAsync("https://query.wikidata.org/sparql", new FormUrlEncodedContent(new Dictionary<string, string> { { "query", string.Format(query,
                            r2.GetAttribute("wikibase_item")) } })).Result;
                        var newtext = result.Content.ReadAsStringAsync().Result.Replace("\r", "").Replace("line\n", "").Replace("\"", "");
                        if (title.StartsWith("Список") && newtext.StartsWith("'''{{subst") || title.StartsWith("Шаблон:") && title != "Шаблон:Звёзды по созвездиям") {
                            var oldtext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeUriString(title) + "?action=raw").Result;
                            if (oldtext.Length - newtext.Length > 2048) {
                                Console.WriteLine("https://ru.wikipedia.org/wiki/" + title + ": new content too short: " + oldtext.Length + " > " + newtext.Length + "\nnewtext=" + newtext); continue; }
                            else rsave(title, newtext);
                        }
                    }
            }
        }
    }
    static void best_article_lists()
    {
        var pagetypes = new Dictionary<string, string>() { { "featured", "Избранные статьи" }, { "good", "Хорошие статьи" }, { "tier3", "Добротные статьи" }, { "lists", "Избранные списки" }, { "aoty", "Статьи года" } };
        var result = new Dictionary<string, List<string>>() { { "featured", new List<string>() }, { "good", new List<string>() }, { "tier3", new List<string>() }, { "lists", new List<string>() }, { "aoty", new List<string>() }, };
        foreach(var cat in pagetypes)
        {
            string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmprop=title&cmlimit=max&cmtitle=К:Википедия:" + cat.Value + " по алфавиту";
            while (cont != null) {
                apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&cmcontinue=" + e(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout))) {
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.Name == "cm")
                            result[cat.Key].Add(r.GetAttribute("title"));
                }
            }
        }
        rsave("MediaWiki:Gadget-navboxFeaturedArticles.json", JsonConvert.SerializeObject(result));
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = login("ru", creds[0], creds[1]); site.DefaultRequestHeaders.Add("Accept", "text/csv"); now = DateTime.Now;
        monthname = new string[13] { "", "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
        prepositional = new string[13] { "", "январе", "феврале", "марте", "апреле", "мае", "июне", "июле", "августе", "сентябре", "октябре", "ноябре", "декабре" };
        try { best_article_lists(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { astro_update(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { exclude_deleted_files(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { user_activity_stats_template(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { main_inc_bot(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { redirs_deletion(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { inc_check_help_requests_img(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { orphan_nonfree_files(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { unlicensed_files(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { outdated_templates(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { nonfree_files_in_nonmain_ns(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { unreviewed_in_nonmain_ns(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { trans_namespace_moves(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { zsf_archiving(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { little_flags(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { catmoves(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        if (now.Day == 1)
        {
            try { orphan_articles(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { dm89_stats(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { incorrect_redirects(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { pats_awarding(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { likes_stats(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { adminstats(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { summary_stats(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { incorrect_redirects(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { apat_for_filemovers(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { popular_wd_items_without_ru(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { most_watched_pages(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { popular_userscripts(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { most_active_users(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
            try { page_creators(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        }
    }
}
