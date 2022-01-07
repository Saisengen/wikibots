using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Web;
using System.Net;
using System.Xml;
using System.IO;
using System.Security;

class Program
{
    static string lang = "ru.wikipedia";
    static string category = "";
    static string page = "";
    static readonly Dictionary<string, List<string>> upcats = new Dictionary<string, List<string>>();
    static readonly List<string> path = new List<string>();
    static readonly WebClient cl = new WebClient();

    static List<List<T>> SplitList<T>(List<T> me, int size = 50)
    {
        var list = new List<List<T>>();
        for (int i = 0; i < me.Count; i += size)
            list.Add(me.GetRange(i, Math.Min(size, me.Count - i)));
        return list;
    }

    static List<string> Processupcats(List<string> layer)
    { // batch gets categories of the given set of pages
        var result = new List<string>();
        foreach (var pages in SplitList(layer))
        {
            var pgs = string.Join("|", pages);
            string clcontinue = "";
            NameValueCollection query = new NameValueCollection()
            {
                {"action", "query"},
                {"prop", "categories"},
                {"format", "xml"},
                {"cllimit", "500"}
            };
            do
            {
                var uri = "https://" + Uri.EscapeDataString(lang) + ".org/w/api.php";
                query.Set("titles", pgs);
                if (clcontinue != "")
                {
                    query.Set("clcontinue", clcontinue);
                }
                byte[] rawapiout = cl.UploadValues(uri, "POST", query);
                clcontinue = "";
                string apiout = Encoding.UTF8.GetString(rawapiout);
                string page = "";
                using (var r = new XmlTextReader(new StringReader(apiout)))
                    while (r.Read())
                        if (r.Name == "page")
                            page = r.GetAttribute("title");
                        else if (r.Name == "cl")
                        {
                            var title = r.GetAttribute("title");
                            upcats[page].Add(title);
                            if (!upcats.ContainsKey(title))
                            {
                                result.Add(title);
                                upcats.Add(title, new List<string> { page });
                            }
                        }
                        else if (r.Name == "continue")
                            clcontinue = r.GetAttribute("clcontinue");
            } while (clcontinue != "");
        }
        return result;
    }

    static void Search(string page)
    {
        upcats.Add(page, new List<string> { "" });
        List<string> layer = new List<string> { page };
        while (layer.Count != 0 && !upcats.ContainsKey(category))
        {
            layer = Processupcats(layer);
        }
        if (upcats.ContainsKey(category))
        {
            var title = category;
            while (title != "")
            {
                path.Add(title);
                title = upcats[title][0];
            }
        }
    }

    public static void Main()
    {
        cl.Headers.Add("user-agent", "Category Pathfinder, a tool by user:MBH and user:Adamant.pwn");

        string get = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (get == "")
        {
            Sendresponse("");
            return;
        }
        var prms = HttpUtility.ParseQueryString(get);
        lang = prms[0];
        category = prms[1];
        page = prms[2];
        byte[] rawapiout;
        try
        {
            rawapiout = cl.DownloadData("https://" + Uri.EscapeDataString(lang) + ".org/w/api.php?action=query&prop=pageprops&format=xml&titles=category:" + Uri.EscapeDataString(category));
        }
        catch
        {
            Sendresponse("<li>Такого раздела (" + SecurityElement.Escape(lang) + ") не существует</li>");
            return;
        }
        string apiout = Encoding.UTF8.GetString(rawapiout);
        using (var r = new XmlTextReader(new StringReader(apiout)))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("_idx") == "-1")
                {
                    Sendresponse("<li>Такой категории (" + SecurityElement.Escape(category) + ") в данном разделе нет</li>");
                    return;
                }
        rawapiout = cl.DownloadData("https://" + Uri.EscapeDataString(lang) + ".org/w/api.php?action=query&prop=pageprops&format=xml&titles=" + Uri.EscapeDataString(page));
        apiout = Encoding.UTF8.GetString(rawapiout);
        using (var r = new XmlTextReader(new StringReader(apiout)))
            while (r.Read())
                if (r.Name == "page" && r.GetAttribute("_idx") == "-1")
                {
                    Sendresponse("<li>Такой страницы (" + SecurityElement.Escape(page) + ") в данном разделе нет</li>");
                    return;
                }

        rawapiout = cl.DownloadData("https://" + Uri.EscapeDataString(lang) + ".org/w/api.php?action=query&format=xml&meta=siteinfo&siprop=namespaces");
        apiout = Encoding.UTF8.GetString(rawapiout);
        string localcatname = "";
        using (var r = new XmlTextReader(new StringReader(apiout)))
            while (r.Read())
                if (r.Name == "ns" && r.GetAttribute("id") == "14")
                {
                    r.Read();
                    localcatname = r.Value;
                }

        category = localcatname + ":" + category;
        Search(page);
        category = category.Remove(0, category.IndexOf(":") + 1);

        if (path.Count != 0)
        {
            var sb = new StringBuilder();
            foreach (string s in path)
                sb.Append("<li><a href=\"https://" + lang + ".org/wiki/" + s + "\" target=\"_blank\">" + s + "</a></li>\n");
            Sendresponse(sb.ToString());
            return;
        }
        Sendresponse("<li>Путь не найден</li>");
    }

    static void Sendresponse(string response)
    {
        var sr = new StreamReader("cpf-template.txt");
        Console.WriteLine(sr.ReadToEnd().Replace("%page%", page).Replace("%uppercat%", category).Replace("%lang%", lang).Replace("%response%", response));
    }
}
