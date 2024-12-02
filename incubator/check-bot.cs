using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using DotNetWikiBot;

class MyBot : Bot
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    public string[] Settings(byte num, Site site)
    {
        string[] ar = new string[num];
        Page setting = new Page(site, "user:MBH/incubator.js");
        setting.Load();
        Regex all = new Regex(@"all.?=.?true", RegexOptions.Singleline);
        Regex on = new Regex(@"checkbot.?=.?true", RegexOptions.Singleline);
        Regex check_page = new Regex(@"check_page.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex cats = new Regex(@"check_cats.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex nbcats = new Regex(@"check_nb_cats.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex nbcolors = new Regex(@"check_nb_colors.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex titles = new Regex(@"check_titles.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex short_titles = new Regex(@"check_short_titles.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex def_color = new Regex(@"check_def_color.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        if (all.Matches(setting.text).Count > 0) // if all bots are allowed
        {
            if (on.Matches(setting.text).Count > 0) // if this bot is allowed
            {
                ar[0] = "1";
                if (cats.Matches(setting.text).Count > 0) // array of cats to work
                {
                    string a = cats.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[1] = a;
                }
                if (nbcats.Matches(setting.text).Count > 0) // array of cats for marking notability
                {
                    string a = nbcats.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[2] = a;
                }
                if (nbcolors.Matches(setting.text).Count > 0) // array of colors for marking notability
                {
                    string a = nbcolors.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[3] = a;
                }
                if (titles.Matches(setting.text).Count > 0) // array of titles for each section
                {
                    string a = titles.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[4] = a;
                }
                if (short_titles.Matches(setting.text).Count > 0) // array of short titles for edit comment
                {
                    string a = short_titles.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[5] = a;
                }
                if (def_color.Matches(setting.text).Count > 0) // array of short titles for edit comment
                {
                    string a = def_color.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[6] = a;
                }
                if (check_page.Matches(setting.text).Count > 0) // array of cats to work
                {
                    string a = check_page.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[7] = a;
                }
                return ar;
            }
            else
            { ar[0] = "0"; return ar; }
        }
        else
        { ar[0] = "0"; return ar; }
    }
    public PageList GetCategoryMembers(Site site, string cat, int limit)
    {
        PageList allpages = new PageList(site);
        string[] all = new string[limit];
        int page_num = 0;
        string URL = site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmnamespace=102&cmlimit=max&cmtitle=" + HttpUtility.UrlEncode("К:" + cat) + "&format=xml";
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
        Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        MyBot bot = new MyBot();
        string[] set = bot.Settings(8, site);
        if (set[0] == "1")
        {
            Page p = new Page(site, set[7]);
            int k = 0;
            string talkdate, pagedate;
            Regex set_parser = new Regex(@".*?" + Regex.Escape("|"), RegexOptions.Singleline);
            int count = set_parser.Matches(set[1]).Count;
            string[] cats = new string[count];
            string[] dt = new string[count];
            foreach (Match m in set_parser.Matches(set[1])) // cats to work on
            {
                cats[k] = m.ToString();
                cats[k] = cats[k].Remove(cats[k].Length - 1);
                k++;
            }
            k = 0;
            string[] nbcats = new string[set_parser.Matches(set[2]).Count];
            foreach (Match m in set_parser.Matches(set[2])) // cats for notability
            {
                nbcats[k] = m.ToString();
                nbcats[k] = nbcats[k].Remove(nbcats[k].Length - 1);
                k++;
            }
            k = 0;
            string[] nbcolors = new string[set_parser.Matches(set[3]).Count];
            foreach (Match m in set_parser.Matches(set[3])) // colors for notability
            {
                nbcolors[k] = m.ToString();
                nbcolors[k] = nbcolors[k].Remove(nbcolors[k].Length - 1);
                k++;
            }
            k = 0;
            /* string[] titles = new string[set_parser.Matches(set[4]).Count];
             foreach (Match m in set_parser.Matches(set[4])) // colors for notability
             {
                 titles[k] = m.ToString();
                 titles[k] = titles[k].Remove(titles[k].Length - 1);
                 k++;
             }
             k = 0;*/
            string[] shtitles = new string[set_parser.Matches(set[5]).Count];
            foreach (Match m in set_parser.Matches(set[5])) // colors for notability
            {
                shtitles[k] = m.ToString();
                shtitles[k] = shtitles[k].Remove(shtitles[k].Length - 1);
                k++;
            }
            // consists of wiki-text
            for (int j = 0; j < count; j++)
            {
                /* if (j == 0)
                    dt[j] = "{| width=\"100%\" \n! align=\"center\"|[[:Категория:" + cats[j] + "|" + titles[j] + "]]\n|-\n|\n{| class=\"standard sortable\" width=\"100%\"\n|-\n! width=\"40%\" | Название\n! Статья, посл. автор\n! Статья, дата\n! Обсуждение, посл. автор\n! Обсуждение, дата\n";
                else */
                // dt[j] = "! align=\"center\"|[[:Категория:" + cats[j] + "|" + titles[j] + "]]\n|-\n|\n{| class=\"standard sortable\" width=\"100%\"\n|-\n! width=\"40%\" | Название\n! Статья, посл. автор\n! Статья, дата\n! Обсуждение, посл. автор\n! Обсуждение, дата\n";
                dt[j] = "\n{{User:Dibot/ЗПП|start|" + (j + 1).ToString() + "}}\n{{User:Dibot/ЗПП|th}}\n";
            }
            // counter of requests
            int[,] num = new int[count, 2];
            for (int j = 0; j < count; j++)
            {
                num[j, 0] = num[j, 1] = 0;
            }
            //string[] bg = new string[set_parser.Matches(set[3]).Count];
            /* for (int j = 0; j < set_parser.Matches(set[3]).Count; j++)
             {
                 bg[j] = "bgcolor=\"#" + nbcolors[j] + "\"";
             }
             string defcolor = "bgcolor=\"#" + set[6] + "\"";*/
            // additional lists to sort by notability
            string[] nb = new string[set_parser.Matches(set[2]).Count];
            PageList nblist;
            for (int j = 0; j < set_parser.Matches(set[2]).Count; j++)
            {
                nb[j] = "|";
                //nblist.FillFromCategory(nbcats[j]);
                nblist = bot.GetCategoryMembers(site, nbcats[j], 1000);
                foreach (Page pp in nblist)
                { nb[j] = nb[j] + pp.title + "|"; }
                nblist.Clear();
            }
            PageList pl = new PageList(site);
            // loop for treatment of cats[]-array
            for (int i = 0; i < count; i++)
            {
                // fill list from cat[i]
                pl.FillFromCategory(cats[i]);
                foreach (Page m in pl)
                {
                    m.Load();
                    int ns = m.GetNamespace();
                    // make a shortcut title fot output [[fullname|shortcut]]
                    string t1 = m.title;
                    string t2 = ""; ;
                    //string t3 = "";
                    string otitle = "";
                    /* if (n.title.IndexOf("Инкубатор/Статьи") != -1)
                     {
                         t2 = t1.Replace("Википедия:Проект:Инкубатор/Статьи/", "");
                         t2 = t2.Replace("Обсуждение Википедии:Проект:Инкубатор/Статьи/", "");
                         t3 = t1.Replace("Википедия", "Обсуждение Википедии");
                         otitle = t1.Replace("Википедия", "Обсуждение Википедии");
                     }
                     else if (n.title.IndexOf("Инкубатор:") != -1)
                     {
                         t2 = t1.Replace("Инкубатор:", "");
                         t3 = t1.Replace("Инкубатор:", "Обсуждение Инкубатора:");
                         otitle = t1.Replace("Инкубатор:", "Обсуждение Инкубатора:");
                     }
                     else
                     {
                         t2 = t1;
                     }*/
                    if (ns == 4 | ns == 5 | ns == 102 | ns == 103)
                    {
                        switch (ns)
                        {
                            case 4:
                                t2 = t1.Replace("Википедия:Проект:Инкубатор/Статьи/", "");
                                otitle = t1.Remove(0, 9);
                                otitle = "Обсуждение Википедии" + otitle;
                                break;
                            case 5:
                                t1 = t1.Replace("Обсуждение Википедии:Проект:Инкубатор/Статьи/", "Википедия:Проект:Инкубатор/Статьи/");
                                t2 = t1.Replace("Википедия:Проект:Инкубатор/Статьи/", "");
                                otitle = t1.Remove(0, 9);
                                otitle = "Обсуждение Википедии" + otitle;
                                break;
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
                            case 104:
                                t2 = t1.Replace("Проект:Инкубатор/Статьи/", "");
                                otitle = t1.Remove(0, 9);
                                otitle = "Обсуждение проекта" + otitle;
                                break;
                            case 105:
                                t1 = t1.Replace("Обсуждение проекта:Инкубатор/Статьи/", "Проект:Инкубатор/Статьи/");
                                t2 = t1.Replace("Проект:Инкубатор/Статьи/", "");
                                otitle = t1.Remove(0, 9);
                                otitle = "Обсуждение проекта" + otitle;
                                break;
                        }
                        Page n = new Page(site, t1);
                        Page talk = new Page(site, otitle);
                        try
                        {
                            n.LoadWithMetadata();
                        }
                        catch
                        {
                            try
                            {
                                n.LoadWithMetadata();
                            }
                            catch
                            {
                                n.Load();
                            }
                        }
                        try
                        {
                            talk.LoadWithMetadata();
                        }
                        catch
                        {
                            try
                            {
                                talk.LoadWithMetadata();
                            }
                            catch
                            {
                                talk.Load();
                            }
                        }
                        //DateTime dnow = DateTime.UtcNow;
                        DateTime dpage = n.timestamp;
                        pagedate = dpage.ToString("yyyy-MM-dd HH:mm");
                        string bgcolor = "";
                        // обрабатываем страницу обсуждения (если она есть) по тому же принципу
                        if (talk.Exists() == true)
                        {
                            for (int j = 0; j < set_parser.Matches(set[2]).Count; j++)
                            {
                                if (nb[j].IndexOf(talk.title) != -1)
                                {
                                    //bgcolor = "bgcolor=\"#" + bg[j] + "\"";
                                    bgcolor = j.ToString();
                                }
                            }
                            DateTime dtalk = talk.timestamp;
                            // TimeSpan ddiff = dnow - dtalk;
                            //Page pt = new Page(site, otitle);
                            talkdate = dtalk.ToString("yyyy-MM-dd HH:mm");
                            pagedate = dpage.ToString("yyyy-MM-dd HH:mm");
                            // preparing talkpage output user-data 
                            //talkuser = "{{u|" + talk.lastUser + "}} ([[Обсуждение участника:" + talk.lastUser + "|о]] • [[Служебная:Contributions/" + talk.lastUser + "|в]])";
                            // preparing page output user-data                         
                            //pageuser = "{{u|" + n.lastUser + "}} ([[Обсуждение участника:" + n.lastUser + "|о]] • [[Служебная:Contributions/" + n.lastUser + "|в]])";
                            // comparison
                            /*if (dpage > dtalk)
                            {
                                // counter of pages with talkpages
                                num[i, 0]++;
                                dt[i] = dt[i] + "|- style=\"font-size:9pt\" " + bgcolor + " \n| [[" + n.title + "|" + t2 + "]] ([[" + otitle + "|обс.]])\n|" + pageuser + "\n|\'\'\'" + pagedate + "\'\'\'\n|" + talkuser + "\n|" + talkdate + "\n";
                            }
                            else // if (ddiff.Days < 3)
                            {
                                num[i, 0]++;
                               dt[i] = dt[i] + "|- style=\"font-size:9pt\" " + bgcolor + " \n| [[" + n.title + "|" + t2 + "]] ([[" + otitle + "|обс.]])\n|" + pageuser + "\n|" + pagedate + "\n|" + talkuser + "\n|\'\'\'" + talkdate + "\'\'\'\n";
                            }*/
                            num[i, 0]++;
                            dt[i] = dt[i] + "{{User:Dibot/ЗПП|td|bg=" + bgcolor + "|page=" + n.title + "|sp=" + t2 + "|talkpage=" + talk.title + "|u1=" + n.lastUser + "|t1=" + pagedate + "|u2=" + talk.lastUser + "|t2=" + talkdate + "}}\n";
                        }
                        else
                        {
                            // counter of pages without talkpages
                            num[i, 1]++;
                            //pageuser = "{{u|" + n.lastUser + "}} ([[Обсуждение участника:" + n.lastUser + "|о]] • [[Служебная:Contributions/" + n.lastUser + "|в]])";
                            //dt[i] = dt[i] + "|- style=\"font-size:9pt\" " + defcolor + " \n| [[" + n.title + "|" + t2 + "]] ([[" + otitle + "|обс.]])\n|" + pageuser + "\n|\'\'\'" + pagedate + "\'\'\'\n| не создана \n| не создана \n";
                            dt[i] = dt[i] + "{{User:Dibot/ЗПП|td|page=" + n.title + "|sp=" + t2 + "|talkpage=" + talk.title + "|u1=" + n.lastUser + "|t1=" + pagedate + "}}\n";
                        }
                    }
                }
                // clear page list for filling from next cat[i]-category
                pl.Clear();
            }
            // collect all output datas to single text
            // compare if existing text is equal to output data
            string final = "";
            for (int j = 0; j < count; j++)
            {
                final = final + dt[j] + "{{User:Dibot/ЗПП|end}}";
            }
            //final = final + "|}";
            p.Load();
            if (p.text.IndexOf(final) == -1)
            {
                // p.text = ":<small>\'\'\'Последнее обновление:\'\'\' ~~~~</small>\n{{Википедия:Проект:Инкубатор/Запросы помощи и проверки/Doc}}\n" + final;
                p.text = "{{" + set[7] + "/Doc|~~~~}}" + final;
                // can save to local file
                // p.SaveToFile("check.txt");
                // edit comment with counter
                string com = "";
                for (int j = 0; j < set_parser.Matches(set[5]).Count; j++)
                {
                    com = com + shtitles[j] + "/";
                }
                com = com.Remove(com.Length - 1) + " = ";
                for (int j = 0; j < set_parser.Matches(set[5]).Count; j++)
                {
                    com = com + num[j, 0] + ":" + num[j, 1] + " / ";
                }
                com = com.Remove(com.Length - 3);
                p.Save(com, true);
            }
        }
    }
}
