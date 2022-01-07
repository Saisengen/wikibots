using MySql.Data.MySqlClient;
using System.IO;
using System.Collections.Generic;
using System;
using DotNetWikiBot;

class Program
{
    static void Main()
    {
        int numofitemstoanalyze = 150000; //100k is okay, 1m isn't
        var allitems = new Dictionary<string, int>();
        var nonruitems = new Dictionary<string, int>();
        string result = "<center>\n{|class=\"standard\"\n!Страница!!Кол-во интервик";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var connect = new MySqlConnection("Server=wikidatawiki.labsdb;Database=wikidatawiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
        connect.Open();
        var query = new MySqlCommand("select ips_item_id, count(*) cnt from wb_items_per_site group by ips_item_id order by cnt desc limit " + numofitemstoanalyze + ";", connect);
        query.CommandTimeout = 99999;
        MySqlDataReader r = query.ExecuteReader();
        while (r.Read())
            allitems.Add(r.GetString("ips_item_id"), r.GetInt16("cnt"));
        r.Close();
        Console.WriteLine(1);
        foreach (var i in allitems)
        {
            query = new MySqlCommand("select ips_site_page from wb_items_per_site where ips_site_id=\"ruwiki\" and ips_item_id=" + i.Key + ";", connect);
            r = query.ExecuteReader();
            if (!r.Read())
                nonruitems.Add(i.Key, i.Value);
            r.Close();
        }
        Console.WriteLine(2);
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
            //else
            //w.WriteLine("|-\n|[[d:Q" + n.Key + "]]||" + n.Value);
            r.Close();
        }
        result += "\n|}{{Проект:Словники/Шаблон:Списки недостающих статей}}[[Категория:Википедия:Статьи без русских интервик]]";
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var page = new Page("ВП:К созданию/Статьи с наибольшим числом интервик без русской");
        page.Save(result);
    }
}
