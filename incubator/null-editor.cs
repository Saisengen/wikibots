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
        Page setting = new Page(site, "Участник:" + creds[8] + "/settings.js");
        setting.Load();
        Regex all = new Regex(@"all.?=.?true", RegexOptions.Singleline);
        Regex onoff = new Regex(@"nullbot.?=.?true", RegexOptions.Singleline);
        Regex nullcats_am = new Regex(@"nullcats_am.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex nullcats_pm = new Regex(@"nullcats_pm.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex nullpages = new Regex(@"null_pages.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex nulllinksto = new Regex(@"null_links_to.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);

        if (all.Matches(setting.text).Count > 0)
        {
            if (onoff.Matches(setting.text).Count > 0)
            {
                ar[0] = "1";
                if (nullcats_am.Matches(setting.text).Count > 0)
                {
                    string a = nullcats_am.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[1] = a;
                }
                if (nullcats_pm.Matches(setting.text).Count > 0)
                {
                    string a = nullcats_pm.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[2] = a;
                }
                if (nullpages.Matches(setting.text).Count > 0)
                {
                    string a = nullpages.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[3] = a;
                }
                if (nulllinksto.Matches(setting.text).Count > 0)
                {
                    string a = nulllinksto.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Substring(a.IndexOf("\"") + 1);
                    a = a.Remove(a.IndexOf("\""));
                    ar[4] = a;
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
    public PageList GetCategoryMembers(Site site, string cat, int limit)
    {
        PageList allpages = new PageList(site);
        string[] all = new string[limit];
        int page_num = 0;
        string URL = site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmlimit=5000&cmtitle=" + HttpUtility.UrlEncode("Категория:" + cat) + "&format=xml";
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
        // счетчик запросов
        MyBot bot = new MyBot();
        string[] set = new string[6];
        set = bot.Settings(6, site);
        if (set[0] == "1")
        {
            Regex nullcat = new Regex(@"..*?" + Regex.Escape("|"), RegexOptions.Singleline);
            // объявляем список страниц
            PageList pl = new PageList(site);
            DateTime now = DateTime.UtcNow;
            // заполняем набор категорий по умолчанию
            string cat_set = set[1];
            // if (now.Hour > 12) // выбираем набор категорий по времени
            if (now.Hour < 6)
            { cat_set = set[1]; }
            else
            { cat_set = set[2]; }
            // теперь циклом обрабатываем набор категорий...
            foreach (Match m in nullcat.Matches(cat_set))
            {
                string cat = m.ToString();
                cat = cat.Remove(cat.Length - 1);
                // заполняем список страниц из категории под номером i
                // pl.FillFromCategory(cat);
                pl = bot.GetCategoryMembers(site, cat, 5000);
                foreach (Page n in pl)
                {
                    try
                    {
                        n.Load();
                        n.Save();
                    }
                    catch
                    {
                        Console.WriteLine(n.title + " can't save or load;\n");
                    }
                }
                // очищаем список страниц перед заполнением из следующей категории и начинаем цикл заново
                pl.Clear();
            }
            // в разовых проходах
            if (now.Hour > 6)
            {
                // обрабатываем страницы...
                string pages = set[3];
                foreach (Match m in nullcat.Matches(pages))
                {
                    string page = m.ToString();
                    page = page.Remove(page.Length - 1);
                    Page p = new Page(site, page);
                    p.Load();
                    try
                    {
                        p.Save();
                    }
                    catch
                    {
                        Console.WriteLine(p.title + " can't save;\n");
                    }
                }
                // обрабатываем ccsстраницы...
                string linksto = set[4];
                foreach (Match mm in nullcat.Matches(linksto))
                {
                    string links = mm.ToString();
                    links = links.Remove(links.Length - 1);
                    // заполняем список страниц из категории под номером i
                    pl.FillFromLinksToPage(links);
                    foreach (Page n in pl)
                    {
                        n.Load();
                        try
                        {
                            n.Save();
                        }
                        catch
                        {
                            Console.WriteLine(n.title + " can't save;\n");
                        }
                    }
                    // очищаем список страниц перед заполнением из следующей категории и начинаем цикл заново
                    pl.Clear();
                }
            }
        }
    }
}
