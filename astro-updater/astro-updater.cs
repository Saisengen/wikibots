using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Xml;
/* Бот для автоматического обновления статей на основе SPARQL-запроса. Требует 1 параметр командной строки - собственно запрос
 * Имя файла запроса (например 91642576.rq) интерпретируется как qid-категории, содержащей статьи для обновления */
class UpdateStarTemplates
{
    public static void Main(string[] args)
    {
        var now = DateTime.Now; if (args.Length != 1 || !File.Exists(args[0])) { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} bad command-line argument", now)); return; }
        var query = new StreamReader(args[0]).ReadToEnd().Replace("{", "{{").Replace("}", "}}").Replace("{{0}}", "{0}"); var creds = new StreamReader("p").ReadToEnd().Split('\n');
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        if (creds[0].Contains("@"))
            client.DefaultRequestHeaders.Add("User-Agent", creds[0].Substring(0, creds[0].IndexOf('@')));
        else
            client.DefaultRequestHeaders.Add("User-Agent", creds[0]);
        client.DefaultRequestHeaders.Add("Accept", "text/csv");
        var result = client.GetAsync("https://www.wikidata.org/w/api.php?action=wbgetentities&format=xml&props=sitelinks&sitefilter=ruwiki&ids=Q" + Path.GetFileNameWithoutExtension(args[0])).Result;
        if (!result.IsSuccessStatusCode) { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while retrieving category name: {1}", now, result.StatusCode)); return; }

        result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmlimit=500&cmtitle=" +
        getXmlAttribute(result, "//sitelink/@title")).Result;
        if (!result.IsSuccessStatusCode) { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while pages within target category: {1}", now, result.StatusCode)); return; }

        var pages = new XmlDocument(); pages.LoadXml(result.Content.ReadAsStringAsync().Result); result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode) { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while retrieving login token: {1}", now, result.StatusCode)); return; }

        result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", creds[0] }, { "lgpassword", creds[1] },
            { "lgtoken", getXmlAttribute(result, "//tokens/@logintoken") } })).Result;
        if (!result.IsSuccessStatusCode) { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while login: {1}", now, result.StatusCode)); return; }
        if (getXmlAttribute(result, "api/login/@result") != "Success") {
            Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} API error while login: {1}", now, getXmlAttribute(result, "api/login/@reason"))); return; }

        result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
        if (!result.IsSuccessStatusCode) { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while requesting edit token: {1}", now, result.StatusCode)); return; }
        var token = getXmlAttribute(result, "//tokens/@csrftoken");

        foreach (XmlNode page in pages.SelectNodes("//cm/@title"))
        {
            var title = page.Value; result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=pageprops&ppprop=wikibase_item&format=xml&titles=" + title).Result;
            if (!result.IsSuccessStatusCode) { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while requesting edit token: {1}", DateTime.Now, result.StatusCode)); return; }
            var qid = getXmlAttribute(result, "//pageprops/@wikibase_item");
            result = client.PostAsync("https://query.wikidata.org/sparql", new FormUrlEncodedContent(new Dictionary<string, string> { { "query", string.Format(query, qid) } })).Result;
            if (!result.IsSuccessStatusCode) {
                Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} WDQS query failed: {2}", DateTime.Now, title, result.StatusCode)); continue; }
            var newtext = result.Content.ReadAsStringAsync().Result.Replace("line\r\n", "").Replace("\"", "");
            var oldtext = client.GetStringAsync("https://ru.wikipedia.org/wiki/" + Uri.EscapeUriString(title) + "?action=raw").Result;
            if (oldtext.Length - newtext.Length > 2048)
            { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} new content too short: {2} > {3}", now, title, oldtext.Length, newtext.Length)); continue; }
            else if (title.StartsWith("Список") && newtext.StartsWith("'''{{subst") || title.StartsWith("Шаблон:") && newtext.StartsWith("{{Навигационная таблица") && newtext.Contains("* "))
            {
                var request = new MultipartFormDataContent { { new StringContent("edit"), "action" }, { new StringContent(title), "title" }, { new StringContent(newtext), "text" }, { new StringContent(token),
                        "token" }, { new StringContent("автоматическое обновление страницы на основе [[SPARQL]]-запроса к [[викиданные|викиданным]]"), "summary" } };
                result = client.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
                if (!result.IsSuccessStatusCode)
                    Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} edit HTTP error: {2}", now, title, result.StatusCode));
                else if (getXmlAttribute(result, "api/edit/@result") != "Success")
                    Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} edit API error: {2}", now, title, getXmlAttribute(result, "api/error/@info")));
            }
            else { Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} malformed content prepared: {2}...", now, title, newtext.Substring(0, 10))); }
        }
    }
    static string getXmlAttribute(HttpResponseMessage result, string xPath) {
        var doc = new XmlDocument(); try { doc.LoadXml(result.Content.ReadAsStringAsync().Result); return doc.SelectSingleNode(xPath).Value; } catch (Exception) { return "-"; }
    }
}
