using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Text.RegularExpressions;

class Program
{
    static void Sendresponse(string source, bool addauthor, string author, string result)
    {
        string answer = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "cite2template.html")).ReadToEnd().Replace("%result%", result).Replace("%source%", source).Replace("%author%", author);
        if (addauthor)
            answer = answer.Replace("%checked_author%", "checked");
        Console.WriteLine(answer);
    }
    static void Main()
    {
        string source = "", default_author = "", result = "";
        bool addauthor = false;
        bool method_is_post = Environment.GetEnvironmentVariable("REQUEST_METHOD") == "POST";
        if (!method_is_post)
        {
            Sendresponse("", false, "", "");
        }
        else
        { 
            string[] inputdata;
            // The CGI 1.1 standard does not specify that the input will be terminated with EOF
            // So if there's content length, we have to read just as many chars
            var inputLength = int.Parse(Environment.GetEnvironmentVariable("CONTENT_LENGTH")!);
            var inputstring = "";
            for (int i = 0; i < inputLength; i++){
                var nextChar = Console.Read();
                if (nextChar == -1) {
                    break;
                }
                inputstring += (char)nextChar;
            }
            inputdata = inputstring.Split('&');
            foreach (var param in inputdata)
            {
                var data = param.Split('=');
                if (data[0] == "source")
                    source = HttpUtility.UrlDecode(data[1]);
                else if (data[0] == "author")
                    default_author = HttpUtility.UrlDecode(data[1]);
                else if (data[0] == "addauthor" && data[1] == "on")
                    addauthor = true;
            }

            var rgxes = new List<Regex>();
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^\d]*)\s*\.\s*(?<year>\d{4})\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^.]*)\s*\.\s*(?<year>\d{4})\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^\d]*)\s*,\s*(?<year>\d{4})\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^,]*)\s*,\s*(?<year>\d{4})\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^.]*)\s*\.\s*(?<year>\d{4})\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^\d]*)\s*\.\s*(?<year>\d{4})\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^,]*)\s*,\s*(?<year>\d{4})\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^\d]*)\s*,\s*(?<year>\d{4})\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^,]*)\s*,\s*(?<year>\d{4})\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^\d]*)\s*,\s*(?<year>\d{4})\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^.]*)\s*\.\s*(?<year>\d{4})\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^\d]*)\s*\.\s*(?<year>\d{4})\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^№]*)\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^,]*)\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^№]*)\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^—]*)\s*—\s*(?<source>[^.]*)\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^.]*)\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^№]*)\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^,]*)\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^/]*)\s*//\s*(?<source>[^№]*)\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^,]*)\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^№]*)\s*,\s*№\s*(?<number>\d+)\s*,\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^.]*)\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<title>[^.]*)\s*\.\s*(?<source>[^№]*)\s*\.\s*№\s*(?<number>\d+)\s*\.\s*[СсCc]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<author>[^.]*)\s*\.\s*(?<title>[^/]*)\s*//\s*(?<source>[^.]*)\s*\.[\s—-]*(?<year>\d{4})\s*\.[\s—-]*Vol.\s*(?<volume>\d+),\s*no.\s*(?<issue>\d+)\s*\.[\s—-]*[Pp]\.\s*(?<pages>[^.]*)\s*\."));
            rgxes.Add(new Regex(@"(?<author>[^.]*)\s*\.\s*(?<title>[^/]*)\s*//\s*(?<source>[^\d]*)\s*\.[\s—-]*(?<year>\d{4})\s*\.[\s—-]*Vol.\s*(?<volume>\d+),\s*no.\s*(?<issue>\d+)\s*\.[\s—-]*[Pp]\.\s*(?<pages>[^.]*)\s*\."));

            foreach (var row in source.Split('\n'))
            {
                bool found = false;
                foreach (var rgx in rgxes)
                    if (rgx.IsMatch(row) && !found)
                    {
                        found = true;
                        var match = rgx.Match(row);
                        string author = match.Groups["author"].Value;
                        if (match.Groups["volume"].Value == "")
                            result += "* {{публикация|статья|автор=" + (author != "" ? author : (addauthor ? default_author : "")) + "|заглавие=" + match.Groups["title"].Value + "|издание=" + match.Groups["source"].Value + "|год=" +
                            match.Groups["year"].Value + "|номер=" + match.Groups["number"].Value + "|страницы=" + match.Groups["pages"].Value + "}}<br>";
                        else
                            result += "* {{публикация|статья|автор=" + (author != "" ? author : (addauthor ? default_author : "")) + "|заглавие=" + match.Groups["title"].Value + "|издание=" + match.Groups["source"].Value + "|год=" +
                            match.Groups["year"].Value + "|volume=" + match.Groups["volume"].Value + "|issue=" + match.Groups["issue"].Value + "|страницы=" + match.Groups["pages"].Value + "}}<br>";
                    }
                if (!found)
                    result += "* {{публикация|статья|автор=" + (addauthor ? default_author : "") + "|заглавие=|издание=|год=|номер=|страницы=}}<br>";
            }

            Sendresponse(source, addauthor, default_author, result);
        }
    }
}
