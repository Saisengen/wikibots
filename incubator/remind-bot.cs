using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using DotNetWikiBot;

class MyBot : Bot
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    public PageList GetCategoryMembers(Site site, string cat, int limit)
    {
        PageList allpages = new PageList(site);
        string[] all = new string[limit];
        int page_num = 0;
        string URL = site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmnamespace=102&cmlimit=5000&cmtitle=" + HttpUtility.UrlEncode("Категория:" + cat) + "&format=xml";
        string h = site.GetWebPage(URL);
        XmlTextReader rdr = new XmlTextReader(new StringReader(h));
        while (rdr.Read())
        {
            if (rdr.NodeType == XmlNodeType.Element)
            {
                if (rdr.Name == "cm")
                {
                    all[page_num] = rdr.GetAttribute("title");
                    page_num++;
                }
            }
            if (page_num > limit)
                break;
        }
        foreach (string m in all)
        {
            if (!String.IsNullOrEmpty(m))
            {
                Page n = new Page(site, m);
                allpages.Add(n);
            }
        }
        return allpages;
    }
    public static void Main()
    {
        Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        PageList all = new PageList(site);
        PageList exc = new PageList(site);
        MyBot bot = new MyBot();
        all.FillFromAllPages("", 102, true, 5000);
        var exceptions = "Инкубатор:Песочница|Инкубатор:Песочница/Пишите ниже|Инкубатор:Тест бота|Инкубатор:ПЕСОК|Инкубатор:ТЕСТ".Split('|');
        var candidats = bot.GetCategoryMembers(site, "Проект:Инкубатор:Кандидаты на мини-рецензирование", 5000);
        var forgotten = bot.GetCategoryMembers(site, "Проект:Инкубатор:Брошенные статьи", 5000);
        var reviewing = bot.GetCategoryMembers(site, "Проект:Инкубатор:Статьи на мини-рецензировании", 5000);
        string[,] pages = new string[5000, 5];
        int pn = 0;
        foreach (Page n in all)
        {
            bool except = false;
            for (int ik = 0; ik < exceptions.Length; ik++)
            {
                if (n.title == exceptions[ik])
                {
                    exc.Add(n);
                    except = true;
                }
            }
            if (!except)
            {
                // проверяем, что нет в категориях
                if (!reviewing.Contains(n) && !candidats.Contains(n) && !forgotten.Contains(n))
                {
                    string tit = n.title.Replace(" ", "_");
                    // и достаем дату последней правки
                    string pURL = site.apiPath + "?action=query&prop=revisions&titles=" + HttpUtility.UrlEncode(tit) + "&rvprop=timestamp|content&format=xml";
                    string h = site.GetWebPage(pURL);
                    string dl = "";
                    string df = "";
                    string pt = "";
                    XmlTextReader rdr = new XmlTextReader(new StringReader(h));
                    while (rdr.Read())
                        if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "rev")
                        {
                            dl = rdr.GetAttribute("timestamp");
                            pt = rdr.ReadString();
                        }
                    // и  дату первой правки
                    pURL = site.apiPath + "?action=query&prop=revisions&titles=" + HttpUtility.UrlEncode(tit) + "&rvprop=timestamp&rvdir=newer&rvlimit=1&format=xml";
                    h = site.GetWebPage(pURL);
                    XmlTextReader rdr2 = new XmlTextReader(new StringReader(h));
                    while (rdr2.Read())
                        if (rdr2.NodeType == XmlNodeType.Element && rdr2.Name == "rev")
                            df = rdr2.GetAttribute("timestamp");
                    if (pt.IndexOf("{{nobots}}") != -1 | pt.IndexOf("{{Инкубатор, черновик ВУС") != -1 | pt.IndexOf("{{инкубатор, черновик ВУС}") != -1)
                        exc.Add(n);
                    else
                    {
                        DateTime dfirst = DateTime.Parse(df); // дата и время создания
                        DateTime dlast = DateTime.Parse(dl); // дата и время последней правки
                        DateTime dnow = DateTime.UtcNow; // текущие дата и время
                        TimeSpan diff1 = dnow - dfirst; // считаем разницу
                        TimeSpan diff2 = dnow - dlast; // считаем разницу
                        pages[pn, 0] = n.title;
                        pages[pn, 1] = diff1.Days.ToString(); // разница от создания
                        pages[pn, 2] = diff2.Days.ToString(); // разница от посл правки
                        pn++;
                    }
                }
            }
        }
            bot.mr1(site, pages);
            bot.mr2(site, pages);
            bot.mrec(site, pages);
    }
    /// напоминаем о забытой статье
    public void mr1(Site site, string[,] pages)
    {
        DateTime dnow = DateTime.UtcNow;
        Page logpage = new Page(site, "Участник:IncubatorBot/Лог напоминаний");
        logpage.Load();
        string log = logpage.text;
        for (int i = 0; i < 5000; i++)
            if (!string.IsNullOrEmpty(pages[i, 0]))
            {
                Page n = new Page(site, pages[i, 0]);
                if (Convert.ToInt32(pages[i, 2]) > 15) // если посл.правка была более 15 дней назад, обрабатываем
                {
                    n.Load();
                    n.text = "{{Инкубатор, Уведомление|wait={{subst:#time:Ymd}}}}\n" + n.text;
                    n.Save("[[User:IncubatorBot/RemindBot|напоминание о завершении срока]]", true);
                }
            }
        Regex logs = new Regex(@"{{\.\..*?}}", RegexOptions.Singleline);
        Regex logdate = new Regex(@"\d{4}-\d{1,2}-\d{1,2}", RegexOptions.Singleline);
        Regex loglink = new Regex(Regex.Escape("|") + @".*?Инкубатор.*?" + Regex.Escape("|"), RegexOptions.Singleline);
        PageList candidats = new PageList(site);
        candidats.FillFromCategory("Проект:Инкубатор:Кандидаты на мини-рецензирование");
        foreach (Match m in logs.Matches(log))
        {
            string datelog = logdate.Matches(m.ToString())[0].ToString();
            string pagelog = loglink.Matches(m.ToString())[0].ToString();
            pagelog = pagelog.Remove(0, 1);
            pagelog = pagelog.Remove(pagelog.Length - 1);
            pagelog = pagelog.TrimEnd();
            Page lpage = new Page(site, pagelog);
            DateTime dlog = DateTime.Parse(datelog);
            TimeSpan difflog = dnow - dlog;
            bool a = candidats.Contains(lpage);
            if (difflog.Days > 10)
            {
                log = log.Replace(m.ToString(), "");
                log = log.Replace("\n\n", "\n");
            }
            else if (a != true)
                log = log.Replace(m.ToString(), "{{../Log|" + pagelog + "|" + datelog + "|шаблон снят}}");
        }
        logpage.text = log;
        logpage.Save("обновление", true);
    }
    /// выгоняем из Инкубатора
    public void mr2(Site site, string[,] pages)
    {
        DateTime dnow = DateTime.UtcNow;
        Page logpage = new Page(site, "Участник:IncubatorBot/Лог уведомлений");
        logpage.Load();
        string log = logpage.text;
        for (int i = 0; i < 5000; i++)
            if (!string.IsNullOrEmpty(pages[i, 0]))
            {
                Page n = new Page(site, pages[i, 0]);
                if (Convert.ToInt32(pages[i, 1]) > 70) // если создано более 70 дней назад, обрабатываем
                {
                    PageList fromKU = new PageList(site); // перенести из цикла
                    fromKU.FillFromCategory("Проект:Инкубатор:Статьи на доработке");
                    if (fromKU.Contains(n) != true) // если не с ВП:КУ
                    {
                        if (log.IndexOf(n.title) == -1) // если нет в логах
                        {
                            n.Load();
                            n.text = "{{Инкубатор, Уведомление|away={{subst:#time:Ymd}}}}\n" + n.text;
                            n.Save("[[User:IncubatorBot/RemindBot|уведомление о завершении срока]]", true);
                        }
                    }
                }
            }
        Regex logs = new Regex(@"{{\.\..*?}}", RegexOptions.Singleline);
        Regex logdate = new Regex(@"\d{4}-\d{1,2}-\d{1,2}", RegexOptions.Singleline);
        Regex loglink = new Regex(Regex.Escape("|") + @".*?Инкубатор.*?" + Regex.Escape("|"), RegexOptions.Singleline);
        PageList candidats = new PageList(site);
        candidats.FillFromCategory("Проект:Инкубатор:Кандидаты на мини-рецензирование");
        foreach (Match m in logs.Matches(log))
        {
            string datelog = logdate.Matches(m.ToString())[0].ToString();
            string pagelog = loglink.Matches(m.ToString())[0].ToString();
            pagelog = pagelog.Remove(0, 1);
            pagelog = pagelog.Remove(pagelog.Length - 1);
            pagelog = pagelog.TrimEnd();
            DateTime dlog = DateTime.Parse(datelog);
            TimeSpan difflog = dnow - dlog;
            Page lpage = new Page(site, pagelog);
            bool a = candidats.Contains(lpage);
            if (difflog.Days > 30)
                log = log.Replace(m.ToString(), "").Replace("\n\n", "\n");
            else if (a != true)
                log = log.Replace(m.ToString(), "{{../Log|" + pagelog + "|" + datelog + "|шаблон снят}}");
        }
        logpage.text = log;
        logpage.Save("обновление", true);
    }
    /// Мини-рецензирование
    public void mrec(Site site, string[,] pages)
    {
        MyBot bot = new MyBot();
        var forgotten = bot.GetCategoryMembers(site, "Проект:Инкубатор:Брошенные статьи", 5000);
        Page rp = new Page(site, "Проект:Инкубатор/Мини-рецензирование");
        PageList pact = new PageList(site);
        PageList prez = new PageList(site);
        DateTime dnow = DateTime.UtcNow;
        string[] mon = { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
        int numpages = 0;
        string d16, d10, numusers, numedits, textextract, user1, userN, datenow, d_3, d_7, length; // d_1
        d_3 = ""; // "== * --- Менее 5 правок --- == \n\n";
        d_7 = ""; // "== * --- 5-9 правок --- == \n\n";
        d10 = ""; // "== * --- 10-15 правок --- == \n\n";
        d16 = ""; // "== * --- Более 16 правок --- == \n\n";
        rp.Load();
        PageList fromKU = new PageList(site);
        fromKU.FillFromCategory("Проект:Инкубатор:Статьи на доработке");
        for (int i = 0; i < 5000; i++)
        {
            if (!String.IsNullOrEmpty(pages[i, 0]))
            {
                Page n = new Page(site, pages[i, 0]);
                if (Convert.ToInt32(pages[i, 1]) > 90 || Convert.ToInt32(pages[i, 2]) > 20) // 90 дней возраст или 20 нет правок
                    if (rp.text.IndexOf("== [[" + pages[i, 0] + "]] ==") == -1)
                        if (fromKU.Contains(n) != true) // если не с ВП:КУ
                            forgotten.Add(n); // закидываем в список для последующей обработки
            }
        }
        foreach (Page n in forgotten)
        {
            n.Load();
            int ne, nu;
            int[] un = new int[100];
            string[,] users = new string[1000, 3];
            string[] unusers = new string[100];
            string tit = n.title.Replace(" ", "_");
            pact.Add(n); // сохраняем в список, для проставления шаблонов
            prez.Add(n); // сохраняем в отдельный список упоминание
                         // веб-запрос, из которого парсим дату и автора первой и последней правок, общее кол-во правок и кол-во авторов
            string pageURL = site.apiPath + "?action=query&prop=info|revisions&titles=" + HttpUtility.UrlEncode(tit) + "&rvlimit=100&rvprop=flags|timestamp|user&format=xml";
            string html = site.GetWebPage(pageURL);
            ne = 0;
            XmlTextReader reader = new XmlTextReader(new StringReader(html));
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "page")
                        length = reader.GetAttribute("length");
                    if (reader.Name == "rev")
                    { // заносим в массив все правки в странице
                        users[ne, 0] = reader.GetAttribute("user");
                        users[ne, 1] = reader.GetAttribute("timestamp");
                        if (reader.GetAttribute("minor") != null)
                            users[ne, 2] = "true";
                        else
                            users[ne, 2] = "false";
                        ne++;
                    }
                }
            }
            // определяем уникальных юзеров
            nu = 1;
            unusers[0] = users[0, 0];
            un[0] = 1;
            for (int i = 0; i < ne; i++) // i = 1 ???
            {
                bool a = false;
                int k = 0;
                while (k < nu & a == false)
                {
                    if (users[i, 0] == unusers[k])
                        a = true;
                    k++;
                }
                if (a == true)
                    un[k - 1]++;
                else { unusers[k] = users[i, 0]; un[k] = 1; nu++; }
            }
            // готовим к выводу
            numedits = ne.ToString();
            numusers = nu.ToString();
            DateTime date2 = DateTime.Parse(users[0, 1]);
            DateTime date1 = DateTime.Parse(users[ne - 1, 1]);
            TimeSpan ddiff1 = date2 - date1;
            TimeSpan ddiff = dnow - date2;
            // формируем ссылки на страницы участников, их СО, и их вклад
            //user1 = "[[User:" + users[ne - 1, 0] + "|" + users[ne - 1, 0] + "]] ([[User talk:" + users[ne - 1, 0] + "|о]] | [[Special:Contributions/" + users[ne - 1, 0] + "|в]])";
            //userN = "[[User:" + users[0, 0] + "|" + n.lastUser + "]] ([[User talk:" + users[0, 0] + "|о]] | [[Special:Contributions/" + users[0, 0] + "|в]])";
            user1 = users[ne - 1, 0];
            userN = users[0, 0];
            // текущая дата
            datenow = dnow.Day + " " + mon[dnow.Month - 1] + " " + dnow.Year;
            // вычищаем из текста страницы шаблоны, для предв просмотра на мини-реценз
            string pagetext = n.text;
            string rep = "";
            string rep2 = "";
            Regex c1 = new Regex("{[^{}]*}", RegexOptions.Singleline);
            Regex c2 = new Regex("{.*?}", RegexOptions.Singleline);
            Regex br = new Regex("<br.*?>", RegexOptions.Singleline);
            Regex c3 = new Regex("<[^<>]*>", RegexOptions.Singleline);
            Regex c4 = new Regex("<.*?>", RegexOptions.Singleline);
            Regex f1 = new Regex(Regex.Escape("[[") + ":?(category|категория|файл|file|изображение|image|http).*?" + Regex.Escape("]]"), RegexOptions.Singleline | RegexOptions.IgnoreCase);
            Regex f2 = new Regex(Regex.Escape("[[") + ".{1,5}:.*?" + Regex.Escape("]]"), RegexOptions.Singleline);
            Regex c5 = new Regex(Regex.Escape("[[") + ".*?" + Regex.Escape("]]"), RegexOptions.Singleline);
            Regex c6 = new Regex(Regex.Escape("[") + ".*?" + Regex.Escape("]"), RegexOptions.Singleline);
            Regex c7 = new Regex("={1,5}.*?={1,5}", RegexOptions.Singleline);
            while (c2.IsMatch(pagetext, 0))
            {
                for (int qw = 0; qw < c1.Matches(pagetext).Count; qw++)
                {
                    rep = c1.Matches(pagetext)[qw].ToString();
                    pagetext = pagetext.Replace(rep, "");
                }
            }
            for (int qw = 0; qw < br.Matches(pagetext).Count; qw++)
            {
                rep = br.Matches(pagetext)[qw].ToString();
                pagetext = pagetext.Replace(rep, " \n");
            }
            while (c4.IsMatch(pagetext, 0))
            {
                for (int qw = 0; qw < c3.Matches(pagetext).Count; qw++)
                {
                    rep = c3.Matches(pagetext)[qw].ToString();
                    pagetext = pagetext.Replace(rep, "");
                }
            }
            while (f1.IsMatch(pagetext, 0))
            {
                for (int qw = 0; qw < f1.Matches(pagetext).Count; qw++)
                {
                    rep = f1.Matches(pagetext)[qw].ToString();
                    pagetext = pagetext.Replace(rep, "");
                }
            }
            while (f2.IsMatch(pagetext, 0))
            {
                for (int qw = 0; qw < f2.Matches(pagetext).Count; qw++)
                {
                    rep = f2.Matches(pagetext)[qw].ToString();
                    pagetext = pagetext.Replace(rep, "");
                }
            }
            while (c5.IsMatch(pagetext, 0))
            {
                for (int qw = 0; qw < c5.Matches(pagetext).Count; qw++)
                {
                    rep = c5.Matches(pagetext)[qw].ToString();
                    int st = 2;
                    if (rep.IndexOf("|") != -1) { st = 1 + rep.IndexOf("|"); }
                    rep2 = rep.Substring(st, rep.Length - 2 - st);
                    pagetext = pagetext.Replace(rep, rep2);
                }
            }
            while (c6.IsMatch(pagetext, 0))
            {
                for (int qw = 0; qw < c6.Matches(pagetext).Count; qw++)
                {
                    rep = c6.Matches(pagetext)[qw].ToString();
                    pagetext = pagetext.Replace(rep, "");
                }
            }
            while (c7.IsMatch(pagetext, 0))
            {
                for (int qw = 0; qw < c7.Matches(pagetext).Count; qw++)
                {
                    rep = c7.Matches(pagetext)[qw].ToString();
                    pagetext = pagetext.Replace(rep, "");
                }
            }
            pagetext = pagetext.Replace("<ref>", "").Replace("</ref>", "").Replace("<!--", "").Replace("-->", "").Replace("\'", "").Replace("*", " ").Replace("#", " ").Replace("  ", " ").Replace("  ", " ");
            for (int q1 = 0; q1 < 20; q1++)
                pagetext = pagetext.Replace("\n\n", "\n");
            // выводим текст страницы в предварительный просмотр <nowiki></nowiki>
            if (pagetext.Length > 300)
                textextract = pagetext.Substring(0, 300) + " <...>";
            else
                textextract = pagetext.Substring(0);
            int ns = n.GetNamespace();
            string otitle = "";
            switch (ns)
            {
                case 4:
                    otitle = "Обсуждение Википедии";
                    break;
                case 102:
                default:
                    otitle = "Обсуждение Инкубатора";
                    break;
            }
            string titlefortalk = otitle + n.title.Substring(9);
            string titleforhist = n.title.Replace(" ", "_").Replace("\"", "%22").Replace("&", "%26").Replace("!", "&#33");
            // теперь в зависимости от кол-ва правок раскидываем информацию о статье по 3ем переменным, которые потом составят страницу мини-рецензирования...
            if (rp.text.IndexOf("== [[" + n.title + "]] ==") == -1)
            {
                // чтобы не дублировать код в каждой секции ниже
                string review_section = "== [[" + n.title + "]] ==\n{{User:IncubatorBot/Review\n|fe=" + date1.ToString("yyyy.MM.dd HH:mm") + "|fu=" + user1 + "\n|le=" + date2.ToString("yyyy.MM.dd HH:mm") +
                    "|lu=" + userN + "\n|te=" + numedits + "|tu=" + nu.ToString() + "\n|d=" + ddiff1.Days + "|h=" + ddiff1.Hours + "|in=" + ddiff.Days + "\n|tp=" + titlefortalk +
                    "\n|hp=" + titleforhist + "\n|date=~~~~~" + "\n|text=<nowiki>" + textextract + "</nowiki>}}\n<!-- пишите ниже этой строки -->\n\n";
                if (ne < 5)
                    d_3 += review_section;
                else if (ne < 10)
                    d_7 += review_section;
                else if (ne < 16)
                    d10 += review_section;
                else
                    d16 += review_section;
            }
            numpages++;
        }
        if (numpages > 0)
        {
            // из переменных сортированных по кол-ву правок составляем итоговый текст
            rp.text = rp.text + "\n\n" + d16 + d10 + d_7 + d_3;
            /* а теперь убираем лишние шаблоны с выставленных страниц, и ставим на них шаблон мини-рецензирования */
            // pact.SaveTitlesToFile("template.txt", false);
            // загружаем активный список
            foreach (Page n in pact)
            {
                n.Load();
                Regex itemplates = new Regex(Regex.Escape("{{") + "Инкубатор.*?" + Regex.Escape("}}"), RegexOptions.Singleline | RegexOptions.IgnoreCase);
                while (itemplates.IsMatch(n.text, 0))
                {
                    for (int qw = 0; qw < itemplates.Matches(n.text).Count; qw++)
                    {
                        string rep = itemplates.Matches(n.text)[qw].ToString();
                        n.text = n.text.Replace(rep, "");
                    }
                }
                n.text = "{{subst:i-recense}}" + n.text;
                n.Save("[[User:IncubatorBot/RemindBot|выставлено на мини-рецензирование]]", true);
            }
            for (int z = 0; z < 3; z++)
            {
                MatchCollection specsections = new Regex(@"==.?" + Regex.Escape("* ---") + ".*?==[^=]*?==.?" + Regex.Escape("*"), RegexOptions.IgnoreCase).Matches(rp.text);
                foreach (Match m in specsections)
                    rp.text = rp.text.Replace(m.ToString(), "== *");
                MatchCollection mainsections = new Regex(@"==.?" + Regex.Escape("* Загружены") + ".*?==[^=]*?==.?" + Regex.Escape("* Загружены"), RegexOptions.IgnoreCase).Matches(rp.text);
                foreach (Match m in mainsections)
                    rp.text = rp.text.Replace(m.ToString(), "== * Загружены");
                while (rp.text.IndexOf("\n\n\n") != -1)
                    rp.text = rp.text.Replace("\n\n\n", "\n\n");
            }
            rp.Save("новая партия просроченных статей (" + numpages + ")", true);
        }
    }
}
