using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml;
using System.Threading;
using Newtonsoft.Json;
public class Root { public string country, regionName; public Dictionary<int, int> edits; }
class Program
{
    static string cell(int number) { if (number == 0) return ""; else return number.ToString(); }
    static Dictionary<string, string> ranges2regions = new Dictionary<string, string>(); static HttpClient client = new HttpClient();
    static void save(HttpClient site, string title, string text)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result; if (!result.IsSuccessStatusCode) return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" },
            { new StringContent(text), "text" }, { new StringContent(token), "token" } }).Result; if (!result.ToString().Contains("uccess")) Console.WriteLine(result.ToString());
    }
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument(); doc.LoadXml(
            result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value; result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new 
                FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken } })).Result; return client;
    }
    static string range2region(string range)
    {
        if (ranges2regions.ContainsKey(range)) return ranges2regions[range];
        else try { Root data = JsonConvert.DeserializeObject<Root>(client.GetStringAsync("http://ip-api.com/json/" + range).Result); Thread.Sleep(1500); string answer = data.country + "!" + data.regionName;
                ranges2regions.Add(range, answer); return answer; } catch (Exception e) { Console.WriteLine("exception=" + e.ToString()); return null; }
    }
    static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); var site = Site(creds[0], creds[1]);
        var iprgx = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3})\.\d{1,3}$"); string lang = "ur"; var resulttable = new Dictionary<string, Root>(); int startyear = 2002, endyear = 2025;
        for (int year = startyear; year <= endyear; year = year + 2) {
            string query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allrevisions&arvprop=user&arvlimit=max&arvend=" + year + "-01-01T00:00:00&&arvstart=" + (year + 1) +
                "-12-31T23:59:59", cont = "";
            while (cont != null) {
                var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arvcontinue=" + cont).Result)); r.Read(); r.Read(); r.Read();
                cont = r.GetAttribute("arvcontinue"); while (r.Read())
                    if (r.Name == "rev" && r.GetAttribute("anon") == "") {
                        string user = r.GetAttribute("user");
                        if (IPAddress.TryParse(user, out IPAddress address)) {
                            string range = "";
                            if (iprgx.IsMatch(user)) range = iprgx.Match(user).Groups[1].Value + ".0";
                            else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                                byte[] bytes = address.GetAddressBytes(); for (int i = 4; i < 16; i++) bytes[i] = 0; range = new IPAddress(bytes).ToString(); }
                            if (range != "") {
                                string region = range2region(range).Split('!')[1]; string country = range2region(range).Split('!')[0];
                                if (!resulttable.ContainsKey(region)) {
                                    var root = new Root() { edits = new Dictionary<int, int> { { 0, 0 } }, regionName = region, country = country };
                                    for (int y = startyear; y <= endyear; y = y + 2)
                                        root.edits.Add(y, 0);
                                    resulttable.Add(range, root);
                                }
                                resulttable[range].edits[year]++; resulttable[range].edits[0]++;
                            }
                        }
                    }
            }
        }
        string result = "<center>\n{|class=\"standard sortable\"\n!rowspan=2|Регион!!colspan=13|Правок в этой вики\n|-\n!02-03!!04-05!!06-07!!08-09!!10-11!!12-13!!14-15!!16-17!!18-19!!20-21!!22-23!!24-25!!Всего";
        foreach (var r in resulttable.OrderByDescending(r => r.Value.edits[0])) {
            result += "\n|-\n|{{flag|" + r.Value.country + "}} " + r.Key;
            for (int year = startyear; year <= endyear; year = year + 2)
                result += "||" + cell(r.Value.edits[year]);
            result += "||" + cell(r.Value.edits[0]);
        }
        result += "\n|}";
        var w = new StreamWriter(lang + ".txt"); w.Write(JsonConvert.SerializeObject(resulttable)); w.Close();
        save(site, "ВП:Геопозиция анонимных правщиков/" + lang + "wiki", result);
    }
}
