using System;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;

class Program
{
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
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
        Console.WriteLine(DateTime.Now.ToString() + " written " + title);
    }

    static void Main()
    {
        string cont, query, apiout;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site("ru", creds[0], creds[1]);

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
                        filename = filename.Replace(@"\ ", "[ _]");
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
}
