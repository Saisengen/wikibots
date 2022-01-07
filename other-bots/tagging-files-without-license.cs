using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using DotNetWikiBot;

class Program
{
    static void Main()
    {
        var monthname = new string[13];
        monthname[1] = "января"; monthname[2] = "февраля"; monthname[3] = "марта"; monthname[4] = "апреля"; monthname[5] = "мая"; monthname[6] = "июня"; monthname[7] = "июля"; monthname[8] = "августа"; monthname[9] = "сентября"; monthname[10] = "октября"; monthname[11] = "ноября"; monthname[12] = "декабря";
        var autocatfiles = new HashSet<string>();
        var legalfiles = new HashSet<string>();
        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        string cont, query, apiout;
        apiout = site.GetWebPage("/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=Категория:Файлы:Без машиночитаемой лицензии&cmprop=title&cmlimit=50");
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                    autocatfiles.Add(r.GetAttribute("title"));
        }
        cont = ""; query = "/w/api.php?action=query&list=embeddedin&format=xml&eititle=t:No license&einamespace=6&eilimit=max";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&eicontinue=" + Uri.EscapeDataString(cont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("eicontinue");
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                        legalfiles.Add(r.GetAttribute("title"));
            }
        }

        autocatfiles.ExceptWith(legalfiles);
        foreach (var n in autocatfiles)
        {
            var p = new Page(n);
            p.Load();
            p.Save("{{subst:nld}}\n" + p.text, "вынос на КБУ файла без валидной лицензии", false);
        }

        var dt = DateTime.Now;
        var c = new Page("К:Файлы:Неясный лицензионный статус от " + dt.Day + " " + monthname[dt.Month] + " " + dt.Year);
        c.Save("[[К:Файлы:Неясный лицензионный статус|" + dt.ToString("MM-dd") + "]]", "", false);
    }
}
