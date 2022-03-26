using MySql.Data.MySqlClient;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;

class RemoveQid
{
	static HttpClient wd;
	static HttpClient ruwiki;
		
	public static void Main()
	{
		ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		var creds = new StreamReader("p").ReadToEnd().Split('\n');		
		
		wd = Setup("https://www.wikidata.org/w/api.php", creds[0], creds[1]);
		ruwiki = Setup("https://ru.wikipedia.org/w/api.php", creds[0], creds[1]);

		var client = new WebClient();
		client.Encoding = Encoding.UTF8;
			
		var connect = new MySqlConnection("Server=wikidatawiki.labsdb;Database=wikidatawiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8;SslMode=none;");
		connect.Open();
		var command = new MySqlCommand(@"SELECT page_title, ips_item_id
										   FROM ruwiki_p.categorylinks
										  INNER JOIN ruwiki_p.page ON cl_from = page_id
										   LEFT JOIN wikidatawiki_p.wb_items_per_site ON ips_site_page = REPLACE(page_title, '_', ' ') AND ips_site_id = 'ruwiki'
										  WHERE cl_to = 'Википедия:Карточки_с_явно_указанным_элементом_викиданных'", connect);

		using (var reader = command.ExecuteReader()) {
			while (reader.Read()) {
				var buffer = new byte[1000];
				var title = Encoding.UTF8.GetString(buffer, 0, (int)reader.GetBytes(0, 0, buffer, 0, 1000));
				var qid = reader.IsDBNull(1) ? null : reader.GetString(1);
				var page = client.DownloadString("https://ru.wikipedia.org/w/index.php?action=raw&title=" + title);
				var newPage = Regex.Replace(page, @"^{{([^|]+)\|from=Q([\d]+)}}",
					              m => (FixWikidata(qid, m.Groups[2].Value, title) ? "{{" + m.Groups[1].Value + "}}" : m.Value));
				if (!page.Equals(newPage)) {
					SaveRuWiki(title, newPage);
				}
			}
		}
	}
		
	static void SaveRuWiki(string title, string text)
	{
		var doc = new XmlDocument();
		var result = ruwiki.GetAsync("https://ru.wikipedia.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
		if (!result.IsSuccessStatusCode)
			return;

		doc.LoadXml(result.Content.ReadAsStringAsync().Result);
		var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;

		result = ruwiki.PostAsync("https://ru.wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "edit" 
			}, { "title",title
			}, { "text", text
			}, { "token", token 
			}, { "summary", "Убрано явное указание элемента викиданных из карточки"
			}, { "format","xml"
			}
		})).Result;
	}
		
	static bool FixWikidata(string expectedQid, string articleQid, string pageTitle)
	{
		if (string.IsNullOrEmpty(expectedQid)) {
			var doc = new XmlDocument();
			var result = wd.GetAsync("https://www.wikidata.org/w/api.php?action=query&format=xml&meta=tokens&type=csrf").Result;
			if (!result.IsSuccessStatusCode)
				return false;

			doc.LoadXml(result.Content.ReadAsStringAsync().Result);
			var token = doc.SelectSingleNode("//tokens/@csrftoken").Value;

			result = wd.PostAsync("https://www.wikidata.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "wbsetsitelink" 
				}, { "id", "Q" + articleQid 
				}, { "linksite", "ruwiki"
				}, { "linktitle", pageTitle 
				}, { "token",token 
				}, { "format", "xml"
				}
			})).Result;

			var content = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			return true;	
		}
		return expectedQid == articleQid;
	}
		
	static HttpClient Setup(string site, string login, string password)
	{	
		var client = new HttpClient(new HttpClientHandler {
			AllowAutoRedirect = true,
			UseCookies = true,
			CookieContainer = new CookieContainer()
		});
		client.DefaultRequestHeaders.Add("User-Agent", "MBHbot");
			
		var result = client.GetAsync(site + "?action=query&meta=tokens&type=login&format=xml").Result;
		if (!result.IsSuccessStatusCode)
			return null;
			
		var doc = new XmlDocument();
		doc.LoadXml(result.Content.ReadAsStringAsync().Result);
		var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;					

		result = client.PostAsync(site, new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" 
			}, { "lgname", login 
			}, {"lgpassword", password
			}, { "lgtoken", logintoken
			}, { "format", "xml"
			}
		})).Result;
		if (!result.IsSuccessStatusCode)
			return null;
		
		return client;				
	}
}
