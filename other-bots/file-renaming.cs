using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

class Program
{
    static HttpClient site;
    static HttpClient login(string login, string password, string ua) {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", ua);
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument(); doc.LoadXml(result.Content
            .ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value; result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new
                FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result; return client;
    }
    static void save(string title, string text, string comment) {
        var doc = new XmlDocument(); var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result; if (!result.IsSuccessStatusCode) return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" },
            { new StringContent(text), "text" }, { new StringContent(comment), "summary" }, { new StringContent(token), "token" } }).Result;
    }
    public static void Main() {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = login(creds[0], creds[1], creds[3]);
        while (true) {
            var apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=title|user|comment|details&letype=move&lenamespace=6&lelimit=25").Result;
            var r = new XmlTextReader(new StringReader(apiout));
            while (r.Read())
                if (r.Name == "item" && r.NodeType == XmlNodeType.Element) {
                    string user = r.GetAttribute("user");
                    if (user == "Atsirbot")
                        continue;
                    string filename = r.GetAttribute("title").Replace("Файл:", ""); string comment = r.GetAttribute("comment") == null ? "" : r.GetAttribute("comment"); r.Read();
                    string newname = r.GetAttribute("target_title").Replace("Файл:", "");
                    var r2 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=imageusage&iutitle=file:" + Uri.EscapeUriString(filename) + "&format=xml").Result));
                    while (r2.Read())
                        if (r2.Name == "iu") {
                            string pagename = r2.GetAttribute("title"); string pagetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(pagename) + "?action=raw").Result;
                            Regex filename_rgx = new Regex(@"([\n=:|] *)" + Regex.Escape(filename), RegexOptions.IgnoreCase);
                            foreach (Match match in filename_rgx.Matches(pagetext))
                                pagetext = pagetext.Replace(match.ToString(), match.Groups[1].Value + newname);
                            save(pagename, pagetext, "[[file:" + filename + "]] переименован [[u:" + user + "]] в [[file:" + newname + "]]" + " (" + comment + ")");
                        }
                }
            Thread.Sleep(7000);
        }
    }
}
