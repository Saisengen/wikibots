using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using DotNetWikiBot;

class MyBot : Bot
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    public static void Main()
    {
        Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        Page p = new Page(site, "Википедия:Проект:Инкубатор/Статьи");
        PageList pl = new PageList(site);
        MyBot bot = new MyBot();
        string[] pages = new string[5000];
        int q = 0;
        XmlTextReader rdr = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&list=allpages&apprefix=Проект:Инкубатор/Статьи&apnamespace=4&apfilterredir=nonredirects&aplimit=max&format=xml")));
        while (rdr.Read())
        {
            if (rdr.NodeType == XmlNodeType.Element)
            {
                if (rdr.Name == "p")
                {
                    string abc = rdr.GetAttribute("title");
                    if (abc != "Википедия:Проект:Инкубатор/Статьи Инкубатора")
                    {
                        if (abc != "Википедия:Проект:Инкубатор/Статьи/")
                        {
                            pages[q] = abc;
                            q++;
                        }
                    }
                }
            }
        }
        rdr = new XmlTextReader(new StringReader(site.GetWebPage(site.apiPath + "?action=query&list=allpages&apnamespace=102&apfilterredir=nonredirects&aplimit=max&format=xml")));
        while (rdr.Read())
        {
            if (rdr.NodeType == XmlNodeType.Element)
            {
                if (rdr.Name == "p")
                {
                    pages[q] = rdr.GetAttribute("title");
                    q++;
                }
            }
        }
        var exceptions = "Инкубатор:Песочница|Инкубатор:Песочница/Пишите ниже|Инкубатор:Тест бота|Инкубатор:ПЕСОК|Инкубатор:ТЕСТ".Split('|');
        for (int z = 0; z < q; z++)
        {
            string tttt = "";
            bool except = false;  // start of module for exception some pages
            try
            {
                tttt = pages[z];
                for (int ik = 0; ik < exceptions.Length; ik++)
                {
                    if (tttt == exceptions[ik])
                        except = true;
                }
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
                            if (n.text.Length - red.Length - dbt.Length > 2)
                            {
                                if (n.text.IndexOf("{{В инкубаторе") == -1)
                                {
                                    if (n.text.IndexOf("{{в инкубаторе") == -1)
                                    {
                                        n.text = "{{В инкубаторе}}\n" + n.text;
                                        e_inc = true;
                                    }
                                }
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
                        if (e_inc == true)
                        {
                            if (e_cat == true)
                            {
                                com = "добавлен {{В инкубаторе}}, [[User:IncubatorBot/Скрытие категорий и интервик|скрытие категорий и/или интервик]]";
                            }
                            else
                            {
                                com = "добавлен {{В инкубаторе}}";
                            }
                            try
                            {
                                n.Save(com, true);
                            }
                            catch { Console.WriteLine(n.title + " - can't save;\n"); }
                        }
                        else if (e_cat == true)
                        {
                            com = "[[User:IncubatorBot/Скрытие категорий и интервик|скрытие категорий и/или интервик]]";
                            try
                            {
                                n.Save(com, true);
                            }
                            catch { Console.WriteLine(n.title + " - can't save;\n"); }
                        }
                    }
                }
            }
            catch
            {
                Console.WriteLine(tttt + " - some strange error;\n");
            }
        }
    }
}
