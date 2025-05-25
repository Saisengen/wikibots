using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Xml;
using System.Net.Http;
using System.Globalization;
using System.Linq;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
class tnm_record
{
    public string oldtitle, oldns, newtitle, user, date, comment;
}
class catmoves_record
{
    public string oldtitle, newtitle, user, timestamp, comment, title;
}
class Program
{
    static HttpClient site = new HttpClient();
    static DateTime dtn;
    static string[] monthname, creds;
    static HashSet<string> highflags = new HashSet<string>();
    static bool legit_link_found;
    static string orphan_article;
    static string serialize(HashSet<string> list)
    {
        list.ExceptWith(highflags);
        return JsonConvert.SerializeObject(list);
    }
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
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
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
        if (!result.ToString().Contains("uccess"))
            Console.WriteLine(result.ToString());
    }
    static void nonfree_files_in_nonmain_ns()
    {
        string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&generator=categorymembers&fuprop=title&fulimit=5000&gcmtitle=Категория:Файлы:Несвободные&gcmtype=file&gcmlimit=1000";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&gcmcontinue=" + Uri.EscapeDataString(cont)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("gcmcontinue");
                string file = "";
                while (r.Read())
                {
                    if (r.Name == "page")
                        file = r.GetAttribute("title");
                    if (r.Name == "fu" && r.GetAttribute("ns") != "0" && r.GetAttribute("ns") != "102")
                    {
                        string title = r.GetAttribute("title");
                        string text = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + title + "?action=raw").Result;
                        string initialtext = text;
                        string filename = file.Substring(5);
                        filename = "(" + Regex.Escape(filename) + "|" + Regex.Escape(Uri.EscapeDataString(filename)) + ")";
                        filename = filename.Replace(@"\ ", "[ _]+");
                        var r1 = new Regex(@"\[\[\s*(file|image|файл|изображение):\s*" + filename + @"[^[\]]*\]\]", RegexOptions.IgnoreCase);
                        var r2 = new Regex(@"\[\[\s*(file|image|файл|изображение):\s*" + filename + @"[^[]*(\[\[[^\[\]]*\]\][^[\]]*)*\]\]", RegexOptions.IgnoreCase);
                        var r3 = new Regex(@"<\s*gallery[^>]*>\s*(file|image|файл|изображение):\s*" + filename + @"[^\n]*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        var r4 = new Regex(@"(<\s*gallery[^>]*>.*)(file|image|файл|изображение):\s*" + filename + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        var r5 = new Regex(@"(<\s*gallery[^>]*>.*)" + filename + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        var r6 = new Regex(@"<\s*gallery[^>]*>\s*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                        var r7 = new Regex(@"\{\{\s*(flagicon image|audio)[^|}]*\|\s*" + filename + @"[^}]*\}\}");
                        var r8 = new Regex(@"([=|]\s*)(file|image|файл|изображение):\s*" + filename, RegexOptions.IgnoreCase);
                        var r9 = new Regex(@"([=|]\s*)" + filename, RegexOptions.IgnoreCase);
                        text = r1.Replace(text, "");
                        text = r2.Replace(text, "");
                        text = r3.Replace(text, "");
                        text = r4.Replace(text, "$1");
                        text = r5.Replace(text, "$1");
                        text = r6.Replace(text, "");
                        text = r7.Replace(text, "");
                        text = r8.Replace(text, "$1");
                        text = r9.Replace(text, "$1");
                        if (text != initialtext)
                        {
                            Save(site, title, text, "удаление несвободного файла из служебных пространств");
                            if (r.GetAttribute("ns") == "10")
                            {
                                string tracktext = site.GetStringAsync("https://ru.wikipedia.org/wiki/u:MBH/Шаблоны с удалёнными файлами?action=raw").Result;
                                Save(site, "u:MBH/Шаблоны с удалёнными файлами", tracktext + "\n* [[" + title + "]]", "");
                            }
                        }
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
                    if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                    {
                        string text = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeUriString(r.GetAttribute("title")) + "?action=raw").Result;
                        Save(site, r.GetAttribute("title"), rgx.Replace(text, ""), "удалены просроченные шаблоны");
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
            string pagetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(file) + "?action=raw").Result;
            Save(site, file, "{{subst:nld}}\n" + pagetext, "вынос на КБУ файла без валидной лицензии");
        }

        if (autocatfiles.Count != 0)
            Save(site, "К:Файлы:Неясный лицензионный статус от " + dtn.Day + " " + monthname[dtn.Month] + " " + dtn.Year, "[[К:Файлы:Неясный лицензионный статус|" + dtn.ToString("MM-dd") + "]]", "");
    }
    static void orphan_nonfree_files()
    {
        string cont, apiout, query, fucont = "", gcmcont = "";
        var tagged_files = new HashSet<string>();
        var nonfree_files = new HashSet<string>();
        var unused_files = new HashSet<string>();
        query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&list=&continue=gcmcontinue%7C%7C&generator=categorymembers&fulimit=max&gcmtitle=Категория:Файлы:Несвободные&gcmnamespace=6&gcmlimit=max";
        do
        {
            apiout = site.GetStringAsync(query + (fucont == "" ? "" : "&fucontinue=" + Uri.EscapeDataString(fucont)) + (gcmcont == "" ? "" : "&gcmcontinue=" + Uri.EscapeDataString(gcmcont))).Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); fucont = r.GetAttribute("fucontinue"); gcmcont = r.GetAttribute("gcmcontinue");
                if (fucont == null) fucont = "";
                if (gcmcont == null) gcmcont = "";

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
            apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&cmcontinue=" + Uri.EscapeDataString(cont)).Result);
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
                string file_descr = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(file) + "?action=raw").Result;
                var x = pagerx.Matches(file_descr);
                foreach (Match xx in x)
                    legal_file_using_pages.Add(xx.Groups[1].Value);
                foreach (var page in legal_file_using_pages)
                    try
                    {
                        string using_page_text = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(page) + "?action=raw").Result;
                        if (!redirrx.IsMatch(using_page_text))
                            Save(site, page, using_page_text + "\n", "");
                        else
                        {
                            string redirect_target_page = redirrx.Match(using_page_text).Groups[1].Value;
                            string target_page_text = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(redirect_target_page) + "?action=raw").Result;
                            Save(site, redirect_target_page, target_page_text + "\n", "");
                        }
                    }
                    catch { continue; }
            }
            catch { }
        }
        foreach (var file in nonfree_files)
        {
            apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&titles=" + Uri.EscapeDataString(file)).Result;
            if (!apiout.Contains("<fileusage>"))
                unused_files.Add(file);
        }

        foreach (var file in unused_files)
        {
            string uploaddate = "";
            string file_descr = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(file) + "?action=raw").Result;
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&titles=" + Uri.EscapeDataString(file) + "&rvprop=timestamp&rvlimit=1&rvdir=newer").Result)))
                while (r.Read())
                    if (r.Name == "rev")
                        uploaddate = r.GetAttribute("timestamp").Substring(0, 10);
            if (DateTime.Now - DateTime.ParseExact(uploaddate, "yyyy-MM-dd", CultureInfo.InvariantCulture) > new TimeSpan(0, 1, 0, 0))
                Save(site, file, "{{subst:ofud}}\n" + file_descr, "вынос на КБУ неиспользуемого в статьях несвободного файла");
        }
        if (unused_files.Count != 0)
            Save(site, "К:Файлы:Неиспользуемые несвободные от " + dtn.Day + " " + monthname[dtn.Month] + " " + dtn.Year, "__NOGALLERY__\n[[К:Файлы:Неиспользуемые несвободные|" + dtn.ToString("MM-dd") + "]]", "");
    }
    static void redirs_deletion()
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var legal_redirs = new List<string>();

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Категория:Википедия:Намеренные перенаправления между СО&cmlimit=max").Result)))
            while (r.Read())
                if (r.Name == "cm")
                    legal_redirs.Add(r.GetAttribute("pageid"));

        foreach (int ns in new int[] { 1, 3, 5, 7, 9, 11, 13, 15, 101, 103, 105, 107, 829 })
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allredirects&arprop=title|ids&arnamespace=" + ns + "&arlimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); cont = r.GetAttribute("arcontinue");
                    while (r.Read())
                        if (r.Name == "r" && (ns != 3 || r.GetAttribute("title").Contains("/")))
                        {
                            int cntr = 0;
                            string id = r.GetAttribute("fromid");
                            using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + id + "&rvprop=ids&rvlimit=max").Result)))
                                while (rr.Read())
                                    if (rr.Name == "rev" && rr.NodeType == XmlNodeType.Element)
                                        cntr++;
                            if (!legal_redirs.Contains(id) && cntr == 1)
                            {
                                using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + id + "&rvprop=ids&rvlimit=max").Result)))
                                {
                                    rr.WhitespaceHandling = WhitespaceHandling.None;
                                    while (rr.Read())
                                        if (rr.Name == "rev")
                                        {
                                            rr.Read();
                                            if (rr.NodeType == XmlNodeType.EndElement && rr.Name == "revisions")
                                                using (var rrr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blpageid=" + id).Result)))
                                                {
                                                    rrr.WhitespaceHandling = WhitespaceHandling.None;
                                                    bool there_are_links = false;
                                                    while (rrr.Read())
                                                        if (rrr.Name == "bl" && !rrr.GetAttribute("title").StartsWith("Википедия:Страницы с похожими названиями") && !rrr.GetAttribute("title").StartsWith("Участник:DvoreBot/Оставленные перенаправления"))
                                                            there_are_links = true;
                                                    if (!there_are_links)
                                                    {
                                                        var request = new MultipartFormDataContent { { new StringContent("delete"), "action" }, { new StringContent(id), "pageid" },
                                                            { new StringContent("[[ВП:КБУ#П6|редирект между СО без ссылок]]"), "reason" }, { new StringContent(token), "token" } };
                                                        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
                                                        if (!result.ToString().Contains("uccess"))
                                                            Console.WriteLine(result);
                                                    }
                                                }
                                            break;
                                        }
                                }
                            }
                        }

                }
            }
        }

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=querypage&qppage=BrokenRedirects&qplimit=max").Result)))
            while (r.Read())
                if (r.Name == "page")
                {
                    string title = r.GetAttribute("title");
                    string ns = r.GetAttribute("ns");
                    if (ns != "2" || (ns == "2" && title.Contains("/")))
                        using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&titles=" + title + "&rvprop=ids&rvlimit=max").Result)))
                        {
                            int cntr = 0;
                            while (rr.Read())
                                if (rr.Name == "rev" && rr.NodeType == XmlNodeType.Element)
                                    cntr++;
                            if (cntr == 1)
                            {
                                var request = new MultipartFormDataContent { { new StringContent("delete"), "action" }, { new StringContent(title), "title" }, { new StringContent(token), "token" } };
                                result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
                                if (!result.ToString().Contains("uccess"))
                                    Console.WriteLine(result);
                            }
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
                        apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&urcontinue=" + Uri.EscapeDataString(cont)).Result);
                        using (var r = new XmlTextReader(new StringReader(apiout)))
                        {
                            r.WhitespaceHandling = WhitespaceHandling.None;
                            r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("urcontinue");
                            while (r.Read())
                                if (r.Name == "p")
                                {
                                    string title = r.GetAttribute("title");
                                    result += type == "nonredirects" ? "#[[:" + title + "]]\n" : "#[https://ru.wikipedia.org/w/index.php?title=" + Uri.EscapeDataString(title) + "&redirect=no " + title + "]\n";
                                }
                        }
                    }
                }
        Save(site, "Проект:Патрулирование/Непроверенные вне ОП", result, "");
    }
    static void user_activity_stats_template()
    {
        var days = new Dictionary<string, int>();
        var edits = new Dictionary<string, int>();
        var itemrgx = new Regex("<item");
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allusers&augroup=sysop&aulimit=max").Result)))
            while (r.Read())
                if (r.Name == "u")
                    days.Add(r.GetAttribute("name"), 1);
        var initialusers = site.GetStringAsync("https://ru.wikipedia.org/wiki/Шаблон:User activity stats/users?action=raw").Result.Split('\n');
        foreach (var user in initialusers)
            if (!days.ContainsKey(user))
                days.Add(user, 1);

        foreach (string tmplt in new string[] { "Шаблон:Участник покинул проект", "Шаблон:Вики-отпуск" })
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=" + tmplt + "&einamespace=2|3&eilimit=max").Result)))
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                    {
                        string user = r.GetAttribute("title");
                        if (!user.Contains("/"))
                            user = user.Substring(user.IndexOf(':') + 1, user.Length - user.IndexOf(':') - 1);
                        else
                            user = user.Substring(user.IndexOf(':') + 1, user.IndexOf("/") - user.IndexOf(':') - 1);
                        if (!edits.ContainsKey(user))
                            edits.Add(user, 0);
                    }

        foreach (var u in days.Keys.ToList())
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucuser=" + Uri.EscapeDataString(u)).Result)))
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string ts = r.GetAttribute("timestamp");
                        int y = Convert.ToInt32(ts.Substring(0, 4));
                        int m = Convert.ToInt32(ts.Substring(5, 2));
                        int d = Convert.ToInt32(ts.Substring(8, 2));
                        days[u] = (DateTime.Now - new DateTime(y, m, d)).Days;
                    }

        foreach (var v in edits.Keys.ToList())
        {
            var res = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=max&ucend=" + DateTime.Now.AddDays(-7).ToString("yyyy-MM-ddTHH:mm:ss") +
                ".000Z&ucprop=&ucuser=" + Uri.EscapeDataString(v)).Result;
            edits[v] = itemrgx.Matches(res).Count;
        }

        string result = "{{#switch:{{{1}}}\n";
        foreach (var r in days.OrderBy(r => r.Value))
            result += "|" + r.Key + "=" + r.Value + "\n";
        Save(site, "Шаблон:User activity stats/days", result + "|}}", "");

        result = "{{#switch:{{{1}}}\n";
        foreach (var v in edits.OrderByDescending(v => v.Value))
            if (v.Value > 0)
                result += "|" + v.Key + "=" + (v.Value == 0 ? "" : v.Value.ToString()) + "\n";
        Save(site, "Шаблон:User activity stats/edits", result + "|}}", "");
    }
    static void trans_namespace_moves()
    {
        var table = new List<tnm_record>();
        var apatusers = new HashSet<string>();
        var header = new Dictionary<string, string>() {
            { "ru", "<center>{{Плавающая шапка таблицы}}{{shortcut|ВП:TRANSMOVE}}Красным выделены неавтопатрулируемые.{{clear}}\n{|class=\"standard sortable ts-stickytableheader\"\n!Дата!!Источник!!Название в ОП!!Переносчик!!Коммент" },
            { "en", "<center>Красным выделены неавтопатрулируемые.\n{|class=\"wikitable sortable\"\n!Дата!!Источник!!Название в ОП!!Переносчик!!Коммент" } };
        foreach (var lang in new string[] { /*"en",*/ "ru" })
        {
            string apiout = site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title|type|user|timestamp|comment|details&letype=move&lelimit=5000").Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                    {
                        string user = r.GetAttribute("user");
                        if (user.StartsWith("IncubatorBot"))
                            continue;
                        if (!apatusers.Contains(user))
                            using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=users&usprop=rights&ususers=" + user).Result)))
                                while (rr.Read())
                                    if (rr.Value == "autoreview" || rr.Value.Contains("patrol"))
                                        apatusers.Add(user);
                        string oldns = r.GetAttribute("ns");
                        if (oldns == "0")
                            continue;
                        string oldtitle = r.GetAttribute("title");
                        string date = r.GetAttribute("timestamp").Substring(5, 5);
                        string comment = r.GetAttribute("comment");
                        if (comment != null)
                            comment = Uri.UnescapeDataString(comment);
                        r.Read();
                        string newns = r.GetAttribute("target_ns");
                        if (newns != "0")
                            continue;
                        string newtitle = r.GetAttribute("target_title");
                        table.Add(new tnm_record() { oldtitle = oldtitle, oldns = oldns, newtitle = newtitle, user = user, date = date, comment = comment });
                    }
            }
            string result = header[lang];
            foreach (var t in table)
            {
                string comment;
                if (t.comment.Contains("{|") || t.comment.Contains("|}") || t.comment.Contains("||") || t.comment.Contains("|-"))
                    comment = "<nowiki>" + t.comment + "</nowiki>";
                else
                    comment = t.comment;
                result += "\n|-" + (apatusers.Contains(t.user) ? "" : "style=\"background-color:#fcc\"") + "\n|" + t.date + "||[[:" + t.oldtitle + "|" + t.oldtitle + "]]||[[:" + t.newtitle + "]]||{{u|" + t.user + "}}||" + comment;
            }
            Save(site, lang == "ru" ? "ВП:Страницы, перенесённые в пространство статей" : "user:MBHbot/transnamespace moves", result + "\n|}", "");
            table.Clear();
        }
    }
    static void zsf_archiving()
    {
        var year = DateTime.Now.Year;
        string zsftext = site.GetStringAsync("https://ru.wikipedia.org/wiki/Википедия:Заявки на снятие флагов?action=raw").Result;
        string initialtext = zsftext;
        var threadrgx = new Regex(@"\n\n==[^\n]*: флаг [^=]*==[^⇧]*===\s*Итог[^=]*===([^⇧]*)\((апат|пат|откат|загр|ПИ|ПФ|ПбП|инж|АИ|бот)\)\s*—\s*{{(за|против)([^⇧]*)⇧-->", RegexOptions.Singleline);
        var signature = new Regex(@"(\d\d:\d\d, \d{1,2} \w+ \d{4}) \(UTC\)");
        var threads = threadrgx.Matches(zsftext);
        foreach (Match thread in threads)
        {
            string archivepage = "";
            string threadtext = thread.Groups[0].Value;
            var summary = signature.Matches(thread.Groups[1].Value);
            var summary_discuss = signature.Matches(thread.Groups[4].Value);
            bool outdated = true;
            foreach (Match s in summary)
                if (DateTime.Now - DateTime.Parse(s.Groups[1].Value, System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) < new TimeSpan(2, 0, 0, 0))
                    outdated = false;
            foreach (Match s in summary_discuss)
                if (DateTime.Now - DateTime.Parse(s.Groups[1].Value, System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) < new TimeSpan(2, 0, 0, 0))
                    outdated = false;
            if (!outdated)
                continue;
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
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Боты";
                    break;
                default:
                    continue;
            }
            zsftext = zsftext.Replace(threadtext, "");
            try
            {
                string archivetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + archivepage + "?action=raw").Result;
                Save(site, archivepage, archivetext + threadtext, "");
            }
            catch
            {
                Save(site, archivepage, threadtext, "");
            }

        }
        if (zsftext != initialtext)
            Save(site, "Википедия:Заявки на снятие флагов", zsftext, "архивация");
    }
    static void little_flags()
    {
        var ru = new MySqlConnection(creds[2].Replace("%project%", "ruwiki"));
        var global = new MySqlConnection(creds[2].Replace("%project%", "centralauth"));
        ru.Open();
        global.Open();
        MySqlCommand command;
        MySqlDataReader rdr;
        var pats = new HashSet<string>();
        var rolls = new HashSet<string>();
        var apats = new HashSet<string>();
        var fmovers = new HashSet<string>();

        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"editor\";", ru);
        rdr = command.ExecuteReader();
        while (rdr.Read())
            pats.Add(rdr.GetString(0));
        rdr.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"rollbacker\";";
        rdr = command.ExecuteReader();
        while (rdr.Read())
            rolls.Add(rdr.GetString(0));
        rdr.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"autoreview\";";
        rdr = command.ExecuteReader();
        while (rdr.Read())
            apats.Add(rdr.GetString(0));
        rdr.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"filemover\";";
        rdr = command.ExecuteReader();
        while (rdr.Read())
            fmovers.Add(rdr.GetString(0));
        rdr.Close();

        foreach (string flag in new string[] { "sysop", "closer", "engineer" })
        {
            command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"" + flag + "\";";
            rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                string user = rdr.GetString(0);
                if (!highflags.Contains(user))
                    highflags.Add(user);
            }
            rdr.Close();
        }

        command = new MySqlCommand("SELECT cast(gu_name as char) user FROM global_user_groups JOIN globaluser ON gu_id=gug_user WHERE gug_group=\"global-rollbacker\"", global);
        rdr = command.ExecuteReader();
        while (rdr.Read())
            if (!rolls.Contains(rdr.GetString(0)))
                rolls.Add(rdr.GetString(0));

        var patnotrolls = new HashSet<string>(pats);
        patnotrolls.ExceptWith(rolls);

        var rollnotpats = new HashSet<string>(rolls);
        rollnotpats.ExceptWith(pats);

        var patrolls = new HashSet<string>(pats);
        patrolls.IntersectWith(rolls);

        string result = "{\"userSet\":{\"p,r\":" + serialize(patrolls) + ",\"ap\":" + serialize(apats) + ",\"p\":" + serialize(patnotrolls) + ",\"r\":" + serialize(rollnotpats) + "," + "\"f\":" + serialize(fmovers) + "}}";
        Save(site, "MediaWiki:Gadget-markothers.json", result, "");
    }
    static void catmoves()
    {
        var catnames = new HashSet<string>();
        var table = new List<catmoves_record>();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&letype=move&lenamespace=14&lelimit=max").Result)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                {
                    bool nonempty = false;
                    string title = r.GetAttribute("title");
                    using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + Uri.EscapeDataString(title)).Result)))
                        while (rr.Read())
                            if (rr.NodeType == XmlNodeType.Element && rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0")
                                nonempty = true;
                    if (nonempty)
                    {
                        var n = new catmoves_record { oldtitle = title, user = r.GetAttribute("user"), timestamp = r.GetAttribute("timestamp").Substring(0, 10), comment = r.GetAttribute("comment") };
                        r.Read();
                        n.newtitle = r.GetAttribute("target_title");
                        table.Add(n);
                    }
                }
        }
        string result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Откуда (страниц в категории)!!Куда (страниц в категории)!!Юзер!!Коммент";
        foreach (var t in table)
            result += "\n|-\n|" + t.timestamp + "||[[:" + t.oldtitle + "]] ({{PAGESINCATEGORY:" + t.oldtitle.Substring(10) + "}})||[[:" + t.newtitle + "]] ({{PAGESINCATEGORY:" + t.newtitle.Substring(10) +
                "}})||[[u:" + t.user + "]]||" + t.comment;
        result += "\n|}";
        Save(site, "u:MBH/Переименованные категории с недоперенесёнными страницами", result, "");
        catnames.Clear();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&leaction=delete/delete&lenamespace=14&lelimit=max").Result)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                {
                    bool nonempty = false;
                    string title = r.GetAttribute("title");
                    using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + Uri.EscapeDataString(title)).Result)))
                    {
                        rr.WhitespaceHandling = WhitespaceHandling.None;
                        while (rr.Read())
                            if (rr.NodeType == XmlNodeType.Element && rr.Name == "page" && rr.GetAttribute("missing") != null)
                            {
                                rr.Read();
                                if (rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0")
                                    nonempty = true;
                            }
                    }
                    if (nonempty)
                        try
                        {
                            var n = new catmoves_record { title = title, user = r.GetAttribute("user"), timestamp = r.GetAttribute("timestamp").Substring(0, 10) };
                            string comment = r.GetAttribute("comment").Replace("[[К", "[[:К");
                            n.comment = (comment.Contains("}}") ? "<nowiki>" + comment + "</nowiki>" : comment);
                            table.Add(n);
                        }
                        catch
                        {
                            continue;
                        }
                }
        }
        result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Имя (страниц в категории)!!Юзер!!Коммент";
        foreach (var t in table)
            if (t.title != null)
                result += "\n|-\n|" + t.timestamp + "||[[:" + t.title + "]] ({{PAGESINCATEGORY:" + t.title.Substring(10) + "}})||[[u:" + t.user + "]]||" + t.comment;
        result += "\n|}";
        Save(site, "u:MBH/Удалённые категории со страницами", result, "");
    }
    static void orphan_articles()
    {
        var nonlegit_link_pages = new List<string>();
        foreach (string templatename in "Ш:Координационный список|Ш:Неоднозначность".Split('|'))
        {
            string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eilimit=max&eititle=" + templatename;
            while (cont != null)
            {
                apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&eicontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                    while (r.Read())
                        if (r.Name == "ei" && !nonlegit_link_pages.Contains(r.GetAttribute("title")))
                            nonlegit_link_pages.Add(r.GetAttribute("title"));
                }
            }
        }
        string apiout1, cont1 = "", query1 = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eilimit=max&eititle=ш:изолированная статья";
        while (cont1 != null)
        {
            apiout1 = (cont1 == "" ? site.GetStringAsync(query1).Result : site.GetStringAsync(query1 + "&eicontinue=" + Uri.EscapeDataString(cont1)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout1)))
            {
                r.Read(); r.Read(); r.Read(); cont1 = r.GetAttribute("eicontinue"); Console.WriteLine(cont1);
                while (r.Read())
                    if (r.Name == "ei")
                    {
                        orphan_article = r.GetAttribute("title");
                        legit_link_found = false;
                        using (var r2 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blfilterredir=nonredirects" +
                                "&bllimit=max&bltitle=" + Uri.EscapeUriString(orphan_article)).Result)))
                            while (r2.Read())
                                if (r2.Name == "bl" && r2.GetAttribute("ns") == "0" && !nonlegit_link_pages.Contains(r2.GetAttribute("title")))
                                {
                                    remove_template_from_non_orphan_page();
                                    break;
                                }
                        if (!legit_link_found)
                            using (var r3 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blfilterredir=redirects" +
                                "&bllimit=max&bltitle=" + Uri.EscapeUriString(orphan_article)).Result)))
                                while (r3.Read())
                                    if (r3.Name == "bl" && !legit_link_found)
                                    {
                                        string linked_redirect = r3.GetAttribute("title");
                                        using (var r4 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blfilterredir=redirects" +
                                "&bllimit=max&bltitle=" + Uri.EscapeUriString(linked_redirect)).Result)))
                                            while (r4.Read())
                                                if (r4.Name == "bl" && r4.GetAttribute("ns") == "0" && !nonlegit_link_pages.Contains(r4.GetAttribute("title")))
                                                {
                                                    remove_template_from_non_orphan_page();
                                                    break;
                                                }
                                    }
                    }
            }
        }
    }
    static void remove_template_from_non_orphan_page()
    {
        try
        {
            string pagetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeUriString(orphan_article) + "?action=raw").Result;
            Save(site, orphan_article, pagetext.Replace("{{изолированная статья|", "{{subst:ET|").Replace("{{Изолированная статья|", "{{subst:ET|"), "удаление неактуального шаблона изолированной статьи");
            legit_link_found = true;
        }
        catch { }
    }
    static void stat_bot()
    {        
        var cats = new Dictionary<string, string>() { {"Википедия:Статьи для срочного улучшения","0" },{ "Википедия:Незакрытые обсуждения переименования страниц","0" },{ "Википедия:Статьи на улучшении " +
                "более года", "0" },{ "Википедия:Незакрытые обсуждения статей для улучшения", "0" },{ "Википедия:Статьи на улучшении более полугода", "0" },{ "Википедия:Статьи на улучшении более 90 дней",
                "0" },{ "Википедия:Статьи на улучшении более 30 дней", "0" },{ "Википедия:Статьи на улучшении менее 30 дней", "0" },{ "Википедия:Кандидаты на удаление", "0" },{ "Википедия:Незакрытые " +
                "обсуждения удаления страниц", "0" },{ "Википедия:Статьи для переименования", "0" },{ "Википедия:Кандидаты на объединение", "0" },{ "Википедия:Незакрытые обсуждения объединения страниц",
                "0" },{ "Википедия:Статьи для разделения", "0" },{ "Инкубатор:Запросы на проверку", "0" },{ "Википедия:Незакрытые обсуждения разделения страниц", "0" },{ "Википедия:Незакрытые обсуждения " +
                "восстановления страниц", "0" },{ "Инкубатор:Все статьи", "0" },{ "Инкубатор:Запросы о помощи", "0" },{ "Инкубатор:Статьи на мини-рецензировании", "0" }};
        foreach (var cat in cats.Keys.ToList())
        {
            var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=categoryinfo&titles=К:" + Uri.EscapeUriString(cat) + "&format=xml").Result));
            while (rdr.Read())
                if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "categoryinfo")
                    cats[cat] = rdr.GetAttribute("pages");
        }

        string vus_text = site.GetStringAsync("https://ru.wikipedia.org/wiki/Википедия:К восстановлению?action=raw").Result;
        var non_summaried_vus = new Regex(@"[^>]\[\[([^\]]*)\]\][^<]");

        string stat_text = site.GetStringAsync("https://ru.wikipedia.org/wiki/Участник:MBH/Завалы?action=raw").Result;
        string result = "\n|-\n|{{subst:#time:j.m.Y}}||" + cats["Википедия:Статьи для срочного улучшения"] + "||" + cats["Википедия:Статьи на улучшении более года"] + "||" + cats["Википедия:Статьи на улучшении " +
            "более полугода"] + "||" + cats["Википедия:Статьи на улучшении более 90 дней"] + "||" + cats["Википедия:Статьи на улучшении более 30 дней"] + "||" + cats["Википедия:Статьи на улучшении менее " +
            "30 дней"] + "||" + cats["Википедия:Незакрытые обсуждения статей для улучшения"] + "||" + cats["Википедия:Кандидаты на удаление"] + "||" + cats["Википедия:Незакрытые обсуждения удаления " +
            "страниц"] + "||" + cats["Википедия:Статьи для переименования"] + "||" + cats["Википедия:Незакрытые обсуждения переименования страниц"] + "||" + cats["Википедия:Кандидаты на объединение"] +
            "||" + cats["Википедия:Незакрытые обсуждения объединения страниц"] + "||" + cats["Википедия:Статьи для разделения"] + "||" + cats["Википедия:Незакрытые обсуждения разделения страниц"] + "||" +
            non_summaried_vus.Matches(vus_text).Count + "||" + cats["Википедия:Незакрытые обсуждения восстановления страниц"] + "||" + cats["Инкубатор:Все статьи"] + "||" + cats["Инкубатор:Статьи на " +
            "мини-рецензировании"] + "||" + cats["Инкубатор:Запросы на проверку"] + "||" + cats["Инкубатор:Запросы о помощи"] + "\n|}";
        Save(site, "Участник:MBH/Завалы", stat_text + result, "");
    }
    static void inc_check_bot()
    {
        string result = "{{/Doc}}";
        string comment = "";
        foreach (var cat in "Инкубатор:Запросы на проверку|Инкубатор:Запросы о помощи".Split('|'))
        {
            result += "\n==[[:К:" + cat + "|]]==\n{|class=\"standard sortable\"\n!Статья!!Размер!!Посл. правщик!!Посл. правка";
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmprop=title&cmtitle=К:" + cat).Result)))
                while (r.Read())
                    if (r.Name == "cm" && r.GetAttribute("ns").StartsWith("10"))
                    {
                        string title = r.GetAttribute("title");
                        result += "\n{{/строка|" + title + "}}";
                        comment += "[[" + title + "]], ";
                    }
            result += "\n|}";
        }
        Save(site, "Проект:Инкубатор/Запросы помощи и проверки", result, comment.Substring(0, comment.Length - 2));
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = Site(creds[0], creds[1]);
        dtn = DateTime.Now;
        monthname = new string[13] { "", "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
        try { inc_check_bot(); } catch(Exception e) { Console.WriteLine(e.ToString()); }
        try { stat_bot(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { orphan_nonfree_files(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { unlicensed_files(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { outdated_templates(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { nonfree_files_in_nonmain_ns(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { redirs_deletion(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { unreviewed_in_nonmain_ns(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { user_activity_stats_template(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { trans_namespace_moves(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { zsf_archiving(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { little_flags(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { catmoves(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
        try { orphan_articles(); } catch (Exception e) { Console.WriteLine(e.ToString()); }
    }
}
