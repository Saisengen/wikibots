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
        var site = Site(creds[0], creds[1]);
        var doc = new XmlDocument();
        var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var legal_redirs = new List<string>();

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Категория:Википедия:Намеренные перенаправления между СО&cmlimit=max").Result)))
            while (r.Read())
                if (r.Name == "cm")
                    legal_redirs.Add(r.GetAttribute("pageid"));
        
        foreach (int ns in new int[] { 1,3,5,7,9,11,13,15,101,103,105,107,829 })
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allredirects&arprop=title|ids&arnamespace=" + ns + "&arlimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); cont = r.GetAttribute("arcontinue");
                    while (r.Read())
                        if (r.Name == "r" && (ns != 3 || r.GetAttribute("title").Contains("/")))
                        {
                            int cntr = 0;
                            string id = r.GetAttribute("fromid");
                            using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&pageids=" + id + "&rvprop=ids&rvlimit=max").Result)))
                                while (rr.Read())
                                    if (rr.Name == "rev" && rr.NodeType == XmlNodeType.Element)
                                        cntr++;
                            if (!legal_redirs.Contains(id) && cntr == 1)
                            {
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
                                                    while (rrr.Read())
                                                        if (rrr.Name == "bl" && !rrr.GetAttribute("title").StartsWith("Википедия:Страницы с похожими названиями") && !rrr.GetAttribute("title").StartsWith("Участник:DvoreBot/Оставленные перенаправления"))
                                                            there_are_links = true;
                                                    if (!there_are_links)
                                                    {
                                                        var request = new MultipartFormDataContent();
                                                        request.Add(new StringContent("delete"), "action");
                                                        request.Add(new StringContent(id), "pageid");
                                                        request.Add(new StringContent("[[ВП:КБУ#П6|редирект между СО без ссылок]]"), "reason");
                                                        request.Add(new StringContent(token), "token");
                                                        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
                                                        if (!result.ToString().Contains("uccess"))
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
        }

        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=querypage&qppage=BrokenRedirects&qplimit=max").Result)))
            while (r.Read())
                if (r.Name == "page")
                {
                    string title = r.GetAttribute("title");
                    string ns = r.GetAttribute("ns");
                    if (ns != "2" || (ns == "2" && title.Contains("/")))
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
