using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Web.UI;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
class most_edits_record
{
    public int all, main, user, templ, file, cat, portproj, meta, tech, main_edits_index;
    public bool globalbot;
}
class redir
{
    public string src_title, dest_title;
    public int src_ns, dest_ns;
    public override string ToString()
    {
        return src_ns + ' ' + src_title + ' ' + dest_ns + ' ' + dest_title;
    }
}
class pageviews_result
{
    public string date;
    public int max, median;
}
class script_usages
{
    public int active, inactive;
}
public class Allpage
{
    public int pageid;
    public int ns;
    public string title;
}
public class Continue
{
    public string apcontinue;
    public string @continue;
}
public class Query
{
    public List<Allpage> allpages;
}
class Root
{
    public bool batchcomplete;
    public Continue @continue;
    public Query query;
}
class Program
{
    static string[] creds, monthname = new string[13], prepositional = new string[13];
    static HttpClient site = new HttpClient();
    static DateTime now = DateTime.Now;
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
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword",
                password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
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
        var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action");
        request.Add(new StringContent(title), "title");
        request.Add(new StringContent(text), "text");
        request.Add(new StringContent(comment), "summary");
        request.Add(new StringContent(token), "token");
        request.Add(new StringContent("xml"), "format");
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result;
        if (!result.ToString().Contains("uccess"))
            Console.WriteLine(result);
    }
    static void pats_awarding()
    {
        var newfromabove = new HashSet<string>();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Категория:Википедия:Участники с " +
            "добавлением тем сверху&cmprop=title&cmlimit=max").Result)))
            while (r.Read())
                if (r.Name == "cm")
                    newfromabove.Add(r.GetAttribute("title").Substring(r.GetAttribute("title").IndexOf(":") + 1));
        var lastmonth = now.AddMonths(-1);
        var pats = new Dictionary<string, HashSet<string>>();
        string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=title|user&letype=review&leend=" + lastmonth.ToString("yyyy-MM") +
            "-01T00:00:00.000Z&lestart=" + now.ToString("yyyy-MM") + "-01T00:00:00.000Z&lelimit=5000";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&lecontinue=" + cont).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string user = r.GetAttribute("user");
                        string page = r.GetAttribute("title");
                        if (user != null)
                        {
                            if (!pats.ContainsKey(user))
                                pats.Add(user, new HashSet<string>() { page });
                            else if (!pats[user].Contains(page))
                                pats[user].Add(page);
                        }
                    }
            }
        }
        string addition = "\n|-\n";
        if (lastmonth.Month == 1)
            addition += "|rowspan=\"12\"|" + lastmonth.Year + "||" + monthname[lastmonth.Month];
        else
            addition += "|" + monthname[lastmonth.Month];
        int c = 0;
        pats.Remove("MBHbot");
        foreach (var p in pats.OrderByDescending(p => p.Value.Count))
        {
            if (++c > 10) break;
            addition += "||{{u|" + p.Key + "}} (" + p.Value.Count + ")";
            string usertalk = site.GetStringAsync("https://ru.wikipedia.org/wiki/user talk:" + Uri.EscapeDataString(p.Key) + "?action=raw").Result;
            string grade = c < 4 ? "I" : (c < 7 ? "II" : "III");
            if (!newfromabove.Contains(p.Key) || (newfromabove.Contains(p.Key) && usertalk.IndexOf("==") == -1))
                Save(site, "ru", "user talk:" + p.Key, usertalk + "\n\n==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year + ")==\n{{subst:u:Орденоносец/Заслуженному патрулирующему " + grade + "|За " + c +
                    " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года. Поздравляем! ~~~~}}", "орден заслуженному патрулирующему за " + monthname[lastmonth.Month] + " " + lastmonth.Year + " года");
            else
            {
                int border = usertalk.IndexOf("==");
                string header = usertalk.Substring(0, border - 1);
                string pagebody = usertalk.Substring(border);
                Save(site, "ru", "user talk:" + p.Key, header + "==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year + ")==\n{{subst:u:Орденоносец/Заслуженному патрулирующему " + grade + "|За " + c +
                    " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года. Поздравляем! ~~~~}}\n\n" + pagebody, "орден заслуженному патрулирующему за " + monthname[lastmonth.Month] + " " + lastmonth.Year + " года");
            }
        }
        string pats_order = site.GetStringAsync("https://ru.wikipedia.org/wiki/ВП:Ордена/Заслуженному патрулирующему?action=raw").Result;
        Save(site, "ru", "ВП:Ордена/Заслуженному патрулирующему", pats_order + addition, "ордена за " + monthname[lastmonth.Month]);
    }
    static void most_edits()
    {
        var falsebots = new Dictionary<string, string[]>() { { "ru", new string[] { "Alex Smotrov", "Wind", "Tutaishy" } }, { "be", new string[] { "Maksim L.", "Artsiom91" } }, { "kk", new string[] { "Arystanbek", "Нұрлан Рахымжанов" } } };
        var min_num_of_edits = new Dictionary<string, int>() { { "ru", 10000 }, { "be", 5000 }, { "kk", 500 } };

        var headers = new Dictionary<string, string>() { { "ru", "{{Плавающая шапка таблицы}}{{Самые активные участники}}%shortcut%<center>\nВ каждой колонке приведена сумма правок в указанном пространстве и его обсуждении. Первично отсортировано и пронумеровано по общему числу правок.%specific_text%\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!{{abbr|№ п/с|место по числу правок в статьях|0}}!!Участник!!Всего правок!!В статьях!!шаблонах!!файлах!!категориях!!порталах и проектах!!модулях и MediaWiki!!страницах участников!!метапедических страницах" },
            { "be", "{{Самыя актыўныя ўдзельнікі}}%shortcut%<center>У кожным слупку прыведзена сума правак у адпаведнай прасторы і размовах пра яе. Першасна адсартавана і пранумаравана паводле агульнай колькасці правак.%specific_text%\n{|class=\"standard sortable\"\n!№!!{{abbr|№ п/с|месца па колькасці правак у артыкулах|0}}!!Удзельнік!!Агулам правак!!У артыкулах!!шаблонах!!файлах!!катэгорыях!!парталах і праектах!!модулях і MediaWiki!!старонках удзельнікаў!!метапедычных старонках" },
            { "kk", "%shortcut%<center>Әрбір бағанда көрсетілген кеңістіктегі және оның талқылауындағы өңдеулер саны берілген. Ең алдымен жалпы түзетулер бойынша сұрыпталған және нөмірленген.%specific_text%\n{{StatInfo}}\n{|class=\"standard sortable ts-stickytableheader\"\n!#!!{{abbr|#м/о|мақалалардағы өңдеме саны бойынша орны|0}}!!Қатысушы!!Барлық өңдемесі!!Мақалалар!!Үлгілер!!Файлдар!!Санаттар!!Порталдар + жобалар!!Модулдар + MediaWiki!!Қатысушы беттері!!Метапедиялық (Уикипедия)" } };

        var resultpages = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "ВП:Самые активные боты", Second = "ВП:Участники по числу правок" } },
            { "be", new Pair() { First = "Вікіпедыя:Боты паводле колькасці правак", Second = "Вікіпедыя:Удзельнікі паводле колькасці правак" } },
            { "kk", new Pair() { First = "Уикипедия:Өңдеме саны бойынша боттар", Second = "Уикипедия:Өңдеме саны бойынша қатысушылар" } } };

        var footers = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "[[К:Википедия:Боты]]", Second = "" } },
            { "be", new Pair() { First = "[[Катэгорыя:Вікіпедыя:Боты]][[Катэгорыя:Вікіпедыя:Статыстыка і прагнозы]]", Second = "[[Катэгорыя:Вікіпедыя:Статыстыка і прагнозы]]" } },
            { "kk", new Pair() { First = "{{Wikistats}}[[Санат:Уикипедия:Боттар]]", Second = "{{Wikistats}}[[Санат:Уикипедия:Қатысушылар]]" } } };

        var shortcuts = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "ВП:САБ", Second = "ВП:САУ" } }, { "be", new Pair() { First = "ВП:САБ", Second = "ВП:САУ" } }, { "kk", new Pair() { First = "УП:ӨСБ", Second = "УП:ӨСҚ" } } };

        foreach (var lang in new string[] { "ru", "be", "kk" })
        {
            var hdr_modifications = new Dictionary<string, Pair>() { { "ru", new Pair() { First = " Голубым выделены глобальные боты без локального флага.", Second = " В список включены участники, имеющие не менее " + min_num_of_edits[lang] + " правок, включая удалённые правки (из-за них число живых правок в таблице может быть меньше)." } },
            { "be", new Pair() { First = " Блакітным вылучаныя глабальныя боты без лакальнага сцяга.", Second = " У спіс уключаны ўдзельнікі, якія маюць не менш за " + min_num_of_edits[lang] + " правак." } },
            { "kk", new Pair() { First = " Жергілікті жалаусыз ғаламдық боттар көкпен ерекшеленген.", Second = " Тізімге " + min_num_of_edits[lang] + " өңдемеден кем емес өңдеме жасаған қатысушылар кірістірілген." } } };

            var users = new Dictionary<string, most_edits_record>();
            var bots = new Dictionary<string, most_edits_record>();
            var connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            var command = new MySqlCommand("select distinct cast(log_title as char) bot from logging where log_type=\"rights\" and log_params like \"%bot%\";", connect) { CommandTimeout = 9999 };
            var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string bot = reader.GetString("bot").Replace("_", " ");
                if (!falsebots[lang].Contains(bot))
                    bots.Add(bot, new most_edits_record() { globalbot = false });
            }
            reader.Close();

            command.CommandText = "select cast(user_name as char) user from user where user_editcount >= " + min_num_of_edits[lang] + ";";
            reader = command.ExecuteReader();
            while (reader.Read())
            {
                string user = reader.GetString("user");
                if (!bots.ContainsKey(user))
                    users.Add(user, new most_edits_record());
            }
            reader.Close();
            connect.Close();

            connect = new MySqlConnection(creds[2].Replace("%project%", "metawiki"));
            connect.Open();
            command = new MySqlCommand("select distinct cast(log_title as char) bot from logging where log_type='gblrights' and (log_params like '%lobal-bot%' or log_params like '%lobal_bot%');", connect) { CommandTimeout = 9999 };
            reader = command.ExecuteReader();
            while (reader.Read())
            {
                string bot = reader.GetString("bot").Replace("_", " ");
                if (!bots.ContainsKey(bot))
                {
                    bots.Add(bot, new most_edits_record() { globalbot = true });
                    users.Remove(bot);
                }
            }
            reader.Close();
            connect.Close();

            var site = Site(lang, creds[0], creds[1]);
            foreach (var type in new Dictionary<string, most_edits_record>[] { users, bots })
            {
                foreach (var k in type.Keys)
                {
                    string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=max&ucprop=title&ucuser=" + Uri.EscapeDataString(k);
                    while (cont != null)
                    {
                        string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&uccontinue=" + Uri.EscapeDataString(cont)).Result);
                        using (var r = new XmlTextReader(new StringReader(apiout)))
                        {
                            r.WhitespaceHandling = WhitespaceHandling.None;
                            r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("uccontinue");
                            while (r.Read())
                                if (r.Name == "item")
                                {
                                    int ns = Convert.ToInt16(r.GetAttribute("ns"));
                                    type[k].all++;
                                    if (ns == 0 || ns == 1)
                                        type[k].main++;
                                    else if (ns == 2 || ns == 3)
                                        type[k].user++;
                                    else if (ns == 4 || ns == 5 || ns == 12 || ns == 13 || ns == 106 || ns == 107)
                                        type[k].meta++;
                                    else if (ns == 100 || ns == 101 || ns == 104 || ns == 105)
                                        type[k].portproj++;
                                    else if (ns == 10 || ns == 11)
                                        type[k].templ++;
                                    else if (ns == 6 || ns == 7)
                                        type[k].file++;
                                    else if (ns == 8 || ns == 9 || ns == 828 || ns == 829)
                                        type[k].tech++;
                                    else if (ns == 14 || ns == 15)
                                        type[k].cat++;
                                }
                        }
                    }
                }
            }

            string result = headers[lang].Replace("%specific_text%", hdr_modifications[lang].First.ToString()).Replace("%shortcut%", "{{shortcut|" + shortcuts[lang].First + "}}");

            int main_edits_index = 0;
            foreach (var bot in bots.OrderByDescending(bot => bot.Value.main))
            {
                if (bot.Value.all == 0)
                    bots.Remove(bot.Key);
                else bot.Value.main_edits_index = ++main_edits_index;
            }
            main_edits_index = 0;
            foreach (var user in users.OrderByDescending(user => user.Value.main))
                user.Value.main_edits_index = ++main_edits_index;

            int all_edits_index = 0;
            foreach (var s in bots.OrderByDescending(s => s.Value.all))
            {
                string color = "";
                if (s.Value.globalbot)
                    color = "style=\"background-color:#ccf\"";
                result += "\n|-" + color + "\n|" + ++all_edits_index + "||" + s.Value.main_edits_index + "||{{u|" + s.Key + "}}||" + s.Value.all + "||" + s.Value.main + "||" + s.Value.templ + "||" + s.Value.file + "||" + s.Value.cat + "||" + s.Value.portproj + "||" + s.Value.tech + "||" + s.Value.user + "||" + s.Value.meta;
            }
            result += "\n|}" + footers[lang].First;
            Save(site, lang, resultpages[lang].First.ToString(), result, "");

            all_edits_index = 0;
            result = headers[lang].Replace("%specific_text%", hdr_modifications[lang].Second.ToString()).Replace("%shortcut%", "{{shortcut|" + shortcuts[lang].Second + "}}");
            foreach (var s in users.OrderByDescending(s => s.Value.all))
                result += "\n|-\n|" + ++all_edits_index + "||" + s.Value.main_edits_index + "||{{u|" + s.Key + "}}||" + s.Value.all + "||" + s.Value.main + "||" + s.Value.templ + "||" + s.Value.file + "||" + s.Value.cat + "||" + s.Value.portproj + "||" + s.Value.tech + "||" + s.Value.user + "||" + s.Value.meta;
            result += "\n|}" + footers[lang].Second;
            Save(site, lang, resultpages[lang].Second.ToString(), result, "");
        }
    }
    static void most_watched_pages()
    {
        int limit = 30;
        var nss = new Dictionary<int, string>();
        string cont, query, apiout, result = "<center>Отсортировано сперва по числу активных следящих, когда их меньше " + limit + " - по числу следящих в целом.\n";

        apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces").Result;
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ns")
                {
                    int ns = Convert.ToInt16(r.GetAttribute("id"));
                    if (ns % 2 == 0 || ns == 3)
                    {
                        r.Read();
                        nss.Add(ns, r.Value);
                    }
                }
        }
        nss.Remove(2);
        nss.Remove(-2);

        foreach (var n in nss.Keys)
        {
            var pageids = new HashSet<string>();
            var pagecountswithactive = new Dictionary<string, Pair>();
            var pagecountswoactive = new Dictionary<string, int>();
            cont = ""; query = "https://ru.wikipedia.org/w/api.php?action=query&list=allpages&format=xml&aplimit=max&apfilterredir=nonredirects&apnamespace=";
            while (cont != null)
            {
                apiout = (cont == "" ? site.GetStringAsync(query + n).Result : site.GetStringAsync(query + n + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                            pageids.Add(r.GetAttribute("pageid"));
                }
            }

            var requeststrings = new HashSet<string>();
            string idset = ""; int c = 0;
            foreach (var p in pageids)
            {
                idset += "|" + p;
                if (++c % 500 == 0)
                {
                    requeststrings.Add(idset.Substring(1));
                    idset = "";
                }
            }
            if (idset.Length != 0)
                requeststrings.Add(idset.Substring(1));

            foreach (var q in requeststrings)
                using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=info&format=xml&inprop=visitingwatchers%7Cwatchers&pageids=" + q).Result)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    while (r.Read())
                        if (r.GetAttribute("watchers") != null)
                        {
                            string title = r.GetAttribute("title");
                            if (n == 3)
                            {
                                if (title.Contains("/Архив"))
                                    continue;
                                title = title.Replace("Обсуждение участника:", "Участник:").Replace("Обсуждение участницы:", "Участница:");
                            }
                            int watchers = Convert.ToInt16(r.GetAttribute("watchers"));
                            if (n == 0 && watchers >= 60 || n != 0)
                            {
                                if (r.GetAttribute("visitingwatchers") != null)
                                    pagecountswithactive.Add(title, new Pair() { First = watchers, Second = r.GetAttribute("visitingwatchers") });
                                else
                                    pagecountswoactive.Add(title, watchers);
                            }
                        }
                }

            if (pagecountswoactive.Count != 0)
            {
                result += "==" + (nss[n] == "" ? "Статьи" : (nss[n] == "Обсуждение участника" ? "Участник" : nss[n])) + "==\n{|class=\"standard sortable\"\n!Страница!!Всего следящих!!Активных\n";
                foreach (var p in pagecountswithactive.OrderByDescending(p => Convert.ToInt16(p.Value.Second)))
                    result += "|-\n|[[:" + p.Key + "]]||" + p.Value.First + "||" + p.Value.Second + "\n";
                foreach (var p in pagecountswoactive.OrderByDescending(p => p.Value))
                    result += "|-\n|[[:" + p.Key + "]]||" + p.Value + "||<" + limit + "\n";
                result += "|}\n";
            }
        }
        Save(site, "ru", "u:MBH/most watched pages", result, "");
    }
    static void adminstats()
    {
        var discussiontypes = new string[] { "К удалению", "К восстановлению" };
        var monthnames = new string[13];
        monthnames[1] = "января"; monthnames[2] = "февраля"; monthnames[3] = "марта"; monthnames[4] = "апреля"; monthnames[5] = "мая"; monthnames[6] = "июня";
        monthnames[7] = "июля"; monthnames[8] = "августа"; monthnames[9] = "сентября"; monthnames[10] = "октября"; monthnames[11] = "ноября"; monthnames[12] = "декабря";
        var botnames = new HashSet<string>();
        var statstable = new Dictionary<string, Dictionary<string, int>>();
        var sixmonths_earlier = now.AddMonths(-6);
        var now_ym = now.ToString("yyyyMM");
        var sixmonths_earlier_ym = sixmonths_earlier.ToString("yyyyMM");
        var connect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki"));
        connect.Open();
        MySqlCommand command;
        MySqlDataReader r;

        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"sysop\";", connect) { CommandTimeout = 99999 };
        r = command.ExecuteReader();
        while (r.Read())
            statstable.Add(r.GetString(0), new Dictionary<string, int>() { { "closer", 0 }, { "totalactions", 0}, { "delsum", 0 }, { "restoresum", 0 }, { "contentedits", 0 }, { "totaledits", 0 }, { "del_rev_log", 0 }, { "abusefilter", 0}, { "block", 0}, { "contentmodel", 0},
                { "delete", 0}, { "gblblock", 0}, { "managetags", 0}, { "merge", 0}, { "protect", 0}, { "renameuser", 0}, { "restore", 0}, { "review", 0}, { "rights", 0}, { "stable", 0}, { "mediawiki", 0}, { "checkuser", 0}, { "tag", 0} });
        r.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"closer\";";
        r = command.ExecuteReader();
        while (r.Read())
            statstable.Add(r.GetString(0), new Dictionary<string, int>() { { "closer", 1 }, { "totalactions", 0}, { "delsum", 0 }, { "restoresum", 0 }, { "contentedits", 0 }, { "totaledits", 0 }, { "del_rev_log", 0 }, { "abusefilter", 0}, { "block", 0}, { "contentmodel", 0},
                { "delete", 0}, { "gblblock", 0}, { "managetags", 0}, { "merge", 0}, { "protect", 0}, { "renameuser", 0}, { "restore", 0}, { "review", 0}, { "rights", 0}, { "stable", 0}, { "mediawiki", 0}, { "checkuser", 0}, { "tag", 0} });
        r.Close();

        command.CommandText = "select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"bot\";";
        r = command.ExecuteReader();
        while (r.Read())
            botnames.Add(r.GetString(0));
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, log_type, log_action, COUNT(log_title) count FROM user_groups INNER JOIN actor_logging ON actor_user = ug_user INNER JOIN logging_userindex ON actor_id = log_actor WHERE ug_group IN ('sysop', 'closer') AND " +
            "log_timestamp BETWEEN " + sixmonths_earlier_ym + "01000000 AND " + now_ym + "01000000 and log_type = 'delete' and log_action <> 'delete_redir' GROUP BY actor_name, log_type, log_action;";
        r = command.ExecuteReader();
        while (r.Read())
        {
            statstable[r.GetString("user")]["totalactions"] += r.GetInt32("count");
            switch (r.GetString("log_action"))
            {
                case "delete":
                    statstable[r.GetString("user")]["delete"] += r.GetInt32("count");
                    break;
                case "restore":
                    statstable[r.GetString("user")]["restore"] += r.GetInt32("count");
                    break;
                case "revision":
                case "event":
                    statstable[r.GetString("user")]["del_rev_log"] += r.GetInt32("count");
                    break;
            }
        }
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, log_type, COUNT(log_title) count FROM user_groups INNER JOIN actor_logging ON actor_user = ug_user INNER JOIN logging_userindex ON actor_id = log_actor WHERE ug_group IN ('sysop', 'closer') AND log_timestamp " +
            "BETWEEN " + sixmonths_earlier_ym + "01000000 AND " + now_ym + "01000000 and log_action not like 'move_%' and log_action not like '%-a' and log_action not like '%-ia' and log_type <> 'spamblacklist' and log_type <> 'thanks' and log_type <> 'upload' and log_type <> 'create' " +
            "and log_type <> 'move' and log_type <> 'delete' and log_type <> 'newusers' and log_type <> 'timedmediahandler' and log_type <> 'massmessage' and log_type<>'growthexperiments' and log_type<>'import' GROUP BY actor_name, log_type;";
        r = command.ExecuteReader();
        while (r.Read())
            if (r.GetString("log_type") == "review")
                statstable[r.GetString("user")]["review"] += r.GetInt32("count");
            else
            {
                statstable[r.GetString("user")]["totalactions"] += r.GetInt32("count");
                statstable[r.GetString("user")][r.GetString("log_type")] += r.GetInt32("count");
            }
        r.Close();

        command.CommandText = "SELECT cast(actor_name as char) user, page_namespace, COUNT(rev_page) count FROM revision_userindex INNER JOIN page ON rev_page = page_id INNER JOIN actor_revision ON rev_actor = actor_id INNER JOIN user_groups ON ug_user = actor_user WHERE ug_group IN " +
            "('sysop', 'closer') AND rev_timestamp BETWEEN " + sixmonths_earlier_ym + "01000000 AND " + now_ym + "01000000 GROUP BY actor_name, page_namespace;";
        r = command.ExecuteReader();
        while (r.Read())
        {
            statstable[r.GetString("user")]["totaledits"] += r.GetInt32("count");
            switch (r.GetString("page_namespace"))
            {
                case "0":
                case "6":
                case "10":
                case "14":
                case "100":
                case "102":
                    statstable[r.GetString("user")]["contentedits"] += r.GetInt32("count");
                    break;
                case "8":
                    statstable[r.GetString("user")]["totalactions"] += r.GetInt32("count");
                    statstable[r.GetString("user")]["mediawiki"] += r.GetInt32("count");
                    break;
            }
        }
        r.Close();

        var lm = now.AddMonths(-1);
        var summaryrgx = new Regex(@"={1,}\s*Итог\s*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(" + monthnames[lm.Month] + "|" +
            monthnames[lm.AddMonths(-1).Month] + "|" + monthnames[lm.AddMonths(-2).Month] + "|" + monthnames[lm.AddMonths(-3).Month] + "|" + monthnames[lm.AddMonths(-4).Month] + "|" + monthnames[lm.AddMonths(-5).Month] + ") (" + lm.Year + "|" +
            lm.AddMonths(-5).Year + @") \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (var t in discussiontypes)
            using (var xr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allpages&apprefix=" + t + "/&apnamespace=4&aplimit=max").Result)))
                while (xr.Read())
                    if (xr.Name == "p")
                    {
                        string page = xr.GetAttribute("title");
                        int year;
                        try
                        { year = Convert.ToInt16(page.Substring(page.Length - 4)); }
                        catch
                        { continue; }
                        if (year >= 2018)
                        {
                            string pagetext;
                            try
                            { pagetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(page) + "?action=raw").Result; }
                            catch
                            { continue; }
                            var results = summaryrgx.Matches(pagetext);
                            foreach (Match m in results)
                            {
                                string user = m.Groups[3].ToString().Replace('_', ' ');
                                if (!statstable.ContainsKey(user))
                                    continue;
                                statstable[user]["totalactions"]++;
                                if (t == "К удалению")
                                    statstable[user]["delsum"]++;
                                else
                                    statstable[user]["restoresum"]++;
                            }
                        }
                    }

        string cutext = site.GetStringAsync("https://ru.wikipedia.org/wiki/u:BotDR/CU_stats?action=raw").Result;
        var custats = cutext.Split('\n');
        foreach (var s in custats)
            if (s.Contains('='))
            {
                var data = s.Split('=');
                statstable[data[0]]["checkuser"] += Convert.ToInt32(data[1]);
                statstable[data[0]]["totalactions"] += Convert.ToInt32(data[1]);
            }

        string result = "<templatestyles src=\"Википедия:Администраторы/Активность/styles.css\"/>\n{{Самые активные участники}}{{списки администраторов}}{{shortcut|ВП:АДА}}<center>\nСтатистика активности " +
            "администраторов и подводящих итоги Русской Википедии за период с 1 " + monthnames[sixmonths_earlier.Month] + " " + sixmonths_earlier.Year + " по 1 " + monthnames[now.Month] + " " + now.Year +
            " года. Первично отсортирована по сумме числа правок и админдействий, нулевые значения не показаны. Включает только участников, имеющих флаг сейчас - после снятия флага строка участника пропадёт " +
            "из таблицы при следующем обновлении.\n\nДля подтверждения активности [[ВП:А#Неактивность администратора|администраторы]] должны сделать за полгода минимум 100 правок, из них 50 — в содержательных " +
            "пространствах имён, а также 25 админдействий, включая подведение итогов на специальных страницах. [[ВП:ПИ#Процедура снятия статуса|Подводящие итоги]] должны совершить 10 действий (итоги плюс удаления)" +
            ", из которых не менее двух — именно итоги.\n{|class=\"ts-википедия_администраторы_активность-table standard sortable\"\n!rowspan=2|Участник!!colspan=3|Правки!!colspan=13|Админдействия\n|-\n!{{abbr" +
            "|Σ∀|все правки|0}}!!{{abbr|Σ|контентные правки|0}}!!{{abbr|✔|патрулирование|0}}!!{{abbr|Σ|все действия|0}}!!{{abbr|<big>🗑</big> (📝)|удаление (итоги на КУ)|0}}!!{{abbr|<big>🗑⇧</big> (📝)|" +
            "восстановление (итоги на ВУС)|0}}!!{{abbr|<big>≡🗑</big>|удаление правок и записей журналов|0}}!!{{abbr|🔨|(раз)блокировки|0}}!!{{abbr|🔒|защита и её снятие|0}}!!{{abbr|1=<big>⚖</big>|2=(де)" +
            "стабилизация|3=0}}!!{{abbr|👮|изменение прав участников|0}}!!{{abbr|<big>⚙</big>|правка MediaWiki, изменение тегов и контентной модели страниц|0}}!!{{abbr|<big>🕸</big>|изменение фильтров " +
            "правок|0}}!!{{abbr|<big>🔍</big>|чекъюзерские проверки|0}}!!{{abbr|<big>⇨</big>👤|переименование участников|0}}";
        foreach (var u in statstable.OrderByDescending(t => t.Value["totalactions"] + t.Value["totaledits"]))
        {
            bool inactivecloser = u.Value["closer"] == 1 && (u.Value["delete"] + u.Value["delsum"] < 10 || u.Value["delsum"] < 2);
            bool lessactions = u.Value["closer"] == 0 && u.Value["totalactions"] < 25;
            bool lesscontent = u.Value["closer"] == 0 && u.Value["contentedits"] + u.Value["review"] < 50;
            bool lesstotal = u.Value["closer"] == 0 && u.Value["totaledits"] + u.Value["review"] < 100;
            string color = "";
            if (!botnames.Contains(u.Key))
            {
                if (inactivecloser || lessactions || lesscontent || lesstotal)
                    color = "style=\"background-color:#fcc\"";
            }
            else
                color = "style=\"background-color:#ccf\"";
            string deletetext = u.Value["delete"] + u.Value["delsum"] == 0 ? "" : inactivecloser ? "'''" + u.Value["delete"] + " (" + u.Value["delsum"] + ")'''" : u.Value["delete"] + " (" + u.Value["delsum"] + ")";
            string restoretext = u.Value["restore"] + u.Value["restoresum"] == 0 ? "" : u.Value["restore"] + " (" + u.Value["restoresum"] + ")";
            //пробелы после ''' нужны чтоб не было висящих '
            result += "\n|-" + color + "\n|{{u|" + u.Key + "}} ([[special:contribs/" + u.Key + "|вклад]] | [[special:log/" + u.Key + "|журн]])||" + (lesstotal ? "''' " + cell(u.Value["totaledits"]) +
                "'''" : cell(u.Value["totaledits"])) + "||" + (lesscontent ? "''' " + cell(u.Value["contentedits"]) + "'''" : cell(u.Value["contentedits"])) + "||" + cell(u.Value["review"]) + "||" +
                (lessactions ? "''' " + cell(u.Value["totalactions"]) + "'''" : cell(u.Value["totalactions"])) + "||" + deletetext + "||" + restoretext + "||" + cell(u.Value["del_rev_log"]) + "||" +
                cell(u.Value["block"] + u.Value["gblblock"]) + "||" + cell(u.Value["protect"]) + "||" + cell(u.Value["stable"]) + "||" + cell(u.Value["rights"]) + "||" + cell(u.Value["managetags"] +
                u.Value["contentmodel"] + u.Value["mediawiki"] + u.Value["tag"]) + "||" + cell(u.Value["abusefilter"]) + "||" + cell(u.Value["checkuser"]) + "||" + cell(u.Value["renameuser"]);
        }
        Save(site, "ru", "ВП:Администраторы/Активность", result + "\n|}", "");
    }
    static string cell(int number)
    {
        if (number == 0) return "";
        else return number.ToString();
    }
    static void likes_stats()
    {
        int num_of_rows_in_output_table = 2000;
        var pairs = new Dictionary<string, int>();
        var thankedusers = new Dictionary<string, int>();
        var thankingusers = new Dictionary<string, int>();
        var ratio = new Dictionary<string, double>();
        string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp&letype=thanks&lelimit=max";
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
        string result = "{{Плавающая шапка таблицы}}<center>См. также [https://mbh.toolforge.org/cgi-bin/likes интерактивную статистику].\n{|style=\"word-break: break-all\"\n|valign=top|\n{|class=" +
            "\"standard ts-stickytableheader\"\n!max-width=300px|Участник!!{{comment|👤⇨👍🏻|место}}";
        foreach (var p in thankingusers.OrderByDescending(p => p.Value))
            if (++c1 <= num_of_rows_in_output_table)
                result += "\n|-\n|{{u|" + p.Key + "}}||{{comment|" + p.Value + "|" + c1 + "}}";
            else
                break;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!max-width=400px|Направление!!Число";
        foreach (var p in pairs.OrderByDescending(p => p.Value))
            if (++c2 <= num_of_rows_in_output_table)
                result += "\n|-\n|" + p.Key + "||{{comment|" + p.Value + "|" + c2 + "}}";
            else
                break;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!max-width=300px|Участник!!{{comment|👍🏻⇨👤|место}}";
        foreach (var p in thankedusers.OrderByDescending(p => p.Value))
            if (++c3 <= num_of_rows_in_output_table)
                result += "\n|-\n|{{u|" + p.Key + "}}||{{comment|" + p.Value + "|" + c3 + "}}";
            else
                break;
        Save(site, "ru", "ВП:Пинг/Статистика лайков", result + "\n|}\n|}", "");
    }
    static void incorrect_redirects()
    {
        var redirs = new Dictionary<string, redir>();
        var nss = new Dictionary<string, string>();
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces").Result)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ns" && !r.GetAttribute("id").StartsWith("-"))
                {
                    string id = r.GetAttribute("id");
                    r.Read();
                    nss.Add(id, r.Value);
                }
        }

        foreach (var current_target_ns in nss)
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allredirects&format=xml&arprop=ids%7Ctitle&arnamespace=" + current_target_ns.Key + "&arlimit=500";//NOT 5000
            while (cont != null)
            {
                var temp = new Dictionary<string, redir>();
                string idset = "";
                using (var rdr = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arcontinue=" + Uri.EscapeDataString(cont)).Result)))
                {
                    rdr.WhitespaceHandling = WhitespaceHandling.None;
                    rdr.Read(); rdr.Read(); rdr.Read(); cont = rdr.GetAttribute("arcontinue");
                    while (rdr.Read())
                        if (rdr.Name == "r")
                        {
                            idset += '|' + rdr.GetAttribute("fromid");
                            temp.Add(rdr.GetAttribute("fromid"), new redir() { dest_title = rdr.GetAttribute("title"), dest_ns = Convert.ToInt16(rdr.GetAttribute("ns")) });
                        }
                }
                if (idset.Length != 0)
                    idset = idset.Substring(1);

                using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=info&format=xml&pageids=" + idset).Result)))
                {
                    rdr.WhitespaceHandling = WhitespaceHandling.None;
                    while (rdr.Read())
                        if (rdr.Name == "page")
                        {
                            var id = rdr.GetAttribute("pageid");
                            int src_ns = Convert.ToInt16(rdr.GetAttribute("ns"));
                            if (temp[id].dest_ns != src_ns || temp[id].dest_ns == 6 || temp[id].dest_ns == 14)
                                if (!(sameuser(rdr.GetAttribute("title"), temp[id].dest_title) && ((temp[id].dest_ns == 3 && src_ns == 2) || (temp[id].dest_ns == 2 && src_ns == 3))) && 
                                    !(src_ns == 4 && temp[id].dest_ns == 104))//если не редиректы между ЛС и СО одного участника и не ВП -> Проект
                                    redirs.Add(id, new redir() { src_ns = src_ns, src_title = rdr.GetAttribute("title"), dest_ns = temp[id].dest_ns, dest_title = temp[id].dest_title });
                        }
                }
            }
        }

        var result = "<center>\n{| class=\"standard sortable\"\n|-\n!Откуда!!Куда";
        foreach (var r in redirs)
        {
            string sort_src_title = r.Value.src_ns == 0 ? r.Value.src_title : r.Value.src_title.Substring(r.Value.src_title.IndexOf(':') + 1);
            string sort_dest_title = r.Value.dest_ns == 0 ? r.Value.dest_title : r.Value.dest_title.Substring(r.Value.dest_title.IndexOf(':') + 1);
            result += "\n|-\n|data-sort-value=\"" + r.Value.src_ns + "-" + sort_src_title + "\"|[[:" + r.Value.src_title + "]]||data-sort-value=\"" + r.Value.dest_ns + "-" + sort_dest_title + "\"|[[:" + r.Value.dest_title + "]]";
        }
        Save(site, "ru", "u:MBH/incorrect redirects", result + "\n|}", "");
    }

    static HashSet<string> invoking_pages = new HashSet<string>(), script_users = new HashSet<string>();
    static Dictionary<string, bool> users_activity = new Dictionary<string, bool>();
    static Dictionary<string, script_usages> scripts = new Dictionary<string, script_usages>();
    static Regex is_rgx = new Regex(@"importscript\s*\(\s*['""]([^h/].*?)\s*['""]\s*\)", RegexOptions.IgnoreCase),
    is2_rgx = new Regex(@"importscript\s*\(\s*['""]/wiki/(.*?)\s*['""]\s*\)", RegexOptions.IgnoreCase), multiline_comment = new Regex(@"/\*.*?\*/", RegexOptions.Singleline),
    is_foreign_rgx = new Regex(@"importscript\s*\(\s*['""]([^h].*?)\s*['""],\s*['""]([^""']*)\s*['""]", RegexOptions.IgnoreCase),
    is_ext_rgx = new Regex(@"importscript\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/wiki/(.*?\.js)", RegexOptions.IgnoreCase),
    loader_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""]/w/index\.php\?title=(.*?\.js)", RegexOptions.IgnoreCase),
    loader_foreign_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/w/index\.php\?title=(.*?\.js)", RegexOptions.IgnoreCase),
    loader_foreign2_rgx = new Regex(@"\.(load|getscript|using)\s*\(\s*['""](https?:|)//([^.]*)\.([^.]*)\.org/wiki/([^?]*)\?", RegexOptions.IgnoreCase),
    r1 = new Regex(@"importscript.*\.js", RegexOptions.IgnoreCase), r2 = new Regex(@"\.(load|getscript|using)\b.*\.js", RegexOptions.IgnoreCase);
    static string username, invoking_page, debug_result = "<center>\n{|class=\"standard sortable\"\n!Страница вызова!!Скрипт";
    static void popular_userscripts()
    {
        var result = "[[К:Википедия:Статистика и прогнозы]]{{shortcut|ВП:СИС}}<center>Статистика собирается по незакомментированным включениям importScript/.load/.using/.getscript на скриптовых страницах " +
            "участников рувики, а также их global.js-файлах на Мете. Отсортировано по числу активных участников - сделавших хоть одно действие за последний месяц. Показаны лишь скрипты, имеющие более " +
            "одного включения. Статистику использования гаджетов см. [[Special:GadgetUsage|тут]]. Подробная разбивка скриптов по страницам - [[/details|тут]]. Обновлено " + now.ToString("dd.MM.yyyy") +
            ". \n{|class=\"standard sortable\"\n!Скрипт!!Активных!!Неактивных!!Всего";
        foreach (string skin in new string[] { "common", "monobook", "vector", "cologneblue", "minerva", "timeless", "simple", "myskin", "modern" })
        {
            string offset = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=search&srsearch=" + skin + ".js&srnamespace=2&srlimit=max&srprop=";
            while (offset != null)
            {
                string apiout = (offset == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&sroffset=" + Uri.EscapeDataString(offset)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); offset = r.GetAttribute("sroffset");
                    while (r.Read())
                        if (r.Name == "p" && r.GetAttribute("title").EndsWith(skin + ".js") && !invoking_pages.Contains(r.GetAttribute("title")))
                            invoking_pages.Add(r.GetAttribute("title"));
                }
            }
        }

        foreach (var invoking_page in invoking_pages)
        {
            username = invoking_page.Substring(invoking_page.IndexOf(':') + 1, invoking_page.IndexOf('/') - 1 - invoking_page.IndexOf(':'));
            Program.invoking_page = invoking_page;
            process_site("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=revisions&rvprop=content&rvlimit=1&titles=" + Uri.EscapeUriString(invoking_page));
            if (!script_users.Contains(username))
                script_users.Add(username);
        }

        foreach (var username in script_users)
        {
            Program.username = username;
            invoking_page = "meta:" + username + "/global.js";
            process_site("https://meta.wikimedia.org/w/api.php?action=query&format=xml&prop=revisions&rvprop=content&rvlimit=1&titles=user:" + Uri.EscapeUriString(username) + "/global.js");
        }

        foreach (var s in scripts.OrderByDescending(s => s.Value.active))
            if ((s.Value.active + s.Value.inactive) > 1)
                result += "\n|-\n|[[:" + s.Key + "]]||" + s.Value.active + "||" + s.Value.inactive + "||" + (s.Value.active + s.Value.inactive);
        Save(site, "ru", "ВП:Самые используемые скрипты", result + "\n|}", "update");
        Save(site, "ru", "ВП:Самые используемые скрипты/details", debug_result + "\n|}", "update");
    }
    static bool sameuser(string s1, string s2)
    {
        if (s1.Contains(":"))
            s1 = s1.Substring(s1.IndexOf(':'));
        if (s2.Contains(":"))
            s2 = s2.Substring(s2.IndexOf(':'));
        if (s1.Contains("/"))
            s1 = s1.Substring(0, s1.IndexOf('/'));
        if (s2.Contains("/"))
            s2 = s2.Substring(0, s2.IndexOf('/'));
        if (s1 == s2) return true;
        return false;
    }
    static bool user_is_active()
    {
        if (users_activity.ContainsKey(username))
            return users_activity[username];
        else
        {
            DateTime edit_ts = new DateTime(), log_ts = new DateTime();
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucprop=timestamp&ucuser=" + Uri.EscapeUriString(username)).Result)))
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string raw_ts = r.GetAttribute("timestamp");
                        edit_ts = new DateTime(Convert.ToInt16(raw_ts.Substring(0, 4)), Convert.ToInt16(raw_ts.Substring(5, 2)), Convert.ToInt16(raw_ts.Substring(8, 2)));
                    }
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=timestamp&lelimit=1&leuser=" + Uri.EscapeUriString(username)).Result)))
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string raw_ts = r.GetAttribute("timestamp");
                        log_ts = new DateTime(Convert.ToInt16(raw_ts.Substring(0, 4)), Convert.ToInt16(raw_ts.Substring(5, 2)), Convert.ToInt16(raw_ts.Substring(8, 2)));
                    }

            if (edit_ts < now.AddMonths(-1) && log_ts < now.AddMonths(-1))
            {
                users_activity.Add(username, false); return false;
            }
            else
            {
                users_activity.Add(username, true); return true;
            }

        }
    }
    static void add_script(string scriptname)
    {
        if (scriptname.StartsWith(":"))
            scriptname = scriptname.Substring(1);
        if (scriptname.StartsWith("ru:"))
            scriptname = scriptname.Substring(3);
        if (scriptname.IndexOf(":") > -1)
            scriptname = scriptname.Substring(0, scriptname.IndexOf(":")).ToLower() + scriptname.Substring(scriptname.IndexOf(":"));
        scriptname = Uri.UnescapeDataString(scriptname).Replace("_", " ").Replace("у:", "user:").Replace("участник:", "user:").Replace("участница:", "user:").Replace("вп:", "project:")
            .Replace("википедия:", "project:").Replace("вікіпедія:", "project:").Replace("користувач:", "user:").Replace("користувачка:", "user:");
        if (scriptname.StartsWith("u:"))
            scriptname = "user:" + scriptname.Substring(2);
        //if (g_invoking_page.EndsWith("/global.js") && scriptname.ToLower().StartsWith("mediawiki:"))
        //    scriptname = "meta:" + scriptname;
        debug_result += "\n|-\n|[[:" + invoking_page + "]]||[[:" + scriptname + "]]";
        if (user_is_active() && scripts.ContainsKey(scriptname))
            scripts[scriptname].active++;
        else if (user_is_active() && !scripts.ContainsKey(scriptname))
            scripts.Add(scriptname, new script_usages() { active = 1, inactive = 0 });
        else if (!user_is_active() && scripts.ContainsKey(scriptname))
            scripts[scriptname].inactive++;
        else
            scripts.Add(scriptname, new script_usages() { active = 0, inactive = 1 });
    }
    static void process_site(string url)
    {
        string content = "";
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync(url).Result)))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("_idx") != "-1")
                {
                    r.Read(); r.Read(); r.Read(); content = r.Value; break;
                }
        content = Uri.UnescapeDataString(multiline_comment.Replace(content, "")).Replace("(\n", "(").Replace("{\n", "{");
        foreach (var s in content.Split('\n'))
            if (!s.TrimStart(' ').StartsWith("//"))
            {
                //if (r1.IsMatch(s) && !(is_ext_rgx.IsMatch(s) || is_foreign_rgx.IsMatch(s) || is_rgx.IsMatch(s) || is2_rgx.IsMatch(s)))
                //e.WriteLine(s);
                //if (r2.IsMatch(s) && !(loader_foreign_rgx.IsMatch(s) || loader_rgx.IsMatch(s)) || loader_foreign2_rgx.IsMatch(s))
                //e.WriteLine(s);
                if (is_foreign_rgx.IsMatch(s))
                    foreach (Match m in is_foreign_rgx.Matches(s))
                        add_script(m.Groups[2].Value + ":" + m.Groups[1].Value);
                else
                {
                    foreach (Match m in is_rgx.Matches(s))
                        add_script(m.Groups[1].Value);
                    foreach (Match m in is2_rgx.Matches(s))
                        add_script(m.Groups[1].Value);
                    foreach (Match m in is_ext_rgx.Matches(s))
                        if (m.Groups[3].Value.EndsWith("edia"))
                            add_script(m.Groups[2].Value + ":" + m.Groups[4].Value);
                        else if (m.Groups[3].Value == "wikidata")
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value);
                        else if (m.Groups[3].Value == "mediawiki")
                            add_script("mw:" + m.Groups[4].Value);
                        else
                            add_script(m.Groups[2].Value + ":" + m.Groups[3].Value + ":" + m.Groups[4].Value);
                    foreach (Match m in loader_rgx.Matches(s))
                        add_script(m.Groups[2].Value);
                    foreach (Match m in loader_foreign_rgx.Matches(s))
                        if (m.Groups[4].Value.EndsWith("edia"))
                            add_script(m.Groups[3].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "wikidata")
                            add_script(m.Groups[4].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "mediawiki")
                            add_script("mw:" + m.Groups[5].Value);
                        else
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value + ":" + m.Groups[5].Value);
                    foreach (Match m in loader_foreign2_rgx.Matches(s))
                        if (m.Groups[4].Value.EndsWith("edia"))
                            add_script(m.Groups[3].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "wikidata")
                            add_script(m.Groups[4].Value + ":" + m.Groups[5].Value);
                        else if (m.Groups[4].Value == "mediawiki")
                            add_script("mw:" + m.Groups[5].Value);
                        else
                            add_script(m.Groups[3].Value + ":" + m.Groups[4].Value + ":" + m.Groups[5].Value);
                }
            }
    }
    static Dictionary<string, Dictionary<string, int>> users = new Dictionary<string, Dictionary<string, int>>();
    static MySqlCommand command;
    static MySqlDataReader rdr;
    static MySqlConnection connect;
    static void page_creators()
    {
        var falsebots = new Dictionary<string, string[]>() { { "ru", new string[] { "Alex Smotrov", "Wind", "Tutaishy" } }, { "kk", new string[] { "Arystanbek", "Нұрлан_Рахымжанов" } } };
        var resultpage = new Dictionary<string, string>() { { "ru", "ВП:Участники по числу созданных страниц" }, { "kk", "Уикипедия:Бет бастауы бойынша қатысушылар" } };
        var disambigcategory = new Dictionary<string, string>() { { "ru", "Страницы значений по алфавиту" }, { "kk", "Алфавит бойынша айрық беттер" } };
        var headers = new Dictionary<string, string>() { { "ru", "{{Плавающая шапка таблицы}}{{Самые активные участники}}{{shortcut|ВП:УПЧС}}<center>Бот, генерирующий таблицу, работает так: берёт " +
                "все страницы основного пространства, включая редиректы, и для каждой смотрит имя первого правщика. Таким образом бот не засчитывает создание удалённых статей и статей, авторство в " +
                "которых скрыто. Обновлено " + now.ToString("d.M.yyyy") + ".\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!Участник!!Статьи!!Редиректы!!Дизамбиги!!Шаблоны!!Категории!!Файлы" },
            { "kk", "{{shortcut|УП:ББҚ}}<center>{{StatInfo}}\n{|class=\"standard sortable ts-stickytableheader\"\n!#!!Қатысушы!!Мақалалар!!Бағыттау беттері!!Айрық беттер!!Үлгілер!!Санаттар!!Файлдар" } };
        var footers = new Dictionary<string, string>() { { "ru", "" }, { "kk", "\n{{Wikistats}}[[Санат:Уикипедия:Қатысушылар]]" } };
        var limit = new Dictionary<string, int>() { { "ru", 100 }, { "kk", 50 } };
        foreach (var lang in new string[] { "kk", "ru" })
        {
            users.Clear();
            Dictionary<string, Dictionary<string, int>> bestusers = new Dictionary<string, Dictionary<string, int>>();
            HashSet<string> bots = new HashSet<string>(), disambs = new HashSet<string>();
            connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            command = new MySqlCommand("select distinct cast(log_title as char) title from logging where log_type=\"rights\" and log_params like \"%bot%\";", connect) { CommandTimeout = 9999 };
            rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                string bot = rdr.GetString("title");
                if (!falsebots[lang].Contains(bot) && !bots.Contains(bot))
                    bots.Add(bot.Replace("_", " "));
            }
            rdr.Close();
            var site = Site(lang, creds[0], creds[1]);
            string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=category:" + disambigcategory[lang] + "&cmprop=ids&cmlimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&cmcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.Name == "cm")
                            disambs.Add(r.GetAttribute("pageid"));
                }
            }
            foreach (var ns in new string[] { "0", "6", "10", "14" })
            {
                cont = ""; query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allpages&aplimit=max&apfilterredir=nonredirects&apnamespace=" + ns;
                while (cont != null)
                {
                    string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
                    using (var r = new XmlTextReader(new StringReader(apiout)))
                    {
                        r.WhitespaceHandling = WhitespaceHandling.None;
                        r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                        while (r.Read())
                            if (r.Name == "p")
                            {
                                string id = r.GetAttribute("pageid");
                                if (ns != "0")
                                    get_page_author(id, ns);
                                else if (disambs.Contains(id))
                                    get_page_author(id, "d");
                                else
                                    get_page_author(id, "0");
                            }
                    }
                }
            }
            cont = ""; query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allpages&aplimit=max&apfilterredir=redirects&apnamespace=0";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                            get_page_author(r.GetAttribute("pageid"), "r");
                }
            }
            foreach (var u in users)
                if (u.Value["0"] + u.Value["6"] + u.Value["10"] + u.Value["14"] + u.Value["r"] + u.Value["d"] >= limit[lang])
                    bestusers.Add(u.Key, u.Value);
            string result = headers[lang];
            int c = 0;
            foreach (var u in bestusers.OrderByDescending(u => u.Value["0"]))
            {
                bool bot = bots.Contains(u.Key);
                string color = (bot ? "style=\"background-color:#ddf\"" : "");
                string number = (bot ? "" : (++c).ToString());
                result += "\n|-" + color + "\n|" + number + "||{{u|" + (u.Key.Contains('=') ? "1=" + u.Key : u.Key) + "}}||" + u.Value["0"] + "||" + u.Value["r"] + "||" + u.Value["d"] + "||" +
                    u.Value["10"] + "||" + u.Value["14"] + "||" + u.Value["6"];
            }
            Save(site, lang, resultpage[lang], result + "\n|}" + footers[lang], "");
        }
    }
    static void get_page_author(string id, string ns)
    {
        command = new MySqlCommand("SELECT cast(actor_name as char) user FROM revision JOIN actor ON rev_actor = actor_id where rev_page=" + id + " order by rev_timestamp asc limit 1;", connect);
        rdr = command.ExecuteReader();
        while (rdr.Read())
        {
            string user = rdr.GetString("user");
            if (!users.ContainsKey(user))
                users.Add(user, new Dictionary<string, int>() { { "0", 0 }, { "6", 0 }, { "10", 0 }, { "14", 0 }, { "r", 0 }, { "d", 0 } });
            users[user][ns]++;
        }
        rdr.Close();
    }
    static void apat_for_filemovers()
    {
        var badusers = new List<string>() { "Шухрат Саъдиев" };
        var globalusers = new HashSet<string>();
        var globalusers_needs_flag = new HashSet<string>();
        var apats = new HashSet<string>();
        var connect = new MySqlConnection(creds[2].Replace("%project%", "ruwiki"));
        connect.Open();
        var command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"editor\" or ug_group = \"autoreview\";", connect) { CommandTimeout = 99999 };
        var r = command.ExecuteReader();
        while (r.Read())
            apats.Add(r.GetString(0));
        r.Close();

        connect = new MySqlConnection(creds[2].Replace("%project%", "commonswiki"));
        connect.Open();
        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"filemover\";", connect) { CommandTimeout = 99999 };
        r = command.ExecuteReader();
        while (r.Read())
            globalusers.Add(r.GetString(0));
        r.Close();

        using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://meta.wikimedia.org/w/api.php?action=query&format=xml&list=globalallusers&agugroup=global-rollbacker&agulimit=max").Result)))
            while (rdr.Read())
                if (rdr.Name == "globaluser")
                    if (!globalusers.Contains(rdr.GetAttribute("name")))
                        globalusers.Add(rdr.GetAttribute("name"));

        globalusers.ExceptWith(apats);

        var lastmonth = DateTime.Now.AddMonths(-1);
        foreach (var mover in globalusers)
            using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=max&ucend=" + lastmonth.ToString("yyyy-MM-dd") + "T00:00:00.000Z&ucprop=comment&ucuser=" + Uri.EscapeDataString(mover)).Result)))
                while (rdr.Read())
                    if (rdr.Name == "item" && rdr.GetAttribute("comment") != null)
                        if (rdr.GetAttribute("comment").Contains("GR]") && !badusers.Contains(mover))
                        {
                            globalusers_needs_flag.Add(mover);
                            break;
                        }

        if (globalusers_needs_flag.Count > 0)
        {
            string zkatext = site.GetStringAsync("https://ru.wikipedia.org/wiki/ВП:Запросы к администраторам?action=raw").Result;
            var header = new Regex(@"(^\{[^\n]*\}\s*<[^>]*>\n)");
            string newmessage = "==Выдать апата глобальным правщикам==\nПеречисленные ниже участники занимаются переименованием файлов на Викискладе с заменой включений во всех разделах. В соответствии с [[ВП:ПАТ#ГЛОБ]] прошу рассмотреть их вклад и выдать им апата, чтобы такие правки не распатрулировали страницы.";
            foreach (var mover in globalusers_needs_flag)
                newmessage += "\n* [[special:contribs/" + mover + "|" + mover + "]]";
            newmessage += "\n~~~~\n\n";
            if (header.IsMatch(zkatext))
                Save(site, "ru", "ВП:Запросы к администраторам", header.Replace(zkatext, "$1" + "\n\n" + newmessage), "новые переименовывающие для выдачи апата");
            else
                Save(site, "ru", "ВП:Запросы к администраторам", newmessage + zkatext, "новые переименовывающие для выдачи апата");
        }
    }
    static int position_number = 0;
    static string resulttext_per_year, resulttext_per_month, resulttext_alltime, user, common_resulttext = "{{самые активные участники}}{{Плавающая шапка таблицы}}{{shortcut|ВП:ИТОГИ}}<center>\nСтатистика" +
        " по числу итогов, подведённых %type%.\n\nСтатистика собирается поиском по тексту страниц обсуждений и потому верна лишь приближённо, нестандартный синтаксис итога или подписи итогоподводящего " +
        "может привести к тому, что такой итог не будет засчитан. Первично отсортировано по сумме всех итогов, кроме итогов на КУЛ и ЗКП(АУ).\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!" +
        "Участник!!Σ!!{{vh|[[ВП:КУ|]]}}!!{{vh|[[ВП:ВУС|]]}}!!{{vh|[[ВП:КПМ|]]}}!!{{vh|[[ВП:ПУЗ|]]}}!!{{vh|[[ВП:КОБ|]]+[[ВП:КРАЗД|РАЗД]]}}!!{{vh|[[ВП:ОБК|]]}}!!{{vh|[[ВП:КУЛ|]]}}!!{{vh|[[ВП:ЗКА|]]}}!!" +
        "{{vh|[[ВП:ОСП|]]+[[ВП:ОАД|]]}}!!{{vh|[[ВП:ЗС|]]}}!!{{vh|[[ВП:ЗС-|]]}}!!{{vh|[[ВП:ЗСП|ЗС]]+[[ВП:ЗСАП|(А)П]]}}!!{{vh|[[ВП:ЗСПИ|]]}}!!{{vh|[[ВП:ЗСФ|]]}}!!{{vh|[[ВП:КОИ|]]}}!!{{vh|[[ВП:ИСЛ|]]}}!!" +
        "{{vh|[[ВП:ЗКП|]][[ВП:ЗКПАУ|(АУ)]]}}!!{{vh|[[ВП:КИС|]]}}!!{{vh|[[ВП:КИСЛ|]]}}!!{{vh|[[ВП:КХС|]]}}!!{{vh|[[ВП:КЛСХС|]]}}!!{{vh|[[ВП:КДС|]]}}!!{{vh|[[ВП:КЛСДС|]]}}!!{{vh|[[ВП:КИСП|]]}}!!{{vh|[[ВП:" +
        "КЛСИСП|]]}}!!{{vh|[[ВП:РДБ|]]}}!!{{vh|[[ВП:ФТ|]]+[[ВП:ТЗ|]]}}!!{{vh|[[ВП:Ф-АП|АП]]}}!!{{vh|[[ПРО:ИНК-МР|]]}}";
    static Dictionary<string, Dictionary<string, Dictionary<string, int>>> stats = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>
    { { "month", new Dictionary<string, Dictionary<string, int>>() }, { "year", new Dictionary<string, Dictionary<string, int>>() }, { "alltime", new Dictionary<string, Dictionary<string, int>>() } };
    static void writerow(KeyValuePair<string, Dictionary<string, int>> s, string type)
    {
        string newrow = "\n|-\n|" + ++position_number + "||{{u|" + s.Key + "}}||" + cell(s.Value["sum"]) + "||" + cell(s.Value["К удалению"]) + "||" + cell(s.Value["К восстановлению"]) + "||" + cell(
            s.Value["К переименованию"]) + "||" + cell(s.Value["Запросы на переименование учётных записей"]) + "||" + cell(s.Value["К объединению"] + s.Value["К разделению"]) + "||" + cell(s.Value[
                "Обсуждение категорий"]) + "||" + cell(s.Value["К улучшению"]) + "||" + cell(s.Value["Запросы к администраторам"]) + "||" + cell(s.Value["Оспаривание итогов"] + s.Value["Оспаривание " +
                "административных действий"]) + "||" + cell(s.Value["Установка защиты"]) + "||" + cell(s.Value["Снятие защиты"]) + "||" + cell(s.Value["Заявки на статус автопатрулируемого"] + s.Value[
                    "Заявки на статус патрулирующего"]) + "||" + cell(s.Value["Заявки на статус подводящего итоги"]) + "||" + cell(s.Value["Заявки на снятие флагов"]) + "||" + cell(s.Value["К оценке " +
                    "источников"]) + "||" + cell(s.Value["Изменение спам-листа"]) + "||" + cell(s.Value["Запросы к патрулирующим от автоподтверждённых участников"] + s.Value["Запросы к патрулирующим"]) +
                    "||" + cell(s.Value["Избранные статьи/Кандидаты"]) + "||" + cell(s.Value["Избранные статьи/Кандидаты в устаревшие"]) + "||" + cell(s.Value["Хорошие статьи/Кандидаты"]) + "||" + cell(
                        s.Value["Хорошие статьи/К лишению статуса"]) + "||" + cell(s.Value["Добротные статьи/Кандидаты"]) + "||" + cell(s.Value["Добротные статьи/К лишению статуса"]) + "||" + cell(s.Value
                        ["Избранные списки и порталы/Кандидаты"]) + "||" + cell(s.Value["Избранные списки и порталы/К лишению статуса"]) + "||" + cell(s.Value["Запросы к ботоводам"]) + "||" + cell(s.Value
                        ["Форум/Архив/Технический"] + s.Value["Технические запросы"]) + "||" + cell(s.Value["Форум/Архив/Авторское право"]) + "||" + cell(s.Value["Инкубатор/Мини-рецензирование"]);
        if (type == "month")
            resulttext_per_month += newrow;
        else if (type == "year")
            resulttext_per_year += newrow;
        else
            resulttext_alltime += newrow;
    }
    static void initialize(string type, string pagetype)
    {
        if (!stats[type].ContainsKey(user))
            stats[type].Add(user, new Dictionary<string, int>() { { "К удалению", 0 },{ "К улучшению", 0 },{ "К разделению", 0 },{ "К объединению", 0 },{ "К переименованию", 0 },{ "К восстановлению", 0 },
                { "Обсуждение категорий", 0 }, { "Снятие защиты", 0 }, { "Установка защиты", 0 }, { "sum", 0 }, { "Изменение спам-листа", 0 }, { "Запросы к администраторам", 0 },{ "К оценке источников", 0 },
                { "Инкубатор/Мини-рецензирование", 0 }, { "Заявки на статус патрулирующего", 0 },{ "Заявки на статус автопатрулируемого", 0 }, { "Заявки на статус подводящего итоги", 0 },
                { "Оспаривание итогов", 0 }, { "Заявки на снятие флагов", 0 },{ "Оспаривание административных действий", 0 }, { "Хорошие статьи/Кандидаты", 0 }, { "Добротные статьи/Кандидаты", 0 },
                { "Избранные списки и порталы/Кандидаты", 0 }, { "Избранные статьи/Кандидаты", 0 }, { "Запросы к ботоводам", 0 }, { "Запросы к патрулирующим", 0 },{ "Технические запросы", 0 },
                { "Форум/Архив/Технический", 0 },{ "Запросы к патрулирующим от автоподтверждённых участников", 0 },{ "Избранные статьи/Кандидаты в устаревшие", 0 },{ "Добротные статьи/К лишению статуса", 0 },
                { "Хорошие статьи/К лишению статуса", 0 },{ "Избранные списки и порталы/К лишению статуса", 0 }, { "Запросы на переименование учётных записей", 0},{ "Форум/Архив/Авторское право", 0 } });
        stats[type][user]["sum"]++;
        stats[type][user][pagetype]++;
    }
    static void summary_stats()
    {
        var alltime = false;
        var lastmonthdate = now.AddMonths(-1);
        var lastyear = now.AddYears(-1);
        var first_not_fully_summaried_year = new Dictionary<string, int>
        {
            { "К удалению", 2018 },{ "К улучшению", 2018 },{ "К разделению", 2018 },{ "К объединению", 2015 },{ "К переименованию", 2015 },{ "К восстановлению", 2018 },{ "Обсуждение категорий", 2017 },
            { "Снятие защиты", 0 },{ "Установка защиты", 0 },{ "Оспаривание итогов", 0 },{ "Оспаривание административных действий", 0 },{ "Форум/Архив/Технический", 0 },{ "Технические запросы", 0 },
            { "К оценке источников", 0 },{ "Изменение спам-листа", 0 },{ "Запросы к патрулирующим", 0 },{ "Запросы к патрулирующим от автоподтверждённых участников", 0 },{ "Запросы к ботоводам", 0 },
            { "Заявки на снятие флагов", 0 },{ "Запросы к администраторам", 0 },{ "Инкубатор/Мини-рецензирование", 0 },{ "Хорошие статьи/Кандидаты", 0 },{ "Избранные статьи/Кандидаты", 0 },
            { "Добротные статьи/Кандидаты", 0 },{ "Избранные списки и порталы/Кандидаты", 0 },{ "Заявки на статус патрулирующего", 0 },{ "Заявки на статус подводящего итоги", 0 }, { "Заявки на статус" +
            " автопатрулируемого", 0 },{ "Избранные статьи/Кандидаты в устаревшие", 0 },{ "Хорошие статьи/К лишению статуса", 0 },{ "Добротные статьи/К лишению статуса", 0 }, { "Избранные списки и " +
            "порталы/К лишению статуса", 0 }, {"Запросы на переименование учётных записей", 0},{ "Форум/Архив/Авторское право", 0 }
        };
        var monthnumbers = new Dictionary<string, int>{{ "января", 1 },{ "февраля", 2 },{ "марта", 3 },{ "апреля", 4 },{ "мая", 5 },{ "июня", 6 },{ "июля", 7 },{ "августа", 8 },
            { "сентября", 9 },{ "октября", 10 },{ "ноября", 11 },{ "декабря", 12 }};//НЕ ПЕРЕНОСИТЬ СТРОКУ НИЖЕ, ОНА ЛОМАЕТСЯ
        var summary_rgx = new Regex(@"={1,}\s*(Итог)[^=\n]*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря) (\d{4}) \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var rdb_zkp_summary_rgx = new Regex(@"(done|сделано|отпатрулировано|отклонено)\s*\}\}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря) (\d{4}) \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var yearrgx = new Regex(@"\d{4}");
        foreach (var pagetype in first_not_fully_summaried_year.Keys)
        {
            int ns;
            if (pagetype.Contains("статьи") || pagetype.Contains("списки") || pagetype.Contains("нкубатор"))
                ns = 104;
            else ns = 4;
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&list=allpages&apprefix=" + pagetype + "&apnamespace=" + ns + "&aplimit=max";

            while (cont != "-")
            {
                Root response = JsonConvert.DeserializeObject<Root>(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
                if (response.@continue != null)
                    cont = response.@continue.apcontinue;
                else
                    cont = "-";
                foreach (var pageinfo in response.query.allpages)
                {
                    string pagetitle = pageinfo.title;
                    bool correctpage = false;
                    int startyear = alltime ? 0 : (first_not_fully_summaried_year[pagetype] == 0 ? lastyear.Year : first_not_fully_summaried_year[pagetype]);
                    if (pagetitle.Contains("Избранные"))
                        correctpage = true;
                    else if (yearrgx.IsMatch(pagetitle))
                        if (Convert.ToInt16(yearrgx.Match(pagetitle).Value) >= startyear)
                            correctpage = true;
                        else if (pagetitle.IndexOf('/') == -1)
                            correctpage = true;
                    if (correctpage)
                    {
                        string pagetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(pagetitle) + "?action=raw").Result;
                        var summaries = (pagetype == "Запросы к ботоводам" || pagetype == "Запросы к патрулирующим от автоподтверждённых участников" || pagetype == "Запросы к патрулирующим") ?
                            rdb_zkp_summary_rgx.Matches(pagetext) : summary_rgx.Matches(pagetext);
                        foreach (Match summary in summaries)
                        {
                            int signature_year = Convert.ToInt16(summary.Groups[7].Value);
                            int signature_month = monthnumbers[summary.Groups[6].Value];
                            user = summary.Groups[4].ToString().Replace('_', ' ');
                            if (user.Contains("/"))
                                user = user.Substring(0, user.IndexOf("/"));
                            if (user == "TextworkerBot")
                                continue;
                            initialize("alltime", pagetype);
                            if (signature_year == lastmonthdate.Year && signature_month == lastmonthdate.Month)
                                initialize("month", pagetype);
                            if (signature_year == lastmonthdate.Year || (signature_year == lastmonthdate.Year - 1 && signature_month > lastmonthdate.Month))
                                initialize("year", pagetype);
                        }
                    }
                }
            }
        }

        if (alltime)
        {
            resulttext_alltime = common_resulttext.Replace("%type%", "за все годы существования Русской Википедии").Replace("%otherpage%", "итоги за [[ВП:Статистика итогов|последний месяц]] и [[ВП:Статистика итогов/За год|год]]");
            foreach (var s in stats["alltime"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
                writerow(s, "alltime");
            Save(site, "ru", "ВП:Статистика итогов/За всё время", resulttext_alltime + "\n|}", "");
        }
        else
        {
            resulttext_per_month = common_resulttext.Replace("%type%", "в " + prepositional[lastmonthdate.Month] + " " + lastmonthdate.Year + " года");
            resulttext_per_year = common_resulttext.Replace("%type%", "за последние 12 месяцев");
            foreach (var s in stats["year"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
                writerow(s, "year");
            position_number = 0;
            foreach (var s in stats["month"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
                writerow(s, "month");
            Save(site, "ru", "ВП:Статистика итогов/За год", resulttext_per_year + "\n|}", "");
            Save(site, "ru", "ВП:Статистика итогов", resulttext_per_month + "\n|}", "");
        }
    }
    static void popular_wd_items_without_ru()
    {
        int numofitemstoanalyze = 150000; //100k is okay, 1m isn't
        var allitems = new Dictionary<string, int>();
        var nonruitems = new Dictionary<string, int>();
        string result = "<center>\n{|class=\"standard\"\n!Страница!!Кол-во интервик";
        var connect = new MySqlConnection(creds[2].Replace("%project%", "wikidatawiki"));
        connect.Open();
        var query = new MySqlCommand("select ips_item_id, count(*) cnt from wb_items_per_site group by ips_item_id order by cnt desc limit " + numofitemstoanalyze + ";", connect);
        query.CommandTimeout = 99999;
        MySqlDataReader r = query.ExecuteReader();
        while (r.Read())
            allitems.Add(r.GetString("ips_item_id"), r.GetInt16("cnt"));
        r.Close();
        foreach (var i in allitems)
        {
            query = new MySqlCommand("select ips_site_page from wb_items_per_site where ips_site_id=\"ruwiki\" and ips_item_id=" + i.Key + ";", connect);
            r = query.ExecuteReader();
            if (!r.Read())
                nonruitems.Add(i.Key, i.Value);
            r.Close();
        }
        foreach (var n in nonruitems)
        {
            query = new MySqlCommand("select cast(ips_site_page as char) title from wb_items_per_site where ips_site_id=\"enwiki\" and ips_item_id=" + n.Key + ";", connect);
            r = query.ExecuteReader();
            if (r.Read())
            {
                string title = r.GetString(0);
                if (!title.StartsWith("Template:") && !title.StartsWith("Category:") && !title.StartsWith("Module:") && !title.StartsWith("Wikipedia:") && !title.StartsWith("Help:") && !title.StartsWith("Portal:"))
                    result += "\n|-\n|[[:en:" + title + "]]||" + n.Value;
            }
            r.Close();
        }
        Save(site, "ru", "ВП:К созданию/Статьи с наибольшим числом интервик без русской", result + "\n|}{{Проект:Словники/Шаблон:Списки недостающих статей}}[[Категория:Википедия:Статьи без русских интервик]]", "");
    }
    static Dictionary<string, string> tableheader = new Dictionary<string, string>() { { "ru", "Статья!!Пик!!Медиана!!Дата пика" }, { "uk", "Стаття!!Пік!!Медіана!!Дата піку" },
        { "be", "Артыкул!!Пік!!Медыяна!!Дата піка" } };
    static Dictionary<string, string> enddate = new Dictionary<string, string>() { { "01", "31" }, { "02", "28" }, { "03", "31" }, { "04", "30" }, { "05", "31" }, { "06", "30" }, { "07", "31" },
            { "08", "31" }, { "09", "30" }, { "10", "31" }, { "11", "30" }, { "12", "31" } };
    static Dictionary<string, Dictionary<string, string>> outputpage = new Dictionary<string, Dictionary<string, string>>
        { { "uk", new Dictionary<string, string>() { { "month", "Вікіпедія:Спалахи інтересу до статей" }, { "year", "Вікіпедія:Спалахи інтересу до статей/За рік" }, { "total", "Вікіпедія:Спалахи інтересу до статей/За весь час" } } },
            { "be", new Dictionary<string, string>() { { "month", "Вікіпедыя:Папулярныя артыкулы" }, { "year", "Вікіпедыя:Папулярныя артыкулы/За год" }, { "total", "Вікіпедыя:Папулярныя артыкулы/За ўвесь час" } } },
            { "ru", new Dictionary<string, string>() { { "month", "ВП:Популярные статьи/Пики за месяц" }, { "year", "ВП:Популярные статьи/Пики за год" }, { "total", "ВП:Популярные статьи/Пики за всё время" } } } };
    static Dictionary<string, Dictionary<string, int>> minneededpeakvalue = new Dictionary<string, Dictionary<string, int>>
        { { "ru", new Dictionary<string, int>() { { "month", 10000 }, { "year", 15000 }, { "total", 20000 }, } },
            { "uk", new Dictionary<string, int>() { { "month", 1000 }, { "year", 2000 }, { "total", 3000 }, } },
            { "be", new Dictionary<string, int>() { { "month", 15 }, { "year", 30 }, { "total", 100 }, } } };
    static Dictionary<string, Dictionary<string, string>> monthnames = new Dictionary<string, Dictionary<string, string>>
        { {"ru", new Dictionary<string, string>() { {"01","января"}, {"02","февраля"}, {"03","марта"}, {"04","апреля"}, {"05","мая"}, {"06","июня"}, {"07","июля"}, {"08","августа"},
                {"09","сентября"}, {"10","октября"}, {"11","ноября"}, {"12","декабря"} } },
            {"uk", new Dictionary<string, string>() { {"01","січня"}, {"02","лютого"}, {"03","березня"}, {"04","квітня"}, {"05","травня"}, {"06","червня"}, {"07","липня"}, {"08","серпня"},
                {"09","вересня"}, {"10","жовтня"}, {"11","листопада"}, {"12","грудня"} } },
            {"be", new Dictionary<string, string>() { {"01","студзеня"}, {"02","лютага"}, {"03","сакавіка"}, {"04","красавіка"}, {"05","траўня"}, {"06","чэрвеня"}, {"07","ліпеня"}, {"08","жніўня"},
                {"09","верасня"}, {"10","кастрычніка"}, {"11","лістапада"}, {"12","снежня"} } } };
    static WebClient cl = new WebClient();
    static void pageview_peaks()
    {
        int year_of_previous_month = now.AddMonths(-1).Year;
        string lastmonth = DateTime.Now.AddMonths(-1).ToString("MM");
        if (lastmonth != "12")
            process_pageviews("month", year_of_previous_month + lastmonth + "01/" + year_of_previous_month + lastmonth + enddate[lastmonth]);
        else
        {
            process_pageviews("year", year_of_previous_month + "0101/" + year_of_previous_month + "1231");
            process_pageviews("month", year_of_previous_month + lastmonth + "01/" + year_of_previous_month + lastmonth + enddate[lastmonth]);
            process_pageviews("total", "20150701/" + year_of_previous_month + "1231");
        }
    }
    static void process_pageviews(string mode, string reqstr_period)
    {
        cl.Headers.Add("user-agent", "Stats grabber of ruwiki user MBH");
        foreach (string lang in new HashSet<string>() { "uk", "be", "ru" })
        {
            var results = new Dictionary<string, pageviews_result>();
            var site = Site(lang, creds[0], creds[1]);
            string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allpages&apnamespace=0&apfilterredir=nonredirects&aplimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                        {
                            string page = r.GetAttribute("title");
                            var thispagestats = new Dictionary<string, int>();
                            string currres = "";
                            string reqstr = "";
                            reqstr = "https://wikimedia.org/api/rest_v1/metrics/pageviews/per-article/" + lang + ".wikipedia/all-access/user/" + Uri.EscapeDataString(page) + "/daily/" + reqstr_period;
                            try
                            {
                                currres = cl.DownloadString(reqstr);
                            }
                            catch
                            {
                                continue;
                            }
                            int maxviews = 0;
                            string peakdate = "";
                            foreach (Match match in Regex.Matches(currres, "(\\d{10})\",\"access\":\"all-access\",\"agent\":\"user\",\"views\":(\\d*)"))
                            {
                                int views = Convert.ToInt32(match.Groups[2].Value);
                                string date = match.Groups[1].Value;
                                thispagestats.Add(date, views);
                                if (views > maxviews)
                                {
                                    maxviews = views;
                                    peakdate = date;
                                }
                            }
                            var orderedlist = thispagestats.OrderBy(o => o.Value).ToList();
                            int median = orderedlist[orderedlist.Count / 2].Value;
                            int currentminneededpeakvalue = minneededpeakvalue[lang][mode];
                            if (maxviews >= currentminneededpeakvalue)
                                results.Add(page, new pageviews_result() { date = peakdate, max = maxviews, median = median });
                        }
                }
            }
            string result = "{{popular pages}}{{floating table header}}<center>\n{|class=\"standard sortable ts-stickytableheader\" style=\"text-align:center\"\n!" + tableheader[lang];
            foreach (var r in results.OrderByDescending(r => r.Value.max))
            {
                string month = r.Value.date.Substring(4, 2);
                string day = r.Value.date.Substring(6, 2);
                string date = mode == "total" ? r.Value.date.Substring(0, 4) + "-" + month + "-" + day : "{{~|" + month + day + "}}" + day + " " + monthnames[lang][month];
                result += "\n|-\n|[[" + r.Key + "]]||{{formatnum:" + r.Value.max + "}}||" + r.Value.median + "||" + date;
            }
            Save(site, lang, outputpage[lang][mode], result + "\n|}", "");
        }
    }
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = Site("ru", creds[0], creds[1]);
        monthname[1] = "январь"; monthname[2] = "февраль"; monthname[3] = "март"; monthname[4] = "апрель"; monthname[5] = "май"; monthname[6] = "июнь";
        monthname[7] = "июль"; monthname[8] = "август"; monthname[9] = "сентябрь"; monthname[10] = "октябрь"; monthname[11] = "ноябрь"; monthname[12] = "декабрь";
        prepositional[1] = "январе"; prepositional[2] = "феврале"; prepositional[3] = "марте"; prepositional[4] = "апреле"; prepositional[5] = "мае"; prepositional[6] = "июне";
        prepositional[7] = "июле"; prepositional[8] = "августе"; prepositional[9] = "сентябре"; prepositional[10] = "октябре"; prepositional[11] = "ноябре"; prepositional[12] = "декабре";
        incorrect_redirects();
        popular_userscripts();
        most_edits();
        most_watched_pages();
        adminstats();
        likes_stats();
        summary_stats();
        popular_wd_items_without_ru();
        page_creators();
        apat_for_filemovers();
        pats_awarding();
        pageview_peaks();
    }
}
