using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using DotNetWikiBot;
class record
{
    public string oldtitle, oldns, newtitle, user, date, comment;
}
class Program
{
    static void Main()
    {
        var table = new List<record>();
        var apatusers = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        string apiout = site.GetWebPage("/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Ctype%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&letype=move&lelimit=max");
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                {
                    string user = r.GetAttribute("user");
                    if (user == "Dibоt")
                        continue;
                    if (!apatusers.Contains(user))
                        using (var rr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=users&usprop=rights&ususers=" + user))))
                            while (rr.Read())
                                if (rr.Value == "autoreview")
                                    apatusers.Add(user);
                    string oldns = r.GetAttribute("ns");
                    if (oldns == "0")
                        continue;
                    string oldtitle = r.GetAttribute("title");
                    string date = r.GetAttribute("timestamp").Substring(5, 5);
                    string comment = r.GetAttribute("comment");
                    r.Read();
                    string newns = r.GetAttribute("target_ns");
                    if (newns != "0")
                        continue;
                    string newtitle = r.GetAttribute("target_title");
                    table.Add(new record() { oldtitle = oldtitle, oldns = oldns, newtitle = newtitle, user = user, date = date, comment = comment });
                }
        }
        string result = "<center>{{Плавающая шапка таблицы}}Красным выделены неавтопатрулируемые.{{shortcut|ВП:TRANSMOVE}}{{clear}}\n{|class=\"standard sortable ts-stickytableheader\"\n!Дата!!Источник!!Название в ОП!!Переносчик!!Коммент";
        foreach (var t in table)
        {
            string comment;
            if (t.comment.Contains("{|") || t.comment.Contains("|}") || t.comment.Contains("||") || t.comment.Contains("|-"))
                comment = "<nowiki>" + t.comment + "</nowiki>";
            else
                comment = t.comment;
            result += "\n|-" + (apatusers.Contains(t.user) ? "" : "style=\"background-color:#fcc\"") + "\n|" + t.date + "||[[:" + t.oldtitle + "|" + t.oldtitle + "]]||[[:" + t.newtitle + "]]||{{u|" + t.user + "}}||" + comment;
        }
        result += "\n|}";
        var p = new Page("Википедия:Страницы, перенесённые в пространство статей");
        p.Save(result);
    }
}
