using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
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

    static int cntr = 0;
    static string resulttext_per_year, resulttext_per_month;
    static void writerow(KeyValuePair<string, Dictionary<string, int>> s, bool per_year)
    {
        string newrow = "\n|-\n|" + ++cntr + "||{{u|" + s.Key + "}}||" + s.Value["sum"] + "||" + s.Value["К удалению"] + "||" + s.Value["К восстановлению"] + "||" + s.Value["К переименованию"] + "||" + (s.Value["К объединению"] + s.Value["К разделению"]) + "||" + 
            s.Value["Обсуждение категорий"] + "||" + s.Value["К улучшению"] + "||" + s.Value["Запросы к администраторам"] + "||" + (s.Value["Оспаривание итогов"] + s.Value["Оспаривание административных действий"]) + "||" + s.Value["К оценке источников"] + "||" + 
            (s.Value["Установка защиты"] + s.Value["Снятие защиты"]) + "||" + (s.Value["Заявки на статус автопатрулируемого"] + s.Value["Заявки на статус патрулирующего"]) + "||" + s.Value["Заявки на статус подводящего итоги"] + "||" + s.Value["Заявки на снятие флагов"] + "||" + 
            s.Value["Изменение спам-листа"] + "||" + s.Value["Инкубатор/Мини-рецензирование"];
        if (per_year)
            resulttext_per_year += newrow;
        else
            resulttext_per_month += newrow;
    }
    static void Main()
    {
        var dtn = DateTime.Now;
        var lastmonthdate = dtn.AddMonths(-1);
        var last2monthdate = dtn.AddMonths(-2);
        var archivationtype = new Dictionary<string, string>
        {
            { "К удалению", "2018" },
            { "К улучшению", "2018" },
            { "К разделению", "2018" },
            { "К объединению", "2015" },
            { "К переименованию", "2015" },
            { "К восстановлению", "2018" },
            { "Обсуждение категорий", "2017" },
            { "Снятие защиты", "monthly" },
            { "Установка защиты", "monthly" },
            { "Изменение спам-листа", "monthly" },
            { "Запросы к администраторам", "monthly" },
            { "Инкубатор/Мини-рецензирование", "monthly" },
            { "Заявки на статус патрулирующего", "monthly" },
            { "Заявки на статус автопатрулируемого", "monthly" },
            { "Заявки на статус подводящего итоги", "monthly" },
            { "Оспаривание итогов", "yearly" },
            { "Заявки на снятие флагов", "yearly" },
            { "К оценке источников", "quarterly" },
            { "Оспаривание административных действий", "quarterly" },
        };
        var monthnames = new string[13];
        monthnames[1] = "январе"; monthnames[2] = "феврале"; monthnames[3] = "марте"; monthnames[4] = "апреле"; monthnames[5] = "мае"; monthnames[6] = "июне"; monthnames[7] = "июле"; monthnames[8] = "августе"; monthnames[9] = "сентябре"; monthnames[10] = "октябре"; monthnames[11] = "ноябре"; monthnames[12] = "декабре";
        var monthnumbers = new Dictionary<string, int>();
        monthnumbers.Add("января", 1); monthnumbers.Add("февраля", 2); monthnumbers.Add("марта", 3); monthnumbers.Add("апреля", 4); monthnumbers.Add("мая", 5); monthnumbers.Add("июня", 6);
        monthnumbers.Add("июля", 7); monthnumbers.Add("августа", 8); monthnumbers.Add("сентября", 9); monthnumbers.Add("октября", 10); monthnumbers.Add("ноября", 11); monthnumbers.Add("декабря", 12);
        var stats_per_year = new Dictionary<string, Dictionary<string, int>>();
        var stats_per_month = new Dictionary<string, Dictionary<string, int>>();
        var summary_rgx = new Regex(@"={1,}\s*Итог[^=\n]*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря) (\d{4}) \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        foreach (var t in archivationtype.Keys)
        {
            string apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=allpages&apprefix=" + t + "&apnamespace=" + (t == "Инкубатор/Мини-рецензирование" ? 104 : 4) + "&aplimit=max").Result;
            using (var xr = new XmlTextReader(new StringReader(apiout)))
                while (xr.Read())
                    if (xr.Name == "p")
                    {
                        string page = xr.GetAttribute("title");
                        bool correctpage = false;
                        int discussion_year;
                        if (archivationtype[t] == "monthly")
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            else
                            {
                                try { discussion_year = Convert.ToInt16(page.Substring(page.Length - 7, 4)); }
                                catch { continue; }
                                if (discussion_year >= last2monthdate.Year)
                                    correctpage = true;
                            }
                        }
                        else if (archivationtype[t] == "quarterly")
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            else
                            {
                                try { discussion_year = Convert.ToInt16(page.Substring(page.Length - 6, 4)); }
                                catch { continue; }
                                if (discussion_year >= last2monthdate.Year)
                                    correctpage = true;
                            }
                        }
                        else if (archivationtype[t] == "yearly")
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            else
                            {
                                try { discussion_year = Convert.ToInt16(page.Substring(page.Length - 4)); }
                                catch { continue; }
                                if (discussion_year >= last2monthdate.Year)
                                    correctpage = true;
                            }
                        }
                        else
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            try { discussion_year = Convert.ToInt16(page.Substring(page.Length - 4)); }
                            catch { continue; }
                            if (discussion_year >= Convert.ToInt16(archivationtype[t]))
                                correctpage = true;
                        }
                        if (correctpage)
                        {
                            string pagetext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + page + "?action=raw").Result;
                            var matches = summary_rgx.Matches(pagetext);
                            foreach (Match m in matches)
                            {
                                int signature_year = Convert.ToInt16(m.Groups[6].Value);
                                int signature_month = monthnumbers[m.Groups[5].Value];
                                string user = m.Groups[3].ToString().Replace('_', ' ');
                                if (user.Contains("/"))
                                    continue;
                                if (signature_year == lastmonthdate.Year && signature_month == lastmonthdate.Month)
                                {
                                    if (!stats_per_month.ContainsKey(user))
                                        stats_per_month.Add(user, new Dictionary<string, int>() { { "К удалению", 0 }, { "К улучшению", 0 }, { "К разделению", 0 }, { "К объединению", 0 }, { "К переименованию", 0 }, { "К восстановлению", 0 },
                                            { "Обсуждение категорий", 0 }, { "Снятие защиты", 0 }, { "Установка защиты", 0 }, { "sum", 0 }, { "Изменение спам-листа", 0 }, { "Запросы к администраторам", 0 }, { "Инкубатор/Мини-рецензирование", 0 },
                                            { "Заявки на статус патрулирующего", 0 }, { "Заявки на статус автопатрулируемого", 0 }, { "Заявки на статус подводящего итоги", 0 }, { "Оспаривание итогов", 0 }, { "Заявки на снятие флагов", 0 },
                                        { "К оценке источников", 0 }, { "Оспаривание административных действий", 0 }, });
                                    stats_per_month[user]["sum"]++;
                                    stats_per_month[user][t]++;
                                }
                                if (signature_year == lastmonthdate.Year || (signature_year == lastmonthdate.Year - 1 && signature_month >= dtn.Month))
                                {
                                    if (!stats_per_year.ContainsKey(user))
                                        stats_per_year.Add(user, new Dictionary<string, int>() { { "К удалению", 0 }, { "К улучшению", 0 }, { "К разделению", 0 }, { "К объединению", 0 }, { "К переименованию", 0 }, { "К восстановлению", 0 },
                                            { "Обсуждение категорий", 0 }, { "Снятие защиты", 0 }, { "Установка защиты", 0 }, { "sum", 0 }, { "Изменение спам-листа", 0 }, { "Запросы к администраторам", 0 }, { "Инкубатор/Мини-рецензирование", 0 },
                                            { "Заявки на статус патрулирующего", 0 }, { "Заявки на статус автопатрулируемого", 0 }, { "Заявки на статус подводящего итоги", 0 }, { "Оспаривание итогов", 0 }, { "Заявки на снятие флагов", 0 },
                                        { "К оценке источников", 0 }, { "Оспаривание административных действий", 0 }, });
                                    stats_per_year[user]["sum"]++;
                                    stats_per_year[user][t]++;
                                }
                            }
                        }
                    }
        }
        string common_resulttext = "{{Плавающая шапка таблицы}}{{shortcut|ВП:ИТОГИ}}{{clear}}<center>{{самые активные участники}}\nСтатистика по числу итогов, подведённых %type%. См. также %otherpage%.\n\nСтатистика собирается поиском по тексту " +
            "страниц обсуждений и потому верна лишь приближённо, нестандартный синтаксис итога или подписи итогоподводящего может привести к тому, что такой итог не будет засчитан. Первично отсортировано по сумме всех итогов, кроме итогов на КУЛ." +
            "\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!Участник!!Σ!![[ВП:КУ|]]!![[ВП:ВУС|]]!![[ВП:КПМ|]]!![[ВП:КОБ|]]+<br>[[ВП:КРАЗД|]]!![[ВП:ОБК|]]!![[ВП:КУЛ|]]!![[ВП:ЗКА|]]!![[ВП:ОСП|]]+<br>[[ВП:ОАД|]]!![[ВП:КОИ|]]!![[ВП:ЗС|]]+<br>" +
            "[[ВП:ЗС-|]]!![[ВП:ЗСП|]]+<br>[[ВП:ЗСАП|]]!![[ВП:ЗСПИ|]]!![[ВП:ЗСФ|]]!![[ВП:ИСЛ|]]!![[ПРО:ИНК-МР|ИНК]]";
        resulttext_per_year = common_resulttext.Replace("%type%", "за последние 12 месяцев").Replace("%otherpage%", "[[ВП:Статистика итогов|итоги за последний месяц]]");
        resulttext_per_month = common_resulttext.Replace("%type%", "в " + monthnames[lastmonthdate.Month] + " " + lastmonthdate.Year + " года").Replace("%otherpage%", "[[ВП:Статистика итогов/За год|итоги за последние 12 месяцев]]");

        foreach (var s in stats_per_year.OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"]))
            writerow(s, true);
        cntr = 0;
        foreach (var s in stats_per_month.OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"]))
            writerow(s, false);

        Save(site, "ВП:Статистика итогов/За год", resulttext_per_year + "\n|}", "");
        Save(site, "ВП:Статистика итогов", resulttext_per_month + "\n|}", "");
    }
}
