using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Xml;

class Program
{
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
        if (result.ToString().Contains("uccess"))
            Console.WriteLine(DateTime.Now.ToString() + " written " + title);
        else
            Console.WriteLine(result);
    }
    static void Main()
    {
        var year = DateTime.Now.Year;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
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
}
