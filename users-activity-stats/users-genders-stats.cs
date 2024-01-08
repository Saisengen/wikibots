using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Xml;

class sliceinfo
{
    public int male, female;
}
class record
{
    public string flag, gender, id;
    public int edits;
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
        string result = "<center>";
        var langs = new Dictionary<string, string>() {
            { "en", "английский" },{ "simple", "простой английский" },{ "commons", "викисклад" },{ "eo", "эсперанто" },
            { "de", "немецкий" },{ "da", "датский" },{ "nl", "нидерландский" },
            { "fr", "французский" },{ "es", "испанский" },{ "ca", "каталанский" },{ "eu", "баскский" },{ "it", "итальянский" },{ "pt", "португальский" },
            { "ja", "японский" },{ "zh", "китайский" },{ "ko", "корейский" },{ "vi", "вьетнамский" },
            { "ru", "русский" },{ "uk", "украинский" },{ "be", "белорусский" },{ "pl", "польский" },{ "cs", "чешский" },{ "sk", "словацкий" },{ "sr", "сербский" },{ "hr", "хорватский" },{ "ro", "румынский" },{ "bg", "болгарский" },
            { "kk", "казахский" },{ "az", "азербайджанский" },{ "hy", "армянский" },{ "ka", "грузинский" },
            { "ar", "арабский" },{ "fa", "персидский" },{ "he", "иврит" },{ "tr", "турецкий" },
            { "no", "норвежский (букмол)" },{ "sv", "шведский" },{ "fi", "финский" },{ "et", "эстонский" },{ "lt", "литовский" },{ "lv", "латышский" },
            { "id", "индонезийский" },{ "ms", "малайский" },{ "th", "тайский" },{ "hi", "хинди" },{ "ur", "урду" },{ "bn", "бенгальский" },{ "ta", "тамильский" },
            { "el", "греческий" },{ "hu", "венгерский" }
        };
        foreach (var lang in langs.Keys)
        {
            Console.WriteLine(lang);
            result += "\n==" + langs[lang] + "==\n";
            var slices = new Dictionary<string, sliceinfo>
            {
                { "sysop", new sliceinfo() },
                { "rollbacker", new sliceinfo() },
                { "all", new sliceinfo() },
                { "0", new sliceinfo() },
                { "1-3", new sliceinfo() },
                { "3-10", new sliceinfo() },
                { "10-30", new sliceinfo() },
                { "30-100", new sliceinfo() },
                { "100-300", new sliceinfo() },
                { "300-1k", new sliceinfo() },
                { "1k-3k", new sliceinfo() },
                { "3k-10k", new sliceinfo() },
                { "10k-30k", new sliceinfo() },
                { "30k-100k", new sliceinfo() },
                { "100k+", new sliceinfo() }
            };
            var userdata = new List<record> ();
            var processed_users = new HashSet<string> ();
            var connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            var command = new MySqlCommand("select up_value, ug_group, user_id, user_editcount from user left join user_groups on user.user_id = user_groups.ug_user join user_properties on user.user_id = user_properties.up_user where (up_property = 'gender' and up_value = 'male');", connect);
            command.CommandTimeout = 9999;
            MySqlDataReader r = command.ExecuteReader();
            while (r.Read())
                userdata.Add(new record() { id = r.GetString("user_id"), gender = r.GetString("up_value"), edits = r.GetInt32("user_editcount"), flag = r.IsDBNull(r.GetOrdinal("ug_group")) ? "" : r.GetString("ug_group") });
            foreach (var user in userdata)
            {
                if (user.flag == "sysop")
                    slices["sysop"].male++;
                if (user.flag == "rollbacker")
                    slices["rollbacker"].male++;
                if (!processed_users.Contains(user.id))
                {
                    slices["all"].male++;
                    if (user.edits == 0)
                        slices["0"].male++;
                    else if (user.edits <= 3)
                        slices["1-3"].male++;
                    else if (user.edits <= 10)
                        slices["3-10"].male++;
                    else if (user.edits <= 30)
                        slices["10-30"].male++;
                    else if (user.edits <= 100)
                        slices["30-100"].male++;
                    else if (user.edits <= 300)
                        slices["100-300"].male++;
                    else if (user.edits <= 1000)
                        slices["300-1k"].male++;
                    else if (user.edits <= 3000)
                        slices["1k-3k"].male++;
                    else if (user.edits <= 10000)
                        slices["3k-10k"].male++;
                    else if (user.edits <= 30000)
                        slices["10k-30k"].male++;
                    else if (user.edits <= 100000)
                        slices["30k-100k"].male++;
                    else slices["100k+"].male++;
                    processed_users.Add(user.id);
                }
            }
            r.Close();

            command = new MySqlCommand("select up_value, ug_group, user_id, user_editcount from user left join user_groups on user.user_id = user_groups.ug_user join user_properties on user.user_id = user_properties.up_user where (up_property = 'gender' and up_value = 'female');", connect);
            r = command.ExecuteReader();
            userdata.Clear();
            processed_users.Clear();
            while (r.Read())
                userdata.Add(new record() { id = r.GetString("user_id"), gender = r.GetString("up_value"), edits = r.GetInt32("user_editcount"), flag = r.IsDBNull(r.GetOrdinal("ug_group")) ? "" : r.GetString("ug_group") });
            foreach (var user in userdata)
            {
                if (user.flag == "sysop")
                    slices["sysop"].female++;
                if (user.flag == "rollbacker")
                    slices["rollbacker"].female++;
                if (!processed_users.Contains(user.id))
                {
                    slices["all"].female++;
                    if (user.edits == 0)
                        slices["0"].female++;
                    else if (user.edits <= 3)
                        slices["1-3"].female++;
                    else if (user.edits <= 10)
                        slices["3-10"].female++;
                    else if (user.edits <= 30)
                        slices["10-30"].female++;
                    else if (user.edits <= 100)
                        slices["30-100"].female++;
                    else if (user.edits <= 300)
                        slices["100-300"].female++;
                    else if (user.edits <= 1000)
                        slices["300-1k"].female++;
                    else if (user.edits <= 3000)
                        slices["1k-3k"].female++;
                    else if (user.edits <= 10000)
                        slices["3k-10k"].female++;
                    else if (user.edits <= 30000)
                        slices["10k-30k"].female++;
                    else if (user.edits <= 100000)
                        slices["30k-100k"].female++;
                    else slices["100k+"].female++;
                    processed_users.Add(user.id);
                }
            }

            result += "\n{|class=\"standard sortable\"\n!Число правок!!Мужчин!!Женщин!!Доля мужчин";
            foreach (var s in slices.OrderByDescending(s => s.Value.male))
                result += "\n|-\n|" + s.Key + "||" + s.Value.male + "||" + s.Value.female + "||" + (100 * (float)s.Value.male / (s.Value.male + s.Value.female)) + "%";
            result += "\n|}";
            connect.Close();
        }
        Save(site, "u:MBH/genders", result, "");
    }
}
