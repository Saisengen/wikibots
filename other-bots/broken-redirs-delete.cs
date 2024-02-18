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
        var doc = new XmlDocument();
        var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=querypage&qppage=BrokenRedirects&qplimit=max").Result)))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("ns") != "2")
                {
                    string title = r.GetAttribute("title");
                    using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&titles=" + title + "&rvprop=ids&rvlimit=max").Result)))
                    {
                        int cntr = 0;
                        while (rr.Read())
                            if (rr.Name == "rev" && rr.NodeType == XmlNodeType.Element)
                                cntr++;
                        if (cntr == 1)
                        {
                            var request = new MultipartFormDataContent();
                            request.Add(new StringContent("delete"), "action");
                            request.Add(new StringContent(title), "title");
                            request.Add(new StringContent(token), "token");
                            result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
                            if (!result.ToString().Contains("uccess"))
                                Console.WriteLine(result);
                        }
                    }
                }                
    }
}
