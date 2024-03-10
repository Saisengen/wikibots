using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

class Program
{
    static void Sendresponse(string page, string result, bool loadfromtool)
    {
        string answer = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "compare.html")).ReadToEnd().Replace("%result%", result).Replace("%page%", page).Replace("%ruwiki%", Uri.EscapeUriString(page)/*.Replace("%20", "_").Replace("%3A", ":").Replace("%2C", ",")*/);
        string runi = page, bug = page;
        if (page.StartsWith("Википедия:"))
        {
            runi = page.Replace("Википедия:", "Руниверсалис:");
            bug = page.Replace("Википедия:", "Рувики:");
        }
        answer = answer.Replace("%runi%", Uri.EscapeUriString(runi)).Replace("%bug%", Uri.EscapeUriString(bug));
        if (loadfromtool)
            answer = answer.Replace("%checked_loadfromtool%", "checked");
        Console.WriteLine(answer);
    }
    static void Main()
    {
        string width = "49%", height = "500";
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
            Sendresponse("", "", false);
        else
        {
            var parameters = HttpUtility.ParseQueryString(input);
            string page = parameters["page"];
            bool loadfromtool = parameters["loadfromtool"] == "on";
            string result, runitext = "", bugtext = "", znanietext = "";
            if (loadfromtool)
            {
                var cl = new WebClient();
                cl.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36");
                string runititle = page, bugtitle = page;
                if (page.StartsWith("Википедия:"))
                {
                    runititle = page.Replace("Википедия:", "Руниверсалис:");
                    bugtitle = page.Replace("Википедия:", "Рувики:");
                }
                try { runitext = Encoding.UTF8.GetString(cl.DownloadData("https://xn--h1ajim.xn--p1ai/" + runititle)); } catch { }
                try { bugtext = Encoding.UTF8.GetString(cl.DownloadData("https://ru.ruwiki.ru/wiki/" + bugtitle)); } catch { }
                try { znanietext = Encoding.UTF8.GetString(cl.DownloadData("https://znanierussia.ru/articles/" + page)); } catch { }

                result = "<iframe src=\"https://ru.wikipedia.org/wiki/%ruwiki%\" height=" + height + " width=" + width + "></iframe>\n" +
                "<iframe srcdoc=\"" + HttpUtility.HtmlEncode(runitext) + "\" height=" + height + " width=" + width + "></iframe><br clear=all>\n" +
                "<iframe srcdoc=\"" + HttpUtility.HtmlEncode(bugtext) + "\" height=" + height + " width=" + width + "></iframe>\n" +
                "<iframe srcdoc=\"" + HttpUtility.HtmlEncode(znanietext) + "\" height=" + height + " width=" + width + "></iframe>";
            }
            else
            {
                result = "<iframe src=\"https://ru.wikipedia.org/wiki/%ruwiki%\" height=" + height + " width=" + width + "></iframe>\n" +
                "<iframe src=\"https://xn--h1ajim.xn--p1ai/%runi%\" height=" + height + " width=" + width + "></iframe><br clear=all>\n" +
                "<iframe src=\"https://ru.ruwiki.ru/wiki/%bug%\" height=" + height + " width=" + width + "></iframe>\n" +
                "<iframe src=\"https://znanierussia.ru/articles/%ruwiki%\" height=" + height + " width=" + width + "></iframe>";
            }
            Sendresponse(page, result, loadfromtool);
        }
    }
}
