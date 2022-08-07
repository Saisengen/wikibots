using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using System.Linq;
class Result
{
    public int equal, differ;
}
class Program
{
    static bool method_is_post = false;
    static void Sendresponse(string result, string users, string axisxuser, string axisyuser, int earlieryear, int lateryear, int square)
    {
        var sr = new StreamReader(method_is_post ? "clusters-template3.5.txt" : "clusters-template3.txt");
        Console.WriteLine(sr.ReadToEnd().Replace("%result%", result).Replace("%users%", users).Replace("%earlieryear%", earlieryear.ToString()).Replace("%lateryear%", lateryear.ToString())
            .Replace("%axisxuser%", axisxuser).Replace("%axisyuser%", axisyuser).Replace("%square%", square.ToString()));
    }
    static void Main()
    {
        var yes = new Dictionary<string, HashSet<string>>();
        var no = new Dictionary<string, HashSet<string>>();
        //Environment.SetEnvironmentVariable("QUERY_STRING", "users=Jack+who+built+the+house%0D%0AVetrov69%0D%0ASleeps-Darkly%0D%0AStjn%0D%0AMeiræ%0D%0APutnik%0D%0AFacenapalm%0D%0AДжекалоп%0D%0ASerhio+Magpie%0D%0AIniquity%0D%0AЛе+Лой%0D%0AWikisaurus%0D%0AMBH%0D%0AGrain+of+sand%0D%0AЗемлеройкин%0D%0AVenzz%0D%0ADraa+kul%0D%0AMichgrig%0D%0AМиша+Карелин%0D%0AAlex+fand%0D%0ASir+Shurf%0D%0AHelgo13%0D%0ADeltahead%0D%0AIgrek%0D%0AShamash%0D%0AMorihei+Tsunemori%0D%0ATenBaseT%0D%0AWulfson%0D%0ATempus%0D%0ASaramag%0D%0ALuterr%0D%0AFedor+Babkin%0D%0ADaphne+mesereum%0D%0AA.Vajrapani&axisxuser=A.Vajrapani&axisyuser=Arsenal.UC&earlieryear=2014&lateryear=2020&square=1000");
        string input = method_is_post ? Console.ReadLine() : Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
        {
            Sendresponse("", "", "", "", DateTime.Now.Year, DateTime.Now.Year, 1200);
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        var users = parameters[0].Replace("\u200E", "").Replace("\r\n", "\t").Replace("\n", "\t").Replace("\r", "\t").Split('\t');
        string axisxuser = parameters[1];
        string axisyuser = parameters[2];
        int earlieryear = Convert.ToInt16(parameters[3]);
        if (earlieryear < 2006)
            earlieryear = 2006;
        int lateryear = Convert.ToInt16(parameters[4]);
        int square = Convert.ToInt16(parameters[5]);
        var yearrgx = new Regex(@"\d{4}");
        var votings = new HashSet<string>();

        var rdr = new StreamReader("elections.txt");
        while (!rdr.EndOfStream)
        {
            string voting = rdr.ReadLine();
            int year;
            if (voting.StartsWith("2"))
                year = Convert.ToInt16(voting.Substring(0, 4));
            else
            {
                string votingname = voting.Substring(0, voting.IndexOf('/'));
                year = Convert.ToInt16(yearrgx.Match(votingname).ToString());
            }
            if (year < earlieryear || year > lateryear)
            {
                rdr.ReadLine(); rdr.ReadLine();
                continue;
            }
            votings.Add(voting);
            string[] yesarray = new string[0], noarray = new string[0];
            var yesstring = rdr.ReadLine();
            if (yesstring != "")
                yesarray = yesstring.Split('\t');
            var nostring = rdr.ReadLine();
            if (nostring != "")
                noarray = nostring.Split('\t');
            yes.Add(voting, new HashSet<string>());
            foreach (var y in yesarray)
                if (y != "")
                    yes[voting].Add(y);
            no.Add(voting, new HashSet<string>());
            foreach (var n in noarray)
                if (n != "")
                    no[voting].Add(n);
        }

        int alluser1votings = 0;
        var resultarray1 = new Dictionary<string, Result>();
        var allvotingswithuser1 = new Dictionary<string, int>();
        foreach (var voting in votings)
            if (yes[voting].Contains(axisxuser) || no[voting].Contains(axisxuser))
            {
                alluser1votings++;
                var anothervoters = new HashSet<string>();
                if (yes[voting].Contains(axisxuser))
                {
                    foreach (var novoter in no[voting])
                    {
                        if (resultarray1.ContainsKey(novoter))
                            resultarray1[novoter].differ++;
                        else
                            resultarray1.Add(novoter, new Result() { differ = 1, equal = 0 });
                        anothervoters.Add(novoter);
                    }
                    foreach (var yesvoter in yes[voting])
                    {
                        if (resultarray1.ContainsKey(yesvoter))
                            resultarray1[yesvoter].equal++;
                        else
                            resultarray1.Add(yesvoter, new Result() { differ = 0, equal = 1 });
                        if (!anothervoters.Contains(yesvoter))
                            anothervoters.Add(yesvoter);
                    }
                }
                if (no[voting].Contains(axisxuser))
                {
                    foreach (var yesvoter in yes[voting])
                    {
                        if (resultarray1.ContainsKey(yesvoter))
                            resultarray1[yesvoter].differ++;
                        else
                            resultarray1.Add(yesvoter, new Result() { differ = 1, equal = 0 });
                        if (!anothervoters.Contains(yesvoter))
                            anothervoters.Add(yesvoter);
                    }
                    foreach (var novoter in no[voting])
                    {
                        if (resultarray1.ContainsKey(novoter))
                            resultarray1[novoter].equal++;
                        else
                            resultarray1.Add(novoter, new Result() { differ = 0, equal = 1 });
                        if (!anothervoters.Contains(novoter))
                            anothervoters.Add(novoter);
                    }
                }
                foreach (var a in anothervoters)
                    if (allvotingswithuser1.ContainsKey(a))
                        allvotingswithuser1[a]++;
                    else
                        allvotingswithuser1.Add(a, 1);
            }

        int alluser2votings = 0;
        var resultarray2 = new Dictionary<string, Result>();
        var allvotingswithuser2 = new Dictionary<string, int>();
        foreach (var voting in votings)
            if (yes[voting].Contains(axisyuser) || no[voting].Contains(axisyuser))
            {
                alluser2votings++;
                var anothervoters = new HashSet<string>();
                if (yes[voting].Contains(axisyuser))
                {
                    foreach (var novoter in no[voting])
                    {
                        if (resultarray2.ContainsKey(novoter))
                            resultarray2[novoter].differ++;
                        else
                            resultarray2.Add(novoter, new Result() { differ = 1, equal = 0 });
                        anothervoters.Add(novoter);
                    }
                    foreach (var yesvoter in yes[voting])
                    {
                        if (resultarray2.ContainsKey(yesvoter))
                            resultarray2[yesvoter].equal++;
                        else
                            resultarray2.Add(yesvoter, new Result() { differ = 0, equal = 1 });
                        if (!anothervoters.Contains(yesvoter))
                            anothervoters.Add(yesvoter);
                    }
                }
                if (no[voting].Contains(axisyuser))
                {
                    foreach (var yesvoter in yes[voting])
                    {
                        if (resultarray2.ContainsKey(yesvoter))
                            resultarray2[yesvoter].differ++;
                        else
                            resultarray2.Add(yesvoter, new Result() { differ = 1, equal = 0 });
                        if (!anothervoters.Contains(yesvoter))
                            anothervoters.Add(yesvoter);
                    }
                    foreach (var novoter in no[voting])
                    {
                        if (resultarray2.ContainsKey(novoter))
                            resultarray2[novoter].equal++;
                        else
                            resultarray2.Add(novoter, new Result() { differ = 0, equal = 1 });
                        if (!anothervoters.Contains(novoter))
                            anothervoters.Add(novoter);
                    }
                }
                foreach (var a in anothervoters)
                    if (allvotingswithuser2.ContainsKey(a))
                        allvotingswithuser2[a]++;
                    else
                        allvotingswithuser2.Add(a, 1);
            }
        int topindent = 9;
        int leftindent = 2;
        int hsquare = square / 2;
        string result = "<div style=\"position:absolute;left:" + (hsquare + leftindent + 1) + "px;top:" + (hsquare + topindent + 1) + "px;width:" + hsquare + "px;height:" + hsquare + "px;border-width:1px;border-style:solid;\"></div>\n";
        result += "<div style=\"position:absolute;left:" + leftindent + "px;top:" + (hsquare + topindent + 1) + "px;width:" + hsquare + "px;height:" + hsquare + "px;border-width:1px;border-style:solid;\"></div>\n";
        result += "<div style=\"position:absolute;left:" + (hsquare + leftindent + 1) + "px;top:" + topindent + "px;width:" + hsquare + "px;height:" + hsquare + "px;border-width:1px;border-style:solid;\"></div>\n";
        result += "<div style=\"position:absolute;left:" + leftindent + "px;top:" + topindent + "px;width:" + hsquare + "px;height:" + hsquare + "px;border-width:1px;border-style:solid;\"></div>\n";
        foreach (var u in users)
            if(resultarray1.ContainsKey(u) && resultarray2.ContainsKey(u))
            {
                double corrwithuser1 = (double)(resultarray1[u].equal - resultarray1[u].differ) / allvotingswithuser1[u];
                double corrwithuser2 = (double)(resultarray2[u].equal - resultarray2[u].differ) / allvotingswithuser2[u];
                int coord1 = Convert.ToInt32(Math.Round((corrwithuser1 + 1) * square / 2));
                int coord2 = Convert.ToInt32(Math.Round((corrwithuser2 + 1) * square / 2));
                result += "<div style=\"position:absolute;left:" + coord1 + "px;top:" + coord2 + "px;\">• " + u + "</div>\n";
            }
        string usersstring = "";
        foreach (var u in users.OrderBy(u => u))
            usersstring += u + "\n";
        Sendresponse(result, usersstring.Substring(0, usersstring.Length - 1), axisxuser, axisyuser, earlieryear, lateryear, square);
    }
}
