using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;

class Program
{
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(HttpClient site, string title, string text, string comment)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action");
        request.Add(new StringContent(title), "title");
        request.Add(new StringContent(text), "text");
        request.Add(new StringContent(comment), "summary");
        request.Add(new StringContent(token), "token");
        request.Add(new StringContent("xml"), "format");
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
        if (result.ToString().Contains("uccess"))
            Console.WriteLine(DateTime.Now.ToString() + " written " + title);
        else
            Console.WriteLine(result);
    }
    static void Main()
    {
        var monthname = new string[13];
        monthname[1] = "январь"; monthname[2] = "февраль"; monthname[3] = "март"; monthname[4] = "апрель"; monthname[5] = "май"; monthname[6] = "июнь";
        monthname[7] = "июль"; monthname[8] = "август"; monthname[9] = "сентябрь"; monthname[10] = "октябрь"; monthname[11] = "ноябрь"; monthname[12] = "декабрь";
        var prepositional = new string[13];
        prepositional[1] = "январе"; prepositional[2] = "феврале"; prepositional[3] = "марте"; prepositional[4] = "апреле"; prepositional[5] = "мае"; prepositional[6] = "июне";
        prepositional[7] = "июле"; prepositional[8] = "августе"; prepositional[9] = "сентябре"; prepositional[10] = "октябре"; prepositional[11] = "ноябре"; prepositional[12] = "декабре";
        var newfromabove = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=categorymembers&cmtitle=Категория:Википедия:Участники с добавлением тем сверху&cmprop=title&cmlimit=max").Result)))
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
                string apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&lecontinue=" + cont).Result);
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
            string usertalk = site.GetStringAsync("https://ru.wikipedia.org/wiki/user talk:" + Uri.EscapeDataString(p.Key) + "?action=raw").Result;
            string grade = c < 4 ? "I" : (c < 7 ? "II" : "III");
            if (!newfromabove.Contains(p.Key) || (newfromabove.Contains(p.Key) && usertalk.IndexOf("==") == -1))
                Save(site, "user talk:" + p.Key, usertalk + "\n\n==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year + ")==\n{{subst:u:Орденоносец/Заслуженному патрулирующему " + grade + "|За " + c + 
                    " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года. Поздравляем! ~~~~}}", "орден заслуженному патрулирующему за " + monthname[lastmonth.Month] + " " + lastmonth.Year + " года");
            else
            {
                int border = usertalk.IndexOf("==");
                string header = usertalk.Substring(0, border - 1);
                string pagebody = usertalk.Substring(border);
                Save(site, "user talk:" + p.Key, header + "==Орден заслуженному патрулирующему " + grade + " степени (" + monthname[lastmonth.Month] + " " + lastmonth.Year + ")==\n{{subst:u:Орденоносец/Заслуженному патрулирующему " + grade + "|За " + c + 
                    " место по числу патрулирований в " + prepositional[lastmonth.Month] + " " + lastmonth.Year + " года. Поздравляем! ~~~~}}\n\n" + pagebody, "орден заслуженному патрулирующему за " + monthname[lastmonth.Month] + " " + lastmonth.Year + " года");
            }
        }
        string pats_order = site.GetStringAsync("https://ru.wikipedia.org/wiki/ВП:Ордена/Заслуженному патрулирующему?action=raw").Result;
        Save(site, "ВП:Ордена/Заслуженному патрулирующему", pats_order + addition, "ордена за " + monthname[lastmonth.Month]);
    }
}
