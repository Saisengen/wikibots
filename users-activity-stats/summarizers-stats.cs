using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DotNetWikiBot;
using System.Xml;
using System.Text.RegularExpressions;

class Program
{
    static int cntr = 0;
    //static Random rand = new Random();
    static string resulttext_per_year, resulttext_per_month;
    //static List<string> colors = new List<string>() { "#00f; color:#ff0", "#0f0; color:#f0f", "#0ff; color:#f00", "#f00; color:#0ff", "#ff0; color:#00f", "#f0f; color:#0f0", "#07f; color:#f70", "#0f7; color:#f07", "#7f0; color:#70f", "#70f; color:#7f0", "#f70; color:#07f", "#f07; color:#0f7" };
    static void writerow(KeyValuePair<string, Dictionary<string, int>> s, bool year)
    {
        //string newrow = "\n|-\n|" + ++cntr + "||{{u|" + s.Key + "}}||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["sum"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["К удалению"] +
        //"'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["К восстановлению"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["К переименованию"] + "'''||style=\"background-color:" +
        //colors[rand.Next(colors.Count)] + "\"|'''" + (s.Value["К объединению"] + s.Value["К разделению"]) + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["Обсуждение категорий"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] +
        //"\"|'''" + s.Value["К улучшению"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["Запросы к администраторам"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + (s.Value["Оспаривание итогов"] +
        //s.Value["Оспаривание административных действий"]) + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["К оценке источников"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + (s.Value["Установка защиты"] +
        //s.Value["Снятие защиты"]) + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + (s.Value["Заявки на статус автопатрулируемого"] + s.Value["Заявки на статус патрулирующего"]) + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" +
        //s.Value["Заявки на статус подводящего итоги"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["Заявки на снятие флагов"] + "'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["Изменение спам-листа"] +
        //"'''||style=\"background-color:" + colors[rand.Next(colors.Count)] + "\"|'''" + s.Value["Инкубатор/Мини-рецензирование"] + "'''";
        string newrow = "\n|-\n|" + ++cntr + "||{{u|" + s.Key + "}}||" + s.Value["sum"] + "||" + s.Value["К удалению"] + "||" + s.Value["К восстановлению"] + "||" + s.Value["К переименованию"] + "||" + (s.Value["К объединению"] + s.Value["К разделению"]) + "||" + 
            s.Value["Обсуждение категорий"] + "||" + s.Value["К улучшению"] + "||" + s.Value["Запросы к администраторам"] + "||" + (s.Value["Оспаривание итогов"] + s.Value["Оспаривание административных действий"]) + "||" + s.Value["К оценке источников"] + "||" + 
            (s.Value["Установка защиты"] + s.Value["Снятие защиты"]) + "||" + (s.Value["Заявки на статус автопатрулируемого"] + s.Value["Заявки на статус патрулирующего"]) + "||" + s.Value["Заявки на статус подводящего итоги"] + "||" + s.Value["Заявки на снятие флагов"] + "||" + 
            s.Value["Изменение спам-листа"] + "||" + s.Value["Инкубатор/Мини-рецензирование"];
        if (year)
            resulttext_per_year += newrow;
        else
            resulttext_per_month += newrow;
    }
    static void Main()
    {
        var lastmonth = DateTime.Now.AddMonths(-1);
        var last2month = DateTime.Now.AddMonths(-2);
        var archivationtype = new Dictionary<string, string>
        {
            { "К удалению", "2017" },
            { "К улучшению", "2017" },
            { "К разделению", "2014" },
            { "К объединению", "2013" },
            { "К переименованию", "2015" },
            { "К восстановлению", "2017" },
            { "Обсуждение категорий", "2016" },
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
        monthnames[1] = "января"; monthnames[2] = "февраля"; monthnames[3] = "марта"; monthnames[4] = "апреля"; monthnames[5] = "мая"; monthnames[6] = "июня"; monthnames[7] = "июля"; monthnames[8] = "августа"; monthnames[9] = "сентября"; monthnames[10] = "октября"; monthnames[11] = "ноября"; monthnames[12] = "декабря";
        var stats_per_year = new Dictionary<string, Dictionary<string, int>>();
        var stats_per_month = new Dictionary<string, Dictionary<string, int>>();
        var summary_per_year = new Regex(@"={1,}\s*Итог[^=\n]*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*\w+ " + lastmonth.Year + @" \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var summary_per_month = new Regex(@"={1,}\s*Итог[^=\n]*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*" + monthnames[lastmonth.Month] + " " + lastmonth.Year + @" \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        foreach (var t in archivationtype.Keys)
        {
            string apiout = site.GetWebPage("/w/api.php?action=query&format=xml&list=allpages&apprefix=" + t + "&apnamespace=" + (t == "Инкубатор/Мини-рецензирование" ? 104 : 4) + "&aplimit=max");
            using (var xr = new XmlTextReader(new StringReader(apiout)))
                while (xr.Read())
                    if (xr.Name == "p")
                    {
                        string page = xr.GetAttribute("title");
                        bool correctpage = false;
                        int year;
                        if (archivationtype[t] == "monthly")
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            else
                            {
                                try { year = Convert.ToInt16(page.Substring(page.Length - 7, 4)); }
                                catch { continue; }
                                if (year >= last2month.Year)
                                    correctpage = true;
                            }
                        }
                        else if (archivationtype[t] == "quarterly")
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            else
                            {
                                try { year = Convert.ToInt16(page.Substring(page.Length - 6, 4)); }
                                catch { continue; }
                                if (year >= last2month.Year)
                                    correctpage = true;
                            }
                        }
                        else if (archivationtype[t] == "yearly")
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            else
                            {
                                try { year = Convert.ToInt16(page.Substring(page.Length - 4)); }
                                catch { continue; }
                                if (year >= last2month.Year)
                                    correctpage = true;
                            }
                        }
                        else
                        {
                            if (page.IndexOf('/') == -1)
                                correctpage = true;
                            try { year = Convert.ToInt16(page.Substring(page.Length - 4)); }
                            catch { continue; }
                            if (year >= Convert.ToInt16(archivationtype[t]))
                                correctpage = true;
                        }
                        if (correctpage)
                        {
                            string pagetext = site.GetWebPage("https://ru.wikipedia.org/wiki/" + page + "?action=raw");
                            var results_per_year = summary_per_year.Matches(pagetext);
                            var results_per_month = summary_per_month.Matches(pagetext);
                            foreach (Match r in results_per_year)
                            {
                                string user = r.Groups[3].ToString().Replace('_', ' ');
                                if (user.Contains("/"))
                                    continue;
                                if (!stats_per_year.ContainsKey(user))
                                    stats_per_year.Add(user, new Dictionary<string, int>() { { "К удалению", 0 }, { "К улучшению", 0 }, { "К разделению", 0 }, { "К объединению", 0 },
                                        { "К переименованию", 0 }, { "К восстановлению", 0 }, { "Обсуждение категорий", 0 }, { "Снятие защиты", 0 }, { "Установка защиты", 0 }, { "sum", 0 },
                                        { "Изменение спам-листа", 0 }, { "Запросы к администраторам", 0 }, { "Инкубатор/Мини-рецензирование", 0 }, { "Заявки на статус патрулирующего", 0 },
                                        { "Заявки на статус автопатрулируемого", 0 }, { "Заявки на статус подводящего итоги", 0 }, { "Оспаривание итогов", 0 }, { "Заявки на снятие флагов", 0 },
                                        { "К оценке источников", 0 }, { "Оспаривание административных действий", 0 }, });
                                stats_per_year[user]["sum"]++;
                                stats_per_year[user][t]++;
                            }
                            foreach (Match r in results_per_month)
                            {
                                string user = r.Groups[3].ToString().Replace('_', ' ');
                                if (user.Contains("/"))
                                    continue;
                                if (!stats_per_month.ContainsKey(user))
                                    stats_per_month.Add(user, new Dictionary<string, int>() { { "К удалению", 0 }, { "К улучшению", 0 }, { "К разделению", 0 }, { "К объединению", 0 },
                                        { "К переименованию", 0 }, { "К восстановлению", 0 }, { "Обсуждение категорий", 0 }, { "Снятие защиты", 0 }, { "Установка защиты", 0 }, { "sum", 0 },
                                        { "Изменение спам-листа", 0 }, { "Запросы к администраторам", 0 }, { "Инкубатор/Мини-рецензирование", 0 }, { "Заявки на статус патрулирующего", 0 },
                                        { "Заявки на статус автопатрулируемого", 0 }, { "Заявки на статус подводящего итоги", 0 }, { "Оспаривание итогов", 0 }, { "Заявки на снятие флагов", 0 },
                                        { "К оценке источников", 0 }, { "Оспаривание административных действий", 0 }, });
                                stats_per_month[user]["sum"]++;
                                stats_per_month[user][t]++;
                            }
                        }
                    }
        }
        string common_resulttext = "{{Плавающая шапка таблицы}}{{shortcut|ВП:ИТОГИ}}{{clear}}<center>{{самые активные участники}}\nСтатистика по числу итогов, подведённых в течение %month%" + lastmonth.Year +
            " года. См. также %otherpage%.\n\nСтатистика собирается поиском по тексту страниц обсуждений и потому верна лишь приближённо, нестандартный синтаксис итога или подписи итогоподводящего " +
            "может привести к тому, что такой итог не будет засчитан. Первично отсортировано по сумме всех итогов, кроме итогов на КУЛ.\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!" +
            "Участник!!Σ!![[ВП:КУ|]]!![[ВП:ВУС|]]!![[ВП:КПМ|]]!![[ВП:КОБ|]]+<br>[[ВП:КРАЗД|]]!![[ВП:ОБК|]]!![[ВП:КУЛ|]]!![[ВП:ЗКА|]]!![[ВП:ОСП|]]+<br>[[ВП:ОАД|]]!![[ВП:КОИ|]]!![[ВП:ЗС|]]+<br>" +
            "[[ВП:ЗС-|]]!![[ВП:ЗСП|]]+<br>[[ВП:ЗСАП|]]!![[ВП:ЗСПИ|]]!![[ВП:ЗСФ|]]!![[ВП:ИСЛ|]]!![[ПРО:ИНК-МР|ИНК]]";
        resulttext_per_year = common_resulttext.Replace("%month%", "").Replace("%otherpage%", "[[ВП:Статистика итогов|итоги за последний месяц]]");
        resulttext_per_month = common_resulttext.Replace("%month%", monthnames[lastmonth.Month] + " ").Replace("%otherpage%", "[[ВП:Статистика итогов/За год|итоги за год]]");

        foreach (var s in stats_per_year.OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"]))
            writerow(s, true);
        cntr = 0;
        foreach (var s in stats_per_month.OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"]))
            writerow(s, false);

        resulttext_per_year += "\n|}";
        resulttext_per_month += "\n|}";
        var yearresult = new Page("ВП:Статистика итогов/За год");
        yearresult.Save(resulttext_per_year);
        var monthresult = new Page("ВП:Статистика итогов");
        monthresult.Save(resulttext_per_month);
    }
}
