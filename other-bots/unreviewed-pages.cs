using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;

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
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        var nsnames = new Dictionary<int, string>() { { 0, "Статьи" }, { 6, "Файлы" }, { 10, "Шаблоны" }, { 14, "Категории" }, { 100, "Порталы" }, { 828, "Модули" } };
        string result = "";
        foreach (var ns in nsnames.Keys)
            foreach (string type in new string[] {"nonredirects", "redirects" })
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
}
