using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System;
using System.Security.Policy;
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
        var pairs = new Dictionary<string, int>();
        var thankedusers = new Dictionary<string, int>();
        var thankingusers = new Dictionary<string, int>();
        var ratio = new Dictionary<string, double>();
        string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp&letype=thanks&lelimit=max";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);

        while (cont != null)
        {
            if (cont == "") apiout = site.GetStringAsync(query).Result; else apiout = site.GetStringAsync(query + "&lecontinue=" + cont).Result;
            using (var rdr = new XmlTextReader(new StringReader(apiout)))
            {
                rdr.WhitespaceHandling = WhitespaceHandling.None;
                rdr.Read(); rdr.Read(); rdr.Read(); cont = rdr.GetAttribute("lecontinue");
                while (rdr.Read())
                    if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "item")
                    {
                        string source = rdr.GetAttribute("user");
                        if (source == "Erokhin")
                            continue;
                        string target = rdr.GetAttribute("title");
                        if (target != null && source != null)
                        {
                            if (thankingusers.ContainsKey(source))
                                thankingusers[source]++;
                            else
                                thankingusers.Add(source, 1);
                            target = target.Substring(target.IndexOf(":") + 1);
                            if (thankedusers.ContainsKey(target))
                                thankedusers[target]++;
                            else
                                thankedusers.Add(target, 1);
                            string pair = source + " → " + target;
                            if (pairs.ContainsKey(pair))
                                pairs[pair]++;
                            else
                                pairs.Add(pair, 1);
                        }
                    }
            }
        }
        int c1 = 0, c2 = 0, c3 = 0;
        string result = "{{Плавающая шапка таблицы}}<center>См. также https://mbh.toolforge.org/likes.cgi\n{|\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!Место!!Участник!!Число выданных лайков";
        foreach (var p in thankingusers.OrderByDescending(p => p.Value))
            if (++c1 <= 2000)
                result += "\n|-\n|" + c1 + "||{{u|" + p.Key + "}}||" + p.Value;
            else
                break;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!Место!!Направление!!Число лайков";
        foreach (var p in pairs.OrderByDescending(p => p.Value))
            if (++c2 <= 2000)
                result += "\n|-\n|" + c2 + "||" + p.Key + "||" + p.Value;
            else
                break;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!Место!!Участник!!Число полученных лайков";
        foreach (var p in thankedusers.OrderByDescending(p => p.Value))
            if (++c3 <= 2000)
                result += "\n|-\n|" + c3 + "||{{u|" + p.Key + "}}||" + p.Value;
            else
                break;
        Save(site, "u:MBH/Лайки", result + "\n|}\n|}", "");
    }
}
