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
}
class Program
{
    static HttpClient site = new HttpClient(); static string[] creds; static string lang, home_pagename, iw_pagename, new_home_pagename, home_lang, visible_text, comment, newtext; static bool from_template;
    static Dictionary<string, Dictionary<string, bool>> redirects; static Dictionary<string, dis_data> disambs; static Dictionary<string, bool> AfDpages; static Regex iw3, iw0, iw00; static int c;
    static Dictionary<string, Dictionary<string, int>> needed_articles;
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password },
            { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(HttpClient site, string lang, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var request = new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" }, { new StringContent(text), "text" }, /*{ new StringContent("1"), "bot" },*/
            { new StringContent(comment), "summary" }, { new StringContent(token), "token" } };
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result;
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
        foreach (var cat in "Страницы значений по алфавиту|Страницы значений".Split('|'))
            if (!disambs.ContainsKey(lang))
            {
                var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=langlinks&lllang=" + lang + "&titles=К:" + cat).Result));
                while (rr.Read())
                    if (rr.Name == "ll" && rr.NodeType == XmlNodeType.Element)
                    {
                        rr.Read();
                        disambs.Add(lang, new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = rr.Value });
                    }
            }
        if (!disambs.ContainsKey(lang))
        {
            Console.WriteLine(lang + "wiki has no dis category");
            return false;
        }
        try
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
                Console.WriteLine("error in isDis for lang=\"" + lang + "\" and page=\"" + input + "\"");
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
    static bool homePageExist()
    {
        try
        {
            if (from_template && !site.GetStringAsync("https://" + home_lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(home_pagename)).Result.Contains("_idx=\"-1\""))
                return true;
            else
            {
                var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=langlinks&lllang=" + home_lang + "&titles=" + iw_pagename).Result));
                    while (rr.Read())
                        if (rr.Name == "ll")
                        {
                            rr.Read();
                            new_home_pagename = rr.Value;
                            return true;
                        }
            }
            return false;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.ToString());
            return false;
        }
    }
    static void addNeededArticle(Dictionary<string, int> needed_articles, string home_pagename)
    {
        if (!home_pagename.StartsWith("Список глав "))
        {
            string key = "[[" + home_pagename + "]] ([[:" + lang + ":" + iw_pagename + "]])";
            if (needed_articles.ContainsKey(key))
                needed_articles[key]++;
            else
                needed_articles.Add(key, 1);
        }
    }
    static void check_for_blockers_and_generate_new_text_and_comment(string page_for_processing, Match m)
    {
        if (homePageExist())
        {
            if (isRedirect(lang, iw_pagename))
                addNeededArticle(needed_articles["iw_redir"], home_pagename);
            else if (isRedirect(home_lang, home_pagename))
                addNeededArticle(needed_articles["home_redir"], home_pagename);
            else if (isDisambig(lang, iw_pagename))
                addNeededArticle(needed_articles["iw_dis"], home_pagename);
            else if (isDisambig(home_lang, home_pagename))
                addNeededArticle(needed_articles["home_dis"], home_pagename);
            else if (onAfD(home_pagename))
                addNeededArticle(needed_articles["home_afd"], home_pagename);
            else if (new_home_pagename != "" && isRedirect(home_lang, new_home_pagename))
                addNeededArticle(needed_articles["home_redir"], new_home_pagename);
            else if (new_home_pagename != "" && isDisambig(home_lang, new_home_pagename))
                addNeededArticle(needed_articles["home_dis"], new_home_pagename);
            else
            {
                string text_in_article_for_replacement = m.Groups[0].Value, newlink;
                var replacement_rgx = new Regex(Regex.Escape(text_in_article_for_replacement));
                if (new_home_pagename == "")
                    if (visible_text == "" || visible_text == home_pagename)
                        newlink = "[[" + home_pagename + "]]";
                    else
                        newlink = "[[" + home_pagename + "|" + visible_text + "]]";
                else if (visible_text == "")
                    newlink = "[[" + new_home_pagename + "|" + home_pagename + "]]";
                else if (visible_text == new_home_pagename)
                    newlink = "[[" + new_home_pagename + "]]";
                else
                    newlink = "[[" + new_home_pagename + "|" + visible_text + "]]";
                comment += ", " + text_in_article_for_replacement.Substring(1, text_in_article_for_replacement.Length - 2) + "->" + newlink.Substring(1, newlink.Length - 2);
                newtext = replacement_rgx.Replace(newtext, newlink);
            }
        }
        else
            addNeededArticle(needed_articles["other"], home_pagename);
    }
    static void processPage(string page_for_processing)
    {
        if (page_for_processing.Contains(':'))
        {
            string ns = page_for_processing.Substring(0, page_for_processing.IndexOf(':'));
            if (ns.Contains("частни") || ns.Contains("бсуждение") || ns.Contains("икипедия") || ns.Contains("роект") || ns.Contains("рбитраж"))
                return;
        }
        if (++c % 5000 == 0)
            Console.WriteLine(c + "/360000 processed");
        string processed_page_text;
        try { processed_page_text = readpage(home_lang, page_for_processing); } catch { return; }
        newtext = processed_page_text; comment = "";
        foreach (Match m in iw3.Matches(processed_page_text))
        {
            lang = m.Groups[4].Value.Trim();
            home_pagename = m.Groups[2].Value.Trim();
            if (lang == "" || home_pagename == "" || lang == "d")
                continue;
            visible_text = m.Groups[3].Value.Trim();
            iw_pagename = m.Groups[5].Value.Trim();
            if (iw_pagename == "")
                iw_pagename = home_pagename;
            new_home_pagename = "";
            from_template = true;
            check_for_blockers_and_generate_new_text_and_comment(page_for_processing, m);
        }
        //foreach (Match m in iw0.Matches(processed_page_text))
        //{
        //    lang = m.Groups[1].Value.Trim();
        //    iw_pagename = m.Groups[2].Value.Trim();
        //    if (lang == "" || iw_pagename == "")
        //        continue;
        //    visible_text = ""; home_pagename = "";
        //    new_home_pagename = "";
        //    check_for_blockers_and_generate_new_text(page_for_processing, m);
        //}
        foreach (Match m in iw00.Matches(processed_page_text))
        {
            lang = m.Groups[1].Value.Trim();
            iw_pagename = m.Groups[2].Value.Trim();
            if (lang == "" || home_pagename == "" || lang == "d")
                continue;
            visible_text = m.Groups[3].Value.Trim();
            home_pagename = visible_text;
            new_home_pagename = "";
            from_template = false;
            check_for_blockers_and_generate_new_text_and_comment(page_for_processing, m);
        }
        if (newtext != processed_page_text)
            Save(site, home_lang, page_for_processing, newtext, comment.Substring(2));
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); string cont, query; c = 0; home_lang = "ru";
        site = Site(home_lang, creds[0], creds[1]); //var iw1 = new Regex(@"\{\{ *([нН]е переведено [1345]|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\}\}");
        iw3 = new Regex(@"\{\{ *([нН]е переведено \d|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\}\}");
        iw0 = new Regex(@"\[\[ *: *([a-zA-Z\-]{2,3}) *: *([^[|\]]*) *\]\]"); iw00 = new Regex(@"\[\[ *: *([a-zA-Z\-]{2,3}) *: *([^[|\]]*) *\| *([^[|\]]*) *\]\]");
        needed_articles = new Dictionary<string, Dictionary<string, int>>() { { "iw_redir", new Dictionary<string, int>() }, { "home_redir", new Dictionary<string, int>() }, { "iw_dis", 
                new Dictionary<string, int>() }, { "home_dis", new Dictionary<string, int>() }, { "home_afd", new Dictionary<string, int>() }, { "other", new Dictionary<string, int>() } };
        disambs = new Dictionary<string, dis_data>() { { "ru", new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = "Страницы значений по алфавиту" } } };
        redirects = new Dictionary<string, Dictionary<string, bool>>(); AfDpages = new Dictionary<string, bool>();

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
                            processPage(r.GetAttribute("title"));
                }
        }
        foreach (string page_for_processing in new StreamReader("iw0.txt").ReadToEnd().Replace("\r", "").Split('\n'))
            processPage(page_for_processing);

        //foreach (string processed_page in new StreamReader("iw1.txt").ReadToEnd().Replace("\r", "").Split('\n'))
        //{
        //    if (++c % 200 == 0)
        //        Console.WriteLine(c + "/35000 processed");
        //    string processed_page_text;
        //    try
        //    {
        //        processed_page_text = readpage(home_lang, processed_page);
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

        foreach (string type in "home_dis|iw_dis|home_redir|iw_redir|home_afd|other".Split('|'))
        {
            string result = "<center>\n{|class=standard\n!Статья!!Ссылок на неё";
            foreach (var article_data in needed_articles[type].OrderByDescending(t => t.Value))
            {
                if (article_data.Value < 5)
                    break;
                result += "\n|-\n|" + article_data.Key + "||" + article_data.Value;
            }
            Save(site, home_lang, "ВП:К созданию/Из шаблонов \"не переведено\"/" + type, result + "\n|}", "");
        }
    }
}
