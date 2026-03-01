using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Xml;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Linq;
using System.Web.UI;
class dis_data { public Dictionary<string, bool> disambs; public string dis_cat_name; }
enum type { home_dis, home_section_redir, other }
class Program
{
    static HttpClient site = new HttpClient(); static string[] creds; static string lang_from_template, home_pagename, iw_pagename, new_home_pagename, processed_wiki_lang, visible_text, comment, newtext;
    static Dictionary<string, Dictionary<string, bool>> redirects_to_section; static Dictionary<string, dis_data> disambs; static Dictionary<string, bool> AfDpages; static Regex iw3, iw0, iw1, iw4, redir_rgx;
    static Dictionary<type, Dictionary<string, int>> needed_articles; static Dictionary<string, Pair> deletion_cats; static HashSet<string> processed_links = new HashSet<string>();
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent",
            "MBHbot/1.0 (https://github.com/Saisengen/wikibots; mbhwik@gmail.com) no library"); var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        var doc = new XmlDocument(); doc.LoadXml(result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password },
            { "lgtoken", logintoken }, { "format", "xml" } })); return client;
    }
    static void Save(HttpClient site, string lang, string title, string text, string comment)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://" + processed_wiki_lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value; var request = new MultipartFormDataContent { { new StringContent("edit"), 
                "action" }, { new StringContent(title), "title" }, { new StringContent(text), "text" }, /*{ new StringContent("1"), "bot" },*/ { new StringContent(comment), "summary" }, { new StringContent
                (token), "token" } };
        var answer = site.PostAsync("https://" + processed_wiki_lang + ".wikipedia.org/w/api.php", request).Result.ToString(); if (!answer.Contains("200")) Console.WriteLine(answer);
    }
    static string e(string input) { return Uri.EscapeDataString(input); }
    static string readpage(string lang, string input) { return site.GetStringAsync("https://" + lang + ".wikipedia.org/wiki/" + e(input) + "?action=raw").Result; }
    static bool isRedirectToSection(string lang, string input)
    {
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
            if (!disambs.ContainsKey(lang)) {//НЕ ЗАМЕНЯТЬ RU НА LANG, ТУТ ИДЁТ ПОЛУЧЕНИЕ ИНТЕРВИЧНЫХ ИМЁН ДИЗОВ
                var rr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=langlinks&lllang=" + lang + "&titles=category:" + cat).Result));
                while (rr.Read())
                    if (rr.Name == "ll" && rr.NodeType == XmlNodeType.Element) {
                        rr.Read(); disambs.Add(lang, new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = rr.Value }); }
            }
        if (!disambs.ContainsKey(lang)) { disambs.Add(lang, new dis_data() { dis_cat_name = "" }); }
        if (disambs[lang].dis_cat_name == "") return false;
        else if (disambs[lang].disambs.ContainsKey(input)) return disambs[lang].disambs[input];
        else if (site.GetStringAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=category:" +
            e(disambs[lang].dis_cat_name)).Result.Contains("<cl")) { disambs[lang].disambs.Add(input, true); return true; }
        else { disambs[lang].disambs.Add(input, false); return false; }
    }
    static bool onAfD(string input)
    {
        if (AfDpages.ContainsKey(input)) return AfDpages[input]; else {
            if (site.GetStringAsync("https://" + processed_wiki_lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=" + deletion_cats[processed_wiki_lang].First).Result.Contains("<cl") ||
                site.GetStringAsync("https://" + processed_wiki_lang + ".wikipedia.org/w/api.php?action=query&format=xml&prop=categories&titles=" + e(input) + "&clcategories=" + deletion_cats[processed_wiki_lang].Second).Result.Contains("<cl"))
            { AfDpages.Add(input, true); return true; }
            else { AfDpages.Add(input, false); return false; }
        }
    }
    static bool NewHomePagenameFoundFromInterwiki(string page_for_processing)
    {
        string request = "https://" + lang_from_template + ".wikipedia.org/w/api.php?action=query&format=xml&prop=langlinks&lllang=" + processed_wiki_lang + "&titles=" + e(iw_pagename);
        try { var rr = new XmlTextReader(new StringReader(site.GetStringAsync(request).Result)); while (rr.Read()) if (rr.Name == "ll") { rr.Read(); new_home_pagename = rr.Value; return true; } }
        catch { Console.WriteLine("error while lang=" + lang_from_template + " on page " + page_for_processing /*+ "\n" + e.ToString()*/); } return false;
    }
    static void addNeededArticle(Dictionary<string, int> needed_articles, string home_pagename) {
        //if (!processed_links.Contains(home_pagename + "|" + lang_from_template + "|" + iw_pagename)) {
        //    string key = "[[" + home_pagename + "]]([[:" + lang_from_template + ":" + iw_pagename + "]])";
        //    if (needed_articles.ContainsKey(key))
        //        needed_articles[key]++;
        //    else
        //        needed_articles.Add(key, 1);
        //}
        //else processed_links.Add(home_pagename + "|" + lang_from_template + "|" + iw_pagename);
    }
    static void check_for_blockers_and_generate_new_text_and_comment(string page_for_processing, Match m)
    {
        if (lang_from_template.Length > 1 && lang_from_template != "commons" && lang_from_template != "Категория" && lang_from_template != "wikt" && lang_from_template != "Файл") {
            new_home_pagename = "";
            if (NewHomePagenameFoundFromInterwiki(page_for_processing) && !isDisambig(lang_from_template, iw_pagename) && !onAfD(home_pagename) &&
                !(isRedirectToSection(lang_from_template, iw_pagename) && iw_pagename.Contains('#'))) {
                if (isRedirectToSection(processed_wiki_lang, home_pagename) && home_pagename.Contains('#'))
                    addNeededArticle(needed_articles[type.home_section_redir], home_pagename);
                else if (isDisambig(processed_wiki_lang, home_pagename))
                    addNeededArticle(needed_articles[type.home_dis], home_pagename);
                else if (new_home_pagename != "" && isRedirectToSection(processed_wiki_lang, new_home_pagename))
                    addNeededArticle(needed_articles[type.home_section_redir], new_home_pagename);
                else if (new_home_pagename != "" && isDisambig(processed_wiki_lang, new_home_pagename))
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
    }
    static void processPage(string page_for_processing)
    {
        //if (page_for_processing.StartsWith("Список глав "))
        //    return;
        if (page_for_processing.Contains(':')) {
            string ns = page_for_processing.Substring(0, page_for_processing.IndexOf(':'));
            if (ns.Contains("частни") || ns.Contains("бсуждение") || ns.Contains("икипедия") || ns.Contains("роект") || ns.Contains("рбитраж"))
                return;
        }
        string processed_page_text; try { processed_page_text = readpage(processed_wiki_lang, page_for_processing); } catch { return; } newtext = processed_page_text; comment = "";
        foreach (Match m in iw3.Matches(processed_page_text)) {
            lang_from_template = m.Groups[4].Value.Trim();
            home_pagename = m.Groups[2].Value.Trim();
            if (home_pagename == "")
                continue;
            visible_text = m.Groups[3].Value.Trim();
            iw_pagename = m.Groups[5].Value.Trim();
            if (iw_pagename.Contains('#'))
                return;
            if (iw_pagename == "")
                iw_pagename = home_pagename;
            check_for_blockers_and_generate_new_text_and_comment(page_for_processing, m);
        }
        foreach (Match m in iw0.Matches(processed_page_text)) {
            lang_from_template = m.Groups[1].Value.Trim();
            iw_pagename = m.Groups[2].Value.Trim();
            visible_text = m.Groups[3].Value.Trim();
            home_pagename = visible_text;
            check_for_blockers_and_generate_new_text_and_comment(page_for_processing, m);
        }
        foreach (Match m in iw1.Matches(processed_page_text)) {
            lang_from_template = "en";
            iw_pagename = m.Groups[2].Value.Trim();
            visible_text = iw_pagename;
            home_pagename = visible_text;
            check_for_blockers_and_generate_new_text_and_comment(page_for_processing, m);
        }
        foreach (Match m in iw4.Matches(processed_page_text))
        {
            lang_from_template = "en";
            iw_pagename = m.Groups[3].Value.Trim();
            visible_text = m.Groups[2].Value.Trim();
            home_pagename = visible_text;
            check_for_blockers_and_generate_new_text_and_comment(page_for_processing, m);
        }
        if (newtext != processed_page_text)
            Save(site, processed_wiki_lang, page_for_processing, newtext, comment.Substring(2));
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); string cont, query; processed_wiki_lang = "ru";
        site = Site(processed_wiki_lang, creds[0], creds[1]); iw1 = new Regex(@"\{\{ *([нН]е переведено [1345]|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *(\| *nocat=1|) *\}\}");
        iw4 = new Regex(@"\{\{ *([нН]е переведено [1345]|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\| *4 *= *([^{}|]*) *(\| *nocat=1|) *\}\}"); redir_rgx = new Regex(@"#\w*\[\[ *([^#\]]*) *\]\]");
        iw3 = new Regex(@"\{\{ *([нН]е переведено \d|[нН]е переведено|[нН]п\d?|iw) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *\| *([^{}|]*) *(\| *nocat=1|) *\}\}");
        iw0 = new Regex(@"\[\[ *: *([\w\-]*) *: *([^[|\]]*) *\| *([^[|\]]*) *\]\]");
        needed_articles = new Dictionary<type, Dictionary<string, int>>() { { type.home_dis, new Dictionary<string, int>() }, { type.home_section_redir, new Dictionary<string, int>() }, { type.other, 
                new Dictionary<string, int>() } }; disambs = new Dictionary<string, dis_data>() { { "ru", new dis_data() { disambs = new Dictionary<string, bool>(), dis_cat_name = "Страницы значений по алфавиту" } } };
        redirects_to_section = new Dictionary<string, Dictionary<string, bool>>(); AfDpages = new Dictionary<string, bool>();
        var templatenames = new Dictionary<string, string>() { { "ru", "Не переведено 2|Не переведено" }, { "uk", "Не перекладено" } };
        deletion_cats = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "К:Википедия:Кандидаты на удаление", Second = "К:Википедия:К быстрому удалению" } },
                { "uk", new Pair() { First = "Категорія:Статті-кандидати на вилучення", Second = "Категорія:Сторінки до швидкого вилучення" } } };

        //foreach (string page_for_processing in new StreamReader("iwnew.txt").ReadToEnd().Replace("\r", "").Split('\n'))
        //    try { processPage(page_for_processing); } catch (Exception e) { Console.WriteLine(e); }

        foreach (string template in templatenames[processed_wiki_lang].Split('|')) {
            cont = ""; query = "https://" + processed_wiki_lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=embeddedin&eititle=template:" + template + "&eilimit=max"; while (cont != null) {
                //EIDIR НЕ СРАБОТАЕТ ДЛЯ СОРТИРОВКИ, БУДЕТ СОРТИРОВКА ПО ДАТЕ СОЗДАНИЯ СТРАНИЦЫ
                var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&eicontinue=" + e(cont)).Result));
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue"); while (r.Read()) if (r.Name == "ei") try { processPage(r.GetAttribute("title")); } catch (Exception e) { Console.WriteLine(e); }
            }
        }
        //if (processed_wiki_lang == "ru")
        //    foreach (var type in needed_articles.Keys) {
        //        string result = "<center>\n{|class=standard\n!Статья!!Ссылок на неё";
        //        foreach (var article_data in needed_articles[type].OrderByDescending(t => t.Value)) { if (article_data.Value < 4) break; result += "\n|-\n|" + article_data.Key + "||" + article_data.Value; }
        //        try { Save(site, processed_wiki_lang, "ВП:К созданию/Из шаблонов \"не переведено\"/" + type, result + "\n|}", ""); } catch { }
        //    }
    }
}
