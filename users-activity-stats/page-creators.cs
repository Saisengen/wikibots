using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DotNetWikiBot;
using System.Xml;
using MySql.Data.MySqlClient;

class Record
{
    public int art, redir, disamb, templ, cat, file;
}
class Program
{
    static void Main()
    {
        var falsebots = new Dictionary<string, string[]>() { { "ru", new string[] { "Alex Smotrov", "Wind", "Tutaishy" } }, { "kk", new string[] { "Arystanbek", "Нұрлан_Рахымжанов" } } };
        var resultpage = new Dictionary<string, string>() { { "ru", "ВП:Участники по числу созданных страниц" }, { "kk", "Уикипедия:Бет бастауы бойынша қатысушылар" } };
        var disambigcategory = new Dictionary<string, string>() { { "ru", "Категория:Страницы значений по алфавиту" }, { "kk", "Санат:Алфавит бойынша айрық беттер" } };
        var headers = new Dictionary<string, string>() { { "ru", "{{Плавающая шапка таблицы}}{{shortcut|ВП:УПЧС}}{{clear}}{{Самые активные участники}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!Участник!!Статьи!!Редиректы!!Дизамбиги!!Шаблоны!!Категории!!Файлы" },
            { "kk", "{{shortcut|УП:ББҚ}}<center>{{StatInfo}}\n{|class=\"standard sortable ts-stickytableheader\"\n!#!!Қатысушы!!Мақалалар!!Бағыттау беттері!!Айрық беттер!!Үлгілер!!Санаттар!!Файлдар" } };
        var footers = new Dictionary<string, string>() { { "ru", "" }, { "kk", "\n{{Wikistats}}[[Санат:Уикипедия:Қатысушылар]]" } };
        var limit = new Dictionary<string, int>() { { "ru", 100 }, { "kk", 50 } };
        int status_update_freq = 100000;
        var disambs = new HashSet<string>();
        var users = new Dictionary<string, Record>();
        var bestusers = new Dictionary<string, Record>();
        var bots = new HashSet<string>();
        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        foreach (var lang in new string[] { "ru", "kk" })
        {
            var connect = new MySqlConnection("Server=" + lang + "wiki.labsdb;Database=" + lang + "wiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
            connect.Open();
            MySqlCommand command = new MySqlCommand("select distinct cast(log_title as char) title from logging where log_type=\"rights\" and log_params like \"%bot%\";", connect) { CommandTimeout = 9999 };
            MySqlDataReader rdr;
            rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                string bot = rdr.GetString("title");
                if (!falsebots[lang].Contains(bot))
                    bots.Add(bot.Replace("_", " "));
            }
            rdr.Close();
            var site = new Site("https://" + lang + ".wikipedia.org", creds[0], creds[1]);
            string cont = "", query = "/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=" + disambigcategory[lang] + "&cmprop=ids&cmlimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&cmcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                    while (r.Read())
                        if (r.Name == "cm")
                            disambs.Add(r.GetAttribute("pageid"));
                }
            }
            Console.WriteLine(DateTime.Now);
            int c;
            foreach (int ns in new int[] { 0, 6, 10, 14 })
            {
                c = 0;
                cont = ""; query = "/w/api.php?action=query&format=xml&list=allpages&aplimit=max&apfilterredir=nonredirects&apnamespace=" + ns;
                while (cont != null)
                {
                    string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&apcontinue=" + Uri.EscapeDataString(cont)));
                    using (var r = new XmlTextReader(new StringReader(apiout)))
                    {
                        r.WhitespaceHandling = WhitespaceHandling.None;
                        r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                        while (r.Read())
                            if (r.Name == "p")
                            {
                                string pageid = r.GetAttribute("pageid");
                                if (++c % status_update_freq == 0)
                                    Console.WriteLine(ns + " " + c + " " + DateTime.Now);
                                string user = "";
                                try
                                {
                                    using (var rr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=revisions&rvprop=user&rvlimit=1&rvdir=newer&pageids=" + pageid))))
                                        while (rr.Read())
                                            if (rr.Name == "rev")
                                                user = rr.GetAttribute("user");
                                }
                                catch
                                {
                                    continue;
                                }
                                if (user == null || user == "")
                                    continue;
                                if (!users.ContainsKey(user))
                                    users.Add(user, new Record());
                                if (ns == 0)
                                {
                                    if (disambs.Contains(pageid))
                                        users[user].disamb++;
                                    else
                                        users[user].art++;
                                }
                                else if (ns == 10)
                                    users[user].templ++;
                                else if (ns == 14)
                                    users[user].cat++;
                                else users[user].file++;
                            }
                    }
                }
            }
            c = 0;
            cont = ""; query = "/w/api.php?action=query&format=xml&list=allpages&aplimit=max&apfilterredir=redirects&apnamespace=0";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&apcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                        {
                            string pageid = r.GetAttribute("pageid");
                            if (++c % status_update_freq == 0)
                                Console.WriteLine("0r " + c + " " + DateTime.Now);
                            string user = "";
                            try
                            {
                                using (var rr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=revisions&rvprop=user&rvlimit=1&rvdir=newer&pageids=" + pageid))))
                                    while (rr.Read())
                                        if (rr.Name == "rev")
                                            user = rr.GetAttribute("user");
                            }
                            catch
                            {
                                continue;
                            }
                            if (user == null)
                                continue;
                            if (!users.ContainsKey(user))
                                users.Add(user, new Record());
                            users[user].redir++;
                        }
                }
            }
            foreach (var u in users)
                if (u.Value.art + u.Value.redir + u.Value.disamb + u.Value.cat + u.Value.file + u.Value.templ >= limit[lang])
                    bestusers.Add(u.Key, u.Value);
            string result = headers[lang];
            int index = 0;
            foreach (var u in bestusers.OrderByDescending(u => u.Value.art))
            {
                bool bot = bots.Contains(u.Key);
                string color = (bot ? "style=\"background-color:#ddf\"" : "");
                string number = (bot ? "" : (++index).ToString());
                result += "\n|-" + color + "\n|" + number + "||{{u|" + (u.Key.Contains('=') ? "1=" + u.Key : u.Key) + "}}||" + u.Value.art + "||" + u.Value.redir + "||" + u.Value.disamb + "||" +
                    u.Value.templ + "||" + u.Value.cat + "||" + u.Value.file;
            }
            result += "\n|}" + footers[lang];
            Console.WriteLine(DateTime.Now);
            site = new Site("https://" + lang + ".wikipedia.org", creds[0], creds[1]);
            var page = new Page(resultpage[lang]);
            page.Save(result);
        }
    }
}
