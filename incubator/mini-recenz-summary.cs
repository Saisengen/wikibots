using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Web;
using DotNetWikiBot;
using System.Threading;

internal class MyBot : Bot
{
    static void SortByColumn(string[,] m, int c)
    {
        for (int i = 0; i < m.GetLength(0); i++)
            for (int j = i + 1; j < m.GetLength(0); j++)
                if (Convert.ToInt32(m[i, c]) < Convert.ToInt32(m[j, c]))
                    SwapRows(m, i, j);
    }// сортировка двумерного массива по http://www.cyberforum.ru/csharp-beginners/thread369842.html
    static void SwapRows(string[,] m, int row1, int row2)
    {
        for (int i = 0; i < m.GetLength(1); i++)
            (m[row2, i], m[row1, i]) = (m[row1, i], m[row2, i]);
    }
    public PageList GetCategoryMembers(Site site, string cat, int limit)
    {
        PageList allpages = new PageList(site);
        string[] all = new string[limit];
        int page_num = 0;
        string URL = site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmnamespace=102&cmlimit=5000&cmtitle=К:" + HttpUtility.UrlEncode(cat) + "&format=xml";
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
            if (!string.IsNullOrEmpty(m))
            {
                Page n = new Page(site, m);
                allpages.Add(n);
            }
        return allpages;
    }
    public static void Main()
    {
        string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        MyBot bot = new MyBot();
        Page mrpage = new Page(site, "Проект:Инкубатор/Мини-рецензирование");
        mrpage.Load();
        int num = 0;
        PageList cand_list;
        DateTime nn = DateTime.Now;
        string[] mon = { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
        Page kuP = new Page(site, "Википедия:К удалению/" + nn.Day + " " + mon[nn.Month - 1] + " " + nn.Year);
        int max = 0;
        string nom = "";
        cand_list = bot.GetCategoryMembers(site, "Проект:Инкубатор:Статьи на мини-рецензировании", 5000);// предварительный пробег на предмет номинации к удалению старейших стабов
        string[,] forKU = new string[cand_list.Count(), 2];
        int kunum = 0;
        // смотрим дату последней правки
        foreach (Page p in cand_list)
        {
            p.Load();
            long seconds_from_last_edit = (long)(DateTime.Now - p.timestamp).TotalSeconds;
            forKU[kunum, 0] = p.title;
            forKU[kunum, 1] = seconds_from_last_edit.ToString();
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
            if (Convert.ToInt64(forKU[ku, 1]) > (4 * 24 * 3600)) // если больше X дней (в секундах), то работаем дальше...
            {
                if (!vus.Contains(forKU[ku, 0]) && !kucat.Contains(forKU[ku, 0])) // если нет в категории ВУС-Доработки и К удалению, продолжаем...
                {   // проверяем "ссылки сюда"
                    string apiout = site.GetWebPage(string.Concat(site.apiPath + "?action=query&titles=" + HttpUtility.UrlEncode(forKU[ku, 0]) + "&generator=linkshere&glhprop=title&glhnamespace=4&glhlimit=500&format=xml"));
                    if (apiout.IndexOf("Википедия:К восстановлению") != -1 | apiout.IndexOf("Википедия:К_восстановлению") != -1) // если есть ссылки с ВУС на статью, то уточняем актуальность
                    {
                        PageList actvus = new PageList();
                        actvus.FillAllFromCategory("Википедия:Незакрытые обсуждения восстановления страниц");
                        foreach (Page b in actvus)
                            if (apiout.IndexOf(b.title) != -1) // если хотя бы одна ссыдка является актуальным обсуждением
                                work = false; // то выключаем обработку этой страницы
                    }
                    if (work)
                    {
                        bool not_moved = false;
                        string newname = forKU[ku, 0].Replace("Инкубатор:", "");
                        Page newpage = new Page(site, newname);
                        if (newpage.Exists())
                        {
                            if (newname.IndexOf(",") != -1)
                                newname = newname.Replace(",", "");
                            else
                                newname += ".";
                        }
                        string token = "";
                        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&meta=tokens&type=csrf"))))
                            while (r.Read())
                                if (r.Name == "tokens")
                                    token = r.GetAttribute("csrftoken");
                        string result = site.PostDataAndGetResult("/w/api.php?action=move&format=xml", "from=" + Uri.EscapeDataString(forKU[ku, 0]) + "&to=" + Uri.EscapeDataString(newname) +
                            "&reason=автоперенос в ОП для номинации [[ВП:КУ|к удалению]]&movetalk=1&noredirect=1&token=" + Uri.EscapeDataString(token));
                        if (result.Contains("error"))
                        {
                            not_moved = true;
                            Console.WriteLine(forKU[ku, 0] + ";" + newname + ";" + result);
                        }
                        if (!not_moved)
                        {
                            Page page_in_mainspace = new Page(site, newname);
                            page_in_mainspace.Load();
                            
                            string revid_to_unpatrol = "";
                            using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=revisions&titles=" + Uri.EscapeDataString(newname) + "&rvprop=ids"))))
                                while (r.Read())
                                    if (r.Name == "rev")
                                        revid_to_unpatrol = r.GetAttribute("revid");
                            Thread.Sleep(5000);
                            string unpat_result = site.PostDataAndGetResult("/w/api.php?action=review&format=xml", "revid=" + revid_to_unpatrol +
                                "&comment=статья Инкубатора, перенесённая в ОП&unapprove=1&token=" + Uri.EscapeDataString(token));
                            if (!unpat_result.Contains("uccess"))
                                Console.WriteLine(unpat_result);

                            // почистить от шаблонов инкубатора
                            Regex itemplates = new Regex(@"\{\{.{0,5}(инкубатор|пишу|редактирую).*?(/n|\}\})", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            while (itemplates.IsMatch(page_in_mainspace.text, 0))
                            {
                                for (int qw = 0; qw < itemplates.Matches(page_in_mainspace.text).Count; qw++)
                                {
                                    string rep = itemplates.Matches(page_in_mainspace.text)[qw].ToString();
                                    page_in_mainspace.text = page_in_mainspace.text.Replace(rep, "");
                                }
                            }

                            Regex comments = new Regex("<!--.*?-->", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                            while (comments.IsMatch(page_in_mainspace.text, 0))
                            {
                                for (int qw = 0; qw < comments.Matches(page_in_mainspace.text).Count; qw++)
                                {
                                    string rep = comments.Matches(page_in_mainspace.text)[qw].ToString();
                                    page_in_mainspace.text = page_in_mainspace.text.Replace(rep, "");
                                }
                            }
                            page_in_mainspace.text = page_in_mainspace.text.Replace("\n•••", "\n***").Replace("\n••", "\n**").Replace("\n•", "\n*").Replace("[[:Кат", "[[Кат").Replace("[[:кат", "[[Кат").Replace("[[:Cat", "[[Cat").Replace("[[:cat", "[[Cat");
                            while (page_in_mainspace.text.IndexOf("\n ") != -1)
                            {
                                page_in_mainspace.text = page_in_mainspace.text.Replace("\n ", "\n"); // строки начинающиеся с пробела
                            }
                            page_in_mainspace.text = "{{подст:Предложение к удалению}}\n" + page_in_mainspace.text; // к удалению
                            while (page_in_mainspace.text.IndexOf("\n\n\n") != -1)
                            {
                                page_in_mainspace.text = page_in_mainspace.text.Replace("\n\n\n", "\n\n"); // лишние переносы строк
                            }
                            page_in_mainspace.Save("[[" + kuP.title + "#" + page_in_mainspace.title + "|автоматическая номинация к удалению]]", false);
                            nom = nom + "\n\n== [[" + page_in_mainspace.title + "]] ==\n{{subst:User:IncubatorBot/mrKU}} ~~~~";
                            max++;
                            if (max == 5)
                                break;
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
                kuP.text += nom;
                kuP.Save("автоматическая номинация просроченных статей (" + max + ") из Инкубатора", false);
            }
            else
            {
                kuP.text = "{{КУ-Навигация}}\n\n" + kuP.text + nom;
                kuP.Save("автоматическая номинация просроченных статей (" + max + ") из Инкубатора", false);
            }
        }
        cand_list = bot.GetCategoryMembers(site, "Проект:Инкубатор:Статьи на мини-рецензировании", 5000);
        string[] regtitle = new string[] { "== ", @"\[\[", ".*?", @"\]\]", " ==" };
        MatchCollection titles = new Regex(string.Concat(regtitle), RegexOptions.IgnoreCase).Matches(mrpage.text);
        string[] datestring = new string[] { "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
        for (int i = 0; i < titles.Count; i++)
        {
            string target_ns, user, tstamp, comment, target_title, str7;
            DateTime timing = DateTime.Now.AddDays(-5000); //для запоминания времени
            string initial_ns = target_ns = user = tstamp = comment = target_title = str7 = "";
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
                int length = mrpage.text.IndexOf("\n== [[", index + 6);
                int length_add = 0;
                if (length == -1)
                    length = mrpage.text.Length;
                
                if (mrpage.text.IndexOf("=== Итог ===", index, length - index) == -1) // проверяем наличие секции "Итог"
                {
                    string pageHTM; // если ее нет
                    for (int qaza = 0; qaza < 5; qaza++)
                    {
                        string xmlresult = site.GetWebPage(site.apiPath + "?action=query&list=logevents&letitle=" + HttpUtility.UrlEncode(title) + "&letype=move&ledir=newer&format=xml");
                        if (xmlresult.IndexOf("<item") != -1)
                        {
                            XmlTextReader reader = new XmlTextReader(new StringReader(xmlresult));
                            while (reader.Read())
                                if (reader.NodeType == XmlNodeType.Element)
                                {
                                    if (reader.Name == "item")
                                    {
                                        initial_ns = reader.GetAttribute("ns");
                                        user = reader.GetAttribute("user");
                                        tstamp = reader.GetAttribute("timestamp");
                                        comment = reader.GetAttribute("comment");
                                    }
                                    if (reader.Name == "params")
                                    {
                                        target_title = reader.GetAttribute("target_title");
                                        target_ns = reader.GetAttribute("target_ns");
                                    }
                                }
                            // если другое пространство имен подводим итог, если нет, меняем заголовок
                            if (initial_ns != target_ns)
                            {
                                if (comment.Contains("{{") || comment.Contains("{|"))
                                    comment = "<nowiki>" + comment + "</nowiki>";
                                DateTime time = DateTime.Parse(tstamp).AddHours(-3.0);
                                timing = time;
                                object[] objArray1 = new object[] { time.Day, " ", datestring[time.Month - 1], " ", time.Year, " ", time.TimeOfDay };
                                string str11 = string.Concat(objArray1);
                                string[] textArray4 = new string[] { "\n=== Итог ===\nСтраница \x00ab[[", title, "]]\x00bb была переименована ", str11, " (UTC) участником [[u:", user, 
                                    "]] в \x00ab[[", target_title, "]]\x00bb" };
                                str7 = string.Concat(textArray4);
                                if (comment.Length > 0)
                                    str7 = str7 + " с комментарием \x00ab" + comment + "\x00bb.";
                                str7 += " <small>Данный итог подведен ботом</small> ~~~~\n";
                                result = true;
                                break;
                            }
                            else
                            {
                                result = false;
                                string repp = "== [[" + target_title + "]] ==\n:<small>Обсуждение начато под заголовком [[" + title + "]]. ~~~~</small>";
                                mrpage.text = mrpage.text.Replace("== [[" + title + "]] ==", repp);
                                length_add += repp.Length;
                                title = target_title;
                            }
                        }
                        else break;
                    }
                    // еще раз проверим на наличие в категории
                    if (!cand_list.Contains(title2)) // title2 = title до переиенования, если оно было
                    {
                        if (string.IsNullOrEmpty(title2))
                            throw new WikiBotException(Bot.Msg("No title specified for page to load."));
                        // подгружаем лог удалений
                        string[] textArray5 = new string[] { site.apiPath, "?action=query&list=logevents&letitle=", HttpUtility.UrlEncode(title2), "&letype=delete&ledir=newer&format=xml" };
                        string pageURL = string.Concat(textArray5);
                        try { pageHTM = site.GetWebPage(pageURL); }
                        catch { pageHTM = string.Empty; }
                        // если в логе есть - подводим итог
                        if (pageHTM.IndexOf("<item") != -1)
                        {
                            XmlTextReader reader2 = new XmlTextReader(new StringReader(pageHTM));
                            while (reader2.Read())
                                if ((reader2.NodeType == XmlNodeType.Element) && (reader2.Name == "item"))
                                {
                                    user = reader2.GetAttribute("user");
                                    tstamp = reader2.GetAttribute("timestamp");
                                    comment = reader2.GetAttribute("comment");
                                    if (comment.Contains("{{") || comment.Contains("{|"))
                                        comment = "<nowiki>" + comment + "</nowiki>";
                                }
                            DateTime time2 = DateTime.Parse(tstamp);
                            if ((time2 - timing).TotalDays > 2)
                            {
                                object[] objArray2 = new object[] { time2.Day, " ", datestring[time2.Month - 1], " ", time2.Year, " ", time2.TimeOfDay };
                                string str12 = string.Concat(objArray2);
                                string[] textArray6 = new string[] { "\n=== Итог ===\nСтраница \x00ab[[", title2, "]]\x00bb была удалена ", str12, " (UTC) участником [[ut:", user, "|", user,
                                    "]] по причине \x00ab", comment, "\x00bb. <small>Данный итог подведен ботом</small> ~~~~\n" };
                                str7 = string.Concat(textArray6);
                                result = true;
                            }
                        }
                    }
                    if (result != false)
                    {
                        Regex langlinks = new Regex(@"\[\[[a-z-]{2,6}:.*?\]\]", RegexOptions.Singleline);
                        MatchCollection ll = langlinks.Matches(str7);
                        foreach (Match m in ll)
                        {
                            string r7 = m.ToString().Replace("[[", "[[:");
                            str7 = str7.Replace(m.ToString(), r7);
                        }
                        try
                        {
                            if (str7.IndexOf("http://") != -1)
                                str7 = str7.Replace("http://", "");
                            if (str7.IndexOf("https://") != -1)
                                str7 = str7.Replace("https://", "");
                        }
                        catch { Console.WriteLine("Error with link parsing: \n\n\"" + str7 + "\"\n"); }
                        // вставляем итог в страницу
                        int startIndex = mrpage.text.IndexOf("}\n", index, (int)((length + length_add) - index));
                        int num6 = mrpage.text.IndexOf("\n==", startIndex);
                        if (num6 == -1)
                            num6 = mrpage.text.Length;
                        mrpage.text = mrpage.text.Insert(num6, str7);
                        num++;
                    }
                }
            }
        }
        while (mrpage.text.IndexOf("\n\n\n") != -1)
            mrpage.text = mrpage.text.Replace("\n\n\n", "\n\n");
        mrpage.Save("автоматическое подведение итогов (" + num + "), коррекция заголовков", true);
    }
}
