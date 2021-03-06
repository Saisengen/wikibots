using System;
using System.Collections.Generic;
using DotNetWikiBot;
using System.Linq;
using System.Xml;
using System.IO;
using System.Net;
using System.Text;
struct wiki
{
    public long articles, admins, users, pages, files, activeusers, edits;
}
class Program
{
    static void Main()
    {
        var duplcodes = new string[] { "be-x-old", "yue", "zh-tw", "nb", "nan", "lzh" };
        var wikis = new Dictionary<string, wiki>();
        var codes = new HashSet<string>();
        string result = "local info = {\n";
        long tarticles = 0, tusers = 0, tadmins = 0, tactiveusers = 0, tedits = 0, tfiles = 0, tpages = 0;
        var l = new WebClient();
        using (var r = new XmlTextReader(new StringReader(Encoding.UTF8.GetString(l.DownloadData("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=siteinfo&siprop=interwikimap")))))
            while (r.Read())
                if (r.Name == "iw" && r.GetAttribute("language") != null)
                {
                    string code = r.GetAttribute("prefix");
                    if (!duplcodes.Contains(code))
                        codes.Add(code);
                }
        foreach (var c in codes)
        {
            try
            {
                using (var r = new XmlTextReader(new StringReader(l.DownloadString("https://" + c + ".wikipedia.org/w/api.php?action=query&format=xml&meta=siteinfo&siprop=statistics"))))
                    while (r.Read())
                        if (r.Name == "statistics")
                        {
                            long articles = Convert.ToInt64(r.GetAttribute("articles"));
                            long admins = Convert.ToInt64(r.GetAttribute("admins"));
                            long users = Convert.ToInt64(r.GetAttribute("users"));
                            long pages = Convert.ToInt64(r.GetAttribute("pages"));
                            long edits = Convert.ToInt64(r.GetAttribute("edits"));
                            long activeusers = Convert.ToInt64(r.GetAttribute("activeusers"));
                            long files = Convert.ToInt64(r.GetAttribute("images"));
                            wikis.Add(c, new wiki { activeusers = activeusers, admins = admins, articles = articles, edits = edits, files = files, pages = pages, users = users });
                            tarticles += articles;
                            tadmins += admins;
                            tusers += users;
                            tpages += pages;
                            tedits += edits;
                            tactiveusers += activeusers;
                            tfiles += files;
                        }
            }
            catch
            {
                continue;
            }
        }
        int n = 0;
        foreach (var w in wikis.OrderByDescending(w => w.Value.articles))
        {
            result += "['" + w.Key + "'] = { pos = " + ++n + ", activeusers = " + w.Value.activeusers + ", admins = " + w.Value.admins + ", articles = " + w.Value.articles + ", edits = " + w.Value.edits +
                ", files = " + w.Value.files + ", pages = " + w.Value.pages + ", users = " + w.Value.users + ", depth = " + (w.Value.pages != 0 && w.Value.articles != 0 ?
                (w.Value.edits / (float)w.Value.pages) * ((w.Value.pages - w.Value.articles) / (float)w.Value.articles) * ((w.Value.pages - w.Value.articles) / (float)w.Value.articles)
                : 0).ToString().Replace(",", ".") + " },\n";
        }
        result += "['total'] = { activeusers = " + tactiveusers + ", admins = " + tadmins + ", articles = " + tarticles + ", edits = " + tedits + ", files = " + tfiles + ", pages = " + tpages +
            ", users = " + tusers + ", depth = " + (tpages != 0 && tarticles != 0 ? (tedits / (float)tpages) * ((tpages - tarticles) / (float)tarticles) * ((tpages - tarticles) / (float)tarticles)
            : 0).ToString().Replace(",", ".") + ", date = '@" + (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds + "' },\n}\nreturn info";
        foreach (var lang in new HashSet<string>() { "ru", "uk", "be", "uz" })
        {
            var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
            var site = new Site("https://" + lang + ".wikipedia.org", creds[0], creds[1]);
            if (DateTime.Now.Hour == 1 || DateTime.Now.Hour == 0)
            {
                Page d = new Page(site, "Module:NumberOf/today");
                d.Save(result, "", true);
            }
            Page h = new Page(site, "Module:NumberOf/data");
            h.Save(result, "", true);
        }
    }
}
