using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Newtonsoft.Json;
public class Root
{
    public string status, country, region, regionName, city, zip, timezone, isp, org, @as, message, query;
    public double lat, lon;
}
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
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        var doc = new XmlDocument(); doc.LoadXml(result.Content.ReadAsStringAsync().Result); var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword",
                password }, { "lgtoken", logintoken } })).Result; return client;
    }
    static Regex iprgx = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3})\.\d{1,3}$"); static HttpClient site;
    static void Main()
    {
        var client = new HttpClient(); var ranges24 = new Dictionary<string, int>(); var now = DateTime.Now; var countries = new Dictionary<string, int>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n'); site = Site("ru", creds[0], creds[1]);
        foreach (string lang in "ru".Split('|'))
        {
            var date = new DateTime(2025, 1, 1);
            do
            {
                string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allrevisions&arvprop=user&arvlimit=max&arvend=" + date.ToString("yyyy-MM") + "-01T00:00:00" +
                    "&arvstart=" + date.AddMonths(1).ToString("yyyy-MM") + "-01T00:00:00";
                while (cont != null)
                    using (var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arvcontinue=" + cont).Result)))
                    {
                        r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("arvcontinue"); while (r.Read())
                            if (r.Name == "rev" && r.GetAttribute("anon") == "")
                            {
                                string user = r.GetAttribute("user");
                                if (iprgx.IsMatch(user))
                                {
                                    string ip = iprgx.Match(user).Groups[1].Value + ".0";
                                    if (ranges24.ContainsKey(ip)) ranges24[ip]++; else ranges24.Add(ip, 0);
                                }
                            }
                    }
                date = date.AddMonths(1);

            } while (date < now);
            Console.WriteLine("ips=" + ranges24.Count); int c = 0;
            foreach (var range in ranges24)
            {
                Root data = JsonConvert.DeserializeObject<Root>(client.GetStringAsync("http://ip-api.com/json/" + range.Key).Result); Thread.Sleep(1333); if (++c % 100 == 0) Console.WriteLine(c);
                if (data.message != null) { Console.WriteLine(data.message); Console.ReadLine(); }
                if (data.country != null) if (!countries.ContainsKey(data.country)) countries.Add(data.country, 1); else countries[data.country]++;
            }
            string result = "<center>\n{|class=standard";
            foreach (var country in countries)
                result += "\n|-\n|" + country.Key + "||" + country.Value;
            save("u:MBH/Черновик", result + "|}");
        }
    }
}
