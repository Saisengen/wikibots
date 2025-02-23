using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Collections.Generic;
using DotNetWikiBot;

class MyBot : Bot
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    public static void Main()
    {
        Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
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
}
