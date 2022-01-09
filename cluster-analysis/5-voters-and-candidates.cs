using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using System.Xml;
using System.Text.RegularExpressions;
public class User
{
    public int userid, editcount, blockid, blockedbyid;
    public string name, registration, firstedit, lastedit, blockedby, blockreason, blockexpiry;
    public List<string> groups;
    public DateTime blockedtimestamp;
    public bool blockanononly, blockpartial, blocknocreate;
}
public class Query
{
    public List<User> users;
}
public class Root
{
    public string batchcomplete;
    public Query query;
}
class voterspercandidate
{
    public HashSet<string> yes, no;
}
class Program
{
    static bool method_is_post = false;
    static Dictionary<string, voterspercandidate> candidates = new Dictionary<string, voterspercandidate>();
    static HashSet<string> unimportant_flags = new HashSet<string>() { "*", "user", "autoconfirmed", "rollbacker", "suppressredirect", "uploader" };
    static string result = "<table border=\"1\" cellspacing=\"0\"><tr><th style=\"writing-mode:horizontal-tb; transform:rotate(0);\">Голосующий</th><th style=\"writing-mode:horizontal-tb;transform:rotate(0);\">Регистрация</th>" +
        "<th style=\"writing-mode:horizontal-tb; transform:rotate(0);\">Первая правка</th><th style=\"writing-mode:horizontal-tb; transform:rotate(0);\">Последняя правка</th><th style=\"writing-mode:horizontal-tb; transform:rotate(0);\">Правок</th>" +
        "<th style=\"writing-mode:horizontal-tb; transform:rotate(0);\">Флаги</th><th style=\"writing-mode:horizontal-tb; transform:rotate(0);\">Блок</th>";
    static HashSet<string> allvoters = new HashSet<string>();
    static Root usersinfo = new Root();
    static WebClient cl = new WebClient();
    static HashSet<string> requeststrings = new HashSet<string>();
    static StreamReader rdr;
    static Regex yearrgx = new Regex(@"\d{4}");

    static void Sendresponse(string result, string elections, string users, string sort, bool allvoters, string mode, int earlieryear, int lateryear)
    {
        var sr = new StreamReader(method_is_post ? "clusters-template5.5.txt" : "clusters-template5.txt");
        string output = sr.ReadToEnd().Replace("%result%", result).Replace("%elections%", elections).Replace("%users%", users).Replace("%earlieryear%", earlieryear.ToString()).Replace("%lateryear%", lateryear.ToString());
        if (mode == "years")
            output = output.Replace("%checked_years%", "checked");
        else
            output = output.Replace("%checked_varb%", "checked");
        if (sort == "reg")
            output = output.Replace("%selected_reg%", "selected");
        else if (sort == "first")
            output = output.Replace("%selected_first%", "selected");
        else if (sort == "last")
            output = output.Replace("%selected_last%", "selected");
        else if (sort == "edits")
            output = output.Replace("%selected_edits%", "selected");
        else if (sort == "flags")
            output = output.Replace("%selected_flags%", "selected");
        else if (sort == "block")
            output = output.Replace("%selected_block%", "selected");
        else
            output = output.Replace("%selected_no%", "selected");
        if (allvoters)
            output = output.Replace("%checked_allvoters%", "checked");
        Console.WriteLine(output);
        Console.WriteLine();
    }
    static void writerow(User voter)
    {
        string flags = "";
        if (voter.groups != null)
            foreach (var flag in voter.groups)
                if (!unimportant_flags.Contains(flag))
                    flags += ',' + flag;
        if (flags != "")
            flags = flags.Substring(1);
        string regdate = (voter.registration == null ? "" : voter.registration.Substring(0, 10));
        string editdate = (voter.firstedit == null ? "" : voter.firstedit.Substring(0, 10));
        string blockexpiry = (voter.blockexpiry == null ? "" : voter.blockexpiry == "infinite" ? "∞" : "<abbr title=\"" + voter.blockexpiry + "\">⏱</abbr>");
        string blockpartial = (voter.blockpartial == true ? "<abbr title=\"частичная\">🧩</abbr>" : "");
        string startdates = (regdate == editdate ? "<td colspan=\"2\">" + regdate + "</td>" : "<td>" + regdate + "</td><td>" + editdate + "</td>") + "</td>";
        result += "\n<tr><td><a href=\"https://ru.wikipedia.org/wiki/special:centralauth/" + Uri.EscapeDataString(voter.name) + "\">" + voter.name + "</a></td>" + startdates + "<td>" +
            (voter.lastedit == null ? "" : voter.lastedit.Substring(0, 10)) + "</td><td>" + voter.editcount + "</td><td>" + flags + "</td><td>" + blockexpiry + blockpartial + "</td>\n";
        foreach (var c in candidates.Keys)
        {
            string color = (candidates[c].yes.Contains(voter.name) ? "0f0" : candidates[c].no.Contains(voter.name) ? "f00" : "fff");
            result += "<td style=\"background-color:#" + color + "\"><abbr title=\"" + voter.name + " / " + c + "\">" + (candidates[c].yes.Contains(voter.name) ? "+" : candidates[c].no.Contains(voter.name) ? "−" : "_") + "</abbr></td>\n";
        }
        result += "</tr>";
    }
    static void process_votes(string votename)
    {
        candidates.Add(votename, new voterspercandidate() { yes = new HashSet<string>(), no = new HashSet<string>() });
        var yes = rdr.ReadLine().Split('\t');
        foreach (var y in yes)
        {
            candidates[votename].yes.Add(y);
            if (!allvoters.Contains(y))
                allvoters.Add(y);
        }
        var no = rdr.ReadLine().Split('\t');
        foreach (var n in no)
        {
            candidates[votename].no.Add(n);
            if (!allvoters.Contains(n))
                allvoters.Add(n);
        }
        candidates[votename].yes.Remove("");
        candidates[votename].no.Remove("");
        allvoters.Remove("");
    }
    static void Main()
    {
        rdr = new StreamReader("electionnames.txt");
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
        //Environment.SetEnvironmentVariable("QUERY_STRING", "elections=Лето+2021&users=nefedechev%0D%0Afleor%0D%0Atarkhil%0D%0Awiki.wiki1919&sort=no");
        string input = method_is_post ? Console.ReadLine() : Environment.GetEnvironmentVariable("QUERY_STRING");
        if (input == null || input == "")
        {
            Sendresponse("", electionsstring, "", "no", false, "varb", DateTime.Now.Year, DateTime.Now.Year);
            return;
        }
        var parameters = HttpUtility.ParseQueryString(input);
        string electionforanalyze = parameters["elections"];
        string mode = parameters["mode"] == "years" ? "years" : "varb";
        int earlieryear = Convert.ToInt16(parameters["earlieryear"]);
        if (earlieryear < 2006 || earlieryear > DateTime.Now.Year) earlieryear = 2006;
        int lateryear = Convert.ToInt16(parameters["lateryear"]);
        if (lateryear < 2006 || lateryear > DateTime.Now.Year) lateryear = DateTime.Now.Year;
        electionsstring = electionsstring.Replace('s' + electionforanalyze + '>', "selected>");
        var rawvoterslist = parameters["users"].Replace("\u200E", "").Replace("\r\n", "\t").Replace("\n", "\t").Replace("\r", "\t").Split('\t');
        var voterslist = new HashSet<string>();
        foreach(var voter in rawvoterslist)
            if (voter != "")
                try { voterslist.Add(voter[0].ToString().ToUpper() + voter.Substring(1)); } catch { }
        string sort = parameters["sort"];
        bool showallvoters = parameters["allvoters"] != null;

        rdr = new StreamReader("elections.txt");
        if (mode == "varb")
            while (!rdr.EndOfStream)
            {
                string voting = rdr.ReadLine();
                if (!voting.StartsWith(electionforanalyze + '/'))
                {
                    rdr.ReadLine(); rdr.ReadLine();
                    continue;
                }
                else
                    process_votes(voting.Substring(voting.IndexOf('/') + 1));
            }
        else
            while (!rdr.EndOfStream)
            {
                string voting = rdr.ReadLine();
                if (voting.StartsWith("Зима 2019—2020"))
                    voting = voting.Replace("Зима 2019—2020", "Зима 2020");
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
                process_votes(voting);
            }

        string idset = ""; int cntr = 0;
        foreach (var user in showallvoters ? allvoters : voterslist)
        {
            idset += "|" + user;
            if (++cntr % 49 == 0)
            {
                requeststrings.Add(idset.Substring(1));
                idset = "";
            }
        }
        if (idset != "")
            requeststrings.Add(idset.Substring(1));

        usersinfo = JsonConvert.DeserializeObject<Root>(cl.DownloadString("https://ru.wikipedia.org/w/api.php?action=query&format=json&list=users&formatversion=latest&usprop=blockinfo%7Ceditcount%7Cgroups%7Cregistration&ususers=MBH"));
        usersinfo.query.users.Clear();//чтобы создать структуру внутри объекта
        foreach (var requeststring in requeststrings)
        {
            var partusersinfo = JsonConvert.DeserializeObject<Root>(cl.DownloadString("https://ru.wikipedia.org/w/api.php?action=query&format=json&formatversion=latest&list=users&usprop=blockinfo%7Ceditcount%7Cgroups%7Cregistration&ususers=" + requeststring));
            foreach (var u in partusersinfo.query.users)
                usersinfo.query.users.Add(u);
        }
        
        foreach (var user in showallvoters ? allvoters : voterslist)
        {
            using (var r = new XmlTextReader(new StringReader(cl.DownloadString("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucdir=newer&ucprop=timestamp&ucuser=" + user))))
                while (r.Read())
                    if (r.Name == "item")
                        for (int n = 0; n < usersinfo.query.users.Count; n++)
                            if (usersinfo.query.users[n].name == user)
                                usersinfo.query.users[n].firstedit = r.GetAttribute("timestamp");
            using (var r = new XmlTextReader(new StringReader(cl.DownloadString("https://ru.wikipedia.org/w/api.php?action=query&format=xml&list=usercontribs&uclimit=1&ucdir=older&ucprop=timestamp&ucuser=" + user))))
                while (r.Read())
                    if (r.Name == "item")
                        for (int n = 0; n < usersinfo.query.users.Count; n++)
                            if (usersinfo.query.users[n].name == user)
                                usersinfo.query.users[n].lastedit = r.GetAttribute("timestamp");
        }

        foreach (var c in candidates)
            result += "<th>" + c.Key + "</th>\n";
        result += "</tr>";
        if (sort == "reg")
            foreach (var voter in usersinfo.query.users.OrderByDescending(u => u.registration))
                writerow(voter);
        else if (sort == "first")
            foreach (var voter in usersinfo.query.users.OrderByDescending(u => u.firstedit))
                writerow(voter);
        else if (sort == "last")
            foreach (var voter in usersinfo.query.users.OrderByDescending(u => u.lastedit))
                writerow(voter);
        else if (sort == "edits")
            foreach (var voter in usersinfo.query.users.OrderByDescending(u => u.editcount))
                writerow(voter);
        else if (sort == "flags")
            foreach (var voter in usersinfo.query.users.OrderByDescending(u => u.groups.Count))
                writerow(voter);
        else if (sort == "block")
            foreach (var voter in usersinfo.query.users.OrderByDescending(u => u.blockexpiry))
                writerow(voter);
        else
            foreach (var voter in usersinfo.query.users)
                writerow(voter);

        result += "</table>";
        Sendresponse(result, electionsstring, parameters["users"], sort, showallvoters, mode, earlieryear, lateryear);
    }
}
