using System.Collections.Generic;
using System.Xml;
using System.IO;
using DotNetWikiBot;
using System.Text.RegularExpressions;
using System;

class pair
{
    public string page, filename;
    public logrecord file;
}

class logrecord
{
    public string user, comment;
    public bool correct;
}

class Program
{
    static void Main()
    {
        var deletedfiles = new Dictionary<string, logrecord>();
        var deletingpairs = new HashSet<pair>();
        string apiout, cont = "", query = "/w/api.php?action=query&format=xml&list=logevents&leprop=title%7Cuser%7Ccomment&leaction=delete%2Fdelete&lestart=" + DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T00%3A00%3A00.000Z&leend=" + DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd") +
            "T00%3A00%3A00.000Z&lenamespace=6&lelimit=max";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var ru = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var commons = new Site("https://commons.wikimedia.org", creds[0], creds[1]);
        
        while (cont != null)
        {
            apiout = (cont == "" ? commons.GetWebPage(query) : commons.GetWebPage(query + "&lecontinue=" + cont));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string title = r.GetAttribute("title");
                        if (title == null) continue;
                        string comment = r.GetAttribute("comment");
                        if (comment == null) comment = "";
                        if (!deletedfiles.ContainsKey(title) && !comment.Contains("emporary delet") && !comment.Contains("old revision"))
                            deletedfiles.Add(title, new logrecord { user = r.GetAttribute("user"), comment = comment, correct = true });
                    }
            }
        }

        var requeststrings = new HashSet<string>();
        string idset = ""; int c = 0;
        foreach (var d in deletedfiles)
        {
            idset += "|" + d.Key;
            if (++c % 12 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        if (idset != "")
            requeststrings.Add(idset.Substring(1));

        foreach (var q in requeststrings)
        {
            apiout = commons.GetWebPage("/w/api.php?action=query&format=xml&prop=info&titles=" + Uri.EscapeDataString(q));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("_idx")[0] != '-')
                        deletedfiles[r.GetAttribute("title")].correct = false;
            }

            apiout = ru.GetWebPage("/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=5000&titles=" + Uri.EscapeDataString(q));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                bool isexist = true;
                string filename = "";
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page")
                    {
                        filename = "File" + r.GetAttribute("title").Substring(4);
                        if (r.GetAttribute("_idx")[0] == '-') isexist = false;
                        else isexist = true;
                    }
                    if (r.Name == "fu" && !isexist && deletedfiles[filename].correct)
                    {
                        int ns = Convert.ToInt16(r.GetAttribute("ns"));
                        if (ns % 2 == 0 && ns != 4 && ns != 104 && ns != 106)
                            deletingpairs.Add(new pair { filename = filename, file = deletedfiles[filename], page = r.GetAttribute("title") });
                    }
                }
            }
        }
        
        Site write = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        foreach (var dp in deletingpairs)
        {
            var p = new Page(write, dp.page);
            try
            {
                p.Load();
            }
            catch
            {
                continue;
            }
            string text = p.text;
            string filename = dp.filename.Substring(5);
            filename = "(" + Regex.Escape(filename) + "|" + Regex.Escape(Uri.EscapeDataString(filename)) + ")";
            filename = filename.Replace(@"\ ", "[ _]");
            var r1 = new Regex(@"\[\[\s*(file|image|файл|изображение):\s*" + filename + @"[^[\]]*\]\]", RegexOptions.IgnoreCase);
            var r2 = new Regex(@"\[\[\s*(file|image|файл|изображение):\s*" + filename + @"[^[]*(\[\[[^\[\]]*\]\][^[\]]*)*\]\]", RegexOptions.IgnoreCase);
            var r3 = new Regex(@"<\s*gallery[^>]*>\s*(file|image|файл|изображение):\s*" + filename + @"[^\n]*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var r4 = new Regex(@"(<\s*gallery[^>]*>.*)(file|image|файл|изображение):\s*" + filename + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var r5 = new Regex(@"(<\s*gallery[^>]*>.*)" + filename + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var r6 = new Regex(@"<\s*gallery[^>]*>\s*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var r7 = new Regex(@"\{\{\s*(flagicon image|audio)[^|}]*\|\s*" + filename + @"[^}]*\}\}");
            var r8 = new Regex(@"([=|]\s*)(file|image|файл|изображение):\s*" + filename, RegexOptions.IgnoreCase);
            var r9 = new Regex(@"([=|]\s*)" + filename, RegexOptions.IgnoreCase);
            text = r1.Replace(text, "");
            text = r2.Replace(text, "");
            text = r3.Replace(text, "");
            text = r4.Replace(text, "$1");
            text = r5.Replace(text, "$1");
            text = r6.Replace(text, "");
            text = r7.Replace(text, "");
            text = r8.Replace(text, "$1");
            text = r9.Replace(text, "$1");
            if (text != p.text)
                try
                {
                    p.Save(text, "[[c:" + dp.filename + "]] удалён [[c:user:" + dp.file.user + "]] по причине " + dp.file.comment.Replace("[[", "[[c:"), true);
                    if (dp.page.StartsWith("Шаблон:"))
                    {
                        var track = new Page("u:MBH/Шаблоны с удалёнными файлами");
                        track.Load();
                        track.Save(track.text + "\n* [[" + dp.page + "]]");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
        }
    }
}
