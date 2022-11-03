using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System;
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
    static void Save(HttpClient site, string lang, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
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
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result;
        Console.WriteLine(DateTime.Now.ToString() + " writing " + lang + ":" + title);
    }
    static void Main()
    {
        var rgx = new Regex(@"\{\{\s*([Тт]екущие события|[Рр]едактирую|[Сс]вязь с текущим событием)[^{}]*\}\}");
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site("ru", creds[0], creds[1]);

        foreach (string cat in new string[] { "Категория:Википедия:Статьи с просроченным шаблоном текущих событий", "Категория:Википедия:Просроченные статьи, редактируемые прямо сейчас" })
        {
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=" + cat + "&cmlimit=max").Result)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                    {
                        string title = r.GetAttribute("title");
                        string text = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + title + "?action=raw").Result;
                        Save(site, "ru", title, rgx.Replace(text, ""), "удалены просроченные шаблоны");
                    }
            }
        }
    }
}
