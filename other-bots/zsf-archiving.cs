using System.IO;
using DotNetWikiBot;
using System.Text.RegularExpressions;
using System;

class Program
{
    static void Main()
    {
        var year = DateTime.Now.Year;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var page = new Page("Википедия:Заявки на снятие флагов");
        page.Load();
        string zsftext = page.text;
        var threadrgx = new Regex(@"\n\n==[^\n]*: флаг [^=]*==[^⇧]*===\s*Итог[^=]*===([^⇧]*)\((апат|пат|откат|загр|ПИ|ПФ|ПбП|инж|АИ|бот)\)\s*—\s*{{(за|против)([^⇧]*)⇧-->", RegexOptions.Singleline);
        var signature = new Regex(@"(\d\d:\d\d, \d{1,2} \w+ \d{4}) \(UTC\)");
        var threads = threadrgx.Matches(page.text);
        foreach (Match thread in threads)
        {
            string archivepage = "";
            string threadtext = thread.Groups[0].Value;
            var summary = signature.Matches(thread.Groups[1].Value);
            var summary_discuss = signature.Matches(thread.Groups[4].Value);
            bool outdated = true;
            foreach (Match s in summary)
                if (DateTime.Now - DateTime.Parse(s.Groups[1].Value, System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) < new TimeSpan(2, 0, 0, 0))
                    outdated = false;
            foreach (Match s in summary_discuss)
                if (DateTime.Now - DateTime.Parse(s.Groups[1].Value, System.Globalization.CultureInfo.GetCultureInfo("ru-RU")) < new TimeSpan(2, 0, 0, 0))
                    outdated = false;
            if (!outdated)
                continue;
            switch (thread.Groups[2].Value)
            {
                case "апат":
                case "пат":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Патрулирующие/" + year;
                    break;
                case "откат":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Откатывающие/" + year;
                    break;
                case "загр":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Загружающие";
                    break;
                case "ПИ":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Подводящие итоги/" + year;
                    break;
                case "ПбП":
                case "ПФ":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Переименовывающие";
                    break;
                case "инж":
                case "АИ":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Инженеры и АИ";
                    break;
                case "бот":
                    archivepage = "Википедия:Заявки на снятие флагов/Архив/Боты";
                    break;
                default:
                    continue;
            }
            zsftext = zsftext.Replace(threadtext, "");
            var arch = new Page(archivepage);
            arch.Load();
            arch.Save(arch.text + threadtext, "", false);
        }
        if (zsftext != page.text)
            page.Save(zsftext, "архивация", false);
    }
}
