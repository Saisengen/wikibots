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
        string result = "{{плавающая шапка таблицы}}<center>Статистика по участникам, выставившим себе мужской/женский пол в настройках вики (выставившие нейтральный игнорируются). Процент - доля мужчин, " +
            "во всплывающей подсказке - числа мужчин и женщин в указанном слое.\n{|class=\"standard sortable ts-stickytableheader\"\n!Раздел\\Число правок!!Все!!0!!до 10!!до 100!!до 1к!!до 10к!!до 100к!!" +
            "100к+!!Откат!!Админы\n";
        var data = new Dictionary<string, langdata>
        {
            { "simple", new langdata() { name = "простой англ.", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "en", new langdata() { name = "английский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "commons", new langdata() { name = "Викисклад", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "wikidata", new langdata() { name = "Викиданные", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "eo", new langdata() { name = "эсперанто", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "de", new langdata() { name = "немецкий", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "da", new langdata() { name = "датский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "nl", new langdata() { name = "нидерландский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "ar", new langdata() { name = "арабский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "fa", new langdata() { name = "фарси", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "he", new langdata() { name = "иврит", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "tr", new langdata() { name = "турецкий", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "ru", new langdata() { name = "русский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "uk", new langdata() { name = "украинский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "be", new langdata() { name = "белорусский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "fr", new langdata() { name = "французский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "es", new langdata() { name = "испанский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "ca", new langdata() { name = "каталанский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "eu", new langdata() { name = "баскский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "it", new langdata() { name = "итальянский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "pt", new langdata() { name = "португальский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "ja", new langdata() { name = "японский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "zh", new langdata() { name = "китайский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "ko", new langdata() { name = "корейский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "vi", new langdata() { name = "вьетнамский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "el", new langdata() { name = "греческий", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "hu", new langdata() { name = "венгерский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "pl", new langdata() { name = "польский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "cs", new langdata() { name = "чешский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "sk", new langdata() { name = "словацкий", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "sr", new langdata() { name = "сербский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "ro", new langdata() { name = "румынский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "hr", new langdata() { name = "хорватский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "bg", new langdata() { name = "болгарский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "no", new langdata() { name = "норвежский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "kk", new langdata() { name = "казахский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "sv", new langdata() { name = "шведский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "az", new langdata() { name = "азербайджанский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "fi", new langdata() { name = "финский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "hy", new langdata() { name = "армянский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "et", new langdata() { name = "эстонский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "ka", new langdata() { name = "грузинский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "lt", new langdata() { name = "латышский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "id", new langdata() { name = "индонезийский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "lv", new langdata() { name = "латвийский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "ms", new langdata() { name = "малайский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "ur", new langdata() { name = "урду", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "th", new langdata() { name = "тайский", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "bn", new langdata() { name = "бенгальский", stats = new Dictionary<string, Dictionary<string, int>>() } },{ "hi", new langdata() { name = "хинди", stats = new Dictionary<string, Dictionary<string, int>>() } },
            { "ta", new langdata() { name = "тамильский", stats = new Dictionary<string, Dictionary<string, int>>() } }
            };
        foreach (var lang in data.Keys.ToList())
        {
            data[lang].stats = new Dictionary<string, Dictionary<string, int>>
            { { "sysop", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "rollbacker", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
                { "all", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "0", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
                { "1", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "2", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
                { "3", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "4", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
                { "5", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "6", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } },
                { "7", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } }, { "8", new Dictionary<string, int>(){{"male", 0 }, {"female", 0 } } } };
            var processed_users = new HashSet<string> ();
            var connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            foreach (string sex in new string[] {"male", "female" })
            {
                var command = new MySqlCommand("select up_value, ug_group, user_id, user_editcount from user left join user_groups on user.user_id = user_groups.ug_user join user_properties on user.user_id = user_properties.up_user where (up_property = 'gender' and up_value = '" + sex + "');", connect);
                command.CommandTimeout = 9999;
                MySqlDataReader r = command.ExecuteReader();
                while (r.Read())
                {
                    string id = r.GetString("user_id");
                    string gender = r.GetString("up_value");
                    int edits = r.GetInt32("user_editcount");
                    string flag = r.IsDBNull(r.GetOrdinal("ug_group")) ? "" : r.GetString("ug_group");
                    if (flag == "sysop")
                        data[lang].stats["sysop"][sex]++;
                    if (flag == "rollbacker")
                        data[lang].stats["rollbacker"][sex]++;
                    if (!processed_users.Contains(id))
                    {
                        data[lang].stats["all"][sex]++;
                        if (edits == 0)
                            data[lang].stats["0"][sex]++;
                        else
                            data[lang].stats[edits.ToString().Length.ToString()][sex]++;
                        processed_users.Add(id);
                    }
                }
                r.Close();
            }
            connect.Close();
        }
        foreach (var s in data.OrderByDescending(s => s.Value.stats["all"]["male"]))
        {
            Console.WriteLine(data.Count);
            result += "\n|-\n|[[:" + s.Key + ":Main Page|" + s.Value.name + "]]";
            foreach (string slice in new string[] { "all", "0", "1", "2", "3", "4", "5", "6", "rollbacker", "sysop" })
                result += s.Value.stats[slice]["male"] == 0 && s.Value.stats[slice]["female"] == 0 ? "||<ref name=no/>" : "||{{abbr|" + Math.Round(100 * (float)s.Value.stats[slice]["male"] / 
                    (s.Value.stats[slice]["female"] + s.Value.stats[slice]["male"]), 1) + "%|{{formatnum:" + s.Value.stats[slice]["male"] + "}} м / {{formatnum:" + s.Value.stats[slice]["female"] + "}} ж|0}}";
        }
        Save(site, "u:MBH/genders", result + "\n|}{{примечания|refs=<ref name=no>В разделе нет этого флага либо нет участников, имеющих столько правок.</ref>}}", "");
    }
}
