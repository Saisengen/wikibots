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
    static Dictionary<string, bool> AfDpages;
    static string[] creds;
    static string lang, ru_pagename, iw_pagename, new_ru_pagename;
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
        result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password },
            { "lgtoken", logintoken }, { "format", "xml" } })).Result;
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
        var request = new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" }, { new StringContent(text), "text" }, { new StringContent("1"), "bot" },
            { new StringContent(comment), "summary" }, { new StringContent(token), "token" } };
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
        if (lang == "d")
            return false;
        if (!redirects.ContainsKey(lang))
            redirects.Add(lang, new Dictionary<string, bool>());
        try
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
        catch
        {
            Console.WriteLine("error in isRed for " + lang + " and " + input);
            return false;
        }
    }
    static bool isDisambig(string lang, string input)
    {
        if (lang == "d")
            return false;
        if (!disambs.ContainsKey(lang))
        {
            var connect = new MySqlConnection(creds[2].Replace("%project%", "wikidatawiki"));
            connect.Open();
            var query = new MySqlCommand("select cast(ips_site_page as char) p from wb_items_per_site where ips_item_id=9700479 and ips_site_id=\"" + lang + "wiki\";", connect);//https://www.wikidata.org/wiki/Q9700479
            var r = query.ExecuteReader();
            if (r.Read())
                disambs.Add(lang, new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = r.GetString("p"), nocat = false });
            else
            {
                query = new MySqlCommand("select cast(ips_site_page as char) p from wb_items_per_site where ips_item_id=1982926 and ips_site_id=\"" + lang + "wiki\";", connect);//https://www.wikidata.org/wiki/Q1982926
                r.Close();
                r = query.ExecuteReader();
                if (r.Read())
                    disambs.Add(lang, new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = r.GetString("p"), nocat = false });
                else
                    disambs.Add(lang, new dis_data() { nocat = true });
            }
            r.Close(); connect.Close();
        }
        if (disambs[lang].nocat)
        {
            Console.WriteLine(lang + "wiki has no dis category");
            return false;
        }
        else try
        {
            if (disambs[lang].disambs.ContainsKey(input))
                return disambs[lang].disambs[input];
            else if (site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=" + e(disambs[lang].dis_cat_name)).Result.Contains("<c"))
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
            catch
            {
                Console.WriteLine("error in isDis for " + lang + " and " + input);
                return false;
            }
    }
    static bool onAfD(string input)
    {
        if (AfDpages.ContainsKey(input))
            return AfDpages[input];
        else
        {
            if (site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=К:Википедия:Кандидаты на удаление").Result.Contains("<c") ||
                site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=К:Википедия:К быстрому удалению").Result.Contains("<c"))
            {
                AfDpages.Add(input, true);
                return true;
            }
            else
            {
                AfDpages.Add(input, false);
                return false;
            }
        }
    }
    static bool ruPageExist()
    {
        bool sql = false, answer;
        try
        {
            if (!site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(ru_pagename)).Result.Contains("_idx=\"-1\""))
                return true;
            else
            {
                sql = true;
                var connect = new MySqlConnection(creds[2].Replace("%project%", "wikidatawiki"));
                connect.Open();
                var query = new MySqlCommand("select cast(ips_site_page as char) p from wb_items_per_site where ips_site_id=\"ruwiki\" and ips_item_id=(select ips_item_id from wb_items_per_site where " +
                    "ips_site_id=\"" + lang + "wiki\" and ips_site_page=\"" + iw_pagename + "\");", connect);
                var r = query.ExecuteReader();
                if (r.Read())
                {
                    new_ru_pagename = r.GetString("p");
                    answer = true;
                }
                else answer = false;
                r.Close(); connect.Close(); return answer;
            }
        }
        catch
        {
            Console.WriteLine((sql ? "sql" : "api") + " error in checking presence of " + ru_pagename);
            return false;
        }
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = Site(creds[0], creds[1]); //var iw1 = new Regex(@"\{\{ *([нН]е переведено [1345]|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\}\}");
        var iw3 = new Regex(@"\{\{ *([нН]е переведено \d|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\}\}");
        var needed_articles = new Dictionary<string, int>(); string cont, query; int c = 0;
        disambs = new Dictionary<string, dis_data>(); redirects = new Dictionary<string, Dictionary<string, bool>>(); AfDpages = new Dictionary<string, bool>();
        foreach (string template in "Не переведено 2|Не переведено".Split('|'))
        {
            cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=Ш:" + template + "&eilimit=max";
            while (cont != null)
                using (var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&eicontinue=" + e(cont)).Result)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                    while (r.Read())
                        if (r.Name == "ei")
                        {
                            string processed_page = r.GetAttribute("title");
                            if (++c % 5000 == 0)
                                Console.WriteLine(c + "/360000 processed");
                            string processed_page_text;
                            try { processed_page_text = readpage("ru", processed_page); } catch { continue; }
                            string newtext = processed_page_text; string comment = "";
                            foreach (Match m in iw3.Matches(processed_page_text))
                            {
                                lang = m.Groups[4].Value.Trim();
                                ru_pagename = m.Groups[2].Value.Trim();
                                string visible_text = m.Groups[3].Value.Trim();
                                iw_pagename = m.Groups[5].Value.Trim();
                                if (lang == "" || ru_pagename == "" || iw_pagename == "")
                                    continue;
                                new_ru_pagename = "";
                                if (ruPageExist() && !isRedirect(lang, iw_pagename) && !isRedirect("ru", ru_pagename) && !isDisambig(lang, iw_pagename) && !isDisambig("ru", ru_pagename) && !onAfD(ru_pagename))
                                {
                                    string text_in_article_for_replacement = m.Groups[0].Value, newlink;
                                    var replacement_rgx = new Regex(Regex.Escape(text_in_article_for_replacement));
                                    if (new_ru_pagename == "")
                                        newlink = "[[" + ru_pagename + (visible_text == "" ? "" : "|" + visible_text) + "]]";
                                    else
                                        newlink = "[[" + new_ru_pagename + "|" + (visible_text == "" ? ru_pagename : visible_text) + "]]";
                                    comment += ", " + text_in_article_for_replacement + "->" + newlink;
                                    newtext = replacement_rgx.Replace(newtext, newlink);
                                }
                                else
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
                }
        }
        
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
        string result = "<center>\n{|class=standard\n!Статья!!Ссылок на неё";
        foreach (var article_data in needed_articles.OrderByDescending(t => t.Value))
        {
            if (article_data.Value < 10)
                break;
            result += "\n|-\n|" + article_data.Key + "||" + article_data.Value;
        }
        Save(site, "ВП:К созданию/Из шаблонов \"не переведено\"", result + "\n|}", "");
    }
}
