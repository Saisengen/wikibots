using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

class Program
{
    static void Sendresponse(string page, string result, bool loadfromtool)
    {
        var r = new StreamReader("compare.html");
        string answer = r.ReadToEnd().Replace("%result%", result).Replace("%page%", page).Replace("%ruwiki%", Uri.EscapeUriString(page)/*.Replace("%20", "_").Replace("%3A", ":").Replace("%2C", ",")*/);
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
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
            Sendresponse("", "", false);
        else
        {
            var parameters = HttpUtility.ParseQueryString(input);
            string page = parameters["page"];
            bool loadfromtool = parameters["loadfromtool"] == "on";
            string result, runitext="", bugtext="";
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
                result =
                "<iframe src=\"https://ru.wikipedia.org/wiki/%ruwiki%\" width=33% height=1100></iframe>\n" +
                "<iframe srcdoc=\"" + HttpUtility.HtmlEncode(runitext) + "\" width=33% height=1100></iframe>\n" +
                "<iframe srcdoc=\"" + HttpUtility.HtmlEncode(bugtext) + "\" width=33% height=1100></iframe>\n";
            }
            else
            {
                result =
                "<iframe src=\"https://ru.wikipedia.org/wiki/%ruwiki%\" width=33% height=1100></iframe>\n" +
                "<iframe src=\"https://xn--h1ajim.xn--p1ai/%runi%\" width=33% height=1100></iframe>\n" +
                "<iframe src=\"https://ru.ruwiki.ru/wiki/%bug%\" width=33% height=1100></iframe>\n";
            }
            Sendresponse(page, result, loadfromtool);
        }
    }
}
