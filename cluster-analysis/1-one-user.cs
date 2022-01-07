using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Web;

class votes_on_election
{
    public HashSet<string> yes, no;
}
class voterdata
{
    public int samevotes, opposevotes, diff, commonvotings, wkdmtotal;
    public float normalized_diff, wkdm_normal;
}
class Program
{
    static string searcheduser, result, sort;
    static float highlimitdn, lowlimitdn;
    static int highlimit, lowlimit, commonvotings, earlieryear, lateryear;
    static void Sendresponse(string result, string user, int earlieryear, int lateryear, int highlimit, int lowlimit, double highlimitdn, double lowlimitdn, int commonvotings, string sort)
    {
        var sr = new StreamReader("clusters-template.txt");
        string result1 = sr.ReadToEnd().Replace("%result%", result).Replace("%user%", user).Replace("%earlieryear%", earlieryear.ToString()).Replace("%lateryear%", lateryear.ToString())
            .Replace("%highlimit%", highlimit.ToString()).Replace("%lowlimit%", lowlimit.ToString()).Replace("%highlimitdn%", highlimitdn.ToString("G2")).Replace("%lowlimitdn%",
            lowlimitdn.ToString("G3")).Replace("%commonvotings%", commonvotings.ToString());
        if (sort == "d")
            Console.WriteLine(result1.Replace("%selected_d%", "selected"));
        else if (sort == "dn")
            Console.WriteLine(result1.Replace("%selected_dn%", "selected"));
        else if (sort == "wkdm")
            Console.WriteLine(result1.Replace("%selected_wkdm%", "selected"));
        Console.WriteLine();
    }

    static void showtable(KeyValuePair<string, voterdata> voter)
    {
        if (voter.Key != searcheduser)
        {
            if (voter.Value.diff <= lowlimit || voter.Value.diff >= highlimit || ((voter.Value.normalized_diff <= lowlimitdn || voter.Value.normalized_diff >= highlimitdn) && voter.Value.commonvotings >= commonvotings))
            {
                float dn_for_color = (sort == "wkdm" ? voter.Value.wkdm_normal : voter.Value.normalized_diff);
                string antisaturation = Convert.ToInt32(Math.Round(255 * (1 - Math.Abs(dn_for_color)))).ToString("X2");
                string color = (dn_for_color < 0 ? "FF" + antisaturation + antisaturation : antisaturation + "FF" + antisaturation);
                result += "<tr style=\"background-color:#" + color + "\"><td><a href=\"https://mbh.toolforge.org/clusters1.cgi?user=" + Uri.EscapeDataString(voter.Key) + "&earlieryear=" + earlieryear +
                    "&lateryear=" + lateryear + "&highlimit=" + highlimit + "&lowlimit=" + lowlimit + "&highlimitdn=" + highlimitdn + "&lowlimitdn=" + lowlimitdn + "&commonvotings=" + commonvotings +
                    "&sort=" + sort + "\">" + voter.Key + "</a></td><td>" + voter.Value.samevotes + "</td><td>" + voter.Value.opposevotes + "</td><td>" + voter.Value.diff.ToString().Replace('-', '−') +
                    "</td><td>" + voter.Value.commonvotings + "</td><td>" + voter.Value.wkdmtotal + "</td><td>" + voter.Value.normalized_diff.ToString("G2").Replace('-', '−') + "</td><td>" +
                    voter.Value.wkdm_normal.ToString("G2").Replace('-', '−') + "</td></tr>\n";
            }
        }
    }
    static void Main()
    {
        //Environment.SetEnvironmentVariable("QUERY_STRING", "user=Rijikk&earlieryear=2014&lateryear=2021&highlimit=7&lowlimit=-10&highlimitdn=0.3&lowlimitdn=-0.3&commonvotings=20&sort=wkdm");
        string input = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == "" || input == null)
        {
            Sendresponse("", "", DateTime.Now.Year, DateTime.Now.Year, 20, -10, 0.3, -0.3, 20, "d");
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        searcheduser = parameters["user"].First().ToString().ToUpper() + parameters["user"].Substring(1);
        earlieryear = Convert.ToInt16(parameters["earlieryear"]);
        if (earlieryear < 2006)
            earlieryear = 2006;
        lateryear = Convert.ToInt16(parameters["lateryear"]);
        highlimit = Convert.ToInt16(parameters["highlimit"]);
        lowlimit = Convert.ToInt16(parameters["lowlimit"]);
        highlimitdn = Convert.ToSingle(parameters["highlimitdn"].Replace(',', '.'));
        lowlimitdn = Convert.ToSingle(parameters["lowlimitdn"].Replace(',', '.'));
        //highlimitdn = Convert.ToSingle(parameters["highlimitdn"].Replace('.', ','));
        //lowlimitdn = Convert.ToSingle(parameters["lowlimitdn"].Replace('.', ','));
        commonvotings = Convert.ToInt16(parameters["commonvotings"]);
        sort = parameters["sort"];
        var allvoters = new HashSet<string>();
        var yearrgx = new Regex(@"\d{4}");
        var rfabs = new Dictionary<string, bool>();
        var votings = new Dictionary<string, votes_on_election>();
        var results = new Dictionary<string, voterdata>();

        var rdr = new StreamReader("elections.txt");
        while (!rdr.EndOfStream)
        {
            string voting = rdr.ReadLine();
            int year = 0;
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
            votings.Add(voting, new votes_on_election() {yes = new HashSet<string>(), no = new HashSet<string>() });
            string[] yesarray = new string[0], noarray = new string[0];

            var yesstring = rdr.ReadLine();
            if (yesstring != "")
                yesarray = yesstring.Split('\t');
            foreach (var yesvoter in yesarray)
                if (yesvoter != "")
                {
                    votings[voting].yes.Add(yesvoter);
                    if (!allvoters.Contains(yesvoter))
                    {
                        allvoters.Add(yesvoter);
                        results.Add(yesvoter, new voterdata());
                    }
                }

            var nostring = rdr.ReadLine();
            if (nostring != "")
                noarray = nostring.Split('\t');
            foreach (var novoter in noarray)
                if (novoter != "")
                {
                    votings[voting].no.Add(novoter);
                    if (!allvoters.Contains(novoter))
                    {
                        allvoters.Add(novoter);
                        results.Add(novoter, new voterdata());
                    }
                }
        }

        foreach (var voting in votings)
            foreach (var voter in allvoters)
                if (voting.Value.yes.Contains(searcheduser) || voting.Value.no.Contains(searcheduser))
                {
                    if ((voting.Value.yes.Contains(searcheduser) && voting.Value.yes.Contains(voter)) || (voting.Value.no.Contains(searcheduser) && voting.Value.no.Contains(voter)))
                    {
                        results[voter].samevotes++;
                        results[voter].commonvotings++;
                        results[voter].wkdmtotal++;
                    }
                    if ((voting.Value.yes.Contains(searcheduser) && voting.Value.no.Contains(voter)) || (voting.Value.no.Contains(searcheduser) && voting.Value.yes.Contains(voter)))
                    {
                        results[voter].opposevotes++;
                        results[voter].commonvotings++;
                        results[voter].wkdmtotal++;
                    }
                }
                else if (voting.Value.yes.Contains(voter) || voting.Value.no.Contains(voter))
                        results[voter].wkdmtotal++;

        foreach(var r in results)
        {
            r.Value.diff = r.Value.samevotes - r.Value.opposevotes;
            r.Value.normalized_diff = (float)r.Value.diff / r.Value.commonvotings;
            r.Value.wkdm_normal = (float)r.Value.diff / r.Value.wkdmtotal;
        }

        if (!results.ContainsKey(searcheduser))
        {
            Sendresponse("Искомый участник не голосовал в указанный период времени", searcheduser, earlieryear, lateryear, highlimit, lowlimit, highlimitdn, lowlimitdn, commonvotings, sort);
            return;
        }

        result = "За указанный период времени в русской Википедии прошло " + votings.Count + " голосований (ЗСА, ЗСБ, ВАРБ по каждому кандидату в каждый созыв в отдельности), из них в " +
            results[searcheduser].wkdmtotal + " поучаствовал указанный участник.<br><br><table id=\"table\" border=\"1\" cellspacing=\"0\"><tr><th>Участник</th><th>⇈</th><th>⇅</th><th>D</th>" +
            "<th>⋂</th><th>∪</th><th>D / ⋂</th><th>D / ∪</th></tr>\n";

        if (sort == "d")
            foreach (var voter in results.OrderByDescending(r => r.Value.diff))
                showtable(voter);
        else if (sort == "dn")
            foreach (var voter in results.OrderByDescending(r => r.Value.normalized_diff))
                showtable(voter);
        else if (sort == "wkdm")
            foreach (var voter in results.OrderByDescending(r => r.Value.wkdm_normal))
                showtable(voter);
        else
        {
            Sendresponse("Неверный параметр сортировки", searcheduser, earlieryear, lateryear, highlimit, lowlimit, highlimitdn, lowlimitdn, commonvotings, sort);
            return;
        }

        Sendresponse(result + "</table>", searcheduser, earlieryear, lateryear, highlimit, lowlimit, highlimitdn, lowlimitdn, commonvotings, sort);
    }
}
