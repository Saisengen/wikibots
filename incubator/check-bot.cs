using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using DotNetWikiBot;

class MyBot : Bot
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    public PageList GetCategoryMembers(Site site, string cat)
    {
        PageList allpages = new PageList(site);
        var all = new HashSet<string>();
        string apiout = site.GetWebPage(site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmnamespace=102&cmlimit=max&cmtitle=К:" + HttpUtility.UrlEncode(cat) + "&format=xml");
        XmlTextReader rdr = new XmlTextReader(new StringReader(apiout));
        while (rdr.Read())
            if (rdr.NodeType == XmlNodeType.Element)
                if (rdr.Name == "cm")
                    all.Add(rdr.GetAttribute("title"));
        foreach (string m in all)
            {
                Page n = new Page(site, m);
                allpages.Add(n);
            }
        return allpages;
    }
    public static void Main()
    {
        Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
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
            nblist = bot.GetCategoryMembers(site, nbcats[j]);
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
}
