using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

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
    public static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = Site(creds[0], creds[1]);
        while (true)
        {
            string log = "";
            using (var r = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=logevents&letype=move&lenamespace=6&lelimit=5&format=xml").Result)))
                while (r.Read())
                    if (r.Name == "item" && r.NodeType == XmlNodeType.Element)
                    {
                        string user = r.GetAttribute("user");
                        if (user == "Atsirbot")
                            continue;
                        string oldname = r.GetAttribute("title");
                        string comment = r.GetAttribute("comment");
                        r.Read();
                        string newname = r.GetAttribute("target_title");

                        var filelinks = new HashSet<string>();
                        using (var r2 = new XmlTextReader(new StringReader(site.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&list=imageusage&iutitle=" + Uri.EscapeUriString(oldname) + "&format=xml").Result)))
                            while (r2.Read())
                                if (r2.Name == "iu")
                                {
                                    string pagename = r2.GetAttribute("title");
                                    bool done = false;
                                    string currenttext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(pagename) + "?action=raw").Result;
                                    string filename = oldname.Replace("Файл:", "");
                                    // для Regex важно убрать плюсы и пробелы, а для вывода комментария в конце важно их оставить
                                    string oldfilename = filename.Replace(" ", ".").Replace("+", ".").Replace("?", ".").Replace("*", ".").Replace("\\", ".").Replace("$", ".").Replace("(", ".").Replace(")", ".");
                                    Regex fname = new Regex(@"(\n(\s)*|=(\s)*|:)" + oldfilename, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                    foreach (Match m in fname.Matches(currenttext))
                                    {
                                        if (currenttext.IndexOf(m.ToString()) == -1)
                                            done = false;

                                        newname = newname.Replace("Файл:", "");
                                        for (int zxc = 0; zxc < 2; zxc++)
                                            newname = newname.Replace("&quot;", "\"").Replace("&#039;", "\'").Replace("&amp;", "&");
                                        if (m.ToString().IndexOf("=", 0, 2) != -1)
                                        {
                                            currenttext = currenttext.Replace(m.ToString(), "= " + newname);
                                            done = true;
                                        }
                                        else if (m.ToString().IndexOf(":", 0, 2) != -1)
                                        {
                                            currenttext = currenttext.Replace(m.ToString(), ":" + newname);
                                            done = true;
                                        }
                                        else if (m.ToString().IndexOf("\n", 0, 2) != -1)
                                        {
                                            currenttext = currenttext.Replace(m.ToString(), "\n" + newname);
                                            done = true;
                                        }
                                    }
                                    if (!done)
                                        log = log + "\n# [[:File:" + newname + "]] (" + DateTime.UtcNow.ToString("dd MMMM yyyy") + ") - не удалось выполнить замену в [[" + pagename + "]].";
                                    string savecomment = "[[File:" + filename + "]] переименован [[u:" + user + "]] в [[File:" + newname + "]]";
                                    if (comment.Length > 0)
                                        savecomment = savecomment + " (" + comment + ")";
                                    Save(site, pagename, currenttext, savecomment);
                                }
                    }
            if (log.Length > 2)
            {
                string logpagename = "user:" + creds[0] + "/Переименования файлов";
                string currenttext = site.GetStringAsync("https://ru.wikipedia.org/wiki/" + logpagename + "?action=raw").Result;
                Save(site, logpagename, currenttext + log, "ошибки обработки");
            }
            Thread.Sleep(10000);
        }
    }
}
