using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;
using static System.Net.WebRequestMethods;

class record
{
    public string oldtitle, newtitle, user, timestamp, comment, title;
}
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
        var catnames = new HashSet<string>();
        var table = new List<record>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&letype=move&lenamespace=14&lelimit=max").Result)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                {
                    bool nonempty = false;
                    string title = r.GetAttribute("title");
                    using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + Uri.EscapeDataString(title)).Result)))
                    {
                        rr.WhitespaceHandling = WhitespaceHandling.None;
                        while (rr.Read())
                            if (rr.NodeType == XmlNodeType.Element && rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0")
                                nonempty = true;
                    }
                    if (nonempty)
                    {
                        var n = new record { oldtitle = title, user = r.GetAttribute("user"), timestamp = r.GetAttribute("timestamp").Substring(0, 10), comment = r.GetAttribute("comment") };
                        r.Read();
                        n.newtitle = r.GetAttribute("target_title");
                        table.Add(n);
                    }
                }
        }
        string result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Откуда (страниц в категории)!!Куда (страниц в категории)!!Юзер!!Коммент";
        foreach (var t in table)
            result += "\n|-\n|" + t.timestamp + "||[[:" + t.oldtitle + "]] ({{PAGESINCATEGORY:" + t.oldtitle.Substring(10) + "}})||[[:" + t.newtitle + "]] ({{PAGESINCATEGORY:" + t.newtitle.Substring(10) +
                "}})||[[u:" + t.user + "]]||" + t.comment;
        result += "\n|}";
        Save(site, "u:MBH/Переименованные категории с недоперенесёнными страницами", result, "");
        catnames.Clear();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&leaction=delete/delete&lenamespace=14&lelimit=max").Result)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                {
                    bool nonempty = false;
                    string title = r.GetAttribute("title");
                    using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + Uri.EscapeDataString(title)).Result)))
                    {
                        rr.WhitespaceHandling = WhitespaceHandling.None;
                        while (rr.Read())
                            if (rr.NodeType == XmlNodeType.Element && rr.Name == "page" && rr.GetAttribute("missing") != null)
                            {
                                rr.Read();
                                if (rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0")
                                    nonempty = true;
                            }
                    }
                    if (nonempty)
                        try
                        {
                            var n = new record { title = title, user = r.GetAttribute("user"), timestamp = r.GetAttribute("timestamp").Substring(0, 10) };
                            string comment = r.GetAttribute("comment").Replace("[[К", "[[:К");
                            n.comment = (comment.Contains("}}") ? "<nowiki>" + comment + "</nowiki>" : comment);
                            table.Add(n);
                        }
                        catch
                        {
                            continue;
                        }
                }
        }
        result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Имя (страниц в категории)!!Юзер!!Коммент";
        foreach (var t in table)
            if (t.title != null)
                result += "\n|-\n|" + t.timestamp + "||[[:" + t.title + "]] ({{PAGESINCATEGORY:" + t.title.Substring(10) + "}})||[[u:" + t.user + "]]||" + t.comment;
        result += "\n|}";
        Save(site, "u:MBH/Удалённые категории со страницами", result, "");
    }
}
