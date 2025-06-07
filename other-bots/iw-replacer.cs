using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using MySql.Data.MySqlClient;
class dis_data
{
    public Dictionary<string, bool> disambs;
    public string dis_cat_name;
    public bool nocat;
}
class Program
{
    static HttpClient site = new HttpClient();
    static Dictionary<string, Dictionary<string, bool>> redirects;
    static Dictionary<string, dis_data> disambs;
    static string[] creds;
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
        var request = new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" }, { new StringContent(text), "text" }, { new StringContent(comment), "summary" },
            { new StringContent(token), "token" } };
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
        if (!result.ToString().Contains("uccess"))
            Console.WriteLine(result.ToString());
    }
    static string e(string input)
    {
        return Uri.EscapeDataString(input);
    }
    static string readpage(string lang, string input)
    {
        return site.GetStringAsync("https://" + lang + ".wikipedia.org/wiki/" + e(input) + "?action=raw").Result;
    }
    static bool isRedirect(string lang, string input)
    {
        if (redirects[lang].ContainsKey(input))
            return redirects[lang][input];
        else if (site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(input)).Result.Contains("redirect="))
        {
            redirects[lang].Add(input, true);
            return true;
        }
        else
        {
            redirects[lang].Add(input, false);
            return false;
        }
    }
    static bool isDisambig(string lang, string input)
    {
        if (!disambs.ContainsKey(lang))
        {
            var connect = new MySqlConnection(creds[2].Replace("%project%", "wikidatawiki"));
            connect.Open();
            var query = new MySqlCommand("select * from wb_items_per_site where ips_item_id=9700479 and ips_site_id=\"" + lang + "wiki\";", connect);//https://www.wikidata.org/wiki/Q9700479
            MySqlDataReader r = query.ExecuteReader();
            if (r.Read())
                disambs.Add(lang, new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = r.GetString("ips_site_page"), nocat = false });
            else
            {
                query = new MySqlCommand("select * from wb_items_per_site where ips_item_id=1982926 and ips_site_id=\"" + lang + "wiki\";", connect);//https://www.wikidata.org/wiki/Q1982926
                r = query.ExecuteReader();
                if (r.Read())
                    disambs.Add(lang, new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = r.GetString("ips_site_page"), nocat = false });
                else
                    disambs.Add(lang, new dis_data() { nocat = true });
            }            
        }
        if (disambs[lang].nocat)
            return false;
        else
        {
            if (disambs[lang].disambs.ContainsKey(input))
                return disambs[lang].disambs[input];
            else if (site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=" + e(disambs[lang].dis_cat_name)).Result.Contains("<c"))
            {
                disambs[lang].disambs.Add(input, true);
                return true;
            }
            else
            {
                disambs[lang].disambs.Add(input, false);
                return false;
            }
        }
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = Site(creds[0], creds[1]); var iw1 = new Regex(@"\{\{ *([нН]е переведено [1345]|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\}\}");
        var iw2 = new Regex(@"\{\{ *([нН]е переведено [1345]|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\| *\| *\| *([^{}|]*) *\}\}");
        var iw3 = new Regex(@"\{\{ *([нН]е переведено [1345]|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\}\}");
        var needed_articles = new Dictionary<string, int>();
        //string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=Ш:Не переведено&eilimit=max";
        //while (cont != null)
        //    using (var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&eicontinue=" + e(cont)).Result)))
        //    {
        //        r.WhitespaceHandling = WhitespaceHandling.None;
        //        r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
        //        while (r.Read())
        //            if (r.Name == "ei")
        //            {
        //                string pagename = r.GetAttribute("title");
        //            }
        //    }
        disambs = new Dictionary<string, dis_data>() { { "en", new dis_data() { disambs = new Dictionary <string, bool>(), dis_cat_name = "All disambiguation pages" } },
        { "ru", new dis_data() { disambs = new Dictionary <string, bool>(), dis_cat_name = "Страницы значений по алфавиту" } } };
        redirects = new Dictionary<string, Dictionary<string, bool>> { { "en", new Dictionary<string, bool>() }, { "ru", new Dictionary<string, bool>() } };
        int c = 0;
        //foreach (string processed_page in new StreamReader("iw1.txt").ReadToEnd().Replace("\r", "").Split('\n'))
        //{
        //    if (++c % 200 == 0)
        //        Console.WriteLine(c + "/35000 processed");
        //    string processed_page_text;
        //    try
        //    {
        //        processed_page_text = readpage("ru", processed_page);
        //    }
        //    catch { continue; }
        //    string newtext = processed_page_text; string comment = "";
        //    foreach (Match m in iw1.Matches(processed_page_text))
        //    {
        //        string iw_pagename = m.Groups[2].Value;
        //        try
        //        {
        //            string test = readpage("ru", iw_pagename);
        //            if (!isRedirect("en", iw_pagename) && !isRedirect("ru", iw_pagename) && !dis_data_list["en"].disambs.Contains(iw_pagename) && !dis_data_list["ru"].disambs.Contains(iw_pagename))
        //            {
        //                string text_in_article_for_replacement = m.Groups[0].Value;
        //                var replacement_rgx = new Regex(Regex.Escape(text_in_article_for_replacement));
        //                comment += ", " + text_in_article_for_replacement + "->[[" + iw_pagename + "]]";
        //                newtext = replacement_rgx.Replace(newtext, "[[" + iw_pagename + "]]");
        //            }
        //        }
        //        catch
        //        {
        //            if (needed_articles.ContainsKey(iw_pagename))
        //                needed_articles[iw_pagename]++;
        //            else
        //                needed_articles.Add(iw_pagename, 1);
        //        }
        //    }
        //    if (newtext != processed_page_text)
        //        Save(site, processed_page, newtext, comment.Substring(2));
        //}
        //foreach (string processed_page in new StreamReader("iw2.txt").ReadToEnd().Replace("\r", "").Split('\n'))
        //{
        //    if (++c % 200 == 0)
        //        Console.WriteLine(c + "/35500 processed");
        //    string processed_page_text;
        //    try
        //    {
        //        processed_page_text = readpage("ru", processed_page);
        //    }
        //    catch { continue; }
        //    string newtext = processed_page_text; string comment = "";
        //    foreach (Match m in iw2.Matches(processed_page_text))
        //    {
        //        string iw_pagename = m.Groups[3].Value;
        //        string ru_pagename = m.Groups[2].Value;
        //        try
        //        {
        //            string test = readpage("ru", ru_pagename);
        //            if (!isRedirect("en", iw_pagename) && !isRedirect("ru", ru_pagename) && !dis_data_list["en"].disambs.Contains(iw_pagename) && !dis_data_list["ru"].disambs.Contains(ru_pagename))
        //            {
        //                string text_in_article_for_replacement = m.Groups[0].Value;
        //                var replacement_rgx = new Regex(Regex.Escape(text_in_article_for_replacement));
        //                comment += ", " + text_in_article_for_replacement + "->[[" + ru_pagename + "]]";
        //                newtext = replacement_rgx.Replace(newtext, "[[" + ru_pagename + "]]");
        //            }
        //        }
        //        catch
        //        {
        //            string key = "[[" + ru_pagename + "]] ([[:en:" + iw_pagename + "]])";
        //            if (needed_articles.ContainsKey(iw_pagename))
        //                needed_articles[iw_pagename]++;
        //            else
        //                needed_articles.Add(iw_pagename, 1);
        //        }
        //    }
        //    if (newtext != processed_page_text)
        //    {
        //        Save(site, processed_page, newtext, comment.Substring(2));
        //        Console.WriteLine(processed_page + '\t' + comment.Substring(2));
        //    }
        //}
        foreach (string processed_page in new StreamReader("iw3.txt").ReadToEnd().Replace("\r", "").Split('\n'))
        {
            if (++c % 10000 == 0)
                Console.WriteLine(c + "/263000 processed");
            string processed_page_text;
            try
            {
                processed_page_text = readpage("ru", processed_page);
            }
            catch { continue; }
            string newtext = processed_page_text; string comment = "";
            foreach (Match m in iw2.Matches(processed_page_text))
            {
                string ru_pagename = m.Groups[2].Value;
                string visible_text = m.Groups[3].Value;
                string lang = m.Groups[4].Value;
                string iw_pagename = m.Groups[5].Value;
                try
                {
                    string test = readpage("ru", ru_pagename);
                    if (!isRedirect(lang, iw_pagename) && !isRedirect("ru", ru_pagename) && !isDisambig(lang, iw_pagename) && !isDisambig("ru", ru_pagename))
                    {
                        string text_in_article_for_replacement = m.Groups[0].Value;
                        var replacement_rgx = new Regex(Regex.Escape(text_in_article_for_replacement));
                        comment += ", " + text_in_article_for_replacement + "->[[" + ru_pagename + "]]";
                        newtext = replacement_rgx.Replace(newtext, "[[" + ru_pagename + "]]");
                    }
                }
                catch
                {
                    string key = "[[" + ru_pagename + "]] ([[:" + lang + ":" + iw_pagename + "]])";
                    if (needed_articles.ContainsKey(key))
                        needed_articles[key]++;
                    else
                        needed_articles.Add(key, 1);
                }
            }
            if (newtext != processed_page_text)
                Save(site, processed_page, newtext, comment.Substring(2));
        }
        string result = "<center>\n{|class=standard\n!Статья!!Ссылок на неё";
        foreach (var article_data in needed_articles.OrderByDescending(t => t.Value))
        {
            if (article_data.Value < 5)
                break;
            result += "\n|-\n|" + article_data.Key + "||" + article_data.Value;
        }
        Save(site, "ВП:К созданию/Из шаблонов \"не переведено\"", result + "\n|}", "");
    }
}
