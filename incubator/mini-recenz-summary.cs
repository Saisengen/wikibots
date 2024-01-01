using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Web;
using DotNetWikiBot;

internal class MyBot : Bot
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    // сортировка двумерного массива по http://www.cyberforum.ru/csharp-beginners/thread369842.html
    static void SortByColumn(string[,] m, int c)
    {
        for (int i = 0; i < m.GetLength(0); i++)
            for (int j = i + 1; j < m.GetLength(0); j++)
                if (Convert.ToInt32(m[i, c]) < Convert.ToInt32(m[j, c]))
                    SwapRows(m, i, j);
    }
    static void SwapRows(string[,] m, int row1, int row2)
    {
        for (int i = 0; i < m.GetLength(1); i++)
        {
            var tmp = m[row1, i];
            m[row1, i] = m[row2, i];
            m[row2, i] = tmp;
        }
    }
    public string[] Settings(byte num, Site site)
    {
        string[] ar = new string[num];
        Page setting = new Page(site, "Участник:" + creds[8] + "/settings.js");
        setting.Load();
        Regex all = new Regex(@"all.?=.?true", RegexOptions.Singleline);
        Regex on = new Regex(@"mr_itog.?=.?true", RegexOptions.Singleline);
        Regex KU = new Regex(@"mr_KU.?=.?true", RegexOptions.Singleline);
        Regex mrpage = new Regex(@"remind_page.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex remind_cat = new Regex(@"remind_category.?=.*?;", RegexOptions.Singleline);
        Regex KUdays = new Regex(@"mr_KUdays.?=.*?;", RegexOptions.Singleline);
        Regex KUmax = new Regex(@"mr_KUmax.?=.*?;", RegexOptions.Singleline);
        if (all.Matches(setting.text).Count > 0) // if all bots are allowed
        {
            if (on.Matches(setting.text).Count > 0) // if this bot is allowed
            {
                ar[0] = "1";
                if (mrpage.Matches(setting.text).Count > 0) //page to save
                {
                    string a = mrpage.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[1] = a;
                }
                if (remind_cat.Matches(setting.text).Count > 0)
                {
                    string a = remind_cat.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1);
                    a = a.Replace("\"", "");
                    a = a.Replace(";", "");
                    a = a.Replace("\n", "");
                    ar[2] = a.Trim();
                }
                if (KU.Matches(setting.text).Count > 0)
                {
                    ar[3] = "1";
                }
                if (KUmax.Matches(setting.text).Count > 0)
                {
                    string a = KUmax.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1);
                    a = a.Replace("\"", "");
                    a = a.Replace(";", "");
                    a = a.Replace("\n", "");
                    ar[4] = a.Trim();
                }
                if (KUdays.Matches(setting.text).Count > 0)
                {
                    string a = KUdays.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1);
                    a = a.Replace("\"", "");
                    a = a.Replace(";", "");
                    a = a.Replace("\n", "");
                    ar[5] = a.Trim();
                }
                return ar;
            }
            else
            { ar[0] = "0"; return ar; }
        }
        else
        { ar[0] = "0"; return ar; }
    }
    /// <summary>
    /// Альтернативный способ получения содержимого категорий
    /// </summary>
    public PageList GetCategoryMembers(DotNetWikiBot.Site site, string cat, int limit)
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
        Console.WriteLine("Loaded " + page_num + " pages from Category:" + cat);
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
        Site site = new Site("https://ru.wikipedia.org", creds[8], creds[9]);
        Site site2 = new Site("https://ru.wikipedia.org", creds[10], creds[11]); //для переименования
        MyBot bot = new MyBot();
        string[] set = new string[6];

        set = bot.Settings(6, site);
        // если разрешено
        if (set[0] == "1")
        {
            // страница мини-рецензирования
            Page mrpage = new Page(site, set[1]);
            mrpage.Load();
            int num = 0;
            PageList cand_list = new PageList(site);

            // предварительный пробег на предмет номинации к удалению старейших стабов
            if (set[3] == "1") // получаем разрешение на номинацию КУ
            {
                // объявим страницу КУ
                DateTime nn = DateTime.Now;
                string[] mon = { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
                Page kuP = new Page(site2, "Википедия:К удалению/" + nn.Day + " " + mon[nn.Month - 1] + " " + nn.Year);

                int max = 0;
                string nom = "";
                if (DateTime.Now.Hour < 1) // выполняем работу только до 1 часа ночи (один раз в день)
                {
                    // получаем список статей на мини-рец
                    cand_list = bot.GetCategoryMembers(site, set[2], 5000);
                    string[,] forKU = new string[cand_list.Count(), 2];
                    int kunum = 0;
                    // смотрим дату последней правки
                    foreach (Page p in cand_list)
                    {
                        p.Load();
                        long razniza = (long)(DateTime.Now - p.timestamp).TotalSeconds;
                        forKU[kunum, 0] = p.title;
                        forKU[kunum, 1] = razniza.ToString();
                        kunum++;
                    }
                    SortByColumn(forKU, 1);
                    // теперь надо проверить наличие ВУС и прочих исключений
                    PageList vus = new PageList();
                    PageList kucat = new PageList();
                    vus.FillAllFromCategory("Проект:Инкубатор:Статьи на доработке");
                    kucat.FillAllFromCategory("Википедия:Кандидаты на удаление");
                    for (int ku = 0; ku < kunum; ku++)
                    {
                        bool work = true;
                        if (Convert.ToInt64(forKU[ku, 1]) > (Convert.ToInt64(set[5]) * 24 * 3600)) // если больше X дней (в секундах), то работаем дальше...
                        {
                            if (!vus.Contains(forKU[ku, 0]) && !kucat.Contains(forKU[ku, 0])) // если нет в категории ВУС-Доработки и К удалению, продолжаем...
                            {   // проверяем "ссылки сюда"
                                string[] textArray3 = new string[] { site.apiPath, "?action=query&titles=", HttpUtility.UrlEncode(forKU[ku, 0]), "&generator=linkshere&glhprop=title&glhnamespace=4&glhlimit=500&format=xml" };
                                string pageHTM = site.GetWebPage(string.Concat(textArray3));
                                // если есть ссылки с ВУС на статью, то уточняем актуальность
                                if (pageHTM.IndexOf("Википедия:К восстановлению") != -1 | pageHTM.IndexOf("Википедия:К_восстановлению") != -1)
                                {
                                    PageList actvus = new PageList();
                                    actvus.FillAllFromCategory("Википедия:Незакрытые обсуждения восстановления страниц");
                                    foreach (Page b in actvus)
                                    { // если хотя бы одна ссыдка является актуальным обсуждением
                                        if (pageHTM.IndexOf(b.title) != -1)
                                            work = false; // то выключаем обработку этой страницы
                                    }
                                }

                                // если все норм, продолжаем
                                if (work)
                                {
                                    Page p = new Page(site2, forKU[ku, 0]);
                                    Page pp = new Page(site2, forKU[ku, 0].Replace("Инкубатор:", ""));
                                    bool t = false;
                                    string newname = forKU[ku, 0].Replace("Инкубатор:", "");
                                    if (pp.Exists())
                                    {
                                        if (newname.IndexOf(",") != -1)
                                            newname = newname.Replace(",", "");
                                        else
                                            newname = newname + ".";
                                    }
                                    p.Load();
                                    try
                                    {
                                        p.RenameTo(newname, "автоперенос в ОП для номинации [[ВП:КУ|к удалению]]", true, false);
                                        //bot.RenameToSR(site, p.title, newname, "автоперенос в ОП для номинации [[ВП:КУ|к удалению]]", true, false);
                                    }
                                    catch
                                    { // если не переименовывается, попробовать еще раз
                                        try
                                        {
                                            if (newname.IndexOf(",") != -1)
                                                newname = newname.Replace(",", "");
                                            else
                                                newname = newname + ".";
                                            p.RenameTo(newname, "автоперенос в ОП для номинации [[ВП:КУ|к удалению]]", true, false);
                                        }
                                        catch // если не получилось, отбой
                                        { t = true; Console.WriteLine("Problems with moving page <" + newname + ">."); }
                                    }
                                    if (t != true)
                                    {
                                        Page op = new Page(site2, newname);
                                        op.Load();
                                        // тут бы ее распатрулировать
                                        // почистить от шаблонов инкубатора
                                        Regex itemplates = new Regex(Regex.Escape("{{") + ".{0,5}(инкубатор|пишу|редактирую).*?(/n|" + Regex.Escape("}}") + ")", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                                        while (itemplates.IsMatch(op.text, 0))
                                        {
                                            for (int qw = 0; qw < itemplates.Matches(op.text).Count; qw++)
                                            {
                                                string rep = itemplates.Matches(op.text)[qw].ToString();
                                                op.text = op.text.Replace(rep, "");
                                            }
                                        }
                                        // почистить комментарии
                                        Regex comments = new Regex(Regex.Escape("<!--") + ".*?" + Regex.Escape("-->"), RegexOptions.Singleline | RegexOptions.IgnoreCase);
                                        while (comments.IsMatch(op.text, 0))
                                        {
                                            for (int qw = 0; qw < comments.Matches(op.text).Count; qw++)
                                            {
                                                string rep = comments.Matches(op.text)[qw].ToString();
                                                op.text = op.text.Replace(rep, "");
                                            }
                                        }
                                        op.text = op.text.Replace("\n•••", "\n***"); // маркированный список
                                        op.text = op.text.Replace("\n••", "\n**"); // маркированный список
                                        op.text = op.text.Replace("\n•", "\n*"); // маркированный список
                                        op.text = op.text.Replace("[[:Кат", "[[Кат");
                                        op.text = op.text.Replace("[[:кат", "[[Кат");
                                        op.text = op.text.Replace("[[:Cat", "[[Cat");
                                        op.text = op.text.Replace("[[:cat", "[[Cat");
                                        while (op.text.IndexOf("\n ") != -1)
                                        {
                                            op.text = op.text.Replace("\n ", "\n"); // строки начинающиеся с пробела
                                        }
                                        op.text = "{{подст:Предложение к удалению}}\n" + op.text; // к удалению
                                        while (op.text.IndexOf("\n\n\n") != -1)
                                        {
                                            op.text = op.text.Replace("\n\n\n", "\n\n"); // лишние переносы строк
                                        }
                                        try
                                        {
                                            op.Save("[[" + kuP.title + "#" + op.title + "|автоматическая номинация к удалению]]", false);
                                        }
                                        catch
                                        {
                                            try
                                            {
                                                op.Save("[[" + kuP.title + "#" + op.title + "|автоматическая номинация к удалению]]", false);
                                            }
                                            catch
                                            {
                                                op.Save("[[" + kuP.title + "#" + op.title + "|автоматическая номинация к удалению]]", false);
                                                Console.WriteLine("Problems with saving page <" + newname + ">.");
                                            }
                                        }
                                        nom = nom + "\n\n== [[" + op.title + "]] ==\n{{subst:User:Dibot/mrKU}} ~~~~";
                                        max++;
                                        if (max == Convert.ToInt32(set[4])) break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (max > 0)
                {
                    kuP.Load();
                    if (kuP.Exists())
                    {
                        kuP.Load();
                        kuP.text = kuP.text + nom;
                        kuP.Save("автоматическая номинация просроченных статей (" + max + ") из Инкубатора", false);
                    }
                    else
                    {
                        kuP.Load();
                        kuP.text = "{{КУ-Навигация}}\n\n" + kuP.text + nom;
                        kuP.Save("автоматическая номинация просроченных статей (" + max + ") из Инкубатора", false);
                    }
                }
            }
            // работаем по заголовкам

            // получаем список статей на мини-рец
            cand_list = bot.GetCategoryMembers(site, set[2], 5000);
            // получаем список заголовков на странице мини-рец
            string[] regtitle = new string[] { "== ", Regex.Escape("[["), ".*?", Regex.Escape("]]"), " ==" };
            MatchCollection titles = new Regex(string.Concat(regtitle), RegexOptions.IgnoreCase).Matches(mrpage.text);
            string[] datestring = new string[] { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
            for (int i = 0; i < titles.Count; i++)
            {
                string str2;
                string str3;
                string str4;
                string str5;
                string str6;
                string str7;
                DateTime timing = DateTime.Now.AddDays(-5000); //для запоминания времени
                string attribute = str2 = str3 = str4 = str5 = str6 = str7 = string.Empty;
                // переменная - заголовок на мини-рец
                string title = titles[i].Value.Replace("== [[", string.Empty).Replace("]] ==", string.Empty);
                string title2 = title;
                // место положения заголовка
                int index = mrpage.text.IndexOf("== [[" + title, 0);
                bool result = false;
                // если в категории нет такой страницы
                if (!cand_list.Contains(title))
                {
                    // положение след. заголовка или конца страницы
                    int length = mrpage.text.IndexOf("\n== [[", (int)(index + 6));
                    int length_add = 0;
                    if (length == -1)
                    {
                        length = mrpage.text.Length;
                    }
                    // проверяем наличие секции "Итог"
                    if (mrpage.text.IndexOf("=== Итог ===", index, (int)(length - index)) == -1)
                    {
                        // если ее нет
                        string pageHTM;
                        for (int qaza = 0; qaza < 5; qaza++)
                        {

                            if (string.IsNullOrEmpty(title))
                            {
                                throw new WikiBotException(Bot.Msg("No title specified for page to load."));
                            }
                            // проверяем на переименования
                            string[] textArray3 = new string[] { site.apiPath, "?action=query&list=logevents&letitle=", HttpUtility.UrlEncode(title), "&letype=move&ledir=newer&format=xml" };
                            string pageURL = string.Concat(textArray3);
                            try
                            {
                                pageHTM = site.GetWebPage(pageURL);
                            }
                            catch
                            {
                                pageHTM = string.Empty;
                            }
                            // видимо, условие для проверки наличия записи в логах, и если переименование было обрабатываем данные
                            if (pageHTM.IndexOf("<item") != -1)
                            {
                                XmlTextReader reader = new XmlTextReader(new StringReader(pageHTM));
                                while (reader.Read())
                                {
                                    if (reader.NodeType == XmlNodeType.Element)
                                    {
                                        if (reader.Name == "item")
                                        {
                                            attribute = reader.GetAttribute("ns");
                                            str3 = reader.GetAttribute("user");
                                            str4 = reader.GetAttribute("timestamp");
                                            str5 = reader.GetAttribute("comment");
                                        }
                                        if (reader.Name == "params")
                                        {
                                            str6 = reader.GetAttribute("target_title");
                                            str2 = reader.GetAttribute("target_ns");
                                        }
                                    }
                                }
                                // если другое пространство имен подводим итог, если нет, меняем заголовок
                                if (attribute != str2)
                                {
                                    /*if (str5.IndexOf("/*") != -1)
                                    {
                                        str5 = str5.Remove(str5.IndexOf("/*") - 2);
                                    }*/
                                    // код выше глючил при удалении по d-шаблонам, заменен на нижеследующий
                                    if (str5.IndexOf("/*") > 1)
                                        str5 = str5.Remove(str5.IndexOf("/*"));
                                    else if (str5.IndexOf("/*") >= 0)
                                    {
                                        str5 = str5.Replace("{{", "{");
                                        str5 = str5.Replace("}}", "}");
                                        str5 = str5.Replace("http://", " ");
                                    }
                                    DateTime time = DateTime.Parse(str4).AddHours(-3.0);
                                    timing = time;
                                    object[] objArray1 = new object[] { time.Day, " ", datestring[time.Month - 1], " ", time.Year, " ", time.TimeOfDay };
                                    string str11 = string.Concat(objArray1);
                                    //if (str2 != "4")
                                    //{
                                    string[] textArray4 = new string[] { "\n=== Итог ===\nСтраница \x00ab[[", title, "]]\x00bb была переименована ", str11, " (UTC) участником [[ut:", str3, "|", str3, "]] в \x00ab[[", str6, "]]\x00bb" };
                                    str7 = string.Concat(textArray4);
                                    if (str5.Length > 0)
                                    {
                                        str7 = str7 + " с комментарием \x00ab" + str5 + "\x00bb.";
                                    }
                                    str7 = str7 + " <small>Данный итог подведен ботом</small> ~~~~\n";
                                    //}
                                    result = true;
                                    break;
                                }
                                else
                                {
                                    result = false;
                                    // меняем заголовок
                                    string repp = "== [[" + str6 + "]] ==\n:<small>Обсуждение начато под заголовком [[" + title + "]]. ~~~~</small>";
                                    mrpage.text = mrpage.text.Replace("== [[" + title + "]] ==", repp);
                                    length_add = length_add + repp.Length;
                                    title = str6;
                                }
                            }
                            else break;
                        }
                        // еще раз проверим на наличие в категории
                        if (!cand_list.Contains(title2)) // title2 = title до переиенования, если оно было
                        {
                            if (true)//(result != true) // Убрал проверку на наличие итога от переименования, т.к. сравниваем по времени переименования и удаления
                            {
                                if (string.IsNullOrEmpty(title2))
                                {
                                    throw new WikiBotException(Bot.Msg("No title specified for page to load."));
                                }
                                // подгружаем лог удалений
                                string[] textArray5 = new string[] { site.apiPath, "?action=query&list=logevents&letitle=", HttpUtility.UrlEncode(title2), "&letype=delete&ledir=newer&format=xml" };
                                string pageURL = string.Concat(textArray5);
                                try
                                {
                                    pageHTM = site.GetWebPage(pageURL);
                                }
                                catch
                                {
                                    pageHTM = string.Empty;
                                }
                                // если в логе есть - подводим итог
                                if (pageHTM.IndexOf("<item") != -1)
                                {
                                    XmlTextReader reader2 = new XmlTextReader(new StringReader(pageHTM));
                                    while (reader2.Read())
                                    {
                                        if ((reader2.NodeType == XmlNodeType.Element) && (reader2.Name == "item"))
                                        {
                                            str3 = reader2.GetAttribute("user");
                                            str4 = reader2.GetAttribute("timestamp");
                                            str5 = reader2.GetAttribute("comment");
                                        }
                                    }
                                    if (str5.IndexOf("/*") > 1)
                                        str5 = str5.Remove(str5.IndexOf("/*"));
                                    else if (str5.IndexOf("/*") >= 0)
                                    {
                                        str5 = str5.Replace("{{", "{");
                                        str5 = str5.Replace("}}", "}");
                                        str5 = str5.Replace("http://", " ");
                                    }
                                    DateTime time2 = DateTime.Parse(str4);
                                    if ((time2 - timing).TotalDays > 2)
                                    {
                                        object[] objArray2 = new object[] { time2.Day, " ", datestring[time2.Month - 1], " ", time2.Year, " ", time2.TimeOfDay };
                                        string str12 = string.Concat(objArray2);
                                        string[] textArray6 = new string[] { "\n=== Итог ===\nСтраница \x00ab[[", title2, "]]\x00bb была удалена ", str12, " (UTC) участником [[ut:", str3, "|", str3, "]] по причине \x00ab", str5, "\x00bb. <small>Данный итог подведен ботом</small> ~~~~\n" };
                                        str7 = string.Concat(textArray6);
                                        result = true;
                                    }
                                }
                            }
                        }
                        if (result != false)
                        {
                            Regex langlinks = new Regex(@"" + Regex.Escape("[[") + "[a-z-]{2,6}:.*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                            MatchCollection ll = langlinks.Matches(str7);
                            foreach (Match m in ll)
                            {
                                string r7 = m.ToString().Replace("[[", "[[:");
                                str7 = str7.Replace(m.ToString(), r7);
                            }
                            try
                            {
                                if (str7.IndexOf("http://") != -1)
                                {
                                    /* int st_a = str7.IndexOf("http://");
                                     int st_b = str7.IndexOf(" ", st_a);
                                     str7 = str7.Remove(st_a, st_b - st_a);*/
                                    str7 = str7.Replace("http://", "");
                                }
                                if (str7.IndexOf("https://") != -1)
                                {
                                    str7 = str7.Replace("https://", "");
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Error with link parsing: \n\n\"" + str7 + "\"\n");
                            }
                            // вставляем итог в страницу
                            int startIndex = mrpage.text.IndexOf("}\n", index, (int)((length + length_add) - index));
                            int num6 = mrpage.text.IndexOf("\n==", startIndex);
                            if (num6 == -1)
                            {
                                num6 = mrpage.text.Length;
                            }
                            mrpage.text = mrpage.text.Insert(num6, str7);
                            num++;
                        }
                    }
                }
            }
            MatchCollection mainsections = new Regex(@"=.?<small>[^" + Regex.Escape("[") + "]*?<small>", RegexOptions.IgnoreCase & RegexOptions.Singleline).Matches(mrpage.text);
            int dsg = 0;
            while (mainsections.Count > 0)
            {
                foreach (Match m in mainsections)
                {
                    mrpage.text = mrpage.text.Replace(m.ToString(), "= <small>");
                }
                // костыль для выхода из цикла
                dsg++;
                if (dsg > 5)
                    break;
            }
            while (mrpage.text.IndexOf("\n\n\n") != -1)
            {
                mrpage.text = mrpage.text.Replace("\n\n\n", "\n\n");
            }
            mrpage.Save("автоматическое подведение итогов (" + num + "), коррекция заголовков", true);
        }
    }
}
