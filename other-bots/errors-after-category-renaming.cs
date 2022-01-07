using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using DotNetWikiBot;
class record
{
    public string oldtitle, newtitle, user, timestamp, comment, title;
}
class Program
{
    static void Main()
    {
        var catnames = new HashSet<string>();
        var table = new List<record>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);

        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&letype=move&lenamespace=14&lelimit=5000"))))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                {
                    bool nonempty = false;
                    string title = r.GetAttribute("title");
                    using (var rr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + Uri.EscapeDataString(title)))))
                    {
                        rr.WhitespaceHandling = WhitespaceHandling.None;
                        while (rr.Read())
                            if (rr.NodeType == XmlNodeType.Element && rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0")
                                nonempty = true;
                    }
                    if (nonempty)
                    {
                        var n = new record { oldtitle = title, user = r.GetAttribute("user"), timestamp = r.GetAttribute("timestamp").Substring(0, 10), comment = r.GetAttribute("comment") };
                        r.Read();
                        n.newtitle = r.GetAttribute("target_title");
                        table.Add(n);
                    }
                }
        }
        string result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Откуда (страниц в категории)!!Куда (страниц в категории)!!Юзер!!Коммент";
        foreach (var t in table)
            result += "\n|-\n|" + t.timestamp + "||[[:" + t.oldtitle + "]] ({{PAGESINCATEGORY:" + t.oldtitle.Substring(10) + "}})||[[:" + t.newtitle + "]] ({{PAGESINCATEGORY:" + t.newtitle.Substring(10) +
                "}})||[[u:" + t.user + "]]||" + t.comment;
        result += "\n|}";
        var p = new Page("u:MBH/Переименованные категории с недоперенесёнными страницами");
        p.Save(result, "", false);
        catnames.Clear();
        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp%7Ccomment%7Cdetails&leaction=delete/delete&lenamespace=14&lelimit=5000"))))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                {
                    bool nonempty = false;
                    string title = r.GetAttribute("title");
                    using (var rr = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=categoryinfo&titles=" + Uri.EscapeDataString(title)))))
                    {
                        rr.WhitespaceHandling = WhitespaceHandling.None;
                        while (rr.Read())
                            if (rr.NodeType == XmlNodeType.Element && rr.Name == "page" && rr.GetAttribute("missing") != null)
                            {
                                rr.Read();
                                if (rr.Name == "categoryinfo" && rr.GetAttribute("size") != "0")
                                    nonempty = true;
                            }
                    }
                    if (nonempty)
                        try
                        {
                            var n = new record { title = title, user = r.GetAttribute("user"), timestamp = r.GetAttribute("timestamp").Substring(0, 10) };
                            string comment = r.GetAttribute("comment").Replace("[[К", "[[:К");
                            n.comment = (comment.Contains("}}") ? "<nowiki>" + comment + "</nowiki>" : comment);
                            table.Add(n);
                        }
                        catch
                        {
                            continue;
                        }
                }
        }
        result = "{{Плавающая шапка таблицы}}<center>\n{|class=\"standard sortable ts-stickytableheader\"\n!Таймстамп!!Имя (страниц в категории)!!Юзер!!Коммент";
        foreach (var t in table)
            try
            {
                result += "\n|-\n|" + t.timestamp + "||[[:" + t.title + "]] ({{PAGESINCATEGORY:" + t.title.Substring(10) + "}})||[[u:" + t.user + "]]||" + t.comment;
            }
            catch
            {
                continue;
            }
        result += "\n|}";
        p = new Page("u:MBH/Удалённые категории со страницами");
        p.Save(result, "", false);
    }
}
