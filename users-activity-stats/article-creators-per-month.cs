using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.IO;
using DotNetWikiBot;

class Program
{
    static void Main()
    {
        var articleids = new HashSet<string>();
        var redirs = new HashSet<string>();
        var creds = new StreamReader("p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var lastmonth = DateTime.Now.AddMonths(-1);
        var creators = new Dictionary<string, int>();
        string cont = "", query = "/w/api.php?action=query&format=xml&list=logevents&leprop=ids&letype=create&leend=" + lastmonth.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lestart=" + DateTime.Now.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lenamespace=0&lelimit=5000";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&lecontinue=" + cont));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item")
                        if (!articleids.Contains(r.GetAttribute("pageid")))
                            articleids.Add(r.GetAttribute("pageid"));
            }
        }
        var requeststrings = new HashSet<string>();
        string idset = ""; int cntr = 0;
        foreach (var i in articleids)
        {
            idset += "|" + i;
            if (++cntr % 500 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        if (idset.Length != 0)
            requeststrings.Add(idset.Substring(1));

        foreach(var s in requeststrings)
            using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&prop=info&pageids=" + s))))
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("redirect") == "")
                        redirs.Add(r.GetAttribute("pageid"));

        cont = ""; query = "/w/api.php?action=query&format=xml&list=logevents&leprop=ids|user&letype=create&leend=" + lastmonth.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lestart=" + DateTime.Now.ToString("yyyy-MM") + "-01T00%3A00%3A00.000Z&lenamespace=0&lelimit=5000";
        while (cont != null)
        {
            string apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&lecontinue=" + cont));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item" && !redirs.Contains(r.GetAttribute("pageid")))
                    {
                        string user = r.GetAttribute("user");
                        if (creators.ContainsKey(user))
                            creators[user]++;
                        else
                            creators.Add(user, 1);
                    }
            }
        }
        string result = "<center>\n{|class=\"standard\"\n!Участник!!Создал статей за последний месяц";
        foreach (var p in creators.OrderByDescending(p => p.Value))
        {
            if (p.Value < 10) break;
            result += "\n|-\n|[[u:" + p.Key + "]]||" + p.Value;
        }
        var pg = new Page("u:MBH/best article creators");
        pg.Save(result + "\n|}");
    }
}
