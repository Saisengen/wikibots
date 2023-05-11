using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;
using Newtonsoft.Json;
using System.Reflection;
using System.ComponentModel;
using System.Text.RegularExpressions;
using static Program;
using System.Runtime.Remoting.Contexts;

class Program
{
    public class Groupmembership
    {
        public string group { get; set; }
        public string expiry { get; set; }
    }

    public class Query
    {
        public List<User> users { get; set; }
    }

    public class Root
    {
        public bool batchcomplete { get; set; }
        public Query query { get; set; }
    }

    public class User
    {
        public string name { get; set; }
        public bool invalid { get; set; }
        public int? userid { get; set; }
        public int? editcount { get; set; }
        public DateTime? registration { get; set; }
        public List<string> groups { get; set; }
        public List<Groupmembership> groupmemberships { get; set; }
        public List<string> implicitgroups { get; set; }
        public bool? emailable { get; set; }
    }

    public class Continue
    {
        public string uccontinue { get; set; }
        public string @continue { get; set; }
    }

    public class Query2
    {
        public List<Usercontrib> usercontribs { get; set; }
    }

    public class Root2
    {
        public bool batchcomplete { get; set; }
        public Continue @continue { get; set; }
        public Query2 query { get; set; }
    }

    public class Usercontrib
    {
        public int userid { get; set; }
        public string user { get; set; }
        public DateTime timestamp { get; set; }
    }

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
        string result = "";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        var now = DateTime.Now;
        foreach (int ns in new int[] {0, 102 } )
        {
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=title%7Cuser&letype=create&lestart=" + now.ToString("yyyy-MM-ddThh:mm:ss") + ".000Z&leend=" + now.AddMinutes(-1).ToString("yyyy-MM-ddThh:mm:ss") + ".000Z&lenamespace=" + ns + "&lelimit=max").Result)))
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string user = r.GetAttribute("user");
                        string page = r.GetAttribute("title");
                        var data = JsonConvert.DeserializeObject<Root>(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=json&list=users&formatversion=2&usprop=cancreate%7Ceditcount%7Cemailable%7Cgroupmemberships%7Cgroups%7Cimplicitgroups%7Cregistration&ususers=" + Uri.EscapeUriString(user)).Result);
                        if (data.query.users[0].groups != null && (data.query.users[0].groups.Contains("editor") || data.query.users[0].groups.Contains("autoeditor")))
                            continue;
                        else
                        {
                            int? edits, age;
                            if (data.query.users[0].invalid)
                            {
                                var data2 = JsonConvert.DeserializeObject<Root2>(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=json&list=usercontribs&formatversion=2&uclimit=max&ucuser=" + Uri.EscapeUriString(user) + "&ucdir=newer&ucprop=timestamp").Result);
                                age = data2.query.usercontribs.Count == 0 ? 0 : (now - data2.query.usercontribs[0].timestamp).Days;
                                edits = data2.query.usercontribs.Count;
                            }
                            else
                            {
                                edits = data.query.users[0].editcount;
                                age = (now - data.query.users[0].registration).Value.Days;
                                bool trusteduser = false;
                                if (data.query.users[0].groups != null)
                                    foreach (var flag in data.query.users[0].groups)
                                        if (flag == "autoconfirmed")
                                            trusteduser = true;
                                if (trusteduser)
                                    continue;
                            }
                            result += "\n|-\n|[[" + page + "]]||[[special:contribs/" + user + "|" + user + "]]||" + edits + "||" + age;
                        }

                    }
        }
        string initialtext = site.GetStringAsync("https://ru.wikipedia.org/wiki/u:MBH/newpages for ini?action=raw").Result;
        if (result.Length > 0)
            Save(site, "u:MBH/newpages for ini", initialtext + result, "");

    }
}
