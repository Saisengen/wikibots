using System;
using System.IO;
using System.Web;

class Program
{
    static void Sendresponse(string page, string result)
    {
        var r = new StreamReader("compare.html");
        Console.WriteLine(r.ReadToEnd().Replace("%result%", result).Replace("%page%", page).Replace("%encoded%", Uri.EscapeUriString(page).Replace("%20", "_").Replace("%3A", ":").Replace("%2C", ",")));
    }
    static void Main()
    {
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
            Sendresponse("", "");
        else
        {
            var parameters = HttpUtility.ParseQueryString(input);
            string page = parameters["page"];
            string result =
                "<iframe src=\"https://ru.wikipedia.org/wiki/%encoded%\" width=33% height=1100></iframe>\n" +
                "<iframe src=\"https://xn--h1ajim.xn--p1ai/%encoded%\" width=33% height=1100></iframe>\n" +
                "<iframe src=\"https://ru.ruwiki.ru/wiki/%encoded%\" width=33% height=1100></iframe>\n";
            Sendresponse(page, result);
        }
    }
}
