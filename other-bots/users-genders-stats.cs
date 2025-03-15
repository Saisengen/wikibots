using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Xml;

class langdata
{
    public string name;
    public Dictionary<string, Dictionary<string, int>> stats;
}
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
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        var langnames = new Dictionary<string, string>() { { "simple", "простой англ." },{ "en", "английский" },{ "commons", "Викисклад" },{ "wikidata", "Викиданные" },{ "eo", "эсперанто" },
            { "de", "немецкий" },{ "da", "датский" },{ "nl", "нидерландский" },{ "ar", "арабский" },{ "fa", "фарси" },{ "he", "иврит" },{ "tr", "турецкий" },{ "ru", "русский" },{ "uk", "украинский" },
            { "be", "белорусский" },{ "fr", "французский" },{ "es", "испанский" },{ "ca", "каталанский" },{ "eu", "баскский" },{ "it", "итальянский" },{ "pt", "португальский" },{ "ja", "японский" },
            { "zh", "китайский" },{ "ko", "корейский" },{ "vi", "вьетнамский" },{ "el", "греческий" },{ "hu", "венгерский" },{ "pl", "польский" },{ "cs", "чешский" },{ "sk", "словацкий" },
            { "sr", "сербский" },{ "ro", "румынский" },{ "hr", "хорватский" },{ "bg", "болгарский" },{ "no", "норвежский" },{ "kk", "казахский" },{ "sv", "шведский" },{ "az", "азербайджанский" },
            { "fi", "финский" },{ "hy", "армянский" },{ "et", "эстонский" },{ "ka", "грузинский" },{ "lt", "латышский" },{ "id", "индонезийский" },{ "lv", "латвийский" },{ "ms", "малайский" },
            { "ur", "урду" },{ "th", "тайский" },{ "bn", "бенгальский" },{ "hi", "хинди" },{ "ta", "тамильский" } };
        
        var shards = new Dictionary<string, List<string>> { { "s1", new List<string>() }, { "s4", new List<string>() },{ "s8", new List<string>() },{ "s6", new List<string>() },{ "s2", new List<string>()},
            { "s7", new List<string>() }, { "s5", new List<string>() },{ "s3", new List<string>() } };//https://noc.wikimedia.org/db.php
        Console.WriteLine(shards.Count);
        var connect = new MySqlConnection("Server=%project%.analytics.db.svc.wikimedia.cloud;Database=%project%_p;Uid=s52321;Pwd=ocienoonaenohrei;".Replace("%project%", "meta"));
        Console.WriteLine(shards.Count);
        connect.Open();
        Console.WriteLine(shards.Count);
        var command = new MySqlCommand("select * from wiki;", connect);
        Console.WriteLine(shards.Count);
        MySqlDataReader r = command.ExecuteReader();
        Console.WriteLine(shards.Count);
        while (r.Read())
            Console.WriteLine(r.GetString("slice") + r.GetString("lang"));
            /*if (shards.ContainsKey(r.GetString("slice").Substring(0, 2)))
                shards[r.GetString("slice").Substring(0, 2)].Add(r.GetString("lang"));*/


        foreach (var shard in shards.Keys)
            foreach (var lang in shards[shard])
                Console.WriteLine(shard + lang);

        //var data = new Dictionary<string, langdata>();
        //foreach (var lang in langnames)
        //    data.Add(lang.Key, new langdata() { name = lang.Value, stats = new Dictionary<string, Dictionary<string, int>>()});
        //foreach (var shard in shards.Keys)
        //    foreach (var lang in langnames.Keys)
        //    {
        //        data[lang].stats = new Dictionary<string, Dictionary<string, int>>
        //    { { "sysop", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "rollbacker", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
        //        { "all", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "0", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
        //        { "1", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "2", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
        //        { "3", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "4", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
        //        { "5", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "6", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
        //        { "7", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "8", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } } };
        //        var processed_users = new HashSet<string>();
        //        connect = new MySqlConnection(creds[2].Replace("%project%", shard));
        //        connect.Open();
        //        foreach (string sex in new string[] { "male", "female" })
        //        {
        //            command = new MySqlCommand("select up_value, ug_group, user_id, user_editcount from user left join user_groups on user.user_id = user_groups.ug_user join user_properties on user.user_id = user_properties.up_user where (up_property = 'gender' and up_value = '" + sex + "');", connect);
        //            command.CommandTimeout = 9999;
        //            r = command.ExecuteReader();
        //            while (r.Read())
        //            {
        //                string id = r.GetString("user_id");
        //                string gender = r.GetString("up_value");
        //                int edits = r.GetInt32("user_editcount");
        //                string flag = r.IsDBNull(r.GetOrdinal("ug_group")) ? "" : r.GetString("ug_group");
        //                if (flag == "sysop")
        //                    data[lang].stats["sysop"][sex]++;
        //                if (flag == "rollbacker")
        //                    data[lang].stats["rollbacker"][sex]++;
        //                if (!processed_users.Contains(id))
        //                {
        //                    data[lang].stats["all"][sex]++;
        //                    if (edits == 0)
        //                        data[lang].stats["0"][sex]++;
        //                    else
        //                        data[lang].stats[edits.ToString().Length.ToString()][sex]++;
        //                    processed_users.Add(id);
        //                }
        //            }
        //            r.Close();
        //        }
        //        connect.Close();
        //    }

        //string result = "{{плавающая шапка таблицы}}<center>Статистика по участникам, выставившим себе мужской/женский пол в настройках вики (выставившие нейтральный игнорируются). Процент - доля мужчин, " +
        //    "во всплывающей подсказке - числа мужчин и женщин в указанном слое.\n{|class=\"standard sortable ts-stickytableheader\"\n!Раздел\\Число правок!!Все!!0!!до 10!!до 100!!до 1к!!до 10к!!до 100к!!" +
        //    "100к+!!Откат!!Админы\n";
        //foreach (var s in data.OrderByDescending(s => s.Value.stats["all"]["male"]))
        //{
        //    Console.WriteLine(data.Count);
        //    result += "\n|-\n|[[:" + s.Key + ":Main Page|" + s.Value.name + "]]";
        //    foreach (string slice in new string[] { "all", "0", "1", "2", "3", "4", "5", "6", "rollbacker", "sysop" })
        //        result += s.Value.stats[slice]["male"] == 0 && s.Value.stats[slice]["female"] == 0 ? "||<ref name=no/>" : "||{{abbr|" + Math.Round(100 * (float)s.Value.stats[slice]["male"] / 
        //            (s.Value.stats[slice]["female"] + s.Value.stats[slice]["male"]), 1) + "%|{{formatnum:" + s.Value.stats[slice]["male"] + "}} м / {{formatnum:" + s.Value.stats[slice]["female"] + "}} ж|0}}";
        //}
        //Save(site, "u:MBH/genders", result + "\n|}{{примечания|refs=<ref name=no>В разделе нет этого флага либо нет участников, имеющих столько правок.</ref>}}", "");
    }
}
