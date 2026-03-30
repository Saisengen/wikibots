using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Xml;
class page
{
    public string title;
    public int oldsize, newsize;
    public float times;
}
class Program
{
    static void Sendresponse(string inwikiproject, int startyear, int endyear, string result)
    {
        Console.WriteLine(new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "resized-pages.html")).ReadToEnd().Replace("%result%", result)
            .Replace("%inwikiproject%", inwikiproject).Replace("%startyear%", startyear.ToString()).Replace("%endyear%", endyear.ToString()));
    }
    static HttpClient login(string lang, string login, string password, string ua)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", ua);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument(); doc.LoadXml(result.Content
            .ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value; result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new
                FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result; return client;
    }
    static void Main()
    {
        var creds = new StreamReader(Path.Combine(Environment.GetEnvironmentVariable("TOOL_DATA_DIR"), "p")).ReadToEnd().Split('\n');
        var site = login("ru", creds[0], creds[1], creds[3]);
        string get = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (get == "")
            Sendresponse("", DateTime.Now.Year - 1, DateTime.Now.Year, "");
        var parameters = HttpUtility.ParseQueryString(get);
        string inwikiproject = parameters[0];
        int startyear = Convert.ToInt32(parameters[1]);
        int endyear = Convert.ToInt32(parameters[2]);
        if (endyear < startyear) {
            Sendresponse("", DateTime.Now.Year - 1, DateTime.Now.Year, "Конечный год не должен быть больше начального");
            return;
        }
        var pages = new List<page>();
        string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Категория:Статьи проекта " + inwikiproject + "&cmprop=title&cmnamespace=1&cmtype=page&cmlimit=max";
        while (cont != null) {
            string apiout = cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&cmcontinue=" + Uri.EscapeDataString(cont)).Result;
            using (var r = new XmlTextReader(new StringReader(apiout))) {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                    {
                        var title = r.GetAttribute("title");
                        var p = new page() { title = title.Substring(title.IndexOf(':') + 1) };
                        pages.Add(p);
                    }
            }
        }
        foreach (var p in pages) {
            string apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=revisions&format=xml&rvprop=size&rvlimit=1&rvstart=" + startyear + "-01-01T00:00:00Z&titles=" +
                Uri.EscapeDataString(p.title)).Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "rev")
                        p.oldsize = Convert.ToInt32(r.GetAttribute("size"));

            }
            if (p.oldsize != 0) {
                apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=revisions&format=xml&rvprop=size&rvlimit=1&rvstart=" + endyear + "-01-01T00:00:00Z&titles=" +
                    Uri.EscapeDataString(p.title)).Result;
                using (var r = new XmlTextReader(new StringReader(apiout))) {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    while (r.Read())
                        if (r.NodeType == XmlNodeType.Element && r.Name == "rev")
                            p.newsize = Convert.ToInt32(r.GetAttribute("size"));
                }
            }
            p.times = (float)p.newsize / p.oldsize;
        }

        string result = "<table border=\"1\" cellspacing=\"0\"><tr><th>Статья</th><th>Изменила размер во столько раз</th><th>На столько байт</th></tr>\n";
        foreach (var u in pages.OrderByDescending(u => u.times))
            if (u.oldsize != 0 && u.oldsize != u.newsize)
                result += "<tr><td><a href=\"https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(u.title) + "\">" + u.title + "</a></td><td>" + u.times + "</td><td>" + (u.newsize - u.oldsize) +
                    "</td></tr>\n";
        Sendresponse(inwikiproject, startyear, endyear, result + "</table>");
    }
}
