using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Newtonsoft.Json;
public class Root {
    public string country, regionName; public Dictionary<int, int> edits; }
public class regdata {
    public string country; public Dictionary<int, int> edits; }
class Program
{
    static void save(string title, string text)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result; if (!result.IsSuccessStatusCode) return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" },
            { new StringContent(text), "text" }, { new StringContent(token), "token" } }).Result; if (!result.ToString().Contains("uccess")) Console.WriteLine(result.ToString());
    }
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument(); doc.LoadXml(
            result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value; result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new 
                FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken } })).Result; return client;
    }
    static Regex iprgx = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3})\.\d{1,3}$"); static HttpClient site;
    static void Main()
    {
        var client = new HttpClient(); var ranges24 = new Dictionary<string, Root>(); var regions = new Dictionary<string, regdata>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); site = Site("ru", creds[0], creds[1]);
        foreach (string lang in "ru".Split('|'))
            for (int year = 2020; year < 2026; year++)
            {
                string query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allrevisions&arvprop=user&arvlimit=max&arvend=" + year + "-01-01T00:00:00&&arvstart=" + year +
                    "-12-31T23:59:59", cont = "";
                while (cont != null)
                {
                    var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arvcontinue=" + cont).Result)); r.Read(); r.Read(); r.Read();
                    cont = r.GetAttribute("arvcontinue"); while (r.Read())
                        if (r.Name == "rev" && r.GetAttribute("anon") == "")
                        {
                            string user = r.GetAttribute("user");
                            if (iprgx.IsMatch(user))
                            {
                                //var octets = iprgx.Match(user).Groups[1].Value.Split('.'); byte o1, o2, o3; try { o1 = Convert.ToByte(octets[0]); o2 = Convert.ToByte(octets[1]); o3 = Convert.ToByte(octets[2]); }
                                /*catch { continue; }*/ string range = /*new string(new char[] { (char)o1, (char)o2, (char)o3 })*/iprgx.Match(user).Groups[1].Value;
                                if (ranges24.ContainsKey(range)) ranges24[range].edits[year]++; else ranges24.Add(range, new Root() { edits = new Dictionary<int, int> { { year, 1 } } });
                            }
                        }
                }
                Console.WriteLine("ips=" + ranges24.Count); int c = 0;
                foreach (var range in ranges24) try
                    {
                        //string ip = (byte)range.Key[0] + "." + (byte)range.Key[1] + "." + (byte)range.Key[2] + ".0";
                        Root data = JsonConvert.DeserializeObject<Root>(client.GetStringAsync("http://ip-api.com/json/" + range.Key + ".0").Result); Thread.Sleep(1425); if (++c % 2000 == 0) Console.WriteLine(c);
                        if (data.regionName != null)
                            if (!regions.ContainsKey(data.regionName)) regions.Add(data.regionName, new regdata() { edits = range.Value.edits, country = data.country });
                            else regions[data.regionName].edits[year] += range.Value.edits[year];
                    }
                    catch (Exception e) { Console.WriteLine("exception=" + e.ToString()); }
            }
        string result = "<center>\n{|class=\"standard sortable\"\n!rowspan=2|Регион!!colspan=6|Правок в рувики\n|-\n!2020!!2021!!2022!!2023!!2024!!2025!!Всего";
        foreach (var r in regions.OrderByDescending(r => r.Value.edits[2020] + r.Value.edits[2021] + r.Value.edits[2022] + r.Value.edits[2023] + r.Value.edits[2024] + r.Value.edits[2025]))
            result += "\n|-\n|{{флаг|" + r.Value.country + "}} " + r.Key + "||" + r.Value.edits[2020] + "||" + r.Value.edits[2021] + "||" + r.Value.edits[2022] + "||" + r.Value.edits[2023] + "||" + 
                r.Value.edits[2024] + "||" + r.Value.edits[2025] + "||" + (r.Value.edits[2020] + r.Value.edits[2021] + r.Value.edits[2022] + r.Value.edits[2023] + r.Value.edits[2024] + r.Value.edits[2025]);
        save("u:MBH/Черновик", result + "\n|}");
    }
}
