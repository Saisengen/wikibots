using System;
using System.Xml;
using System.IO;
using DotNetWikiBot;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string cont, query, apiout, token = "";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);

        apiout = site.GetWebPage("/w/api.php?action=query&format=xml&meta=tokens&type=csrf%7Crollback");
        using (var r = new XmlTextReader(new StringReader(apiout)))
            while (r.Read())
                if (r.Name == "tokens")
                    token = Uri.EscapeDataString(r.GetAttribute("csrftoken"));

        cont = ""; query = "/w/api.php?action=query&format=xml&prop=fileusage&generator=categorymembers&fuprop=title&fulimit=5000&gcmtitle=Категория:Файлы:Несвободные&gcmtype=file&gcmlimit=1000";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&gcmcontinue=" + Uri.EscapeDataString(cont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("gcmcontinue");
                string file = "";
                while (r.Read())
                {
                    if (r.Name == "page")
                        file = r.GetAttribute("title");
                    if (r.Name == "fu" && r.GetAttribute("ns") != "0" && r.GetAttribute("ns") != "102")
                    {
                        string title = r.GetAttribute("title");
                        var p = new Page(title);
                        p.Load();
                        string text = p.text;
                        string filename = file.Substring(5);
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
                        {
                            site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=" + title + "&text=" + Uri.EscapeDataString(text) + "&summary=удаление несвободного файла из служебных пространств&token=" + token);
                            if(r.GetAttribute("ns") == "10")
                            {
                                var track = new Page("u:MBH/Шаблоны с удалёнными файлами");
                                track.Load();
                                track.Save(track.text + "\n* [[" + title + "]]");
                            }
                        }
                    }
                }
            }
        }
    }
}
