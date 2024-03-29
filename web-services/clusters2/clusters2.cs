using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;
using System.Linq;
class data
{
    public int diff, total;
    public string coinciding_votings_list, opposite_votings_list;
}
class Program
{
    static bool method_is_post = Environment.GetEnvironmentVariable("REQUEST_METHOD") == "POST";
    static void Sendresponse(string result, string users, int earlieryear, int lateryear, string type, bool sort, bool wikidim)
    {
        string template_src = method_is_post ? "clusters2.5.html" : "clusters2.html";
        string result1 = new StreamReader(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), template_src)).ReadToEnd().Replace("%result%", result)
            .Replace("%users%", users).Replace("%earlieryear%", earlieryear.ToString()).Replace("%lateryear%", lateryear.ToString());
        if (type == "d")
            result1 = result1.Replace("%checked_d%", "checked");
        else
            result1 = result1.Replace("%checked_dn%", "checked");
        if (sort)
            result1 = result1.Replace("%checked_sort%", "checked");
        if (wikidim)
            result1 = result1.Replace("%checked_wikidim%", "checked");
        Console.WriteLine(result1);
    }
    static void Main()
    {
        var voters = new Dictionary<string, int>();
        var yes = new Dictionary<string, HashSet<string>>();
        var no = new Dictionary<string, HashSet<string>>();
        int counter = 0;
        //Environment.SetEnvironmentVariable("QUERY_STRING", "users=MBH%0D%0Astjn%0D%0ALe+Loy%0D%0AWulfson&earlieryear=2022&lateryear=2022&type=dn");
        string input = method_is_post ? Console.ReadLine() : Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
        {
            Sendresponse("", "", DateTime.Now.Year, DateTime.Now.Year, "dn", false, false);
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        var users = parameters["users"].Replace("\u200E", "").Replace("\r\n", "\t").Replace("\n", "\t").Replace("\r", "\t").Split('\t');
        foreach (var u in users)
            if (u != "" && !voters.ContainsKey(u))
                voters.Add(u.Trim(), counter++);
        int earlieryear = Convert.ToInt16(parameters["earlieryear"]);
        if (earlieryear < 2006)
            earlieryear = 2006;
        int lateryear = Convert.ToInt16(parameters["lateryear"]);
        string type = parameters["type"];
        bool sort = parameters["sort"] == "on";
        bool wikidim = parameters["wikidim"] == "on";
        var yearrgx = new Regex(@"\d{4}");
        var votings = new HashSet<string>();

        var rdr = new StreamReader(Path.Combine(Environment.GetEnvironmentVariable("TOOL_DATA_DIR"), "www/static/elections.txt"));
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

        data[,] table = new data[voters.Count, voters.Count];
        foreach (var v1 in voters)
            foreach (var v2 in voters)
            {
                table[v1.Value, v2.Value] = new data();
                foreach (var voting in votings)
                {
                    if ((yes[voting].Contains(v1.Key) && no[voting].Contains(v2.Key)) || (no[voting].Contains(v1.Key) && yes[voting].Contains(v2.Key)))
                    {
                        table[v1.Value, v2.Value].diff -= 1;
                        table[v1.Value, v2.Value].total += 1;
                        table[v1.Value, v2.Value].opposite_votings_list += voting + '\n';
                    }
                    if ((yes[voting].Contains(v1.Key) && yes[voting].Contains(v2.Key)) || (no[voting].Contains(v1.Key) && no[voting].Contains(v2.Key)))
                    {
                        table[v1.Value, v2.Value].diff += 1;
                        table[v1.Value, v2.Value].total += 1;
                        table[v1.Value, v2.Value].coinciding_votings_list += voting + '\n';
                    }
                    if (wikidim)
                        if ((yes[voting].Contains(v1.Key) || no[voting].Contains(v1.Key)) && (!no[voting].Contains(v2.Key) && !yes[voting].Contains(v2.Key)) ||
                            (yes[voting].Contains(v2.Key) || no[voting].Contains(v2.Key)) && (!no[voting].Contains(v1.Key) && !yes[voting].Contains(v1.Key)))
                            table[v1.Value, v2.Value].total += 1;
                }
            }

        string result = "За указанный период времени прошло " + votings.Count + " голосований. Прочерк означает, что за этот период нет выборов, на которых проголосовали бы оба участника " +
            "(а по методу Викидима - что нет выборов, где проголосовал бы хоть один из участников).<br><br><table border=\"1\" cellspacing=\"0\"><tr><th></th>";

        if (sort)
        {
            var sortedarray = new Dictionary<string, float>();
            foreach (var v in voters)
                if (table[v.Value, 0].total != 0)
                    sortedarray.Add(v.Key, (float)table[v.Value, 0].diff / table[v.Value, 0].total);
            foreach (var s in sortedarray.OrderByDescending(s => s.Value))
                result += "<th>" + s.Key + "</th>\n";
            result += "</tr>";
            foreach (var s1 in sortedarray.OrderByDescending(s => s.Value))
            {
                result += "\n<tr><td><a href=\"https://ru.wikipedia.org/wiki/user:" + Uri.EscapeDataString(s1.Key) + "\">" + s1.Key + "</a></td>\n";
                foreach (var s2 in sortedarray.OrderByDescending(s => s.Value))
                    if (voters[s1.Key] == voters[s2.Key])
                        result += "<td></td>";
                    else
                    {
                        if (table[voters[s1.Key], voters[s2.Key]].total != 0)
                        {
                            float dn = (float)table[voters[s1.Key], voters[s2.Key]].diff / table[voters[s1.Key], voters[s2.Key]].total;
                            string antisaturation = Convert.ToInt32(Math.Round(255 * (1 - (dn > 0 ? dn : -dn)))).ToString("X2");
                            string color = dn < 0 ? "FF" + antisaturation + antisaturation : antisaturation + "FF" + antisaturation;
                            string dn_string = dn.ToString("G2");
                            if (dn_string.StartsWith("0.") || dn_string.StartsWith("-0."))
                                dn_string = dn_string.Replace("0.", ".");
                            result += "<td style=\"background-color:#" + color + "\"><abbr title=\"⇈:\n" + table[voters[s1.Key], voters[s2.Key]].coinciding_votings_list + "⇅:\n" +
                                table[voters[s1.Key], voters[s2.Key]].opposite_votings_list + "\">" + (type == "d" ? table[voters[s1.Key], voters[s2.Key]].diff.ToString() : dn_string) + "</abbr></td>\n";
                        }
                        else
                            result += "<td><abbr title=\"" + s1.Key + " / " + s1.Key + "\">−</abbr></td>\n";
                    }
                result += "</tr>\n";
            }
        }
        else
        {
            foreach (var v in voters)
                result += "<th>" + v.Key + "</th>\n";
            result += "</tr>";
            foreach (var v1 in voters)
            {
                result += "\n<tr><td><a href=\"https://ru.wikipedia.org/wiki/user:" + Uri.EscapeDataString(v1.Key) + "\">" + v1.Key + "</a></td>\n";
                foreach (var v2 in voters)
                    if (v1.Value == v2.Value)
                        result += "<td></td>";
                    else
                    {
                        if (table[v1.Value, v2.Value].total != 0)
                        {
                            float dn = (float)table[v1.Value, v2.Value].diff / table[v1.Value, v2.Value].total;
                            string antisaturation = Convert.ToInt32(Math.Round(255 * (1 - (dn > 0 ? dn : -dn)))).ToString("X2");
                            string color = dn < 0 ? "FF" + antisaturation + antisaturation : antisaturation + "FF" + antisaturation;
                            string dn_string = dn.ToString("G2");
                            if (dn_string.StartsWith("0.") || dn_string.StartsWith("-0."))
                                dn_string = dn_string.Replace("0.", ".");
                            result += "<td style=\"background-color:#" + color + "\"><abbr title=\"⇈:\n" + table[voters[v1.Key], voters[v2.Key]].coinciding_votings_list + "⇅:\n" +
                                table[voters[v1.Key], voters[v2.Key]].opposite_votings_list + "\">" +
                                (type == "d" ? table[v1.Value, v2.Value].diff.ToString() : dn_string) + "</abbr></td>\n";
                        }
                        else
                            result += "<td><abbr title=\"" + v1.Key + " / " + v2.Key + "\">−</abbr></td>\n";
                    }
                result += "</tr>\n";
            }
        }
        result += "</table>";
        Sendresponse(result, parameters[0], earlieryear, lateryear, type, sort, wikidim);
    }
}
