using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;

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
        var days = new Dictionary<string, int>();
        var edits = new Dictionary<string, int>();
        var itemrgx = new Regex("<item");
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);

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
        foreach (var v in edits.OrderBy(v => v.Value))
            if (v.Value > 0)
                result += "|" + v.Key + "=" + (v.Value == 0 ? "" : v.Value.ToString()) + "\n";
        Save(site, "Шаблон:User activity stats/edits", result + "|}}", "");
    }
}
