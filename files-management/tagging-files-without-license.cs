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
        var monthname = new string[13];
        monthname[1] = "января"; monthname[2] = "февраля"; monthname[3] = "марта"; monthname[4] = "апреля"; monthname[5] = "мая"; monthname[6] = "июня"; monthname[7] = "июля"; monthname[8] = "августа"; monthname[9] = "сентября"; monthname[10] = "октября"; monthname[11] = "ноября"; monthname[12] = "декабря";
        var autocatfiles = new HashSet<string>();
        var legalfiles = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        string cont, query, apiout;
        apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Категория:Файлы:Без машиночитаемой лицензии&cmprop=title&cmlimit=50").Result;
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                    autocatfiles.Add(r.GetAttribute("title"));
        }
        cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&list=embeddedin&format=xml&eititle=t:No_license&einamespace=6&eilimit=max";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&eicontinue=" + Uri.EscapeDataString(cont)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                        legalfiles.Add(r.GetAttribute("title"));
            }
        }

        bool atleast1file = false;
        autocatfiles.ExceptWith(legalfiles);
        foreach (var file in autocatfiles)
        {
            atleast1file = true;
            string pagetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(file) + "?action=raw").Result;
            Save(site, file, "{{subst:nld}}\n" + pagetext, "вынос на КБУ файла без валидной лицензии");
        }

        if (atleast1file)
        {
            var dt = DateTime.Now;
            Save(site, "К:Файлы:Неясный лицензионный статус от " + dt.Day + " " + monthname[dt.Month] + " " + dt.Year, "[[К:Файлы:Неясный лицензионный статус|" + dt.ToString("MM-dd") + "]]", "вынос на КБУ файла без валидной лицензии");
        }
    }
}
