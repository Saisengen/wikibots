using System.Xml;
using System.IO;
using DotNetWikiBot;
using System.Text.RegularExpressions;
using System;

class Program
{
    static void Main()
    {
        var rgx = new Regex(@"\{\{\s*([Тт]екущие события|[Рр]едактирую|[Сс]вязь с текущим событием)[^{}]*\}\}");
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        foreach (string cat in new string[] { "Категория:Википедия:Статьи с просроченным шаблоном текущих событий", "Категория:Википедия:Просроченные статьи, редактируемые прямо сейчас" })
        {
            using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&list=categorymembers&format=xml&cmtitle=" + cat + "&cmlimit=max"))))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "cm")
                    {
                        var p = new Page(r.GetAttribute("title"));
                        p.Load();
                        string text = rgx.Replace(p.text, "");
                        p.Save(text, "удалены просроченные шаблоны", true);
                    }
            }
        }
    }
}
