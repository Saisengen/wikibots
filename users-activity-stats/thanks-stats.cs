using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using DotNetWikiBot;
using System;
class Program
{
    static void Main()
    {
        var pairs = new Dictionary<string, int>();
        var thankedusers = new Dictionary<string, int>();
        var thankingusers = new Dictionary<string, int>();
        var ratio = new Dictionary<string, double>();
        string apiout, cont = "", query = "/w/api.php?action=query&list=logevents&format=xml&leprop=title%7Cuser%7Ctimestamp&letype=thanks&lelimit=max";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);

        while (cont != null)
        {
            if (cont == "") apiout = site.GetWebPage(query); else apiout = site.GetWebPage(query + "&lecontinue=" + cont);
            using (var rdr = new XmlTextReader(new StringReader(apiout)))
            {
                rdr.WhitespaceHandling = WhitespaceHandling.None;
                rdr.Read(); rdr.Read(); rdr.Read(); cont = rdr.GetAttribute("lecontinue");
                while (rdr.Read())
                    if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "item")
                    {
                        string source = rdr.GetAttribute("user");
                        if (source == "Erokhin")
                            continue;
                        string target = rdr.GetAttribute("title");
                        if (target != null && source != null)
                        {
                            if (thankingusers.ContainsKey(source))
                                thankingusers[source]++;
                            else
                                thankingusers.Add(source, 1);
                            target = target.Substring(target.IndexOf(":") + 1);
                            if (thankedusers.ContainsKey(target))
                                thankedusers[target]++;
                            else
                                thankedusers.Add(target, 1);
                            string pair = source + " → " + target;
                            if (pairs.ContainsKey(pair))
                                pairs[pair]++;
                            else
                                pairs.Add(pair, 1);
                        }
                    }
            }
        }
        int c1 = 0, c2 = 0;
        string result = "{{Плавающая шапка таблицы}}<center>См. также https://mbh.toolforge.org/likes.cgi\n{|\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!Позиция!!Участник!!Число выданных лайков";
        foreach (var p in thankingusers.OrderByDescending(p => p.Value))
            if (p.Value >= 25)
                result += "\n|-\n|" + ++c1 + "||{{u|" + p.Key + "}}||" + p.Value;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!Направление!!Число лайков";
        foreach (var p in pairs.OrderByDescending(p => p.Value))
            if (p.Value >= 20)
                result += "\n|-\n|" + p.Key + "||" + p.Value;
        result += "\n|}\n|valign=top|\n{|class=\"standard ts-stickytableheader\"\n!Позиция!!Участник!!Число полученных лайков";
        foreach (var p in thankedusers.OrderByDescending(p => p.Value))
            if (p.Value >= 32)
                result += "\n|-\n|" + ++c2 + "||{{u|" + p.Key + "}}||" + p.Value;
        result += "\n|}\n|}";
        var page = new Page("u:MBH/Лайки");
        page.Save(result);
    }
}
