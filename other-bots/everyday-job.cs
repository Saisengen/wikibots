using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Xml;
using System.Net.Http;
using System.Globalization;

class Program
{
    static string cont, fucont, gcmcont, apiout, query;
    static HttpClient site = new HttpClient();
    static DateTime dtn;
    static string[] monthname;
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
        cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&generator=categorymembers&fuprop=title&fulimit=5000&gcmtitle=Категория:Файлы:Несвободные&gcmtype=file&gcmlimit=1000";
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
    static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = Site(creds[0], creds[1]);
        dtn = DateTime.Now;
        monthname = new string[13] { "", "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
        orphan_nonfree_files();
        unlicensed_files();
        outdated_templates();
        nonfree_files_in_nonmain_ns();
    }
}
