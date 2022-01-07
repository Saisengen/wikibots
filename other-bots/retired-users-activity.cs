using System;
using System.Collections.Generic;
using System.Linq;
using DotNetWikiBot;
using System.Xml;
using System.IO;

class Program
{
    static void Main()
    {
        var retireds = new Dictionary<string, int>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var initialusers = site.GetWebPage("https://ru.wikipedia.org/wiki/User:MBH/users_for_last_activity_day_stats?action=raw").Split('\n');
        foreach (var user in initialusers)
            if (!retireds.ContainsKey(user))
                retireds.Add(user, 1);
        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=embeddedin&eititle=Шаблон:Участник покинул проект&einamespace=2%7C3&eilimit=max"))))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ei")
                {
                    string user = r.GetAttribute("title");
                    if (!user.Contains("/"))
                        user = user.Substring(user.IndexOf(':') + 1, user.Length - user.IndexOf(':') - 1);
                    else
                        user = user.Substring(user.IndexOf(':') + 1, user.IndexOf("/") - user.IndexOf(':') - 1);
                    if (!retireds.ContainsKey(user))
                        retireds.Add(user, 1);
                }
        }

        foreach (var u in retireds.Keys.ToList())
            using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucuser=" + Uri.EscapeDataString(u)))))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.NodeType == XmlNodeType.Element && r.Name == "item")
                    {
                        string ts = r.GetAttribute("timestamp");
                        int y = Convert.ToInt32(ts.Substring(0, 4));
                        int m = Convert.ToInt32(ts.Substring(5, 2));
                        int d = Convert.ToInt32(ts.Substring(8, 2));
                        retireds[u] = (DateTime.Now - new DateTime(y, m, d)).Days;
                    }
            }

        string result = "{{#switch: {{{1}}}\n";
        foreach (var r in retireds.OrderBy(r => r.Value))
            result += "| " + r.Key + " = " + r.Value + "\n";
        result += "| 0 }}";
        var p = new Page("Шаблон:Участник покинул проект/days");
        p.Save(result, "", false);
    }
}
