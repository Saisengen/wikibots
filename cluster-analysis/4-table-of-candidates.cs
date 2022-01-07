using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Linq;

class voterspercandidate
{
    public HashSet<string> yes, no;
    public int id;
}
class Program
{
    static void Sendresponse(string result, string elections, string type)
    {
        var sr = new StreamReader("clusters-template4.txt");
        string result1 = sr.ReadToEnd().Replace("%result%", result).Replace("%elections%", elections);
        if (type == "d")
            result1 = result1.Replace("%checked_d%", "checked").Replace("%checked_dn%", "");
        else
            result1 = result1.Replace("%checked_d%", "").Replace("%checked_dn%", "checked");
        Console.WriteLine(result1);
        Console.WriteLine();
    }
    static void Main()
    {
        var rdr = new StreamReader("electionnames.txt");
        var electionnumbers = new Dictionary<string, int>();
        while (!rdr.EndOfStream)
        {
            var names = rdr.ReadLine().Split('\t');
            electionnumbers.Add(names[0], Convert.ToInt16(names[1]));
        }

        var electionslist = new Dictionary<string, int>();
        rdr = new StreamReader("elections.txt");
        while (!rdr.EndOfStream)
        {
            string voting = rdr.ReadLine();//Весна 2008/Sairam
            if (!voting.StartsWith("2"))
            {
                string election = voting.Substring(0, voting.IndexOf('/'));//Весна 2008
                if (!electionslist.ContainsKey(election))
                    electionslist.Add(election, electionnumbers[election]);
            }
            rdr.ReadLine(); rdr.ReadLine();
        }

        var electionsstring = "";
        foreach (var e in electionslist.OrderByDescending(e => e.Value))
            electionsstring += "<option value=\"" + e.Key + "\" s" + e.Key + ">АК " + e.Value + ": " + e.Key + "</option>\n";
        //Environment.SetEnvironmentVariable("QUERY_STRING", "elections=Весна+2014&type=dn");
        string get = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (get == "")
        {
            Sendresponse("", electionsstring, "dn");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(get);
        var electionforanalyze = parameters[0];
        electionsstring = electionsstring.Replace('s' + electionforanalyze + '>', "selected>");
        var type = parameters[1];
        var candidates = new Dictionary<string, voterspercandidate>();
        var voters = new HashSet<string>();
        int voterid = 0;

        rdr = new StreamReader("elections.txt");
        while (!rdr.EndOfStream)
        {
            string voting = rdr.ReadLine();
            if (voting.StartsWith(electionforanalyze + '/'))
                candidates.Add(voting.Substring(voting.IndexOf('/') + 1), new voterspercandidate() { yes = new HashSet<string>(), no = new HashSet<string>(), id = voterid++ });
            rdr.ReadLine(); rdr.ReadLine();
        }

        int[,] table = new int[candidates.Count, candidates.Count];
        int[,] total = new int[candidates.Count, candidates.Count];

        rdr = new StreamReader("elections.txt");
        while (!rdr.EndOfStream)
        {
            string voting = rdr.ReadLine();
            if (voting.StartsWith(electionforanalyze + '/'))
            {
                var candidate = voting.Substring(voting.IndexOf('/') + 1);
                var yes = rdr.ReadLine().Split('\t');
                foreach (var y in yes)
                    if (y != "")
                    {
                        candidates[candidate].yes.Add(y);
                        if (!voters.Contains(y))
                            voters.Add(y);
                    }
                var no = rdr.ReadLine().Split('\t');
                foreach (var n in no)
                    if (n != "")
                    {
                        candidates[candidate].no.Add(n);
                        if (!voters.Contains(n))
                            voters.Add(n);
                    }
            }
        }

        foreach(var c1 in candidates.Keys)
            foreach(var c2 in candidates.Keys)
                foreach(var voter in voters)
                {
                    if ((candidates[c1].yes.Contains(voter) && candidates[c2].no.Contains(voter)) || (candidates[c1].no.Contains(voter) && candidates[c2].yes.Contains(voter)))
                    {
                        table[candidates[c1].id, candidates[c2].id] -= 1;
                        total[candidates[c1].id, candidates[c2].id] += 1;
                    }
                    if ((candidates[c1].yes.Contains(voter) && candidates[c2].yes.Contains(voter)) || (candidates[c1].no.Contains(voter) && candidates[c2].no.Contains(voter)))
                    {
                        table[candidates[c1].id, candidates[c2].id] += 1;
                        total[candidates[c1].id, candidates[c2].id] += 1;
                    }
                }

        string result = "На выборах проголосовало " + voters.Count + " участников. Прочерк означает, что ни один участник не проголосовал по обоим кандидатам.<br><br><table border=\"1\" cellspacing=\"0\"><tr><th></th>";
        foreach (var c in candidates)
            result += "<th>" + c.Key + "</th>\n";
        result += "</tr>";
        foreach (var c1 in candidates)
        {
            result += "\n<tr><td><a href=\"https://ru.wikipedia.org/wiki/user:" + Uri.EscapeDataString(c1.Key) + "\">" + c1.Key + "</a></td>\n";
            foreach (var c2 in candidates)
                if (c1.Key == c2.Key)
                    result += "<td></td>";
                else
                {
                    if (total[c1.Value.id, c2.Value.id] != 0)
                    {
                        float dn = (float)table[c1.Value.id, c2.Value.id] / total[c1.Value.id, c2.Value.id];
                        string paleness = Convert.ToInt32(Math.Round(256 * (1 - (dn > 0 ? dn : -dn)))).ToString("X2");
                        string color = (dn < 0 ? "FF" + paleness + paleness : paleness + "FF" + paleness);
                        string dns = dn.ToString("G2");
                        if (dns.StartsWith("0.") || dns.StartsWith("-0."))
                            dns = dns.Replace("0.", ".");
                        result += "<td style=\"background-color:#" + color + "\"><abbr title=\"" + c1.Key + " / " + c2.Key + "\">" +
                            (type == "d" ? table[c1.Value.id, c2.Value.id].ToString() : dns) + "</abbr></td>\n";
                    }
                    else
                        result += "<td><abbr title=\"" + c1.Key + " / " + c2.Key + "\">−</abbr></td>\n";
                }
            result += "</tr>";
        }
        result += "</table>";
        Sendresponse(result, electionsstring, type);
    }
}
