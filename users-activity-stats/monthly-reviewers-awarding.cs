using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using DotNetWikiBot;

class Program
{
    static void Main()
    {
        var monthname = new string[13];
        monthname[1] = "январь"; monthname[2] = "февраль"; monthname[3] = "март"; monthname[4] = "апрель"; monthname[5] = "май"; monthname[6] = "июнь"; monthname[7] = "июль"; monthname[8] = "август"; monthname[9] = "сентябрь"; monthname[10] = "октябрь"; monthname[11] = "ноябрь"; monthname[12] = "декабрь";
        var prepositional = new string[13];
        monthname[1] = "января"; monthname[2] = "февраля"; monthname[3] = "марта"; monthname[4] = "апреля"; monthname[5] = "мая"; monthname[6] = "июня"; monthname[7] = "июля"; monthname[8] = "августа"; monthname[9] = "сентября"; monthname[10] = "октября"; monthname[11] = "ноября"; monthname[12] = "декабря";
        var newfromabove = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Категория:Википедия:Участники с добавлением тем сверху&cmprop=title&cmlimit=max"))))
            while (r.Read())
                if (r.Name == "cm")
                    newfromabove.Add(r.GetAttribute("title").Substring(r.GetAttribute("title").IndexOf(":") + 1));
        var lastmonth = DateTime.Now.AddMonths(-1);
        var pats = new Dictionary<string, HashSet<string>>();
        foreach (var t in new string[] { "approve", "approve-i", "unapprove" })
        {
            string cont = "", query = "/w/api.php?action=query&format=xml&list=logevents&leprop=title|user&leaction=review%2F" + t + "&leend=" + lastmonth.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lestart=" + DateTime.Now.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lelimit=5000";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&lecontinue=" + cont));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                    while (r.Read())
                        if (r.Name == "item")
                        {
                            string user = r.GetAttribute("user");
                            string page = r.GetAttribute("title");
                            if (user != null)
                            {
                                if (!pats.ContainsKey(user))
                                    pats.Add(user, new HashSet<string>() { page });
                                else if (!pats[user].Contains(page))
                                    pats[user].Add(page);
                            }
                        }
                }
            }
        }
        string addition = "\n|-\n";
        if (lastmonth.Month == 1)
            addition += "|rowspan=\"12\"|" + lastmonth.Year + "||" + monthname[lastmonth.Month];
        else
            addition += "|" + monthname[lastmonth.Month];
        int c = 0;
        pats.Remove("MBHbot");
        foreach (var p in pats.OrderByDescending(p => p.Value.Count))
        {
            if (++c > 10) break;
            addition += "||{{u|" + p.Key + "}} (" + p.Value.Count + ")";
            var user = new Page("user talk:" + p.Key);
            user.Load();
            string grade = c < 4 ? "I" : (c < 7 ? "II" : "III");
            if (!newfromabove.Contains(p.Key) || (newfromabove.Contains(p.Key) && user.text.IndexOf("==") == -1))
                try
                {
                    user.Save(user.text + "\n\n==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year +
                    ")==\n{{subst:u:Орденоносец/Заслуженному патрульному " + grade + "|За " + c + " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year +
                    " года. Поздравляем! ~~~~}}", "орден заслуженному патрулирующему за " + monthname[lastmonth.Month] + " " + lastmonth.Year + " года", false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            else
            {
                int border = user.text.IndexOf("==");
                string header = user.text.Substring(0, border - 1);
                string pagebody = user.text.Substring(border);
                try
                {
                    user.Save(header + "==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year +
                    ")==\n{{subst:u:Орденоносец/Заслуженному патрульному " + grade + "|За " + c + " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year +
                    " года. Поздравляем! ~~~~}}\n\n" + pagebody, "орден заслуженному патрулирующему за " + monthname[lastmonth.Month] + " " + lastmonth.Year + " года", false);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        var pg = new Page("Википедия:Ордена/Заслуженному патрульному");
        pg.Load();
        pg.Save(pg.text + addition, "ордена за " + monthname[lastmonth.Month], false);
    }
}
