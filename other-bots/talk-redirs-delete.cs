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

    static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[8], creds[9]);
        foreach (int ns in new int[] { 1,3,5,7,9,11,13,15,101,103,105,107,829 })
        {
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allredirects&arprop=title%7Cids&arnamespace=" + ns + "&arlimit=max").Result)))
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "r")
                    {
                        string id = r.GetAttribute("fromid");
                        using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + id + "&rvprop=ids&rvlimit=max").Result)))
                        {
                            rr.WhitespaceHandling = WhitespaceHandling.None;
                            while (rr.Read())
                                if (rr.Name == "rev")
                                {
                                    rr.Read();
                                    if (rr.NodeType == XmlNodeType.EndElement && rr.Name == "revisions")
                                        using (var rrr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=backlinks&blpageid=" + id).Result)))
                                        {
                                            rrr.WhitespaceHandling = WhitespaceHandling.None;
                                            bool there_are_links = false;
                                            string title = "";
                                            while (rrr.Read())
                                            {
                                                if (rrr.GetAttribute("title") != null)
                                                    title = rrr.GetAttribute("title");
                                                if (rrr.Name == "bl" && !title.StartsWith("Википедия:Страницы с похожими названиями") && !title.StartsWith("Участник:DvoreBot/Оставленные перенаправления"))
                                                    there_are_links = true;
                                            }
                                            if (!there_are_links)
                                            {
                                                var doc = new XmlDocument();
                                                var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
                                                if (!result.IsSuccessStatusCode)
                                                    return;
                                                doc.LoadXml(result.Content.ReadAsStringAsync().Result);
                                                var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
                                                var request = new MultipartFormDataContent();
                                                request.Add(new StringContent("delete"), "action");
                                                request.Add(new StringContent(id), "pageid");
                                                request.Add(new StringContent("[[ВП:КБУ#П6]]"), "reason");
                                                request.Add(new StringContent(token), "token");
                                                result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
                                                if (result.ToString().Contains("uccess"))
                                                    Console.WriteLine("deleted " + title);
                                                else
                                                    Console.WriteLine(result);
                                            }
                                        }
                                    break;
                                }
                        }
                    }
        }
    }
}
