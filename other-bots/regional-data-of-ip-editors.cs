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
public class Root { public string country, regionName; public Dictionary<int, int> edits; }
public class regdata { public string country; public Dictionary<int, int> edits; }
class Program
{
    static void save(string title, string text)
    {
        var doc = new XmlDocument(); var result = site.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result; if (!result.IsSuccessStatusCode) return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result); var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        result = site.PostAsync("https://ru.wikipedia.org/w/api.php", new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" },
            { new StringContent(text), "text" }, { new StringContent(token), "token" } }).Result; if (!result.ToString().Contains("uccess")) Console.WriteLine(result.ToString());
    }
    static HttpClient Site(string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() }); client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result; var doc = new XmlDocument(); doc.LoadXml(
            result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value; result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new 
                FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken } })).Result; return client;
    }
    static Regex iprgx = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3})\.\d{1,3}$"); static HttpClient site; static int startyear = 2025, endyear = 2025; static string lang = "be";
    static void Main()
    {
        var client = new HttpClient(); var ranges = new Dictionary<string, Root>(); var output_data = new Dictionary<string, regdata>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); site = Site(creds[0], creds[1]);
        for (int year = startyear; year <= endyear; year++)
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
                        if (IPAddress.TryParse(user, out IPAddress address))
                        {
                            string range = "";
                            if (iprgx.IsMatch(user)) range = iprgx.Match(user).Groups[1].Value + ".0";
                            else if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) {
                                byte[] bytes = address.GetAddressBytes(); for (int i = 4; i < 16; i++) bytes[i] = 0; range = new IPAddress(bytes).ToString(); }
                            if (range != "") {
                                if (!ranges.ContainsKey(range)) {
                                    var root = new Root() { edits = new Dictionary<int, int> { { 0, 0 } } };
                                    for (int y = startyear; y <= endyear; y++)
                                        root.edits.Add(y, 0);
                                    ranges.Add(range, root);
                                }
                                ranges[range].edits[year]++; ranges[range].edits[0]++;
                            }
                        }
                            
                    }
            }
        }
        Console.WriteLine("ranges=" + ranges.Count); int c = 0;
        foreach (var range in ranges) try {
                Root data = JsonConvert.DeserializeObject<Root>(client.GetStringAsync("http://ip-api.com/json/" + range.Key).Result);
                Thread.Sleep(1450); if (++c % 3000 == 0) Console.WriteLine(c); string key = data.regionName;
                if (!output_data.ContainsKey(key)) output_data.Add(key, new regdata() { edits = range.Value.edits, country = data.country });
                else {
                    for (int year = startyear; year <= endyear; year++)
                        output_data[key].edits[year] += range.Value.edits[year];
                    output_data[key].edits[0] += range.Value.edits[0];
                }
            } catch (Exception e) { Console.WriteLine("exception=" + e.ToString()); }
        string result = "<center>\n{|class=\"standard sortable\"\n!rowspan=2|Регион и провайдер!!colspan=" + (endyear - startyear + 2) + "|Правок в этой вики\n|-";
        for (int year = startyear; year <= endyear; year++)
            result += "\n!" + year;
        result += "\n!Всего";
        foreach (var r in output_data.OrderByDescending(r => r.Value.edits[0]))
        {
            result += "\n|-\n|{{flag|" + r.Value.country + "}} " + r.Key;
            for (int year = startyear; year <= endyear; year++)
                result += "||" + r.Value.edits[year];
            result += "||" + r.Value.edits[0];
        }
        save("user:MBH/Черновик", result + "\n|}");
    }
}
