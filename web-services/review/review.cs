using System;
using System.IO;
using System.Net.Http;
using System.Web;
using System.Text.RegularExpressions;
class Program
{
    static void sendresponse(string project, bool onlycat, string cat, string result) {
        string answer = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "review.html")).ReadToEnd().Replace("%result%", result)
            .Replace("%project%", project).Replace("%cat%", cat);
        if (onlycat)
            answer = answer.Replace("%checked%", "checked");
        Console.WriteLine(answer);
    }
    static void Main() {
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null) {
            sendresponse("ru.wikipedia", false, "", "");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        string project = parameters["project"];
        string cat = parameters["cat"];
        bool onlycat = parameters["cat"] == "1";
        bool test = parameters["test"] == "1";
        if (test)
            sendresponse("ru.wikipedia", false, "", "<iframe src=\"https://ru.wikipedia.org/w/index.php?diff=137309552&oldid=135393489\"></iframe>");
        else
        {
            var client = new HttpClient(); var title_rgx = new Regex(@"title=""([^""]*)""");
            var titles = title_rgx.Matches(client.GetStringAsync("https://" + project + ".org/w/api.php?action=query&format=xml&list=oldreviewedpages&ordir=older&orlimit=20").Result);
            var a = new Random();
            sendresponse(project, onlycat, cat, "<iframe src=\"https://" + project + ".org/wiki/" + Uri.EscapeDataString(titles[a.Next(titles.Count)].Groups[1].Value) + "\"></iframe>");
        }
    }
}
