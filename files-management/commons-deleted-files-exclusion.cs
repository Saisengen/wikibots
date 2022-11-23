using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Net.Http;
using System.Net;

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
    static HttpClient Site(string wiki, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + wiki + ".org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + wiki + ".org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword", password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Save(HttpClient site, string wiki, string title, string text)
    {
        var doc = new XmlDocument();
        var result = site.GetAsync("https://" + wiki + ".org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode)
            return;
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;
        var request = new MultipartFormDataContent();
        request.Add(new StringContent("edit"), "action");
        request.Add(new StringContent(title), "title");
        request.Add(new StringContent(text), "text");
        request.Add(new StringContent(token), "token");
        request.Add(new StringContent("xml"), "format");
        result = site.PostAsync("https://" + wiki + ".org/w/api.php", request).Result;
        if (result.ToString().Contains("uccess"))
            Console.WriteLine(DateTime.Now.ToString() + " written " + title);
        else
            Console.WriteLine(result);
    }
    static void Main()
    {
        var deletedfiles = new Dictionary<string, logrecord>();
        var deletingpairs = new HashSet<pair>();
        string apiout, cont = "", query = "https://commons.wikimedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=title%7Cuser%7Ccomment&leaction=delete%2Fdelete&lestart=" + DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T00%3A00%3A00.000Z&leend=" + 
            DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd") + "T00%3A00%3A00.000Z&lenamespace=6&lelimit=max";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var ru = Site("ru.wikipedia", creds[0], creds[1]);
        var commons = Site("commons.wikimedia", creds[0], creds[1]);

        while (cont != null)
        {
            apiout = (cont == "" ? commons.GetStringAsync(query).Result : commons.GetStringAsync(query + "&lecontinue=" + cont).Result);
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
            using (var r = new XmlTextReader(new StringReader(commons.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + Uri.EscapeDataString(q)).Result)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("_idx")[0] != '-')
                        deletedfiles[r.GetAttribute("title")].correct = false;
            }

            using (var r = new XmlTextReader(new StringReader(ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=max&titles=" + Uri.EscapeDataString(q)).Result)))
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

        var write = Site("ru.wikipedia", creds[0], creds[1]);
        foreach (var dp in deletingpairs)
        {
            string page_text = ru.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(dp.page) + "?action=raw").Result;
            string initial_text = page_text;
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
            page_text = r1.Replace(page_text, "");
            page_text = r2.Replace(page_text, "");
            page_text = r3.Replace(page_text, "");
            page_text = r4.Replace(page_text, "$1");
            page_text = r5.Replace(page_text, "$1");
            page_text = r6.Replace(page_text, "");
            page_text = r7.Replace(page_text, "");
            page_text = r8.Replace(page_text, "$1");
            page_text = r9.Replace(page_text, "$1");
            if (page_text != initial_text)
                try
                {
                    Save(ru, dp.page, page_text, "[[c:" + dp.filename + "]] удалён [[c:user:" + dp.file.user + "]] по причине " + dp.file.comment.Replace("[[", "[[c:"));
                    if (dp.page.StartsWith("Шаблон:"))
                    {
                        string logpage_text = ru.GetStringAsync("https://ru.wikipedia.org/wiki/u:MBH/Шаблоны с удалёнными файлами?action=raw").Result;
                        Save(ru, "u:MBH/Шаблоны с удалёнными файлами", logpage_text + "\n* [[" + dp.page + "]]", "");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
        }
    }
}
