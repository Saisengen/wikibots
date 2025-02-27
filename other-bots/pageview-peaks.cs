using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net.Http;

class result
{
    public string date;
    public int max, median;
}
class Program
{
    static Dictionary<string, string> datespan = new Dictionary<string, string>() { { "month", "{{#expr:31+{{CURRENTDAY}}}}" }, { "year", "{{#expr:365+({{CURRENTWEEK}}-1)*7+{{CURRENTDOW}}}}" } };
    static Dictionary<string, string> tableheader = new Dictionary<string, string>() { { "ru", "Статья!!Пик!!Медиана!!Дата пика" }, { "uk", "Стаття!!Пік!!Медіана!!Дата піку" },
        { "be", "Артыкул!!Пік!!Медыяна!!Дата піка" } };
    static Dictionary<string, string> enddate = new Dictionary<string, string>() { { "01", "31" }, { "02", "29" }, { "03", "31" }, { "04", "30" }, { "05", "31" }, { "06", "30" }, { "07", "31" },
            { "08", "31" }, { "09", "30" }, { "10", "31" }, { "11", "30" }, { "12", "31" } };
    static Dictionary<string, Dictionary<string, string>> outputpage = new Dictionary<string, Dictionary<string, string>>
        { { "uk", new Dictionary<string, string>() { { "month", "Вікіпедія:Спалахи інтересу до статей" }, { "year", "Вікіпедія:Спалахи інтересу до статей/За рік" }, { "total", "Вікіпедія:Спалахи інтересу до статей/За весь час" } } },
            { "be", new Dictionary<string, string>() { { "month", "Вікіпедыя:Папулярныя артыкулы" }, { "year", "Вікіпедыя:Папулярныя артыкулы/За год" }, { "total", "Вікіпедыя:Папулярныя артыкулы/За ўвесь час" } } },
            { "ru", new Dictionary<string, string>() { { "month", "ВП:Пики интереса к статьям/За месяц" }, { "year", "ВП:Пики интереса к статьям/За год" }, { "total", "ВП:Пики интереса к статьям/За всё время" } } } };
    static Dictionary<string, Dictionary<string, int>> minneededpeakvalue = new Dictionary<string, Dictionary<string, int>>
        { { "ru", new Dictionary<string, int>() { { "month", 10000 }, { "year", 15000 }, { "total", 20000 }, } },
            { "uk", new Dictionary<string, int>() { { "month", 1000 }, { "year", 2000 }, { "total", 3000 }, } },
            { "be", new Dictionary<string, int>() { { "month", 15 }, { "year", 30 }, { "total", 100 }, } } };
    static Dictionary<string, Dictionary<string, string>> monthnames = new Dictionary<string, Dictionary<string, string>>
        { {"ru", new Dictionary<string, string>() { {"01","января"}, {"02","февраля"}, {"03","марта"}, {"04","апреля"}, {"05","мая"}, {"06","июня"}, {"07","июля"}, {"08","августа"},
                {"09","сентября"}, {"10","октября"}, {"11","ноября"}, {"12","декабря"} } },
            {"uk", new Dictionary<string, string>() { {"01","січня"}, {"02","лютого"}, {"03","березня"}, {"04","квітня"}, {"05","травня"}, {"06","червня"}, {"07","липня"}, {"08","серпня"},
                {"09","вересня"}, {"10","жовтня"}, {"11","листопада"}, {"12","грудня"} } },
            {"be", new Dictionary<string, string>() { {"01","студзеня"}, {"02","лютага"}, {"03","сакавіка"}, {"04","красавіка"}, {"05","траўня"}, {"06","чэрвеня"}, {"07","ліпеня"}, {"08","жніўня"},
                {"09","верасня"}, {"10","кастрычніка"}, {"11","лістапада"}, {"12","снежня"} } } };
    static WebClient cl = new WebClient();
    static HttpClient Site(string wiki, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + wiki + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + wiki + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(HttpClient site, string lang, string title, string text)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action");
        request.Add(new StringContent(title), "title");
        request.Add(new StringContent(text), "text");
        request.Add(new StringContent(token), "token");
        request.Add(new StringContent("xml"), "format");
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result;
        if (result.ToString().Contains("uccess"))
            Console.WriteLine(DateTime.Now.ToString() + " written " + title);
        else
            Console.WriteLine(result);
    }
    static void process(string mode, string reqstr_period)
    {
        cl.Headers.Add("user-agent", "Stats grabber of ruwiki user MBH");
        foreach (string lang in new HashSet<string>() { "ru", "uk", "be" })
        {
            var results = new Dictionary<string, result>();
            var creds = new StreamReader("p").ReadToEnd().Split('\n');
            var site = Site(lang, creds[0], creds[1]);
            string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allpages&apnamespace=0&apfilterredir=nonredirects&aplimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + Uri.EscapeDataString(cont)).Result);
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
                            reqstr = "https://wikimedia.org/api/rest_v1/metrics/pageviews/per-article/" + lang + ".wikipedia/all-access/user/" + Uri.EscapeDataString(page) + "/daily/" + reqstr_period;
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
                            int currentminneededpeakvalue = minneededpeakvalue[lang][mode];
                            if (maxviews >= currentminneededpeakvalue)
                                results.Add(page, new result() { date = peakdate, max = maxviews, median = median });
                        }
                }
            }
            string result = "{{popular pages}}{{floating table header}}<center>\n{|class=\"standard sortable ts-stickytableheader\" style=\"text-align:center\"\n!" + tableheader[lang];
            foreach (var r in results.OrderByDescending(r => r.Value.max))
            {
                string month = r.Value.date.Substring(4, 2);
                string day = r.Value.date.Substring(6, 2);
                string date = mode == "total" ? r.Value.date.Substring(0, 4) + "-" + month + "-" + day : "{{~|" + month + day + "}}" + day + " " + monthnames[lang][month];
                result += "\n|-\n|[[" + r.Key + "]]||{{formatnum:" + r.Value.max + "}}||" + r.Value.median + "||" + date;
            }
            Save(site, lang, outputpage[lang][mode], result + "\n|}");
        }
    }
    static void Main()
    {
        int year_of_previous_month = DateTime.Now.AddMonths(-1).Year;
        string lastmonth = DateTime.Now.AddMonths(-1).ToString("MM");
        if (lastmonth != "12")
            process("month", year_of_previous_month + lastmonth + "01/" + year_of_previous_month + lastmonth + enddate[lastmonth]);
        else
        {
            process("year", year_of_previous_month + "0101/" + year_of_previous_month + "1231");
            process("month", year_of_previous_month + lastmonth + "01/" + year_of_previous_month + lastmonth + enddate[lastmonth]);
            process("total", "20150701/" + year_of_previous_month + "1231");
        }
    }
}
