using MySql.Data.MySqlClient;
using System.IO;
using System.Collections.Generic;
using System;
using System.Net.Http;
using System.Net;
using System.Xml;

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
        int numofitemstoanalyze = 150000; //100k is okay, 1m isn't
        var allitems = new Dictionary<string, int>();
        var nonruitems = new Dictionary<string, int>();
        string result = "<center>\n{|class=\"standard\"\n!Страница!!Кол-во интервик";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var connect = new MySqlConnection(creds[2].Replace("%project%", "wikidatawiki"));
        connect.Open();
        var query = new MySqlCommand("select ips_item_id, count(*) cnt from wb_items_per_site group by ips_item_id order by cnt desc limit " + numofitemstoanalyze + ";", connect);
        query.CommandTimeout = 99999;
        MySqlDataReader r = query.ExecuteReader();
        while (r.Read())
            allitems.Add(r.GetString("ips_item_id"), r.GetInt16("cnt"));
        r.Close();
        foreach (var i in allitems)
        {
            query = new MySqlCommand("select ips_site_page from wb_items_per_site where ips_site_id=\"ruwiki\" and ips_item_id=" + i.Key + ";", connect);
            r = query.ExecuteReader();
            if (!r.Read())
                nonruitems.Add(i.Key, i.Value);
            r.Close();
        }
        foreach (var n in nonruitems)
        {
            query = new MySqlCommand("select cast(ips_site_page as char) title from wb_items_per_site where ips_site_id=\"enwiki\" and ips_item_id=" + n.Key + ";", connect);
            r = query.ExecuteReader();
            if (r.Read())
            {
                string title = r.GetString(0);
                if (!title.StartsWith("Template:") && !title.StartsWith("Category:") && !title.StartsWith("Module:") && !title.StartsWith("Wikipedia:") && !title.StartsWith("Help:") && !title.StartsWith("Portal:"))
                    result += "\n|-\n|[[:en:" + title + "]]||" + n.Value;
            }
            r.Close();
        }
        var site = Site(creds[0], creds[1]);
        Save(site, "ВП:К созданию/Статьи с наибольшим числом интервик без русской", result + "\n|}{{Проект:Словники/Шаблон:Списки недостающих статей}}[[Категория:Википедия:Статьи без русских интервик]]", "");
    }
}
