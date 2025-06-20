using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
class pageviews_result
{
    public string date;
    public int max, median;
}
class Program
{
    static string[] creds; static HttpClient site; static DateTime now = DateTime.Now; static WebClient cl = new WebClient(); static Dictionary<string, string> tableheader = new Dictionary<string, string>()
    { { "ru", "Статья!!Пик!!Медиана!!Дата пика" }, { "uk", "Стаття!!Пік!!Медіана!!Дата піку" }, { "be", "Артыкул!!Пік!!Медыяна!!Дата піка" } };
    static Dictionary<string, Dictionary<string, string>> outputpage = new Dictionary<string, Dictionary<string, string>>
        { { "uk", new Dictionary<string, string>() { { "month", "Вікіпедія:Спалахи інтересу до статей" }, { "year", "Вікіпедія:Спалахи інтересу до статей/За рік" }, { "total", "Вікіпедія:Спалахи інтересу до статей/За весь час" } } },
            { "be", new Dictionary<string, string>() { { "month", "Вікіпедыя:Папулярныя артыкулы" }, { "year", "Вікіпедыя:Папулярныя артыкулы/За год" }, { "total", "Вікіпедыя:Папулярныя артыкулы/За ўвесь час" } } },
            { "ru", new Dictionary<string, string>() { { "month", "ВП:Популярные статьи/Пики за месяц" }, { "year", "ВП:Популярные статьи/Пики за год" }, { "total", "ВП:Популярные статьи/Пики за всё время" } } } };
    static Dictionary<string, Dictionary<string, int>> minneededpeakvalue = new Dictionary<string, Dictionary<string, int>> { { "ru", new Dictionary<string, int>() { { "month", 10000 }, { "year", 15000 },
        { "total", 20000 }, } }, { "uk", new Dictionary<string, int>() { { "month", 1000 }, { "year", 2000 }, { "total", 3000 }, } }, { "be", new Dictionary<string, int>() { { "month", 15 }, { "year", 30 }, { "total", 100 }, } } };
    static Dictionary<string, Dictionary<string, string>> monthnames = new Dictionary<string, Dictionary<string, string>>
        { {"ru", new Dictionary<string, string>() { {"01","января"}, {"02","февраля"}, {"03","марта"}, {"04","апреля"}, {"05","мая"}, {"06","июня"}, {"07","июля"}, {"08","августа"}, {"09","сентября"}, {"10",
                "октября"}, {"11","ноября"}, {"12","декабря"} } }, {"uk", new Dictionary<string, string>() { {"01","січня"}, {"02","лютого"}, {"03","березня"}, {"04","квітня"}, {"05","травня"}, {"06","червня"},
                    {"07","липня"}, {"08","серпня"}, {"09","вересня"}, {"10","жовтня"}, {"11","листопада"}, {"12","грудня"} } }, {"be", new Dictionary<string, string>() { {"01","студзеня"}, {"02","лютага"},
                        {"03","сакавіка"}, {"04","красавіка"}, {"05","траўня"}, {"06","чэрвеня"}, {"07","ліпеня"}, {"08","жніўня"}, {"09","верасня"}, {"10","кастрычніка"}, {"11","лістапада"}, {"12","снежня"} } } };
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); site = Site("ru", creds[0], creds[1]);
        int year_of_previous_month = now.AddMonths(-1).Year; string lastmonth = now.AddMonths(-1).ToString("MM");
        if (lastmonth == "12")
        { process_pageviews("year", year_of_previous_month + "0101/" + year_of_previous_month + "1231"); process_pageviews("total", "20150701/" + year_of_previous_month + "1231"); }
        process_pageviews("month", year_of_previous_month + lastmonth + "01/" + year_of_previous_month + lastmonth + DateTime.DaysInMonth(now.AddMonths(-1).Year, now.AddMonths(-1).Month));
    }
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword",
                password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result; return client;
    }
    static void Save(string lang, string title, string text)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value; var request = new MultipartFormDataContent { { new StringContent("edit"),
                "action" }, { new StringContent(title), "title" }, { new StringContent(text), "text" }, { new StringContent(token), "token" } };
        result = site.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", request).Result;
        if (!result.ToString().Contains("uccess")) Console.WriteLine(result);
    }
    static string e(string input)
    {
        return Uri.EscapeDataString(input);
    }
    static void process_pageviews(string mode, string reqstr_period)
    {
        cl.Headers.Add("user-agent", "Stats grabber of ruwiki user MBH");
        foreach (string lang in new HashSet<string>() { "uk", "be", "ru" })
        {
            var results = new Dictionary<string, pageviews_result>();
            var site = Site(lang, creds[0], creds[1]);
            string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allpages&apnamespace=0&apfilterredir=nonredirects&aplimit=max";
            while (cont != null)
            {
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&apcontinue=" + e(cont)).Result);
                using (var r = new XmlTextReader(new StringReader(apiout)))
                {
                    r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                    while (r.Read())
                        if (r.Name == "p")
                        {
                            string page = r.GetAttribute("title"); var thispagestats = new Dictionary<string, int>(); string currres = "", reqstr = "", peakdate = ""; int maxviews = 0;
                            reqstr = "https://wikimedia.org/api/rest_v1/metrics/pageviews/per-article/" + lang + ".wikipedia/all-access/user/" + e(page) + "/daily/" + reqstr_period;
                            try { currres = cl.DownloadString(reqstr); } catch { continue; }
                            foreach (Match match in Regex.Matches(currres, "(\\d{10})\",\"access\":\"all-access\",\"agent\":\"user\",\"views\":(\\d*)"))
                            {
                                int views = Convert.ToInt32(match.Groups[2].Value);
                                string date = match.Groups[1].Value;
                                thispagestats.Add(date, views);
                                if (views > maxviews) {
                                    maxviews = views; peakdate = date;
                                }
                            }
                            var orderedlist = thispagestats.OrderBy(o => o.Value).ToList();
                            int median = orderedlist[orderedlist.Count / 2].Value;
                            if (maxviews >= minneededpeakvalue[lang][mode])
                                results.Add(page, new pageviews_result() { date = peakdate, max = maxviews, median = median });
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
            Save(lang, outputpage[lang][mode], result + "\n|}");
        }
    }
}
