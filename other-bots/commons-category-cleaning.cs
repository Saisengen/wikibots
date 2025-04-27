using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;

internal class Program
{
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://commons.wikimedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://commons.wikimedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword",
                password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
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
        request.Add(new StringContent("1"), "bot");
        result = site.PostAsync("https://commons.wikimedia.org/w/api.php", request).Result;
        if (!result.ToString().Contains("uccess"))
            Console.WriteLine(result);
    }
    static void Main()
    {
        var api_rgx = new Regex("<cl ns=\".*\" title=\"([^\"]*)\"");
        var catname = "Media needing categories (Cyrillic names)";
        var cat_rgx = new Regex(@"\[\[\s*category\s*:\s*" + catname.Replace("(", @"\(").Replace(")", @"\)") + @"\s*\]\]", RegexOptions.IgnoreCase);
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        string cont = "", query = "https://commons.wikimedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Category:" + catname + "&cmprop=title&cmlimit=max";
        while (cont != null)
            using (var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&cmcontinue=" + cont).Result)))
            {
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                while (r.Read())
                    if (r.Name == "cm")
                    {
                        string page = r.GetAttribute("title");
                        string apiout = site.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + Uri.EscapeDataString(page) + "&clshow=!hidden&cllimit=1").Result;
                        if (api_rgx.IsMatch(apiout))
                            try
                            {
                                string pagetext = site.GetStringAsync("https://commons.wikimedia.org/wiki/" + Uri.EscapeDataString(page) + "?action=raw").Result;
                                if (!cat_rgx.IsMatch(pagetext))
                                    Console.WriteLine("В [[:" + page + "]] не найдена категория");
                                else
                                    Save(site, page, pagetext.Replace(cat_rgx.Match(pagetext).Value, ""), "file has non-hidden [[" + api_rgx.Match(apiout).Groups[1].Value + "]]");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(page + '\n' + e.ToString());
                            }
                    }
            }
    }
}
