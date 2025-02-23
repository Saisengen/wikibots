using System;
using System.IO;
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
        string URL = site.apiPath + "?action=query&list=categorymembers&cmprop=title&cmlimit=5000&cmtitle=" + HttpUtility.UrlEncode("К:" + cat) + "&format=xml";
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
        PageList pl = new PageList(site);
        foreach (string cat in "Википедия:Статьи со ссылками на элементы Викиданных без русской подписи|Проект:Инкубатор:Запросы на проверку|Проект:Инкубатор:Запросы о помощи|Проект:Инкубатор:Кандидаты на мини-рецензирование|Проект:Инкубатор:Брошенные статьи|Файлы:Несвободные для несуществующих статей|Википедия:К отсроченному удалению|Файлы:Неясный лицензионный статус:Все|Файлы:Неиспользуемые свободные:Все|Файлы:Неиспользуемые несвободные:Все|Википедия:Кандидаты на удаление|Википедия:Статьи с неактуальным шаблоном Не переведено|Статьи со ссылками на отсутствующие файлы".Split('|'))
        {
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
            pl.Clear();
        }
    }
}
