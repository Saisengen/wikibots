using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
public class json { public string country, regionName; } public class slicedata { public long reg, ip4, ip6; }
class Program
{
    static string cell(int number) { if (number == 0) return ""; else return number.ToString(); } static double round(double input) { return Math.Round(input, 1); }
    static Dictionary<string, string> ranges2regions = new Dictionary<string, string>(); static HttpClient client = new HttpClient(); static HttpClient site;
    static Dictionary<int, slicedata> total = new Dictionary<int, slicedata>() { { 0, new slicedata { reg = 0, ip4 = 0, ip6 = 0 } } }; static Regex iprgx = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3})\.\d{1,3}$");
    static Dictionary<string, Dictionary<int, int>> resulttable = new Dictionary<string, Dictionary<int, int>>(); static string lang = "zh"; static int startyear = 2008, endyear = 2013;
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
        string answer = "null";
        if (ranges2regions.ContainsKey(range))
            return ranges2regions[range];
        else try {
                answer = client.GetStringAsync("http://ip-api.com/json/" + range).Result;
                json data = JsonConvert.DeserializeObject<json>(answer); Thread.Sleep(1480);
                string result = data.country + "!" + data.regionName;
                ranges2regions.Add(range, result); return result;
            } catch { Console.WriteLine("http://ip-api.com/json/" + range + " => " + answer); return null; }
    }
    static void initialize_resulttable_row(string region)
    {
        if (!resulttable.ContainsKey(region)) {
            resulttable.Add(region, new Dictionary<int, int>()); resulttable[region].Add(0, 0);
            for (int y = 2002; y <= 2024; y = y + 2)
                resulttable[region].Add(y, 0);
        }
    }
    static void merge()
    {
        for (int y = 2002; y <= 2024; y = y + 2)
            total.Add(y, new slicedata { reg = 0, ip4 = 0, ip6 = 0 });
        foreach (int part in new int[4] { 2007, 2013, 2019, 2025 })
        {
            var temp_table = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<int, int>>>(new StreamReader(lang + part + ".txt").ReadToEnd());
            foreach (var region in temp_table.Keys) {
                initialize_resulttable_row(region);
                foreach (var year in temp_table[region].Keys) {
                    resulttable[region][year] += temp_table[region][year]; resulttable[region][0] += temp_table[region][year];
                }
            }
            var temp_total = JsonConvert.DeserializeObject<Dictionary<int, slicedata>>(new StreamReader(lang + part + "total.txt").ReadToEnd());
            foreach (var year in temp_total.Keys) {
                total[year].reg += temp_total[year].reg; total[year].ip4 += temp_total[year].ip4; total[year].ip6 += temp_total[year].ip6;
            }
            total[0].reg += temp_total[0].reg; total[0].ip4 += temp_total[0].ip4; total[0].ip6 += temp_total[0].ip6;
        }
        string result = "<center>\n{|class=\"standard sortable\"\n!rowspan=2|Регион!!colspan=13|Правок в этой вики\n|-\n!02-03!!04-05!!06-07!!08-09!!10-11!!12-13!!14-15!!16-17!!18-19!!20-21!!22-23!!24-25!!" +
            "Всего\n|-\n|Всего правок, тыс.";
        for (int year = 2002; year <= 2024; year = year + 2)
            result += "||" + (total[year].reg + total[year].ip4 + total[year].ip6 == 0 ? "" : ((total[year].reg + total[year].ip4 + total[year].ip6) / 1000).ToString());
        result += "||" + (total[0].reg + total[0].ip4 + total[0].ip6) / 1000;

        result += "\n|-\n|Доля анонимных";
        for (int year = 2002; year <= 2024; year = year + 2)
            result += "||" + (total[year].reg + total[year].ip4 + total[year].ip6 == 0 ? "" : round((double)(total[year].ip4 + total[year].ip6) * 100 / (total[year].reg + total[year].ip4 + total[year].ip6)) + "%");
        result += "||" + round((double)(total[0].ip4 + total[0].ip6) * 100 / (total[0].reg + total[0].ip4 + total[0].ip6)) + "%";

        result += "\n|-\n|Доля IPv6 в анонимных";
        for (int year = 2002; year <= 2024; year = year + 2)
            result += "||" + (total[year].reg + total[year].ip4 + total[year].ip6 == 0 ? "" : round((double)total[year].ip6 * 100 / (total[year].ip4 + total[year].ip6)) + "%");
        result += "||" + round((double)total[0].ip6 * 100 / (total[0].ip4 + total[0].ip6)) + "%";

        foreach (var fullregion in resulttable.OrderByDescending(r => r.Value[0]))
        {
            string region = fullregion.Key.Split('!')[1]; string country = fullregion.Key.Split('!')[0];
            result += "\n|-\n|{{flag|" + country + "}} " + region;
            for (int year = 2002; year <= 2024; year = year + 2)
                result += "||" + cell(fullregion.Value[year]);
            result += "||" + cell(fullregion.Value[0]);
        }
        save(site, "ВП:Геопозиция анонимных правщиков/" + lang + "wiki", result + "\n|}");
    }
    static void read_part()
    {
        for (int year = startyear; year <= endyear; year = year + 2) {
            string query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allrevisions&arvprop=user&arvlimit=max&arvend=" + year + "-01-01T00:00:00&&arvstart=" + (year + 1) +
                "-12-31T23:59:59", cont = ""; Console.WriteLine(year); total.Add(year, new slicedata { reg = 0, ip4 = 0, ip6 = 0 });
            while (cont != null) {
                var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arvcontinue=" + cont).Result)); r.Read(); r.Read(); r.Read();
                cont = r.GetAttribute("arvcontinue"); while (r.Read())
                    if (r.Name == "rev")
                        if (r.GetAttribute("anon") == "") {
                            string user = r.GetAttribute("user");
                            if (IPAddress.TryParse(user, out IPAddress address)) {
                                string range = "";
                                if (iprgx.IsMatch(user)) { range = iprgx.Match(user).Groups[1].Value + ".0"; total[year].ip4++; total[0].ip4++; }
                                else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                                    byte[] bytes = address.GetAddressBytes(); for (int i = 4; i < 16; i++) bytes[i] = 0; range = new IPAddress(bytes).ToString(); total[year].ip6++; total[0].ip6++;
                                }
                                if (range != null && range != "") {
                                    string region = range2region(range);
                                    if (region != null) {
                                        initialize_resulttable_row(region);
                                        resulttable[region][year]++; resulttable[region][0]++;
                                    }
                                }
                            }
                        }
                        else { total[year].reg++; total[0].reg++; }
            }
        }
        try { var w = new StreamWriter(lang + endyear + ".txt"); w.Write(JsonConvert.SerializeObject(resulttable)); w.Close(); } catch { }
        try { var w = new StreamWriter(lang + endyear + "total.txt"); w.Write(JsonConvert.SerializeObject(total)); w.Close(); } catch { }
    }
    static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); site = Site(creds[0], creds[1]);
        merge();
        //read_part();        
    }
}
