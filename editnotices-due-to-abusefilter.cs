using System;
using System.Collections.Generic;
using DotNetWikiBot;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
class Program
{
    static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var rgx = new Regex(@"equals_to_any\(page_prefixedtitle,\s*(.*)\s*\)\\?r?\\n&");
        string[] separstrings = { "','", "' ,'", "' , '", "', '" };
        var rawfilterpages = rgx.Match(site.GetWebPage("/w/api.php?action=query&format=json&list=abusefilters&utf8=1&abfstartid=146&abflimit=1&abfprop=pattern")).Groups[1].ToString().Split(separstrings, StringSplitOptions.RemoveEmptyEntries);
        var pagesinfilter = new HashSet<string>();
        for (int i = 0; i < rawfilterpages.Length; i++)
        {
            if (i == 0)
                pagesinfilter.Add(rawfilterpages[i].Substring(1).Replace('/', '-'));
            else if (i == rawfilterpages.Length - 1)
                pagesinfilter.Add(rawfilterpages[i].Substring(0, rawfilterpages[i].Length - 1).Replace('/', '-'));
            else pagesinfilter.Add(rawfilterpages[i].Replace('/', '-'));
        }
        var pageswithtemplate = new HashSet<string>();
        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=embeddedin&eititle=Шаблон:Editnotice/АПАТ&eilimit=max"))))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.Name == "ei" && r.GetAttribute("title").Contains("MediaWiki:Editnotice-0-"))
                    pageswithtemplate.Add(r.GetAttribute("title").Replace("MediaWiki:Editnotice-0-", ""));
        }
        foreach(var f in pagesinfilter)
            if (!pageswithtemplate.Contains(f))
            {
                var page = new Page("MediaWiki:Editnotice-0-" + f);
                if(page.Exists())
                {
                    page.Load();
                    page.Save(page.text + "\n{{Editnotice/АПАТ}}", "статья защищена до апатов [[special:abusefilter/146|146-м фильтром]]", false);
                }
                else
                    page.Save("{{Editnotice/АПАТ}}", "статья защищена до апатов [[special:abusefilter/146|146-м фильтром]]", false);
            }
        foreach(var t in pageswithtemplate)
            if (!pagesinfilter.Contains(t))
            {
                var page = new Page("MediaWiki:Editnotice-0-" + t);
                page.Load();
                string newtext = page.text.Replace("{{Editnotice/АПАТ}}", "");
                if (newtext == "")
                    page.Save("{{#ifeq:{{NAMESPACENUMBER}}|8|{{db|нотис не нужен, статья удалена из фильтра}}}}", "статья удалена из [[special:abusefilter/146|146-го фильтра]]", false);
                else
                    page.Save(newtext, "статья удалена из [[special:abusefilter/146|146-го фильтра]]", false);
            }
    }
}
