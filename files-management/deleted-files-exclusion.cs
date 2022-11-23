using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Net.Http;
using System.Net;

class Pair
{
    public string page, filename;
    public Logrecord file;
}

class Logrecord
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
        var deletedfiles = new Dictionary<string, Logrecord>();
        var replacedfiles = new Dictionary<string, Logrecord>();
        var deletingpairs = new HashSet<Pair>();
        var replacingpairs = new HashSet<Pair>();
        string apiout, cont = "", query = "https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=logevents&leprop=title%7Cuser%7Ccomment&leaction=delete%2Fdelete&lestart=" + DateTime.Now.AddDays(1).ToString("yyyy-MM-dd") + "T00%3A00%3A00.000Z&leend=" + 
            DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd") + "T00%3A00%3A00.000Z&lenamespace=6&lelimit=max";
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var ru = Site("ru.wikipedia", creds[0], creds[1]);
        var commons = Site("commons.wikimedia", creds[0], creds[1]);
        var filerx = new Regex(@"\[\[(:?c:|:?commons:|)(File|Файл):([^\]]*)\]\]", RegexOptions.IgnoreCase);
        var kburx = new Regex("КБУ#Ф[178]", RegexOptions.IgnoreCase);
        var f8rx = new Regex("КБУ#Ф8", RegexOptions.IgnoreCase);
        var requeststrings = new HashSet<string>();
        string idset; int c;

        while (cont != null)
        {
            apiout = (cont == "" ? ru.GetStringAsync(query).Result : ru.GetStringAsync(query + "&lecontinue=" + cont).Result);
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item" && r.GetAttribute("title") != null)
                    {
                        string comm = r.GetAttribute("comment");
                        string filename = r.GetAttribute("title").Substring(5);
                        if (comm != null && f8rx.IsMatch(comm) && !filerx.IsMatch(comm))
                            continue;
                        if (comm != null && kburx.IsMatch(comm) && filerx.IsMatch(comm) && filerx.Match(comm).Groups[3].Value != filename && !replacedfiles.ContainsKey(filename))
                            replacedfiles.Add(filename, new Logrecord { user = r.GetAttribute("user"), comment = comm, correct = true });
                        else if (!deletedfiles.ContainsKey(filename))
                            deletedfiles.Add(filename, new Logrecord { user = r.GetAttribute("user"), comment = comm, correct = true });
                    }
            }
        }

        idset = ""; c = 0;
        foreach (var r in replacedfiles)
        {
            idset += "|File:" + r.Key;
            if (++c % 15 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        if (idset != "")
            requeststrings.Add(idset.Substring(1));

        foreach (var q in requeststrings)
        {
            apiout = commons.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + Uri.EscapeDataString(q)).Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("_idx")[0] != '-')
                        replacedfiles[r.GetAttribute("title").Substring(5)].correct = false;
            }

            apiout = ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=5000&titles=" + Uri.EscapeDataString(q)).Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                bool isexist = true;
                string filename = "";
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page")
                    {
                        filename = r.GetAttribute("title").Substring(5);
                        if (r.GetAttribute("_idx")[0] == '-') isexist = false;
                        else isexist = true;
                    }
                    if (r.Name == "fu" && !isexist && replacedfiles[filename].correct && Convert.ToInt16(r.GetAttribute("ns")) % 2 == 0)
                        replacingpairs.Add(new Pair { filename = filename, file = replacedfiles[filename], page = r.GetAttribute("title") });
                }
            }
        }

        requeststrings.Clear();
        idset = ""; c = 0;
        foreach (var d in deletedfiles)
        {
            idset += "|File:" + d.Key;
            if (++c % 15 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        if (idset != "")
            requeststrings.Add(idset.Substring(1));

        foreach (var q in requeststrings)
        {
            apiout = commons.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + Uri.EscapeDataString(q)).Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("_idx")[0] != '-')
                        deletedfiles[r.GetAttribute("title").Substring(5)].correct = false;
            }

            apiout = ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=max&titles=" + Uri.EscapeDataString(q)).Result;
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                bool isexist = true;
                string filename = "";
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page")
                    {
                        filename = r.GetAttribute("title").Substring(5);
                        if (r.GetAttribute("_idx")[0] == '-') isexist = false;
                        else isexist = true;
                    }
                    if (r.Name == "fu" && !isexist && deletedfiles[filename].correct)
                    {
                        int ns = Convert.ToInt16(r.GetAttribute("ns"));
                        if (ns % 2 == 0 && ns != 4 && ns != 104 && ns != 106)
                            deletingpairs.Add(new Pair { filename = filename, file = deletedfiles[filename], page = r.GetAttribute("title") });
                    }
                }
            }
        }

        foreach (var rp in replacingpairs)
        {
            string page_text = ru.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(rp.page) + "?action=raw").Result;
            string initial_text = page_text;
            string filename = rp.filename;
            filename = "(" + Regex.Escape(filename) + "|" + Regex.Escape(filename.Replace(" ", "_")) + "|" + Regex.Escape(Uri.EscapeDataString(filename)) + "|" + Regex.Escape(Uri.EscapeDataString(filename.Replace(" ", "_"))) + ")";
            string newname = filerx.Match(rp.file.comment).Groups[3].Value;
            var r = new Regex(filename, RegexOptions.IgnoreCase);
            page_text = r.Replace(page_text, newname);
            if (page_text != initial_text)
                try
                {
                    Save(ru, rp.page, page_text, "[[Файл:" + rp.filename + "]] удалён [[u:" + rp.file.user + "]] по причине " + rp.file.comment);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
        }

        foreach (var dp in deletingpairs)
        {
            string page_text = ru.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(dp.page) + "?action=raw").Result;
            string initial_text = page_text;
            string filename = dp.filename;
            filename = "(" + Regex.Escape(filename) + "|" + Regex.Escape(filename.Replace(" ", "_")) + "|" + Regex.Escape(Uri.EscapeDataString(filename)) + "|" + Regex.Escape(Uri.EscapeDataString(filename.Replace(" ", "_"))) + ")";
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
                    Save(ru, dp.page, page_text, "[[Файл:" + dp.filename + "]] удалён [[u:" + dp.file.user + "]] по причине " + dp.file.comment);
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
