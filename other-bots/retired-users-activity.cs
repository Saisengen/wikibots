using System;
using System.Collections.Generic;
using System.Linq;
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
        var retireds = new Dictionary<string, int>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        var initialusers = site.GetStringAsync("https://ru.wikipedia.org/wiki/User:MBH/users_for_last_activity_day_stats?action=raw").Result.Split('\n');
        foreach (var user in initialusers)
            if (!retireds.ContainsKey(user))
                retireds.Add(user, 1);
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=Шаблон:Участник покинул проект&einamespace=2%7C3&eilimit=max").Result)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                {
                    string user = r.GetAttribute("title");
                    if (!user.Contains("/"))
                        user = user.Substring(user.IndexOf(':') + 1, user.Length - user.IndexOf(':') - 1);
                    else
                        user = user.Substring(user.IndexOf(':') + 1, user.IndexOf("/") - user.IndexOf(':') - 1);
                    if (!retireds.ContainsKey(user))
                        retireds.Add(user, 1);
                }
        }

        foreach (var u in retireds.Keys.ToList())
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucuser=" + Uri.EscapeDataString(u)).Result)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                    {
                        string ts = r.GetAttribute("timestamp");
                        int y = Convert.ToInt32(ts.Substring(0, 4));
                        int m = Convert.ToInt32(ts.Substring(5, 2));
                        int d = Convert.ToInt32(ts.Substring(8, 2));
                        retireds[u] = (DateTime.Now - new DateTime(y, m, d)).Days;
                    }
            }

        string result = "{{#switch: {{{1}}}\n";
        foreach (var r in retireds.OrderBy(r => r.Value))
            result += "| " + r.Key + " = " + r.Value + "\n";
        Save(site, "Шаблон:Участник покинул проект/days", result + "| 0 }}", "");
    }
}
