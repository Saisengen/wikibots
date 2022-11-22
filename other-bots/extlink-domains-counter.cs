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
        var links = new Dictionary<string, int>();
        var shortenedlinks = new Dictionary<string, int>();
        var pages = new Dictionary<string, string>();
        var requeststrings = new HashSet<string>();
        int c = 0; string testurl;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);

        string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allpages&format=xml&apfilterredir=nonredirects&aplimit=max&rawcontinue=";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                while (r.Read())
                    if (r.Name == "p")
                        if (!pages.ContainsKey(r.GetAttribute("pageid")))
                            pages.Add(r.GetAttribute("pageid"), r.GetAttribute("title"));
            }
        }

        string idset = "", id = "";
        query = "https://ru.wikipedia.org/w/api.php?action=query&prop=extlinks&format=xml&ellimit=max&pageids=";
        foreach (var p in pages.Keys)
        {
            idset += "|" + p;
            if (++c % 300 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        requeststrings.Add(idset.Substring(1));

        c = 0;
        foreach (var reqstring in requeststrings)
        {
            cont = "";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query + reqstring).Result : site.GetStringAsync(query + reqstring + "&eloffset=" + cont).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eloffset");
                    while (r.Read())
                    {
                        if (r.NodeType == XmlNodeType.Element && r.Name == "page" && r.GetAttribute("missing") == null)
                            id = r.GetAttribute("pageid");
                        if (r.NodeType == XmlNodeType.Element && r.Name == "el")
                        {
                            r.Read();
                            string link = r.Value;
                            link = link.Substring(link.IndexOf("//") + 2);
                            link = link.IndexOf("/") == -1 ? link : link.Substring(0, link.LastIndexOf("/"));
                            if (!links.ContainsKey(link))
                                links.Add(link, 1);
                            else
                                links[link]++;
                        }
                    }
                }
            }
        }

        foreach (var l in links.OrderByDescending(l => l.Value).ToArray())
        {
            testurl = (l.Key.StartsWith("www.") ? l.Key.Substring(4) : "www." + l.Key);
            if (shortenedlinks.ContainsKey(testurl))
                shortenedlinks[testurl] += l.Value;
            else
                shortenedlinks.Add(l.Key, l.Value);
        }
        string result = "\n{|class=\"standard\"\n!Место!!Число&nbsp;ссылок&nbsp;из&nbsp;рувики&nbsp;на!!style=\"text-align:left\"|данный сайт или его раздел";
        int counter = 0;
        foreach (var l in shortenedlinks.OrderByDescending(l => l.Value))
            if (l.Value < 100)
                break;
            else
                result += "\n|-\n|" + ++counter + "||" + l.Value + "||" + l.Key;
        Save(site, "user:MBH/most linked sites", result + "\n|}", "");
    }
}
