using System;
using System.IO;
using System.Net.Http;
using System.Web;
using System.Text.RegularExpressions;
class Program
{
    static void sendresponse(string project, bool onlycat, string cat, string result)
    {
        string answer = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "review.html")).ReadToEnd().Replace("%result%", result)
            .Replace("%project%", project).Replace("%cat%", cat);
        if (onlycat)
            answer = answer.Replace("%checked%", "checked");
        Console.WriteLine(answer);
    }
    static void Main()
    {
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null) {
            sendresponse("ru.wikipedia", false, "", "");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        string project = parameters["project"];
        string cat = parameters["cat"];
        bool onlycat = parameters["cat"] == "1";
        
        var client = new HttpClient(); var ids = new Regex(@"revid=""(\d*)"" stable_revid=""(\d*)""");
        var results = ids.Matches(client.GetStringAsync("https://" + project + ".org/w/api.php?action=query&format=xml&list=oldreviewedpages&ordir=older&orlimit=50").Result);
        var a = new Random();
        var choice = results[a.Next(results.Count)];
        var oldid = choice.Groups[2].Value; var currid = choice.Groups[1].Value;
        sendresponse(project, onlycat, cat, "<iframe src=\"https://" + project + ".org/w/index.php?diff=" + currid + "&oldid=" + oldid + "\" scrolling=\"yes\"></iframe>");
    }
}
