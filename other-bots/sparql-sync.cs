using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Xml;
/* Бот для автоматического обновления статей на основе SPARQL-запроса
 * Требует 1 параметр командной строки - собственно запрос
 * Имя файла запроса (например 91642576.rq) интерпретируется как qid-категории, содержащей статьи для обновления
 * Репозиторий с запросами https://github.com/ruwiki/sparql-sync/
 */
class UpdateStarTemplates
{
	public static void Main(string[] args)
	{
		if (args.Length != 1 || !File.Exists(args[0])) {
			Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} bad command-line argument", DateTime.Now));
			return;
		}
		var query = new StreamReader(args[0]).ReadToEnd().Replace("{", "{{").Replace("}", "}}").Replace("{{0}}", "{0}");
		
		var creds = new StreamReader("p").ReadToEnd().Split('\n');		

		ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		var client = new HttpClient(new HttpClientHandler {
			AllowAutoRedirect = true,
			UseCookies = true,
			CookieContainer = new CookieContainer()
		});
		if (creds[0].Contains("@"))
			client.DefaultRequestHeaders.Add("User-Agent", creds[0].Substring(0, creds[0].IndexOf('@')));
		else
			client.DefaultRequestHeaders.Add("User-Agent", creds[0]);
		client.DefaultRequestHeaders.Add("Accept", "text/csv");
		
		// retrieve pages to be updated
		var result = client.GetAsync("https://www.wikidata.org/w/api.php?action=wbgetentities&format=xml&props=sitelinks&sitefilter=ruwiki&ids=Q" +
		             Path.GetFileNameWithoutExtension(args[0])).Result;
		if (!result.IsSuccessStatusCode) {
			Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while retrieving category name: {1}", DateTime.Now, result.StatusCode));					
			return;
		} 
			
		result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&list=categorymembers&format=xml&cmlimit=500&cmtitle=" +
		getXmlAttribute(result, "//sitelink/@title")).Result;
		if (!result.IsSuccessStatusCode) {
			Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while pages within target category: {1}", DateTime.Now, result.StatusCode));					
			return;
		}

		var pages = new XmlDocument();
		pages.LoadXml(result.Content.ReadAsStringAsync().Result);
		
		// Logon and obtaining token
		result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
		if (!result.IsSuccessStatusCode) {
			Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while retrieving login token: {1}", DateTime.Now, result.StatusCode));					
			return;
		}
		
		result = client.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" 
			}, { "lgname", creds[0]
			}, { "lgpassword", creds[1]
			}, { "lgtoken", getXmlAttribute(result, "//tokens/@logintoken")
			}, { "format", "xml"
			}
		})).Result;
		if (!result.IsSuccessStatusCode) {
			Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while login: {1}", DateTime.Now, result.StatusCode));					
			return;
		}
		if (getXmlAttribute(result, "api/login/@result") != "Success") {
			Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} API error while login: {1}", DateTime.Now, getXmlAttribute(result, "api/login/@reason")));					
			return;
		}
		
		result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
		if (!result.IsSuccessStatusCode) {
			Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while requesting edit token: {1}", DateTime.Now, result.StatusCode));					
			return;
		}
		var token = getXmlAttribute(result, "//tokens/@csrftoken");
		
		Console.Out.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://www.wikidata.org/wiki/Q{1} process started", DateTime.Now, Path.GetFileNameWithoutExtension(args[0])));
		foreach (XmlNode page in pages.SelectNodes("//cm/@title")) {
			var title = page.Value;
			result = client.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&prop=pageprops&ppprop=wikibase_item&format=xml&titles=" + title).Result;
			if (!result.IsSuccessStatusCode) {
				Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} HTTP error while requesting edit token: {1}", DateTime.Now, result.StatusCode));					
				return;
			}
			
			var qid = getXmlAttribute(result, "//pageprops/@wikibase_item");
	
			result = client.PostAsync("https://query.wikidata.org/sparql", new FormUrlEncodedContent(new Dictionary<string, string> { {
					"query",
					string.Format(query, qid)
				}
			})).Result;
			
			if (!result.IsSuccessStatusCode) {
				Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} WDQS query failed: {2}", DateTime.Now, title, result.StatusCode));					
				continue;
			}
			
			var text = result.Content.ReadAsStringAsync().Result.Replace("line\r\n", "").Replace("\"", "");
			
			if (title.StartsWith("Список") && text.StartsWith("'''{{subst") || title.StartsWith("Шаблон:") && text.StartsWith("{{Навигационная таблица") && text.Contains("* ")) {
				var request = new MultipartFormDataContent();
				request.Add(new StringContent("edit"), "action");
				request.Add(new StringContent(title), "title");
				request.Add(new StringContent(text), "text");
				request.Add(new StringContent(token), "token");
				request.Add(new StringContent("автоматическое обновление страницы на основе [[SPARQL]]-запроса к [[викиданные|викиданным]]"), "summary");
				request.Add(new StringContent("1"), "bot");
				request.Add(new StringContent("xml"), "format");
				result = client.PostAsync("https://ru.wikipedia.org/w/api.php", request).Result;
				if (!result.IsSuccessStatusCode)
					Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} edit HTTP error: {2}", DateTime.Now, title, result.StatusCode));
				else if (getXmlAttribute(result, "api/edit/@result") != "Success")
					Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} edit API error: {2}", DateTime.Now, title, getXmlAttribute(result, "api/error/@info")));
				else if (getXmlAttribute(result, "api/edit/@nochange") == "")
					Console.Out.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} not changed", DateTime.Now, title));
				else
					Console.Out.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} updated", DateTime.Now, title));
			} else {
				Console.Error.WriteLine(string.Format("{0:yyyy-MM-ddTHH:mm:ss} https://ru.wikipedia.org/wiki/{1} malformed content prepared: {2}...", DateTime.Now, title, text.Substring(0, 10)));
			}
		}
	}
	
	static string getXmlAttribute(HttpResponseMessage result, string xPath)
	{
		var doc = new XmlDocument();
		try {
			doc.LoadXml(result.Content.ReadAsStringAsync().Result);
			return doc.SelectSingleNode(xPath).Value;
		} catch (Exception) {
			return "-";
		}
	}
}
