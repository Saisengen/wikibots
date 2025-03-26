using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Net.Http;
using System.Net;
using System.Linq;
class pair
{
    public string page, file;
    public logrecord deletion_data;
}
class logrecord
{
    public string deleter, comment;
}
class Program
{
    static string[] creds;
    static HttpClient ru, commons;
    static DateTime dtn = DateTime.UtcNow;
    static string univ_query;
    static void Main()
    {
        creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        ru = Site("ru.wikipedia", creds[0], creds[1]);
        commons = Site("commons.wikimedia", creds[0], creds[1]);
        univ_query = "https://{domain}.org/w/api.php?action=query&format=xml&list=logevents&leprop=title|user|comment&leaction=delete/delete&leend=" +
            dtn.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ss") + "&lenamespace=6&lelimit=max";
        run_ru();
        run_commons();
    }
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
        if (!result.ToString().Contains("uccess"))
            Console.WriteLine(result);
    }
    static void delete_transclusion(pair dp, bool isCommons)
    {
        string initial_text = ru.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(dp.page) + "?action=raw").Result;
        string new_page_text = initial_text;
        string filename = dp.file.Substring(5);
        string rgxtext = filename.Replace(" ", "[ _]+");
        rgxtext = "(" + rgxtext + "|" + Uri.EscapeDataString(filename.Substring(5)) + ")";
        var r1 = new Regex(@" *\[\[\s*(file|image|файл|изображение):\s*" + rgxtext + @"[^[\]]*\]\]", RegexOptions.IgnoreCase);
        var r2 = new Regex(@" *\[\[\s*(file|image|файл|изображение):\s*" + rgxtext + @"[^[]*(\[\[[^\[\]]*\]\][^[\]]*)*\]\]", RegexOptions.IgnoreCase);
        var r3 = new Regex(@" *<\s*gallery[^>]*>\s*(file|image|файл|изображение):\s*" + rgxtext + @"[^\n]*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r4 = new Regex(@" *(<\s*gallery[^>]*>.*)(file|image|файл|изображение):\s*" + rgxtext + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r5 = new Regex(@" *(<\s*gallery[^>]*>.*)" + rgxtext + @"[^\n]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r6 = new Regex(@" *<\s*gallery[^>]*>\s*<\s*/gallery\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var r7 = new Regex(@" *\{\{\s*(flagicon image|audio)[^|}]*\|\s*" + rgxtext + @"[^}]*\}\}");
        var r8 = new Regex(@" *([=|]\s*)(file|image|файл|изображение):\s*" + rgxtext, RegexOptions.IgnoreCase);
        var r9 = new Regex(@" *([=|]\s*)" + rgxtext, RegexOptions.IgnoreCase);
        new_page_text = r1.Replace(new_page_text, "");
        new_page_text = r2.Replace(new_page_text, "");
        new_page_text = r3.Replace(new_page_text, "");
        new_page_text = r4.Replace(new_page_text, "$1");
        new_page_text = r5.Replace(new_page_text, "$1");
        new_page_text = r6.Replace(new_page_text, "");
        new_page_text = r7.Replace(new_page_text, "");
        new_page_text = r8.Replace(new_page_text, "$1");
        new_page_text = r9.Replace(new_page_text, "$1");
        if (new_page_text != initial_text)
            try
            {
                string comment = "[[file:" + filename + "]] удалён [[user:" + dp.deletion_data.deleter + "]] по причине " + dp.deletion_data.comment;
                Save(ru, dp.page, new_page_text, isCommons ? comment.Replace("[[", "[[c:") : comment);
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
    static void find_and_delete_usages(Dictionary<string, logrecord> deletedfiles, bool iscommons)
    {
        foreach (var df in deletedfiles.Keys.ToList())
        {
            using (var r = new XmlTextReader(new StringReader(commons.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + Uri.EscapeDataString(df)).Result)))
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("_idx")[0] != '-')
                        deletedfiles.Remove(r.GetAttribute("title"));
            using (var r = new XmlTextReader(new StringReader(ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=max&titles=" + Uri.EscapeDataString(df)).Result)))
            {
                bool file_is_used = true;
                string ru_filename = "";
                r.WhitespaceHandling = WhitespaceHandling.None;
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page")
                    {
                        ru_filename = r.GetAttribute("title");
                        file_is_used = r.GetAttribute("_idx")[0] != '-';
                    }
                    if (r.Name == "fu" && !file_is_used)
                    {
                        int ns = Convert.ToInt16(r.GetAttribute("ns"));
                        if (ns % 2 == 0 && ns != 4 && ns != 104 && ns != 106)
                            try
                            {
                                delete_transclusion(new pair { file = ru_filename, deletion_data = deletedfiles[ru_filename.Replace("Файл:", "File:")], page = r.GetAttribute("title") }, iscommons);
                            }
                            catch
                            {
                                try { delete_transclusion(new pair { file = ru_filename, deletion_data = deletedfiles[ru_filename], page = r.GetAttribute("title") }, iscommons); }  catch { }
                            }                            
                    }
                }
            }
        }
    }
    static void run_commons()
    {
        var deletedfiles = new Dictionary<string, logrecord>();
        string cont = "", query = univ_query.Replace("{domain}", "commons.wikimedia");
        var invalid_reasons_for_deletion = new Regex("temporary|maintenance|old revision|redirect", RegexOptions.IgnoreCase);
        while (cont != null)
            using (var r = new XmlTextReader(new StringReader(cont == "" ? commons.GetStringAsync(query).Result : commons.GetStringAsync(query + "&lecontinue=" + cont).Result)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item")
                    {
                        string title = r.GetAttribute("title");
                        if (title == null) continue;
                        string comment = r.GetAttribute("comment") ?? "";
                        if (!deletedfiles.ContainsKey(title) && !invalid_reasons_for_deletion.IsMatch(comment))
                            deletedfiles.Add(title, new logrecord { deleter = r.GetAttribute("user"), comment = comment });
                    }
            }
        find_and_delete_usages(deletedfiles, true);
    }
    static void run_ru()
    {
        var deletedfiles = new Dictionary<string, logrecord>();
        var replacedfiles = new Dictionary<string, logrecord>();
        var usages_for_deletion = new HashSet<pair>();
        var replacingpairs = new HashSet<pair>();
        string cont = "", query = univ_query.Replace("{domain}", "ru.wikipedia");
        var file_is_replaced_rgx = new Regex("КБУ#Ф[178]|икисклад|ommons", RegexOptions.IgnoreCase);
        var inner_link_to_replacement_file = new Regex(@"\[\[(:?c:|:?commons:|)(File|Файл):([^\]]*)\]\]", RegexOptions.IgnoreCase);
        var commons_importer_link = new Regex(@"commons.wikimedia.org/wiki/File:([^ ])", RegexOptions.IgnoreCase);

        while (cont != null)
        {
            using (var r = new XmlTextReader(new StringReader(cont == "" ? ru.GetStringAsync(query).Result : ru.GetStringAsync(query + "&lecontinue=" + cont).Result)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item" && r.GetAttribute("title") != null)
                    {
                        string comm = r.GetAttribute("comment") ?? "";
                        string filename = r.GetAttribute("title");
                        if (file_is_replaced_rgx.IsMatch(comm) && ((inner_link_to_replacement_file.IsMatch(comm) && inner_link_to_replacement_file.Match(comm).Groups[3].Value != filename.Substring(5)) ||
                            (commons_importer_link.IsMatch(comm) && commons_importer_link.Match(comm).Groups[1].Value != filename.Substring(5))) && !replacedfiles.ContainsKey(filename))
                            replacedfiles.Add(filename, new logrecord { deleter = r.GetAttribute("user"), comment = comm });
                        else if (!deletedfiles.ContainsKey(filename))
                            deletedfiles.Add(filename, new logrecord { deleter = r.GetAttribute("user"), comment = comm });
                    }
            }
        }

        foreach (var rf in replacedfiles.Keys.ToList())
        {
            using (var r = new XmlTextReader(new StringReader(commons.GetStringAsync("https://commons.wikimedia.org/w/api.php?action=query&format=xml&prop=info&titles=" + Uri.EscapeDataString(rf)).Result)))
                while (r.Read())
                    if (r.Name == "page" && r.GetAttribute("_idx")[0] != '-')
                        replacedfiles.Remove(r.GetAttribute("title"));
            using (var r = new XmlTextReader(new StringReader(ru.GetStringAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&prop=fileusage&fuprop=title&fulimit=max&titles=" + Uri.EscapeDataString(rf)).Result)))
            {
                bool isexist = true;
                string filename = "";
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "page")
                    {
                        filename = r.GetAttribute("title");
                        if (r.GetAttribute("_idx")[0] == '-') isexist = false;
                        else isexist = true;
                    }
                    if (r.Name == "fu" && !isexist && Convert.ToInt16(r.GetAttribute("ns")) % 2 == 0)
                    {
                        var page = r.GetAttribute("title");
                        string initial_text = ru.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeDataString(page) + "?action=raw").Result;
                        string newname;
                        try
                        {
                            newname = inner_link_to_replacement_file.Match(replacedfiles[rf].comment).Groups[3].Value;
                        }
                        catch
                        {
                            newname = commons_importer_link.Match(replacedfiles[rf].comment).Groups[1].Value;
                        }
                        string rgxtext = filename.Substring(5).Replace(" ", "[ _]");
                        rgxtext = "(" + rgxtext + "|" + Uri.EscapeDataString(filename.Substring(5)) + ")";
                        var rgx = new Regex(rgxtext, RegexOptions.IgnoreCase);
                        string new_page_text = rgx.Replace(initial_text, newname);
                        if (new_page_text != initial_text)
                            try
                            {
                                Save(ru, page, new_page_text, "[[" + rf + "]] удалён [[u:" + replacedfiles[rf].deleter + "]] по причине " + replacedfiles[rf].comment);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.ToString());
                            }
                    }
                }
            }
        }
        find_and_delete_usages(deletedfiles, false);
    }
}
