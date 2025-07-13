using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
class dis_data { public Dictionary<string, bool> disambs; public string dis_cat_name; }
enum type { home_dis, iw_dis, home_section_redir, iw_section_redir, home_afd, other }
class Program
{
    static HttpClient site = new HttpClient(); static string[] creds; static string lang, home_pagename, iw_pagename, new_home_pagename, home_lang, visible_text, comment, newtext; static bool from_template;
    static Dictionary<string, Dictionary<string, bool>> redirects_to_section; static Dictionary<string, dis_data> disambs; static Dictionary<string, bool> AfDpages; static Regex iw3, iw0, iw00, redir_rgx;
    static int c; static Dictionary<type, Dictionary<string, int>> needed_articles;
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login); var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        var doc = new XmlDocument(); doc.LoadXml(result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password },
            { "lgtoken", logintoken }, { "format", "xml" } })); return client;
    }
    static void Save(HttpClient site, string lang, string title, string text, string comment)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value; var request = new MultipartFormDataContent { { new StringContent("edit"), 
                "action" }, { new StringContent(title), "title" }, { new StringContent(text), "text" }, /*{ new StringContent("1"), "bot" },*/ { new StringContent(comment), "summary" }, { new StringContent
                (token), "token" } }; if (!site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result.ToString().Contains("uccess")) Console.WriteLine(result.ToString());
    }
    static string e(string input) { return Uri.EscapeDataString(input); }
    static string readpage(string lang, string input) { return site.GetStringAsync("https://" + lang + ".wikipedia.org/wiki/" + e(input) + "?action=raw").Result; }
    static bool isRedirectToSection(string lang, string input)
    {
        if (lang == "d")
            return false;
        if (!redirects_to_section.ContainsKey(lang))
            redirects_to_section.Add(lang, new Dictionary<string, bool>());
        if (redirects_to_section[lang].ContainsKey(input))
            return redirects_to_section[lang][input];
        else if (!site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(input)).Result.Contains("redirect="))
        { redirects_to_section[lang].Add(input, false); return false; }
        else
        {
            string redir_text = site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + e(input)).Result;
            if (redir_rgx.IsMatch(redir_text)) {
                new_home_pagename = redir_rgx.Match(redir_text).Groups[1].Value; redirects_to_section[lang].Add(input, false); return false; }
            else {
                redirects_to_section[lang].Add(input, true); return true; }
        }
    }
    static bool isDisambig(string lang, string input)
    {
        foreach (var cat in "Страницы значений по алфавиту|Страницы значений".Split('|'))
            if (!disambs.ContainsKey(lang))
            {
                var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=langlinks&lllang=" + lang + "&titles=К:" + cat).Result));
                while (rr.Read())
                    if (rr.Name == "ll" && rr.NodeType == XmlNodeType.Element) {
                        rr.Read(); disambs.Add(lang, new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = rr.Value }); }
            }
        if (!disambs.ContainsKey(lang)) { disambs.Add(lang, new dis_data() { dis_cat_name = "" }); }
        if (disambs[lang].dis_cat_name == "")
            return false;
        else if (disambs[lang].disambs.ContainsKey(input))
            return disambs[lang].disambs[input];
        else if (site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=category:" +
            e(disambs[lang].dis_cat_name)).Result.Contains("<cl")) {
            disambs[lang].disambs.Add(input, true); return true; }
        else {
            disambs[lang].disambs.Add(input, false); return false; }
    }
    static bool onAfD(string input)
    {
        if (AfDpages.ContainsKey(input)) return AfDpages[input]; else {
            if (site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=К:Википедия:Кандидаты на удаление").Result.Contains("<cl") ||
                site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=К:Википедия:К быстрому удалению").Result.Contains("<cl"))
            { AfDpages.Add(input, true); return true; }
            else { AfDpages.Add(input, false); return false; }
        }
    }
    static bool homePageExist()
    {
        string request = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=langlinks&lllang=" + home_lang + "&titles=" + e(iw_pagename);
        try { var rr = new XmlTextReader(new StringReader(site.GetStringAsync(request).Result)); while (rr.Read()) if (rr.Name == "ll") { rr.Read(); new_home_pagename = rr.Value; return true; } }
        catch { Console.WriteLine("error while lang=" + lang); } return false;
    }
    static void addNeededArticle(Dictionary<string, int> needed_articles, string home_pagename) {
        string key = "[[" + home_pagename + "]] ([[:" + lang + ":" + iw_pagename + "]])"; if (needed_articles.ContainsKey(key)) needed_articles[key]++; else needed_articles.Add(key, 1); }
    static void check_for_blockers_and_generate_new_text_and_comment(string page_for_processing, Match m)
    {
        if (homePageExist())
        {
            if (isRedirectToSection(lang, iw_pagename) && iw_pagename.Contains('#'))
                addNeededArticle(needed_articles[type.iw_section_redir], home_pagename);
            else if (isRedirectToSection(home_lang, home_pagename) && home_pagename.Contains('#'))
                addNeededArticle(needed_articles[type.home_section_redir], home_pagename);
            else if (isDisambig(lang, iw_pagename))
                addNeededArticle(needed_articles[type.iw_dis], home_pagename);
            else if (isDisambig(home_lang, home_pagename))
                addNeededArticle(needed_articles[type.home_dis], home_pagename);
            else if (onAfD(home_pagename))
                addNeededArticle(needed_articles[type.home_afd], home_pagename);
            else if (new_home_pagename != "" && isRedirectToSection(home_lang, new_home_pagename))
                addNeededArticle(needed_articles[type.home_section_redir], new_home_pagename);
            else if (new_home_pagename != "" && isDisambig(home_lang, new_home_pagename))
                addNeededArticle(needed_articles[type.home_dis], new_home_pagename);
            else if (home_pagename != page_for_processing && new_home_pagename != page_for_processing)
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
            addNeededArticle(needed_articles[type.other], home_pagename);
    }
    static void processPage(string page_for_processing)
    {
        if (page_for_processing.StartsWith("Список глав "))
            return;
        if (page_for_processing.Contains(':'))
        {
            string ns = page_for_processing.Substring(0, page_for_processing.IndexOf(':'));
            if (ns.Contains("частни") || ns.Contains("бсуждение") || ns.Contains("икипедия") || ns.Contains("роект") || ns.Contains("рбитраж"))
                return;
        }
        if (++c % 5000 == 0)
            Console.WriteLine(c + "/365000 processed");
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
            if (iw_pagename.Contains('#'))
                return;
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
        iw3 = new Regex(@"\{\{ *([нН]е переведено \d|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\}\}"); redir_rgx = new Regex(@"#\w*\[\[ *([^#\]]*) *\]\]");
        iw0 = new Regex(@"\[\[ *: *([a-zA-Z\-]{2,3}) *: *([^[|\]]*) *\]\]"); iw00 = new Regex(@"\[\[ *: *([a-zA-Z\-]{2,3}) *: *([^[|\]]*) *\| *([^[|\]]*) *\]\]");
        needed_articles = new Dictionary<type, Dictionary<string, int>>() { { type.home_dis, new Dictionary<string, int>() }, { type.home_afd, new Dictionary<string, int>() }, { type.home_section_redir, 
                new Dictionary<string, int>() }, { type.iw_section_redir, new Dictionary<string, int>() }, { type.iw_dis, new Dictionary<string, int>() }, { type.other, new Dictionary<string, int>() } };
        disambs = new Dictionary<string, dis_data>() { { "ru", new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = "Страницы значений по алфавиту" } } };
        redirects_to_section = new Dictionary<string, Dictionary<string, bool>>(); AfDpages = new Dictionary<string, bool>();
        //foreach (string page_for_processing in new StreamReader("iw0.txt").ReadToEnd().Replace("\r", "").Split('\n')) processPage(page_for_processing);
        foreach (string template in "Не переведено 2|Не переведено".Split('|'))
        {
            cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=Ш:" + template + "&eilimit=max"; while (cont != null)
            {
                var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&eicontinue=" + e(cont)).Result));
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue"); while (r.Read()) if (r.Name == "ei") try { processPage(r.GetAttribute("title")); } catch (Exception e) { Console.WriteLine(e); }
            }
        }
        foreach (var type in needed_articles.Keys)
        {
            string result = "<center>\n{|class=standard\n!Статья!!Ссылок на неё";
            foreach (var article_data in needed_articles[type].OrderByDescending(t => t.Value))
            {
                if (article_data.Value < 5)
                    break;
                result += "\n|-\n|" + article_data.Key + "||" + article_data.Value;
            }
            try
            {
                Save(site, home_lang, "ВП:К созданию/Из шаблонов \"не переведено\"/" + type, result + "\n|}", "");
            }
            catch { }
        }
    }
}
