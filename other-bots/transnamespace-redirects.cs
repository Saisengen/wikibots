using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using DotNetWikiBot;

class Program
{
    class redir
    {
        public string src, dest, srcns, destns;
        public override string ToString()
        {
            return srcns + ' ' + src + ' ' + destns + ' ' + dest;
        }
    }

    static void Main()
    {
        var redirs = new Dictionary<string, redir>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var nss = new Dictionary<string, string>();

        string apiout = site.GetWebPage("/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces");
        using (var r = new XmlTextReader(new StringReader(apiout)))
        {
            r.WhitespaceHandling = WhitespaceHandling.None;
            while (r.Read())
                if (r.NodeType == XmlNodeType.Element && r.Name == "ns")
                {
                    string id = r.GetAttribute("id");
                    if (id != "0")
                        r.Read();
                    nss.Add(id, r.Value);
                }
        }

        foreach (var n in nss)
        {
            bool end = false; string cont = "", query = "/w/api.php?action=query&list=allredirects&format=xml&arprop=ids%7Ctitle&arnamespace=" + n.Key + "&arlimit=max";
            while (end == false)
            {
                apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&arcontinue=" + Uri.EscapeDataString(cont)));
                using (var rdr = new XmlTextReader(new StringReader(apiout)))
                {
                    rdr.WhitespaceHandling = WhitespaceHandling.None;
                    rdr.Read(); rdr.Read(); rdr.Read(); cont = rdr.GetAttribute("arcontinue");
                    if (cont == null) end = true;
                    while (rdr.Read())
                        if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "r")
                            redirs.Add(rdr.GetAttribute("fromid"), new redir() { dest = rdr.GetAttribute("title"), destns = rdr.GetAttribute("ns") });
                }
            }
        }

        string idset = "";
        int c = 0;
        var requeststrings = new HashSet<string>();
        foreach (var red in redirs)
        {
            idset +=  '|' + red.Key;
            if (++c % 500 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        requeststrings.Add(idset.Substring(1));

        foreach(var q in requeststrings)
        {
            string info = site.GetWebPage("/w/api.php?action=query&prop=info&format=xml&pageids=" + q);
            using (var rdr = new XmlTextReader(new StringReader(info)))
            {
                rdr.WhitespaceHandling = WhitespaceHandling.None;
                while (rdr.Read())
                    if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "page")
                    {
                        var id = rdr.GetAttribute("pageid");
                        if (id == "0") continue;
                        if (!(redirs[id].destns == rdr.GetAttribute("ns")) || redirs[id].destns == "6" || redirs[id].destns == "14")
                            if (!(redirs[id].destns == "3" && rdr.GetAttribute("ns") == "2" &&
                                redirs[id].dest.Substring(redirs[id].dest.IndexOf(":")) == rdr.GetAttribute("title").Substring(rdr.GetAttribute("title").IndexOf(":"))))
                            {
                                redirs[id].srcns = rdr.GetAttribute("ns");
                                redirs[id].src = rdr.GetAttribute("title");
                            }
                    }
            }
        }
        var result = "<center>\n";
        bool flag = false;
        foreach (var n in nss)
        {
            foreach (var r in redirs)
                if (r.Value.srcns == n.Key)
                    flag = true;
            if (flag)
            {
                result += "\n==" + (n.Value == "" ? "Статьи" : n.Value) + "==\n{| class=\"standard\"\n|-\n! Откуда !! Куда";
                foreach (var r in redirs)
                    if (r.Value.srcns == n.Key && r.Value.src != null)
                        result += "\n|-\n|[[:" + r.Value.src + "]]||[[:" + r.Value.dest + "]]";
                result += "\n|}";
            }
            flag = false;
        }
        var p = new Page("u:MBH/incorrect redirects");
        p.Save(result, "", false);
    }
}
