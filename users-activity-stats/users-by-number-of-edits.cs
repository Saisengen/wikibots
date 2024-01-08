using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using MySql.Data.MySqlClient;
using System.Web.UI;
using System.Net;
using System.Net.Http;

class record
{
    public int all, main, user, templ, file, cat, portproj, meta, tech, main_edits_index;
    public bool globalbot;
}

class Program
{
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
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, {"lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(HttpClient site, string lang, string title, string text)
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
        request.Add(new StringContent(token), "token");
        request.Add(new StringContent("xml"), "format");
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result;
        Console.WriteLine(DateTime.Now.ToString() + " writing " + lang + ":" + title);
    }
    static void Main()
    {
        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        var falsebots = new Dictionary<string, string[]>() { { "ru", new string[] { "Alex Smotrov", "Wind", "Tutaishy" } }, { "be", new string[] { "Maksim L.", "Artsiom91" } }, { "kk", new string[] { "Arystanbek", "Нұрлан Рахымжанов" } } };
        var min_num_of_edits = new Dictionary<string, int>() { { "ru", 10000 }, { "be", 5000 }, { "kk", 500 } };

        var headers = new Dictionary<string, string>() { { "ru", "{{Плавающая шапка таблицы}}{{Самые активные участники}}<center>\nВ каждой колонке приведена сумма правок в указанном пространстве и его обсуждении. Первично отсортировано и пронумеровано по общему числу правок.%specific_text%\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!{{abbr|№ п/с|место по числу правок в статьях|0}}!!Участник!!Всего правок!!В статьях!!шаблонах!!файлах!!категориях!!порталах и проектах!!модулях и MediaWiki!!страницах участников!!метапедических страницах" },
            { "be", "{{Самыя актыўныя ўдзельнікі}}<center>У кожным слупку прыведзена сума правак у адпаведнай прасторы і размовах пра яе. Першасна адсартавана і пранумаравана паводле агульнай колькасці правак.%specific_text%\n{|class=\"standard sortable\"\n!№!!{{abbr|№ п/с|месца па колькасці правак у артыкулах|0}}!!Удзельнік!!Агулам правак!!У артыкулах!!шаблонах!!файлах!!катэгорыях!!парталах і праектах!!модулях і MediaWiki!!старонках удзельнікаў!!метапедычных старонках" },
            { "kk", "<center>Әрбір бағанда көрсетілген кеңістіктегі және оның талқылауындағы өңдеулер саны берілген. Ең алдымен жалпы түзетулер бойынша сұрыпталған және нөмірленген.%specific_text%\n{{StatInfo}}\n{|class=\"standard sortable ts-stickytableheader\"\n!#!!{{abbr|#м/о|мақалалардағы өңдеме саны бойынша орны|0}}!!Қатысушы!!Барлық өңдемесі!!Мақалалар!!Үлгілер!!Файлдар!!Санаттар!!Порталдар + жобалар!!Модулдар + MediaWiki!!Қатысушы беттері!!Метапедиялық (Уикипедия)" } };

        var resultpages = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "ВП:Самые активные боты", Second = "ВП:Участники по числу правок" } },
            { "be", new Pair() { First = "Вікіпедыя:Боты паводле колькасці правак", Second = "Вікіпедыя:Удзельнікі паводле колькасці правак" } },
            { "kk", new Pair() { First = "Уикипедия:Өңдеме саны бойынша боттар", Second = "Уикипедия:Өңдеме саны бойынша қатысушылар" } } };

        var footers = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "[[К:Википедия:Боты]]", Second = "" } },
            { "be", new Pair() { First = "[[Катэгорыя:Вікіпедыя:Боты]][[Катэгорыя:Вікіпедыя:Статыстыка і прагнозы]]", Second = "[[Катэгорыя:Вікіпедыя:Статыстыка і прагнозы]]" } },
            { "kk", new Pair() { First = "{{Wikistats}}[[Санат:Уикипедия:Боттар]]", Second = "{{Wikistats}}[[Санат:Уикипедия:Қатысушылар]]" } } };

        var shortcuts = new Dictionary<string, Pair>() { { "ru", new Pair() { First = "ВП:САБ", Second = "ВП:САУ" } }, { "be", new Pair() { First = "ВП:САБ", Second = "ВП:САУ" } }, { "kk", new Pair() { First = "УП:ӨСБ", Second = "УП:ӨСҚ" } } };

        foreach (var lang in new string[] { "ru", "be", "kk" })
        {
            var hdr_modifications = new Dictionary<string, Pair>() { { "ru", new Pair() { First = " Голубым выделены глобальные боты без локального флага.", Second = " В список включены участники, имеющие не менее " + min_num_of_edits[lang] + " правок." } },
            { "be", new Pair() { First = " Блакітным вылучаныя глабальныя боты без лакальнага сцяга.", Second = " У спіс уключаны ўдзельнікі, якія маюць не менш за " + min_num_of_edits[lang] + " правак." } },
            { "kk", new Pair() { First = " Жергілікті жалаусыз ғаламдық боттар көкпен ерекшеленген.", Second = " Тізімге " + min_num_of_edits[lang] + " өңдемеден кем емес өңдеме жасаған қатысушылар кірістірілген." } } };

            var users = new Dictionary<string, record>();
            var bots = new Dictionary<string, record>();
            var connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            var command = new MySqlCommand("select distinct cast(log_title as char) bot from logging where log_type=\"rights\" and log_params like \"%bot%\";", connect) { CommandTimeout = 9999 };
            var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string bot = reader.GetString("bot").Replace("_", " ");
                if (!falsebots[lang].Contains(bot))
                    bots.Add(bot, new record() { globalbot = false });
            }
            reader.Close();

            command.CommandText = "select cast(user_name as char) user from user where user_editcount >= " + min_num_of_edits[lang] + ";";
            reader = command.ExecuteReader();
            while (reader.Read())
            {
                string user = reader.GetString("user");
                if (!bots.ContainsKey(user))
                    users.Add(user, new record());
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
                    bots.Add(bot, new record() { globalbot = true });
                    users.Remove(bot);
                }
            }
            reader.Close();
            connect.Close();

            var site = Site(lang, creds[0], creds[1]);
            foreach (var type in new Dictionary<string, record>[] { users, bots })
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
            
            string result = "{{shortcut|" + shortcuts[lang].First + "}}" + headers[lang].Replace("%specific_text%", hdr_modifications[lang].First.ToString());

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
            Save(site, lang, resultpages[lang].First.ToString(), result);
            
            all_edits_index = 0;
            result = "{{shortcut|" + shortcuts[lang].Second + "}}" + headers[lang].Replace("%specific_text%", hdr_modifications[lang].Second.ToString());
            foreach (var s in users.OrderByDescending(s => s.Value.all))
                result += "\n|-\n|" + ++all_edits_index + "||" + s.Value.main_edits_index + "||{{u|" + s.Key + "}}||" + s.Value.all + "||" + s.Value.main + "||" + s.Value.templ + "||" + s.Value.file + "||" + s.Value.cat + "||" + s.Value.portproj + "||" + s.Value.tech + "||" + s.Value.user + "||" + s.Value.meta;
            result += "\n|}" + footers[lang].Second;
            Save(site, lang, resultpages[lang].Second.ToString(), result);
        }
    }
}
