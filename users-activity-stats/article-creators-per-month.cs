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
        var articleids = new HashSet<string>();
        var redirs = new HashSet<string>();
        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        var lastmonth = DateTime.Now.AddMonths(-1);
        var creators = new Dictionary<string, int>();
        string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=ids&letype=create&leend=" + lastmonth.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lestart=" + DateTime.Now.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lenamespace=0&lelimit=5000";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&lecontinue=" + cont).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item")
                        if (!articleids.Contains(r.GetAttribute("pageid")))
                            articleids.Add(r.GetAttribute("pageid"));
            }
        }
        var requeststrings = new HashSet<string>();
        string idset = ""; int cntr = 0;
        foreach (var i in articleids)
        {
            idset += "|" + i;
            if (++cntr % 500 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        if (idset.Length != 0)
            requeststrings.Add(idset.Substring(1));

        foreach(var s in requeststrings)
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=info&pageids=" + s).Result)))
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("redirect") == "")
                        redirs.Add(r.GetAttribute("pageid"));

        cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=ids|user&letype=create&leend=" + lastmonth.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lestart=" + DateTime.Now.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lenamespace=0&lelimit=5000";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&lecontinue=" + cont).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item" && !redirs.Contains(r.GetAttribute("pageid")))
                    {
                        string user = r.GetAttribute("user");
                        if (user != null)
                        {
                            if (creators.ContainsKey(user))
                                creators[user]++;
                            else
                                creators.Add(user, 1);
                        }
                    }
            }
        }
        string result = "<center>\n{|class=\"standard\"\n!Участник!!Создал статей за последний месяц";
        foreach (var p in creators.OrderByDescending(p => p.Value))
        {
            if (p.Value < 10) break;
            result += "\n|-\n|[[u:" + p.Key + "]]||" + p.Value;
        }
        Save(site, "u:MBH/best article creators", result + "\n|}", "");
    }
}
