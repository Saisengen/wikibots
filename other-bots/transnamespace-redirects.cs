using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Net.Http;
using System.Net;

class Program
{
    static bool sameuser(string s1, string s2)
    {
        if (s1.Contains(":"))
            s1 = s1.Substring(s1.IndexOf(':'));
        if (s2.Contains(":"))
            s2 = s2.Substring(s2.IndexOf(':'));
        if (s1.Contains("/"))
            s1 = s1.Substring(0, s1.IndexOf('/'));
        if (s2.Contains("/"))
            s2 = s2.Substring(0, s2.IndexOf('/'));
        if (s1 == s2) return true;
        return false;
    }
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
        public string src_title, dest_title, src_ns, dest_ns;
        public override string ToString()
        {
            return src_ns + ' ' + src_title + ' ' + dest_ns + ' ' + dest_title;
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
                    //if (id != "0")
                        r.Read();
                    nss.Add(id, r.Value);
                }
            nss.Remove("-2");
            nss.Remove("-1");
        }

        foreach (var current_target_ns in nss)
        {
            string cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&list=allredirects&format=xml&arprop=ids%7Ctitle&arnamespace=" + current_target_ns.Key + "&arlimit=500";//NOT 5000
            while (cont != null)
            {
                var temp = new Dictionary<string, redir>();
                string idset = "";
                apiout = (cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arcontinue=" + Uri.EscapeDataString(cont)).Result);
                using (var rdr = new XmlTextReader(new StringReader(apiout)))
                {
                    rdr.WhitespaceHandling = WhitespaceHandling.None;
                    rdr.Read(); rdr.Read(); rdr.Read(); cont = rdr.GetAttribute("arcontinue");
                    while (rdr.Read())
                        if (rdr.Name == "r")
                        {
                            idset += '|' + rdr.GetAttribute("fromid");
                            temp.Add(rdr.GetAttribute("fromid"), new redir() { dest_title = rdr.GetAttribute("title"), dest_ns = rdr.GetAttribute("ns") });
                        }
                }
                if (idset.Length != 0)
                    idset = idset.Substring(1);

                using (var rdr = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=info&format=xml&pageids=" + idset).Result)))
                {
                    rdr.WhitespaceHandling = WhitespaceHandling.None;
                    while (rdr.Read())
                        if (rdr.Name == "page")
                        {
                            var id = rdr.GetAttribute("pageid");
                            if (temp[id].dest_ns != rdr.GetAttribute("ns") || temp[id].dest_ns == "6" || temp[id].dest_ns == "14")
                                if (!(sameuser(rdr.GetAttribute("title"), temp[id].dest_title) && ((temp[id].dest_ns == "3" && rdr.GetAttribute("ns") == "2") || (temp[id].dest_ns == "2" && rdr.GetAttribute("ns") == "3"))))//если не редиректы между ЛС и СО одного участника
                                    redirs.Add(id, new redir() { src_ns = rdr.GetAttribute("ns"), src_title = rdr.GetAttribute("title"), dest_ns = temp[id].dest_ns, dest_title = temp[id].dest_title });
                        }
                }
            }
        }

        var result = "<center>\n";
        bool flag = false;
        foreach (var n in nss)
        {
            foreach (var r in redirs)
                if (r.Value.src_ns == n.Key)
                {
                    flag = true;
                    break;
                }
                    
            if (flag)
            {
                result += "\n==" + (n.Value == "" ? "Статьи" : n.Value) + "==\n{| class=\"standard\"\n|-\n!Откуда!!Куда";
                foreach (var r in redirs)
                    if (r.Value.src_ns == n.Key && r.Value.src_title != null)
                        result += "\n|-\n|[[:" + r.Value.src_title + "]]||[[:" + r.Value.dest_title + "]]";
                result += "\n|}";
            }
            flag = false;
        }
        Save(site, "u:MBH/incorrect redirects", result, "");
    }
}
