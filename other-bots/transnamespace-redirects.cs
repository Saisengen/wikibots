using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;
using static System.Net.WebRequestMethods;

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
        var site = Site(creds[0], creds[1]);
        var nss = new Dictionary<string, string>();

        string apiout = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=siteinfo&format=xml&siprop=namespaces").Result;
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
            bool end = false; string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allredirects&format=xml&arprop=ids%7Ctitle&arnamespace=" + n.Key + "&arlimit=max";
            while (end == false)
            {
                apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arcontinue=" + Uri.EscapeDataString(cont)).Result);
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
            string info = site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=info&format=xml&pageids=" + q).Result;
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
        Save(site, "u:MBH/incorrect redirects", result, "");
    }
}
