using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using System.IO;
using System.Xml;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;

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
        var globalusers = new HashSet<string>();
        var globalusers_needs_flag = new HashSet<string>();
        var apats = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        var connect = new MySqlConnection(creds[2].Replace("%lang%", "ru"));
        connect.Open();
        MySqlCommand command;
        MySqlDataReader r;

        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"editor\" or ug_group = \"autoreview\";", connect) { CommandTimeout = 99999 };
        r = command.ExecuteReader();
        while (r.Read())
            apats.Add(r.GetString(0));
        r.Close();

        connect = new MySqlConnection("Server=commonswiki.labsdb;Database=commonswiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
        connect.Open();
        command = new MySqlCommand("select cast(user_name as char) user from user_groups join user on user_id = ug_user where ug_group = \"filemover\";", connect) { CommandTimeout = 99999 };
        r = command.ExecuteReader();
        while (r.Read())
            globalusers.Add(r.GetString(0));
        r.Close();

        using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://meta.wikimedia.org/w/api.php?action=query&format=xml&list=globalallusers&agugroup=global-rollbacker&agulimit=max").Result)))
            while (rdr.Read())
                if (rdr.Name == "globaluser")
                    if (!globalusers.Contains(rdr.GetAttribute("name")))
                        globalusers.Add(rdr.GetAttribute("name"));

        globalusers.ExceptWith(apats);

        var lastmonth = DateTime.Now.AddMonths(-1);
        foreach (var mover in globalusers)
            using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=max&ucend=" + lastmonth.ToString("yyyy-MM-dd") + "T00:00:00.000Z&ucprop=comment&ucuser=" + Uri.EscapeDataString(mover)).Result)))
                while (rdr.Read())
                    if (rdr.Name == "item" && rdr.GetAttribute("comment") != null)
                        if (rdr.GetAttribute("comment").Contains("GR]"))
                        {
                            globalusers_needs_flag.Add(mover);
                            break;
                        }

        if (globalusers_needs_flag.Count > 0)
        {
            string zkatext = site.GetStringAsync("https://ru.wikipedia.org/wiki/ВП:Запросы к администраторам?action=raw").Result;
            var header = new Regex(@"(^\{[^\n]*\}\s*<[^>]*>\n)");
            string newmessage = "==Выдать апата глобальным правщикам==\nПеречисленные ниже участники занимаются переименованием файлов на Викискладе с заменой включений во всех разделах. В соответствии с [[ВП:ПАТ#ГЛОБ]] прошу рассмотреть их вклад и выдать им апата, чтобы такие правки не распатрулировали страницы.";
            foreach (var mover in globalusers_needs_flag)
                newmessage += "\n* [[special:contribs/" + mover + "|" + mover + "]]";
            newmessage += "\n~~~~\n\n";
            if (header.IsMatch(zkatext))
                Save(site, "ВП:Запросы к администраторам", header.Replace(zkatext, "$1" + "\n\n" + newmessage), "новые переименовывающие для выдачи апата");
            else
                Save(site, "ВП:Запросы к администраторам", newmessage + zkatext, "новые переименовывающие для выдачи апата");
        }
        
    }
}
