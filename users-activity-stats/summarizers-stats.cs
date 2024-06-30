using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;

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

public class Root
{
    public bool batchcomplete;
    public Continue @continue;
    public Query query;
}
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
        var request = new MultipartFormDataContent{{ new StringContent("edit"), "action" },{ new StringContent(title), "title" },{ new StringContent(text), "text" },
            { new StringContent(comment), "summary" },{ new StringContent(token), "token" },{ new StringContent("xml"), "format" }};
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
        if (result.ToString().Contains("uccess"))
            Console.WriteLine(DateTime.Now.ToString() + " written " + title);
        else
            Console.WriteLine(result);
    }

    static int position_number = 0;
    static string resulttext_per_year, resulttext_per_month, user;
    static Dictionary<string, Dictionary<string, Dictionary<string, int>>> stats = new Dictionary<string, Dictionary<string, Dictionary<string, int>>> { { "month", new Dictionary<string, Dictionary<string, int>>() },
            { "year", new Dictionary<string, Dictionary<string, int>>() } };
    static string cell (int number)
    {
        if (number == 0) return "";
        else return number.ToString();
    }
    static void writerow(KeyValuePair<string, Dictionary<string, int>> s, bool per_year)
    {
        string newrow = "\n|-\n|" + ++position_number + "||{{u|" + s.Key + "}}||" + cell(s.Value["sum"]) + "||" + cell(s.Value["К удалению"]) + "||" + cell(s.Value["К восстановлению"]) + "||" + cell(
            s.Value["К переименованию"]) + "||" + cell(s.Value["К объединению"] + s.Value["К разделению"]) + "||" + cell(s.Value["Обсуждение категорий"]) + "||" + cell(s.Value["К улучшению"]) + "||" + 
            cell(s.Value["Запросы к администраторам"]) + "||" + cell(s.Value["Оспаривание итогов"] + s.Value["Оспаривание административных действий"]) + "||" + cell(s.Value["Установка защиты"] + s.Value
            ["Снятие защиты"]) + "||" + cell(s.Value["Заявки на статус автопатрулируемого"] + s.Value["Заявки на статус патрулирующего"]) + "||" + cell(s.Value["Заявки на статус подводящего итоги"]) + 
            "||" + cell(s.Value["Заявки на снятие флагов"]) + "||" + cell(s.Value["К оценке источников"]) + "||" + cell(s.Value["Изменение спам-листа"]) + "||" + cell(s.Value["Запросы к патрулирующим от " +
            "автоподтверждённых участников"] + s.Value["Запросы к патрулирующим"]) + "||" + cell(s.Value["Кандидаты в избранные статьи"] + s.Value["Кандидаты в устаревшие избранные статьи"]) + "||" +
            cell(s.Value["Кандидаты в хорошие статьи"] + s.Value["Кандидаты в хорошие статьи/К лишению статуса"]) + "||" + cell(s.Value["Кандидаты в добротные статьи"] + s.Value["Добротные статьи/К лишению " +
            "статуса"]) + "||" + cell(s.Value["Кандидаты в избранные списки и порталы"] + s.Value["Кандидаты в избранные списки и порталы/К лишению статуса"]) + "||" + cell(s.Value["Запросы к ботоводам"]) +
            "||" + cell(s.Value["Форум/Архив/Технический"] + s.Value["Технические запросы"]) + "||" + cell(s.Value["Инкубатор/Мини-рецензирование"]);
        if (per_year)
            resulttext_per_year += newrow;
        else
            resulttext_per_month += newrow;
    }
    static void initialize(string type, string pagetype)
    {
        if (!stats[type].ContainsKey(user))
            stats[type].Add(user, new Dictionary<string, int>() { { "К удалению", 0 },{ "К улучшению", 0 },{ "К разделению", 0 },{ "К объединению", 0 },{ "К переименованию", 0 },{ "К восстановлению", 0 },
                { "Обсуждение категорий", 0 }, { "Снятие защиты", 0 }, { "Установка защиты", 0 }, { "sum", 0 }, { "Изменение спам-листа", 0 }, { "Запросы к администраторам", 0 },{ "К оценке источников", 0 },
                { "Инкубатор/Мини-рецензирование", 0 }, { "Заявки на статус патрулирующего", 0 },{ "Заявки на статус автопатрулируемого", 0 }, { "Заявки на статус подводящего итоги", 0 },
                { "Оспаривание итогов", 0 }, { "Заявки на снятие флагов", 0 },{ "Оспаривание административных действий", 0 }, { "Кандидаты в хорошие статьи", 0 }, { "Кандидаты в добротные статьи", 0 },
                { "Кандидаты в избранные списки и порталы", 0 }, { "Кандидаты в избранные статьи", 0 }, { "Запросы к ботоводам", 0 }, { "Запросы к патрулирующим", 0 },{ "Технические запросы", 0 },
                { "Форум/Архив/Технический", 0 },{ "Запросы к патрулирующим от автоподтверждённых участников", 0 },{ "Кандидаты в устаревшие избранные статьи", 0 },{ "Добротные статьи/К лишению статуса", 0 },
                { "Кандидаты в хорошие статьи/К лишению статуса", 0 },{ "Кандидаты в избранные списки и порталы/К лишению статуса", 0 } });
        stats[type][user]["sum"]++;
        stats[type][user][pagetype]++;
    }
    static void Main()
    {
        var dtn = DateTime.Now;
        var lastmonthdate = dtn.AddMonths(-1);
        var lastyear = dtn.AddYears(-1);
        var first_not_fully_summaried_year = new Dictionary<string, int>
        {
            { "К удалению", 2018 },{ "К улучшению", 2018 },{ "К разделению", 2018 },{ "К объединению", 2015 },{ "К переименованию", 2015 },{ "К восстановлению", 2018 },{ "Обсуждение категорий", 2017 },
            { "Снятие защиты", 0 },{ "Установка защиты", 0 },{ "Оспаривание итогов", 0 },{ "Оспаривание административных действий", 0 },{ "Форум/Архив/Технический", 0 },{ "Технические запросы", 0 },
            { "К оценке источников", 0 },{ "Изменение спам-листа", 0 },{ "Запросы к патрулирующим", 0 },{ "Запросы к патрулирующим от автоподтверждённых участников", 0 },{ "Запросы к ботоводам", 0 },
            { "Заявки на снятие флагов", 0 },{ "Запросы к администраторам", 0 },{ "Инкубатор/Мини-рецензирование", 0 },{ "Кандидаты в хорошие статьи", 0 },{ "Кандидаты в избранные статьи", 0 },
            { "Кандидаты в добротные статьи", 0 },{ "Кандидаты в избранные списки и порталы", 0 },{ "Заявки на статус патрулирующего", 0 },{ "Заявки на статус подводящего итоги", 0 },
            { "Заявки на статус автопатрулируемого", 0 },{ "Кандидаты в устаревшие избранные статьи", 0 },{ "Кандидаты в хорошие статьи/К лишению статуса", 0 },{ "Добротные статьи/К лишению статуса", 0 },
            { "Кандидаты в избранные списки и порталы/К лишению статуса", 0 }
        };
        var monthnames = new string[13];
        monthnames[1] = "январе"; monthnames[2] = "феврале"; monthnames[3] = "марте"; monthnames[4] = "апреле"; monthnames[5] = "мае"; monthnames[6] = "июне"; monthnames[7] = "июле";
        monthnames[8] = "августе"; monthnames[9] = "сентябре"; monthnames[10] = "октябре"; monthnames[11] = "ноябре"; monthnames[12] = "декабре";
        var monthnumbers = new Dictionary<string, int>{{ "января", 1 },{ "февраля", 2 },{ "марта", 3 },{ "апреля", 4 },{ "мая", 5 },{ "июня", 6 },{ "июля", 7 },{ "августа", 8 },
            { "сентября", 9 },{ "октября", 10 },{ "ноября", 11 },{ "декабря", 12 }};//НЕ ПЕРЕНОСИТЬ СТРОКУ НИЖЕ, ОНА ЛОМАЕТСЯ
        var summary_rgx = new Regex(@"={1,}\s*(Итог)[^=\n]*={1,}\n{1,}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря) (\d{4}) \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var rdb_zkp_summary_rgx = new Regex(@"(done|сделано|отпатрулировано|отклонено)\s*\}\}((?!\(UTC\)).)*\[\[\s*(u|у|user|участник|участница|оу|ut|обсуждение участника|обсуждение участницы|user talk)\s*:\s*([^\]|#]*)\s*[]|#]((?!\(UTC\)).)*(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря) (\d{4}) \(UTC\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var yearrgx = new Regex(@"\d{4}");
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        foreach (var pagetype in first_not_fully_summaried_year.Keys)
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&list=allpages&apprefix=" + pagetype + "&apnamespace=" +
                (pagetype == "Инкубатор/Мини-рецензирование" || pagetype == "Добротные статьи/К лишению статуса" ? 104 : 4) + "&aplimit=max";

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
                    int startyear = first_not_fully_summaried_year[pagetype] == 0 ? lastyear.Year : first_not_fully_summaried_year[pagetype];
                    if (yearrgx.IsMatch(pagetitle))
                        if (Convert.ToInt16(yearrgx.Match(pagetitle).Value) >= startyear)
                            correctpage = true;
                        else if (pagetitle.IndexOf('/') == -1)
                            correctpage = true;
                    if (pagetitle.Contains("избранные статьи"))
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
                            if (signature_year == lastmonthdate.Year && signature_month == lastmonthdate.Month)
                                initialize("month", pagetype);
                            if (signature_year == lastmonthdate.Year || (signature_year == lastmonthdate.Year - 1 && signature_month > lastmonthdate.Month))
                                initialize("year", pagetype);
                        }
                    }
                }
            }
        }
        string common_resulttext = "{{Плавающая шапка таблицы}}{{shortcut|ВП:ИТОГИ}}{{clear}}<center>{{самые активные участники}}\nСтатистика по числу итогов, подведённых %type%. См. также %otherpage%.\n\n" +
            "Статистика собирается поиском по тексту страниц обсуждений и потому верна лишь приближённо, нестандартный синтаксис итога или подписи итогоподводящего может привести к тому, что такой итог не " +
            "будет засчитан. Первично отсортировано по сумме всех итогов, кроме итогов на КУЛ и ЗКП(АУ).\n{|class=\"standard sortable ts-stickytableheader\"\n!№!!Участник!!Σ!!{{abbr|<big>🗑</big>|КУ|0}}!!" +
            "{{abbr|<big>🗑⇧</big>|ВУС|0}}!!{{abbr|📄<big>⇨</big>|КПМ|0}}!!{{abbr|<big>⇨⇦⇨</big>|КОБ+КРАЗД|0}}!!{{abbr|<big>𓊍</big>|ОБК|0}}!!{{abbr|📄⇧|КУЛ|0}}!!{{abbr|🔨|ЗКА|0}}!!{{abbr|💬⏮|ОСП+ОАД" +
            "|0}}!!{{abbr|🔒|ЗС(-)|0}}!!{{abbr|👮⇧|ЗС(А)П|0}}!!{{abbr|👮⇧⇧|ЗСПИ|0}}!!{{abbr|👮⇩|ЗСФ|0}}!!{{abbr|📄⇩|КОИ|0}}!!{{abbr|🚫📄|ИСЛ|0}}!!{{abbr|✔|ЗКП(АУ)|0}}!!{{abbr|🥇|К(ЛС)ИС|0}}!!{{abbr|🥈|" +
            "К(ЛС)ХС|0}}!!{{abbr|🥉|К(ЛС)ДС|0}}!!{{abbr|📜|К(ЛС)ИСП|0}}!!{{abbr|🤖|РДБ|0}}!!{{abbr|🛠|ФТ+ТЗ|0}}!!{{abbr|👶|ИНК-МР|0}}";
        resulttext_per_year = common_resulttext.Replace("%type%", "за последние 12 месяцев").Replace("%otherpage%", "[[ВП:Статистика итогов|итоги за последний месяц]]");
        resulttext_per_month = common_resulttext.Replace("%type%", "в " + monthnames[lastmonthdate.Month] + " " + lastmonthdate.Year + " года").Replace("%otherpage%", "[[ВП:Статистика итогов/За год|итоги за последние 12 месяцев]]");

        foreach (var s in stats["year"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
            writerow(s, true);
        position_number = 0;
        foreach (var s in stats["month"].OrderByDescending(s => s.Value["sum"] - s.Value["К улучшению"] - s.Value["Запросы к патрулирующим от автоподтверждённых участников"] - s.Value["Запросы к патрулирующим"]))
            writerow(s, false);

        Save(site, "ВП:Статистика итогов/За год", resulttext_per_year + "\n|}", "");
        Save(site, "ВП:Статистика итогов", resulttext_per_month + "\n|}", "");
    }
}
