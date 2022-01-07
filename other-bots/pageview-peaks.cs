using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using DotNetWikiBot;
using System.Xml;
class result
{
    public string date;
    public int max, median;
}
class Program
{
    static void Main()
    {
        int minneededpeakvalue;
        var cl = new WebClient();
        cl.Headers.Add("user-agent", "Stats grabber of ruwiki user MBH");
        var results = new Dictionary<string, result>();
        var enddate = new Dictionary<string,string>(){{"01","31"},{"02","28"},{"03","31"},{"04","30"},{"05","31"},{"06","30"},{"07","31"},{"08","31"},{"09","30"},{"10","31"},{"11","30"},{"12","31"}};
        bool yearly = false;
        string startmonth, endmonth, outputpage, datespan, header;
        int year = DateTime.Now.AddMonths(-1).Year;
        foreach(var lang in new HashSet<string>() { "ru", "uk" })
        {
            results.Clear();
            string templatename, tableheader;
            var monthnames = new Dictionary<string, string>();
            if (lang == "uk")
            {
                monthnames = new Dictionary<string, string>() { {"01","січня"}, {"02","лютого"}, {"03","березня"}, {"04","квітня"}, {"05","травня"}, {"06","червня"}, {"07","липня"}, {"08","серпня"},
                {"09","вересня"}, {"10","жовтня"}, {"11","листопада"}, {"12","грудня"} };
                templatename = "плаваюча шапка таблиці";
                tableheader = "Стаття!!Пік!!Медіана!!Дата піку!!Графік";
            }
            else
            {
                monthnames = new Dictionary<string, string>() { {"01","января"}, {"02","февраля"}, {"03","марта"}, {"04","апреля"}, {"05","мая"}, {"06","июня"}, {"07","июля"}, {"08","августа"},
                {"09","сентября"}, {"10","октября"}, {"11","ноября"}, {"12","декабря"} };
                templatename = "плавающая шапка таблицы";
                tableheader = "Статья!!Пик!!Медиана!!Дата пика!!График";
            }
            if (yearly)
            {
                startmonth = "01";
                endmonth = "12";
                datespan = "{{#expr:365+({{CURRENTWEEK}}-1)*7+{{CURRENTDOW}}}}";
                minneededpeakvalue = (lang == "uk" ? 2000 : 15000);
                outputpage = (lang == "uk" ? "Вікіпедія:Спалахи інтересу до статей/За рік" : "ВП:Пики интереса к статьям/За год");
                header = (lang == "uk" ? "Див. також [[../|за минулий місяць]]." : "См. также [[../|за последний месяц]].");
            }
            else
            {
                startmonth = DateTime.Now.AddMonths(-1).Month.ToString();
                if (startmonth.Length == 1)
                    startmonth = "0" + startmonth;
                endmonth = startmonth;
                datespan = "{{#expr:31+{{CURRENTDAY}}}}";
                minneededpeakvalue = (lang == "uk" ? 1000 : 10000);
                outputpage = (lang == "uk" ? "Вікіпедія:Спалахи інтересу до статей" : "ВП:Пики интереса к статьям");
                header = (lang == "uk" ? "Див. також [[/За рік|за минулий рік]]." : "См. также [[/За год|за прошедший год]].");
            }

            var creds = new StreamReader("p").ReadToEnd().Split('\n');
            var site = new Site("https://" + lang + ".wikipedia.org", creds[0], creds[1]);
            string cont = "", query = "/w/api.php?action=query&format=xml&list=allpages&apnamespace=0&apfilterredir=nonredirects&aplimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&apcontinue=" + Uri.EscapeDataString(cont)));
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.WhitespaceHandling = WhitespaceHandling.None;
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                        {
                            string page = r.GetAttribute("title");
                            var thispagestats = new Dictionary<string, int>();
                            string currres = "";
                            string reqstr = "";
                            reqstr = "https://wikimedia.org/api/rest_v1/metrics/pageviews/per-article/" + lang + ".wikipedia/all-access/user/" + Uri.EscapeDataString(page) + "/daily/" + year +
                                startmonth + "01/" + year + endmonth + enddate[endmonth];
                            try
                            {
                                currres = cl.DownloadString(reqstr);
                            }
                            catch
                            {
                                continue;
                            }
                            int maxviews = 0;
                            string peakdate = "";
                            foreach (Match match in Regex.Matches(currres, "(\\d{10})\",\"access\":\"all-access\",\"agent\":\"user\",\"views\":(\\d*)"))
                            {
                                int views = Convert.ToInt32(match.Groups[2].Value);
                                string date = match.Groups[1].Value;
                                thispagestats.Add(date, views);
                                if (views > maxviews)
                                {
                                    maxviews = views;
                                    peakdate = date;
                                }
                            }
                            var orderedlist = thispagestats.OrderBy(o => o.Value).ToList();
                            int median = orderedlist[orderedlist.Count / 2].Value;
                            if (maxviews >= minneededpeakvalue)
                                results.Add(page, new result() { date = peakdate, max = maxviews, median = median });
                        }
                }
            }
            site = new Site("https://" + lang + ".wikipedia.org", creds[0], creds[1]);
            string result = "<center>" + header + "{{" + templatename + "}}\n{|class=\"standard sortable ts-stickytableheader\" style=\"text-align:center\"\n!" + tableheader;
            foreach (var r in results.OrderByDescending(r => r.Value.max))
            {
                string month = r.Value.date.Substring(4, 2);
                string day = r.Value.date.Substring(6, 2);
                result += "\n|-\n|[[" + r.Key + "]]||{{formatnum:" + r.Value.max + "}}||" + r.Value.median + "||{{~|" + month + day + "}}" + day + " " + monthnames[month] + "||{{Graph:PageViews|" + datespan + "|" + r.Key + "|height=120|width=240}}";
            }
            result += "\n|}";
            var p = new Page(outputpage);
            p.Save(result);
        }
    }
}
