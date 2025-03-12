using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Xml;
using DotNetWikiBot;

class MyBot : Bot
{
    static string[] creds;
    static Site site;
    static void stat_bot()
    {
        DateTime utcNow = DateTime.UtcNow;
        Page pn = new Page(site, "Участник:IncubatorBot/TalkStat/" + utcNow.ToString("yyyyMM"));
        Page inc = new Page(site, "Участник:IncubatorBot/IncubatorStat/" + utcNow.ToString("yyyyMM"));
        Page incmem = new Page(site, "Проект:Инкубатор/Участники");
        Page vos = new Page(site, "Википедия:К восстановлению");
        vos.Load();
        pn.Load();
        inc.Load();
        incmem.Load();
        if (!pn.Exists())
            pn.text = "{{subst:User:IncubatorBot/TalkStat/cap}}\n<noinclude>|}</noinclude>";
        if (!inc.Exists())
            inc.text = "{{subst:User:IncubatorBot/IncubatorStat/cap}}\n<noinclude>|}</noinclude>";

        string[] cats = {"Википедия:Статьи для срочного улучшения","Статьи на улучшении более года","Статьи на улучшении более полугода","Статьи на улучшении более 90 дней","Статьи на улучшении более 30 дней",
        "Статьи на улучшении менее 30 дней","Википедия:Незакрытые обсуждения статей для улучшения","Википедия:Кандидаты на удаление","Википедия:Незакрытые обсуждения удаления страниц",
        "Википедия:Статьи для переименования","Википедия:Незакрытые обсуждения переименования страниц","Википедия:Кандидаты на объединение","Википедия:Незакрытые обсуждения объединения страниц",
        "Википедия:Статьи для разделения","Википедия:Незакрытые обсуждения разделения страниц","Википедия:Незакрытые обсуждения восстановления страниц","Проект:Инкубатор:Все статьи",
        "Проект:Инкубатор:Статьи на мини-рецензировании","Википедия:Стабы в Инкубаторе","Проект:Инкубатор:Запросы на проверку","Проект:Инкубатор:Запросы о помощи","Проект:Инкубатор:К удалению"};
        string[] n = new string[cats.Length];
        for (int i = 0; i < cats.Length; i++)
        {
            var rdr = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&prop=categoryinfo&titles=К:" + HttpUtility.UrlEncode(cats[i]) + "&format=xml")));
            while (rdr.Read())
                if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "categoryinfo")
                    n[i] = rdr.GetAttribute("pages");
        }

        int vos_p = 0;
        var vos_page = new PageList(site);
        vos_page.FillFromPageLinks("Википедия:К восстановлению");
        foreach (Page p in vos_page)
        {
            if (vos.text.IndexOf("* [[" + p.title) != -1)
                vos_p++;
            if (vos.text.IndexOf("* " + p.title) != -1)
                vos_p++;
            if (vos.text.IndexOf("* [[:" + p.title) != -1)
                vos_p++;
        }
        int aa = 0;
        int bb = 0;
        int incmen = -3;
        for (int i = 1; i < 100; i++)
        {
            if (incmem.text.IndexOf("* [[", aa + 10) != -1)
            {
                incmen++;
                aa = incmem.text.IndexOf("* [[", aa + 10);
                incmem.text.Remove(aa, 5);
            }

            if (incmem.text.IndexOf("* {{", bb + 10) != -1)
            {
                incmen++;
                bb = incmem.text.IndexOf("* {{", bb + 10);
                incmem.text.Remove(bb, 5);
            }
        }

        string stat = "|- align=\"right\"\n| {{subst:#time: j.m.Y}} \n|" + n[0] + "||" + n[1] + "||" + n[2] + "||" + n[3] + "||" + n[4] + "||" + n[5] + "||" + n[6] + "\n|" + n[7] + "||" + n[8] + "\n|" +
            n[9] + "||" + n[10] + "\n|" + n[11] + "||" + n[12] + "\n|" + n[13] + "||" + n[14] + "\n|" + vos_p + "||" + n[15] + "\n";
        pn.text = pn.text.Insert(pn.text.LastIndexOf("<noinclude>|}</noinclude>"), stat);
        pn.Save("обновление статистики", true);

        string incstat = "|- align=\"right\"\n| {{subst:#time: j.m.Y}} \n|" + n[16] + "||" + n[17] + "\n|" + n[18] + "\n|" + n[19] + "||" + n[20] + "||" + n[21] + "\n|" + incmen + "\n";
        inc.text = inc.text.Insert(inc.text.IndexOf("<noinclude>|}</noinclude>"), incstat);
        inc.Save("обновление статистики", true);
    }
    static void inc_check_bot()
    {
        MyBot bot = new MyBot();
        Page p = new Page(site, "Проект:Инкубатор/Запросы помощи и проверки");
        string talkdate, pagedate;
        Regex set_parser = new Regex(@".*?" + Regex.Escape("|"), RegexOptions.Singleline);
        var cats = "Проект:Инкубатор:Запросы на проверку|Проект:Инкубатор:Запросы о помощи".Split('|');
        int count = cats.Length;
        var dt = new string[count];
        var nbcats = "Проект:Инкубатор:Статьи nb0|Проект:Инкубатор:Статьи nb1|Проект:Инкубатор:Статьи nb2".Split('|');
        var nbcolors = "FFE0E0|DDFFDD|FFFFCC".Split('|');
        var shtitles = "Пров|Пом".Split('|');
        for (int j = 0; j < count; j++)
            dt[j] = "\n{{User:IncubatorBot/ЗПП|start|" + (j + 1).ToString() + "}}\n{{User:IncubatorBot/ЗПП|th}}\n";
        int[,] num = new int[count, 2];
        for (int j = 0; j < count; j++)
            num[j, 0] = num[j, 1] = 0;
        string[] nb = new string[set_parser.Matches("Проект:Инкубатор:Статьи nb0|Проект:Инкубатор:Статьи nb1|Проект:Инкубатор:Статьи nb2").Count];
        PageList nblist;
        for (int j = 0; j < set_parser.Matches("Проект:Инкубатор:Статьи nb0|Проект:Инкубатор:Статьи nb1|Проект:Инкубатор:Статьи nb2").Count; j++)
        {
            nb[j] = "|";
            nblist = bot.GetCategoryMembers102(site, nbcats[j]);
            foreach (Page pp in nblist)
                nb[j] = nb[j] + pp.title + "|";
            nblist.Clear();
        }
        PageList pl = new PageList(site);
        for (int i = 0; i < count; i++)
        {
            pl.FillFromCategory(cats[i]);
            foreach (Page m in pl)
            {
                m.Load();
                int ns = m.GetNamespace(); // make a shortcut title fot output [[fullname|shortcut]]
                string t1 = m.title;
                string t2 = "";
                string otitle = "";
                switch (ns)
                {
                    case 102:
                        t2 = t1.Remove(0, 10);
                        otitle = t1.Remove(0, 9);
                        otitle = "Обсуждение Инкубатора" + otitle;
                        break;
                    case 103:
                        t1 = t1.Replace("Обсуждение Инкубатора", "Инкубатор");
                        t2 = t1.Remove(0, 10);
                        otitle = t1.Remove(0, 9);
                        otitle = "Обсуждение Инкубатора" + otitle;
                        break;
                    default:
                        continue;
                }
                Page n = new Page(site, t1);
                Page talk = new Page(site, otitle);
                n.LoadWithMetadata();
                talk.LoadWithMetadata();
                DateTime dpage = n.timestamp;
                pagedate = dpage.ToString("yyyy-MM-dd HH:mm");
                string bgcolor = "";
                if (talk.Exists() == true)
                {
                    for (int j = 0; j < set_parser.Matches("Проект:Инкубатор:Статьи nb0|Проект:Инкубатор:Статьи nb1|Проект:Инкубатор:Статьи nb2").Count; j++)
                    {
                        if (nb[j].IndexOf(talk.title) != -1)
                            bgcolor = j.ToString();
                    }
                    DateTime dtalk = talk.timestamp;
                    talkdate = dtalk.ToString("yyyy-MM-dd HH:mm");
                    pagedate = dpage.ToString("yyyy-MM-dd HH:mm");
                    num[i, 0]++;
                    dt[i] = dt[i] + "{{User:IncubatorBot/ЗПП|td|bg=" + bgcolor + "|page=" + n.title + "|sp=" + t2 + "|talkpage=" + talk.title + "|u1=" + n.lastUser + "|t1=" + pagedate + "|u2=" + talk.lastUser + "|t2=" + talkdate + "}}\n";
                }
                else
                {
                    num[i, 1]++;// counter of pages without talkpages
                    dt[i] = dt[i] + "{{User:IncubatorBot/ЗПП|td|page=" + n.title + "|sp=" + t2 + "|talkpage=" + talk.title + "|u1=" + n.lastUser + "|t1=" + pagedate + "}}\n";
                }
            }
            pl.Clear();
        }
        string final = "";
        for (int j = 0; j < count; j++)
        {
            final = final + dt[j] + "{{User:IncubatorBot/ЗПП|end}}";
        }
        p.Load();
        if (p.text.IndexOf(final) == -1)
        {
            p.text = "{{Проект:Инкубатор/Запросы помощи и проверки/Doc|~~~~}}" + final;
            string comment = "";
            for (int j = 0; j < set_parser.Matches("Пров|Пом").Count; j++)
                comment = comment + shtitles[j] + "/";
            comment = comment.Remove(comment.Length - 1) + " = ";
            for (int j = 0; j < set_parser.Matches("Пров|Пом").Count; j++)
                comment = comment + num[j, 0] + ":" + num[j, 1] + " / ";
            comment = comment.Remove(comment.Length - 3);
            p.Save(comment, true);
        }
    }
    static void img_inc_bot()
    {
        Site commons = new Site("https://commons.wikimedia.org", creds[0], creds[1]);
        var cats = "Проект:Инкубатор:Запросы на проверку|Проект:Инкубатор:Запросы о помощи".Split('|');
        Page p = new Page(site, "Проект:Инкубатор/Изображения");
        p.Load();
        PageList pl = new PageList(site);
        PageList pm = new PageList(site);
        PageList ph = new PageList(site);
        pl.FillFromCategory("Проект:Инкубатор:Все статьи");
        pm.FillFromCategory("Проект:Инкубатор:Статьи на мини-рецензировании");
        for (int i = 0; i < cats.Length; i++)
            ph.FillFromCategory(cats[i]);
        string[,] imgs = new string[5000, 10];
        int m;
        m = 0;
        foreach (Page n in pl)
        {
            n.Load();
            string nst = "";
            if (pm.Contains(n) == true)
            { nst = "1"; }
            else if (ph.Contains(n) == true)
            { nst = "2"; }
            else { nst = "0"; }
            List<string> str = n.GetImages();
            string im = "";
            int i;
            if (str.Count > 0)
            {
                for (i = 0; i < str.Count; i++)
                {
                    if (str[i].Contains("Файл:[[Файл:") == true)
                        str[i] = str[i].Replace("Файл:[[Файл:", "Файл:");
                    if (str[i] != "Файл:Example.jpg" & str[i] != "Файл:Person.jpg" & str[i] != "Файл:")
                        im = im + str[i] + "|";
                }
                if (im.Length > 1)
                {
                    im = im.Remove(im.Length - 1);
                    if (string.IsNullOrEmpty(im)) // API запрос
                        throw new WikiBotException(Bot.Msg("No title specified for page to load."));
                    try
                    {
                        var reader = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&prop=imageinfo&iiprop=timestamp|user|size|dimensions&titles=" + HttpUtility.UrlEncode(im) + "&format=xml")));
                        while (reader.Read())
                            if (reader.NodeType == XmlNodeType.Element)
                            {
                                if (reader.Name == "page")
                                {
                                    m++;
                                    imgs[m, 0] = reader.GetAttribute("title");
                                    imgs[m, 1] = reader.GetAttribute("imagerepository");
                                    imgs[m, 7] = n.title;
                                    imgs[m, 8] = nst;
                                    if (imgs[m, 1] == "" ^ imgs[m, 1] == null)
                                        m--;
                                }
                                if (reader.Name == "ii")
                                {
                                    imgs[m, 2] = reader.GetAttribute("timestamp");
                                    imgs[m, 3] = reader.GetAttribute("user");
                                    imgs[m, 4] = reader.GetAttribute("width");
                                    imgs[m, 5] = reader.GetAttribute("height");
                                }
                            }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }
        for (int n = 1; n < m + 1; n++)
        {
            bool exist = true;
            string ptext = "";
            if (imgs[n, 1] == "local")
            { Page temp = new Page(site, imgs[n, 0]); temp.Load(); ptext = temp.text; }
            else if (imgs[n, 1] == "shared")
            {
                string file = imgs[n, 0].Replace("Файл", "File");
                Page temp = new Page(commons, file); temp.Load(); ptext = temp.text;
            }
            else { exist = false; }
            if (exist == true)
            {
                imgs[n, 6] = "";
                Regex CC = new Regex("{{.*?CC.*?}}", RegexOptions.IgnoreCase);
                Regex GFDL = new Regex("{{.*?(GFDL|LGPL|GPL).*?}}", RegexOptions.IgnoreCase);
                Regex PD = new Regex("{{(Not-PD|PD).*?}}", RegexOptions.IgnoreCase);
                Regex FU = new Regex("{{(Несвободный файл|FU|Fairuse|Символ|Скриншот).*?}}", RegexOptions.Singleline);
                Regex FoP = new Regex("{{FoP.*?}}", RegexOptions.IgnoreCase);
                Regex VRT = new Regex("{{.*?(OTRS|VRT).*?}}", RegexOptions.IgnoreCase);
                Regex Attribution = new Regex("{{Attribution.*?}}", RegexOptions.IgnoreCase);
                Regex no = new Regex("{{no .*?}}", RegexOptions.IgnoreCase);
                Regex other = new Regex("{{(VI.com-Gerbovnik|FAL|MTL|BSD|Trivial|Свободный скриншот|Kremlin).*?}}", RegexOptions.IgnoreCase);
                Regex comm = new Regex("{{(Apache|ADRM|AGPL|APL|Artistic|BArch|Beerware|C0|CDDL|CPL|Careware|Copyright|DSL|EPL|Expat|FOLP|FWL|MIT|MPL|MTL|OAL|Open|WTFPL|X11|Zlib).*?}}", RegexOptions.IgnoreCase);
                imgs[n, 9] = "0";
                if (!VRT.IsMatch(ptext)) // if OTRS - somebody has already check file, so we don't need to check it again
                {
                    if (!no.IsMatch(ptext, 0)) // the same, if here is template {{no permission}} (npd, nad, nld etc), file was checked before
                    {
                        imgs[n, 9] = "1";
                        if (CC.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + CC.Matches(ptext)[0].ToString();
                        if (GFDL.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + GFDL.Matches(ptext)[0].ToString();
                        if (PD.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + PD.Matches(ptext)[0].ToString();
                        if (FU.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + FU.Matches(ptext)[0].ToString();
                        if (FoP.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + FoP.Matches(ptext)[0].ToString();
                        if (Attribution.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + Attribution.Matches(ptext)[0].ToString();
                        if (other.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + other.Matches(ptext)[0].ToString();
                        if (comm.IsMatch(ptext))
                            imgs[n, 6] = imgs[n, 6] + comm.Matches(ptext)[0].ToString();
                    }
                }
            }
        }
        p.text = "{{Проект:Инкубатор/Шаблон навигации}}\n<div align=\"right\">'''Последнее обновление:''' ~~~~~ </div> \n\n{| class=\"wikitable sortable\"\n|-\n! Файл\n! Дата\n! Автор\n! Место\n! Размеры\n! Лицензия\n! Статья\n! Статус\n";
        DateTime now = DateTime.UtcNow;
        // дата и время последней правки
        int term = 120;
        for (int n = 1; n < m + 1; n++)
        {
            DateTime datefile = DateTime.Parse(imgs[n, 2]);
            TimeSpan diff = now - datefile;
            if (imgs[n, 9] == "1")
            {
                if (diff.Days < term)
                {
                    if (imgs[n, 0].IndexOf("=") != -1) { imgs[n, 0] = imgs[n, 0].Replace("=", "%3D"); } // поправить - не работает
                    p.text = p.text + "{{User:IncubatorBot/img|" + imgs[n, 0] + "|" + imgs[n, 2] + "|" + imgs[n, 3] + "|" + imgs[n, 1] + "|" + imgs[n, 4] + "x" + imgs[n, 5] + "|<nowiki>" + imgs[n, 6] + "</nowiki>|" + imgs[n, 7] + "|" + imgs[n, 8] + "}}\n";
                }
            }
        }
        p.text = p.text + "|}";
        p.Save("обновление списка", true);
    }
    static void main_inc_bot()
    {
        Page p = new Page(site, "Википедия:Проект:Инкубатор/Статьи");
        PageList pl = new PageList(site);
        MyBot bot = new MyBot();
        string[] pages = new string[5000];
        int q = 0;
        XmlTextReader rdr = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&list=allpages&apprefix=Проект:Инкубатор/Статьи&apnamespace=4&apfilterredir=nonredirects&aplimit=max&format=xml")));
        while (rdr.Read())
            if (rdr.NodeType == XmlNodeType.Element)
                if (rdr.Name == "p")
                {
                    string abc = rdr.GetAttribute("title");
                    if (abc != "Википедия:Проект:Инкубатор/Статьи Инкубатора" && abc != "Википедия:Проект:Инкубатор/Статьи/")
                    {
                        pages[q] = abc;
                        q++;
                    }
                }
        rdr = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&list=allpages&apnamespace=102&apfilterredir=nonredirects&aplimit=max&format=xml")));
        while (rdr.Read())
            if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "p")
            {
                pages[q] = rdr.GetAttribute("title");
                q++;
            }
        var exceptions = "Инкубатор:Песочница|Инкубатор:Песочница/Пишите ниже|Инкубатор:Тест бота|Инкубатор:ПЕСОК|Инкубатор:ТЕСТ".Split('|');
        for (int z = 0; z < q; z++)
        {
            string tttt = "";
            bool except = false;  // start of module for exception some pages
            tttt = pages[z];
            for (int ik = 0; ik < exceptions.Length; ik++)
                if (tttt == exceptions[ik])
                    except = true;
            if (!except) // end of exceptions
            {
                Page n = new Page(site, pages[z]);
                bool e_inc, e_cat;
                e_inc = e_cat = false;
                string com = "";
                n.Load();
                string dbt = "";
                string red = "";
                if (n.text.IndexOf("nobots") == -1)
                {
                    if (n.text.IndexOf("Инкубатор, Статья перенесена в ОП") == -1)
                    {
                        Regex r = new Regex(Regex.Escape("#") + "(REDIRECT|перенаправление) " + Regex.Escape("[[") + ".*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                        Regex db = new Regex(Regex.Escape("{{") + "db-.*?" + Regex.Escape("}}"), RegexOptions.Singleline);
                        for (int qw = 0; qw < r.Matches(n.text).Count; qw++)
                            red = r.Matches(n.text)[qw].ToString();
                        for (int qw = 0; qw < db.Matches(n.text).Count; qw++)
                            dbt = db.Matches(n.text)[qw].ToString();
                        string ttt = n.text;
                        while (ttt.IndexOf("\n") != -1)
                            ttt = ttt.Replace("\n", "");
                        if (n.text.Length - red.Length - dbt.Length > 2 && n.text.IndexOf("{{В инкубаторе") == -1 && n.text.IndexOf("{{в инкубаторе") == -1)
                        {
                            n.text = "{{В инкубаторе}}\n" + n.text;
                            e_inc = true;
                        }
                        else if (n.text.Length == 0)
                        {
                            n.text = "{{В инкубаторе}}\n" + n.text;
                            e_inc = true;
                        }
                    }
                    string temp = n.text;
                    Regex comment = new Regex(Regex.Escape("<!--") + ".*?" + Regex.Escape("-->"), RegexOptions.Singleline);
                    foreach (Match m in comment.Matches(temp))
                    {
                        red = m.ToString();
                        while (temp.IndexOf(red) != -1)
                        {
                            temp = temp.Replace(red, "");
                        }
                    }
                    Regex cats = new Regex(Regex.Escape("[[") + "(Category|Категория).*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                    foreach (Match m in cats.Matches(temp))
                    {
                        string replacer = m.ToString().Replace("[[", "[[:");
                        n.text = n.text.Replace(m.ToString(), replacer);
                        e_cat = true;
                    }
                    Regex index = new Regex(Regex.Escape("__") + "(INDEX|ИНДЕКС)" + Regex.Escape("__"), RegexOptions.Singleline);
                    foreach (Match m in index.Matches(temp))
                    {
                        n.text = n.text.Replace(m.ToString(), "");
                        e_cat = true;
                    }
                    if (e_inc)
                    {
                        if (e_cat)
                            com = "добавлен {{В инкубаторе}}, [[User:IncubatorBot/Скрытие категорий и интервик|скрытие категорий]]";
                        else
                            com = "добавлен {{В инкубаторе}}";
                        n.Save(com, true);
                    }
                    else if (e_cat)
                    {
                        com = "[[User:IncubatorBot/Скрытие категорий и интервик|скрытие категорий]]";
                        n.Save(com, true);
                    }
                }
            }
        }
    }
    static void null_editor()
    {
        foreach (string cat in "Википедия:Статьи с неактуальным шаблоном Не переведено|Статьи со ссылками на отсутствующие файлы".Split('|'))
            foreach (Page n in new MyBot().GetCategoryMembersAll(site, cat))
            {
                n.Load();
                n.Save(n.text + '\n');
            }
    }
    static void remind_bot()
    {
        PageList all = new PageList(site);
        PageList exc = new PageList(site);
        MyBot bot = new MyBot();
        all.FillFromAllPages("", 102, true, 5000);
        var exceptions = "Инкубатор:Песочница|Инкубатор:Песочница/Пишите ниже|Инкубатор:Тест бота|Инкубатор:ПЕСОК|Инкубатор:ТЕСТ".Split('|');
        var candidats = bot.GetCategoryMembers102(site, "Проект:Инкубатор:Кандидаты на мини-рецензирование");
        var forgotten = bot.GetCategoryMembers102(site, "Проект:Инкубатор:Брошенные статьи");
        var reviewing = bot.GetCategoryMembers102(site, "Проект:Инкубатор:Статьи на мини-рецензировании");
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
    static void mini_recenz()
    {
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
        cand_list = bot.GetCategoryMembers102(site, "Проект:Инкубатор:Статьи на мини-рецензировании");// предварительный пробег на предмет номинации к удалению старейших стабов
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
        for (int row1 = 0; row1 < forKU.GetLength(0); row1++)
            for (int row2 = row1 + 1; row2 < forKU.GetLength(0); row2++)
                if (Convert.ToInt32(forKU[row1, 1]) < Convert.ToInt32(forKU[row2, 1]))
                    for (int i = 0; i < forKU.GetLength(1); i++)
                        (forKU[row2, i], forKU[row1, i]) = (forKU[row1, i], forKU[row2, i]);
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
        cand_list = bot.GetCategoryMembers102(site, "Проект:Инкубатор:Статьи на мини-рецензировании");
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
    }/// напоминаем о забытой статье
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
    }/// выгоняем из Инкубатора
    public void mrec(Site site, string[,] pages)
    {
        MyBot bot = new MyBot();
        var forgotten = bot.GetCategoryMembers102(site, "Проект:Инкубатор:Брошенные статьи");
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
    }/// Мини-рецензирование
    public PageList GetCategoryMembers102(Site site, string cat)
    {
        PageList allpages = new PageList(site);
        XmlTextReader rdr = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmnamespace=102&cmlimit=max&cmtitle=К:" + HttpUtility.UrlEncode(cat) + "&format=xml")));
        while (rdr.Read())
            if (rdr.NodeType == XmlNodeType.Element)
                if (rdr.Name == "cm")
                    allpages.Add(new Page(site, rdr.GetAttribute("title")));
        return allpages;
    }
    public PageList GetCategoryMembersAll(Site site, string cat)
    {
        PageList allpages = new PageList(site);
        XmlTextReader rdr = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmlimit=max&cmtitle=К:" + HttpUtility.UrlEncode(cat) + "&format=xml")));
        while (rdr.Read())
            if (rdr.NodeType == XmlNodeType.Element)
                if (rdr.Name == "cm")
                    allpages.Add(new Page(site, rdr.GetAttribute("title")));
        return allpages;
    }
    public static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        inc_check_bot();
        img_inc_bot();
        main_inc_bot();
        stat_bot();
        null_editor();
        remind_bot();
        mini_recenz();
    }
}
