using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
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
        var pagesrgx = new Regex(@"equals_to_any\(page_prefixedtitle,\s*(\'.*\')\s*\)\\?r?\\n&");
        var prefixrgx = new Regex(@"MediaWiki:Editnotice-(\d)-");
        string[] separstrings = { "','", "' ,'", "' , '", "', '" };

        var rawfilterpages = pagesrgx.Match(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=json&list=abusefilters&utf8=1&abfstartid=146&abflimit=1&abfprop=pattern").Result).Groups[1].ToString().Split(separstrings, StringSplitOptions.RemoveEmptyEntries);
        var pagesinfilter = new HashSet<string>();
        for (int i = 0; i < rawfilterpages.Length; i++)
        {
            if (i == 0)
                pagesinfilter.Add(rawfilterpages[i].Substring(1).Replace('/', '-'));
            else if (i == rawfilterpages.Length - 1)
                pagesinfilter.Add(rawfilterpages[i].Substring(0, rawfilterpages[i].Length - 1).Replace('/', '-'));
            else pagesinfilter.Add(rawfilterpages[i].Replace('/', '-'));
        }

        var pageswithtemplate = new HashSet<string>();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=Шаблон:Editnotice/АПАТ&eilimit=max").Result)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.Name == "ei" && prefixrgx.IsMatch(r.GetAttribute("title")))
                    pageswithtemplate.Add(prefixrgx.Replace(r.GetAttribute("title"), ""));
        }
        foreach (var page in pagesinfilter)
            if (!pageswithtemplate.Contains(page))
                try
                {
                    string notice = site.GetStringAsync("https://ru.wikipedia.org/wiki/MediaWiki:Editnotice-0-" + Uri.EscapeDataString(page) + "?action=raw").Result;
                    Save(site, "MediaWiki:Editnotice-0-" + page, notice + "\n{{Editnotice/АПАТ}}", "статья защищена до апатов [[special:abusefilter/146|146-м фильтром]]");
                }
                catch
                {
                    Save(site, "MediaWiki:Editnotice-0-" + page, "{{Editnotice/АПАТ}}", "статья защищена до апатов [[special:abusefilter/146|146-м фильтром]]");
                }
        foreach (var page in pageswithtemplate)
            if (!pagesinfilter.Contains(page))
                try
                {
                    string notice = site.GetStringAsync("https://ru.wikipedia.org/wiki/MediaWiki:Editnotice-0-" + Uri.EscapeDataString(page) + "?action=raw").Result.Replace("{{Editnotice/АПАТ}}", "");
                    if (notice == "")
                        Save(site, "MediaWiki:Editnotice-0-" + page, "{{#ifeq:{{NAMESPACENUMBER}}|8|{{db|нотис не нужен, статья удалена из фильтра}}}}", "статья удалена из [[special:abusefilter/146|146-го фильтра]]");
                    else
                        Save(site, "MediaWiki:Editnotice-0-" + page, notice, "статья удалена из [[special:abusefilter/146|146-го фильтра]]");
                }
                catch { continue; }
    }
}
