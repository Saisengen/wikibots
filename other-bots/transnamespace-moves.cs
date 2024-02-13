using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;

class record
{
    public string oldtitle, oldns, newtitle, user, date, comment;
}
class Program
{
    static HttpClient Site(string login, string password, string lang)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(string lang, HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
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
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result;
        if (result.ToString().Contains("uccess"))
            Console.WriteLine(DateTime.Now.ToString() + " written " + title);
        else
            Console.WriteLine(result);
    }
    static void Main()
    {
        var table = new List<record>();
        var apatusers = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var header = new Dictionary<string, string>() { 
            { "ru", "<center>{{Плавающая шапка таблицы}}{{shortcut|ВП:TRANSMOVE}}Красным выделены неавтопатрулируемые.{{clear}}\n{|class=\"standard sortable ts-stickytableheader\"\n!Дата!!Источник!!Название в ОП!!Переносчик!!Коммент" },
            { "en", "<center>Красным выделены неавтопатрулируемые.\n{|class=\"wikitable sortable\"\n!Дата!!Источник!!Название в ОП!!Переносчик!!Коммент" } };
        foreach (var lang in new string[] { "ru", "en"})
        {
            var site = Site(creds[0], creds[1], lang);
            string apiout = site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title|type|user|timestamp|comment|details&letype=move&lelimit=max").Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                    {
                        string user = r.GetAttribute("user");
                        if (user.StartsWith("IncubatorBot"))
                            continue;
                        if (!apatusers.Contains(user))
                            using (var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=users&usprop=rights&ususers=" + user).Result)))
                                while (rr.Read())
                                    if (rr.Value == "autoreview")
                                        apatusers.Add(user);
                        string oldns = r.GetAttribute("ns");
                        if (oldns == "0")
                            continue;
                        string oldtitle = r.GetAttribute("title");
                        string date = r.GetAttribute("timestamp").Substring(5, 5);
                        string comment = r.GetAttribute("comment");
                        if (comment != null)
                            comment = Uri.UnescapeDataString(comment);
                        r.Read();
                        string newns = r.GetAttribute("target_ns");
                        if (newns != "0")
                            continue;
                        string newtitle = r.GetAttribute("target_title");
                        table.Add(new record() { oldtitle = oldtitle, oldns = oldns, newtitle = newtitle, user = user, date = date, comment = comment });
                    }
            }
            string result = header[lang];
            foreach (var t in table)
            {
                string comment;
                if (t.comment.Contains("{|") || t.comment.Contains("|}") || t.comment.Contains("||") || t.comment.Contains("|-"))
                    comment = "<nowiki>" + t.comment + "</nowiki>";
                else
                    comment = t.comment;
                result += "\n|-" + (apatusers.Contains(t.user) ? "" : "style=\"background-color:#fcc\"") + "\n|" + t.date + "||[[:" + t.oldtitle + "|" + t.oldtitle + "]]||[[:" + t.newtitle + "]]||{{u|" + t.user + "}}||" + comment;
            }
            Save(lang, site, lang == "ru" ? "ВП:Страницы, перенесённые в пространство статей" : "user:Кронас/transnamespace moves", result + "\n|}", "");
            table.Clear();
        }
    }
}
