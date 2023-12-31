using System;
using System.IO;
using System.Web;
using System.Xml;
using DotNetWikiBot;

class MyBot : Bot
{
    public static void Main()
    {
        DateTime utcNow = DateTime.UtcNow;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        Site site = new Site("https://ru.wikipedia.org", creds[8], creds[9]);
        Page pn = new Page(site, "Участник:Dibot/TalkStat/" + utcNow.ToString("yyyyMM"));
        Page inc = new Page(site, "Участник:Dibot/IncubatorStat/" + utcNow.ToString("yyyyMM"));
        Page incmem = new Page(site, "Проект:Инкубатор/Участники");
        Page vos = new Page(site, "Википедия:К восстановлению");

        vos.Load();
        pn.Load();
        inc.Load();
        incmem.Load();
        if (!pn.Exists())
        {
            pn.text = "{{subst:User:Dibot/TalkStat/cap}}\n<noinclude>|}</noinclude>";
        }
        if (!inc.Exists())
        {
            inc.text = "{{subst:User:Dibot/IncubatorStat/cap}}\n<noinclude>|}</noinclude>";
        }
        string[] cats = {
                            "Википедия:Статьи для срочного улучшения",
                            "Статьи на улучшении более года",
                            "Статьи на улучшении более полугода",
                            "Статьи на улучшении более 90 дней",
                            "Статьи на улучшении более 30 дней",
                            "Статьи на улучшении менее 30 дней",
                            "Википедия:Незакрытые обсуждения статей для улучшения",
                            "Википедия:Кандидаты на удаление",
                            "Википедия:Незакрытые обсуждения удаления страниц",
                            "Википедия:Статьи для переименования",
                            "Википедия:Незакрытые обсуждения переименования страниц",
                            "Википедия:Кандидаты на объединение",
                            "Википедия:Незакрытые обсуждения объединения страниц",
                            "Википедия:Статьи для разделения",
                            "Википедия:Незакрытые обсуждения разделения страниц",
                            "Википедия:Незакрытые обсуждения восстановления страниц",
                            "Проект:Инкубатор:Все статьи",
                            "Проект:Инкубатор:Статьи на мини-рецензировании",
                            "Википедия:Стабы в Инкубаторе",
                            "Проект:Инкубатор:Запросы на проверку",
                            "Проект:Инкубатор:Запросы о помощи",
                            "Проект:Инкубатор:К удалению"
                        };
        string[] n = new string[cats.Length];
        for (int i = 0; i < cats.Length; i++)
        {
            string URL = site.apiPath + "?action=query&prop=categoryinfo&titles=" + HttpUtility.UrlEncode("Категория:" + cats[i]) + "&format=xml";
            string h = site.GetWebPage(URL);
            XmlTextReader rdr = new XmlTextReader(new StringReader(h));
            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Element)
                {
                    if (rdr.Name == "categoryinfo")
                    {
                        n[i] = rdr.GetAttribute("pages");
                    }
                }
            }
        }

        int vos_p = 0;
        string stat = "";
        PageList vos_page = new PageList(site);
        vos_page.FillFromPageLinks("Википедия:К восстановлению");
        foreach (Page p in vos_page)
        {
            if (vos.text.IndexOf("* [[" + p.title) != -1)
            {
                vos_p++;
            }
            if (vos.text.IndexOf("* " + p.title) != -1)
            {
                vos_p++;
            }
            if (vos.text.IndexOf("* [[:" + p.title) != -1)
            {
                vos_p++;
            }
        }
        int aa = 0;
        int bb = 0;
        int incmen = -3;
        string incstat = "";
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

        stat = "|- align=\"right\"\n| {{subst:#time: j.m.Y}} \n|" + n[0] + "||" + n[1] + "||" + n[2] +
            "||" + n[3] + "||" + n[4] + "||" + n[5] + "||" + n[6] + "\n|" + n[7] + "||" + n[8] + "\n|" +
            n[9] + "||" + n[10] + "\n|" + n[11] + "||" + n[12] + "\n|" + n[13] + "||" +
            n[14] + "\n|" + vos_p + "||" + n[15] + "\n";
        pn.text = pn.text.Insert(pn.text.LastIndexOf("<noinclude>|}</noinclude>"), stat);
        // pn.SaveToFile("TalkStat.txt");
        pn.Save("обновление статистики", true);

        incstat = "|- align=\"right\"\n| {{subst:#time: j.m.Y}} \n|" + n[16] + "||" + n[17] + "\n|" + n[18] +
            "\n|" + n[19] + "||" + n[20] + "||" + n[21] + "\n|" + incmen + "\n";
        inc.text = inc.text.Insert(inc.text.IndexOf("<noinclude>|}</noinclude>"), incstat);
        // inc.SaveToFile("IncStat.txt");
        inc.Save("обновление статистики", true);
    }
}
