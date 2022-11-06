using System;
using System.Collections.Generic;
using System.IO;
using DotNetWikiBot;
using System.Text.RegularExpressions;
using System.Net;
using System.Xml;
class Program
{
    static void Main()
    {
        var monthname = new string[13];
        monthname[1] = "января"; monthname[2] = "февраля"; monthname[3] = "марта"; monthname[4] = "апреля"; monthname[5] = "мая"; monthname[6] = "июня"; monthname[7] = "июля"; monthname[8] = "августа"; monthname[9] = "сентября"; monthname[10] = "октября"; monthname[11] = "ноября"; monthname[12] = "декабря";
        var validfiles = new HashSet<string>();
        var nonfree = new HashSet<string>();
        var nonvalid = new HashSet<string>();
        var pageswithfile = new HashSet<string>();
        var cl = new WebClient();
        string cont, fucont = "", gcmcont = "", apiout, query = "/w/api.php?action=query&format=xml&prop=fileusage&list=&continue=gcmcontinue%7C%7C&generator=categorymembers&fulimit=5000&gcmtitle=Категория:Файлы:Несвободные&gcmnamespace=6&gcmlimit=max";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);

        do
        {
            apiout = site.GetWebPage(query + (fucont == "" ? "" : "&fucontinue=" + Uri.EscapeDataString(fucont)) + (gcmcont == "" ? "" : "&gcmcontinue=" + Uri.EscapeDataString(gcmcont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); fucont = r.GetAttribute("fucontinue"); gcmcont = r.GetAttribute("gcmcontinue");
                if (fucont == null) fucont = "";
                if (gcmcont == null) gcmcont = "";

                string filename = "";
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page")
                        filename = r.GetAttribute("title");
                    if (r.Name == "fu" && (r.GetAttribute("ns") == "0" || r.GetAttribute("ns") == "102") && !validfiles.Contains(filename))
                        validfiles.Add(filename);
                }
            }
        } while (fucont != "" || gcmcont != "");

        cont = ""; query = "/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Категория:Файлы:Несвободные&cmprop=title&cmnamespace=6&cmlimit=max";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&cmcontinue=" + Uri.EscapeDataString(cont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("cmcontinue");
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                        nonfree.Add(r.GetAttribute("title"));
            }
        }

        query = "/w/api.php?action=query&list=embeddedin&format=xml&eititle=t%3AOrphaned-fairuse&einamespace=6&eilimit=max";
        apiout = site.GetWebPage(query);
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                    validfiles.Add(r.GetAttribute("title"));
        }

        nonfree.ExceptWith(validfiles);
        var pagerx = new Regex(@"\|\s*статья\s*=\s*([^|\n]*)\s*\|");
        var redirrx = new Regex(@"#(redirect|перенаправление)\s*\[\[([^\]]*)\]\]", RegexOptions.IgnoreCase);
        foreach (var n in nonfree)
        {
            try
            {
                var p = new Page(n);
                p.Load();
                var x = pagerx.Matches(p.text);
                foreach (Match xx in x)
                    pageswithfile.Add(xx.Groups[1].Value);
                foreach (var fp in pageswithfile)
                {
                    var p2 = new Page(fp);
                    if (p2.Exists())
                    {
                        p2.Load();
                        if (!redirrx.IsMatch(p2.text))
                            p2.Save(p2.text + "\n");
                        else
                        {
                            var p3 = new Page(redirrx.Match(p2.text).Groups[1].Value);
                            if (p3.Exists())
                            {
                                p3.Load();
                                p3.Save(p3.text + "\n");
                            }
                        }
                    }
                }
                pageswithfile.Clear();
            }
            catch
            {
                pageswithfile.Clear();
            }
        }
        foreach (var n in nonfree)
            cl.DownloadString("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(n) + "?action=purge");
        foreach (var n in nonfree)
        {
            var reqstr = "/w/api.php?action=query&format=xml&prop=fileusage&titles=" + Uri.EscapeDataString(n);
            apiout = site.GetWebPage(reqstr);
            if (!apiout.Contains("<fileusage>"))
                nonvalid.Add(n);
        }

        foreach (var n in nonvalid)
        {
            var p = new Page(n);
            try
            {
                p.Load();
                if (p.timestamp.AddHours(1) < DateTime.Now)
                    p.Save("{{subst:ofud}}\n" + p.text, "вынос на КБУ неиспользуемого в статьях несвободного файла", false);
            }
            catch { }
        }

        var dt = DateTime.Now;
        if (nonvalid.Count != 0)
        {
            var p = new Page("К:Файлы:Неиспользуемые несвободные от " + dt.Day + " " + monthname[dt.Month] + " " + dt.Year);
            p.Save("__NOGALLERY__\n[[К:Файлы:Неиспользуемые несвободные|" + dt.ToString("MM-dd") + "]]", "", false);
        }

        //int num_of_files_to_notify = 20;
        //if (nonvalid.Count > num_of_files_to_notify)
        //{
        //    var p = new Page("ut:Sealle");
        //    p.Load();
        //    p.Save(p.text + "\n\n==Уведомление о файлах-сиротах от " + dt.Day + " " + month + "==\nВ [[:К:Файлы:Неиспользуемые несвободные от " + dt.Day + " " + month + " " + dt.Year +
        //        "|сегодняшней категории]] более " + num_of_files_to_notify + " файлов. ~~~~", "много кбушных файлов", false);
        //}
    }
}
