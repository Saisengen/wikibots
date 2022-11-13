using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Web.UI;
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
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        var nss = new Dictionary<int, string>();
        string cont, query, apiout, result = "<center>\n";

        apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces").Result;
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ns")
                {
                    int ns = Convert.ToInt16(r.GetAttribute("id"));
                    if (ns % 2 == 0 || ns == 3)
                    {
                        r.Read();
                        nss.Add(ns, r.Value);
                    }
                }
        }
        nss.Remove(2);
        nss.Remove(-2);

        foreach (var n in nss.Keys)
        {
            var pageids = new HashSet<string>();
            var pagecounts = new Dictionary<string, Pair>();
            cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&list=allpages&format=xml&aplimit=max&apfilterredir=nonredirects&apnamespace=";
            while (cont != null)
            {
                apiout = (cont == "" ? site.GetStringAsync(query + n).Result : site.GetStringAsync(query + n + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                            pageids.Add(r.GetAttribute("pageid"));
                }
            }

            var requeststrings = new HashSet<string>();
            string idset = ""; int c = 0;
            foreach (var p in pageids)
            {
                idset += "|" + p;
                if (++c % 500 == 0)
                {
                    requeststrings.Add(idset.Substring(1));
                    idset = "";
                }
            }
            if (idset.Length != 0)
                requeststrings.Add(idset.Substring(1));

            foreach (var q in requeststrings)
                using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=info&format=xml&inprop=visitingwatchers%7Cwatchers&pageids=" + q).Result)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    while (r.Read())
                        if (r.GetAttribute("watchers") != null)
                        {
                            string title = r.GetAttribute("title");
                            if (n == 3)
                            {
                                if (title.Contains("/Архив"))
                                    continue;
                                title = title.Replace("Обсуждение участника:", "Участник:").Replace("Обсуждение участницы:", "Участница:");
                            }
                            int watchers = Convert.ToInt16(r.GetAttribute("watchers"));
                            string activewatchers = r.GetAttribute("visitingwatchers");
                            if (activewatchers == null)
                                activewatchers = "<30";
                            if (n == 0 && watchers >= 50 || n != 0)
                                pagecounts.Add(title, new Pair() { First = watchers, Second = activewatchers });
                        }
                }

            if (pagecounts.Count != 0)
            {
                result += "==" + (nss[n] == "" ? "Статьи" : (nss[n] == "Обсуждение участника" ? "Участник" : nss[n])) + "==\n{|class=\"standard sortable\"\n!Страница!!Всего следящих!!Активных\n";
                foreach (var p in pagecounts.OrderByDescending(p => p.Value.First))
                    result += "|-\n|[[:" + p.Key + "]]||" + p.Value.First + "||" + p.Value.Second + "\n";
                result += "|}\n";
            }
        }
        Save(site, "u:MBH/most watched pages", result, "");
    }
}
