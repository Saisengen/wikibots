using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Net;
using DotNetWikiBot;
using System.Web.UI;
using System.Text;
using System.Threading;

class arbvote
{
    public string voter;
    public DateTime timestamp;
    public bool support;
}
class Program // с лабса работает медленнее, пускай локально
{
    static WebClient cl = new WebClient();
    //static Dictionary<string, string> fixes_and_merges = new Dictionary<string, string>{{ "Borealis55", "Daphne mesereum"}, { "ИкИлевап", "Pikryukov" },{ "Emin Bashirov", "Abu Zarr" }, { "Kor!An", "Andrey Korzun" }, { "Алексей Глушков", "Gajmar" },{ "Makakaaaa", "Lpi4635" },
    //{ "Microcell", "TheStrayCat" },{ "Temirov1960", "Игорь Темиров" }, { "Shanghainese.ua--", "Shanghainese.ua" },{ "Morrfeux", "Meiræ" }, { "Nоvа", "Andrey" }, { "Drakosha999", "Quaerite" }, { "Alexei Pechko", "TheCureMan" }, { "Cchrx23", "Александр Чебоксарский" } };
    static Dictionary<string, string> fixes_and_merges = new Dictionary<string, string>{ { "Wanderer", "Wanderer777" }, { "D.bratchuk", "Good Will Hunting" }, { "Qh13", "VladXe" }, { "Ыфь77", "VladXe" }, { "Грей2010", "Ouaf-ouaf2021" }, { "Ouaf-ouaf2010", "Ouaf-ouaf2021" },
        { "Гав-Гав2010", "Ouaf-ouaf2021" }, { "Гав-Гав2020", "Ouaf-ouaf2021"}, { "Гав-Гав2021", "Ouaf-ouaf2021"}, { "Le Loy", "Ле Лой" }, { "Otria1", "Otria" }, { "User239", "Dimetr"}, { "Vlsergey-at-work", "Vlsergey"}, { "Bcba3cf28a06", "Pikryukov" }};
    static Dictionary<string, string> processed_users = new Dictionary<string, string>();
    static Dictionary<string, HashSet<string>> yes = new Dictionary<string, HashSet<string>>();
    static Dictionary<string, HashSet<string>> no = new Dictionary<string, HashSet<string>>();
    static HashSet<string> elections = new HashSet<string>();
    static Dictionary<string, Pair> renamed_users = new Dictionary<string, Pair>();
    static Regex voterrgx = new Regex(@"^#[^\*:].*\[(у:|участник:|участница:|u:|user:|оу:|обсуждение участника:|обсуждение участницы:|ut:|user talk:|special:contribs/|special:contributions/|
служебная:вклад/)\s*([^#|\]]*)\s*[#|\]]", RegexOptions.IgnoreCase);
    static int earlieryear = 2006, lateryear = DateTime.Now.Year;//ЗСА имеют правильный формат с 2006, ВАРБ - с осени 2007
    static DateTime SULfinalisation = new DateTime(2015, 04, 20);
    static void Main()
    {
        gather_renamed_users();
        gather_rfabs();
        gather_arbvotings();

        var result = new StreamWriter("elections.txt");
        var renames = new StreamWriter("renames.txt");
        foreach (var renamed_user in renamed_users.OrderBy(r => r.Key))
            renames.WriteLine(renamed_user.Key + "\t" + renamed_user.Value.First + "\t" + renamed_user.Value.Second);
        foreach (var election_id in elections)
        {
            result.WriteLine(election_id);

            foreach (var yesvoter in yes[election_id])
                result.Write('\t' + yesvoter);
            result.WriteLine();

            foreach (var novoter in no[election_id])
                result.Write('\t' + novoter);
            result.WriteLine();
        }
        result.Close();
        renames.Close();
    }
    static void Addvoter(string voter, string election_id, bool support)
    {
        voter = voter.First().ToString().ToUpper() + voter.Substring(1).Replace('_', ' ');
        if (voter.Contains('/'))
            voter = voter.Substring(0, voter.IndexOf('/'));

        if (!processed_users.ContainsKey(voter))
        {
            string oldvoter = voter;
            foreach (var move in renamed_users.Values)
                if (voter == move.First.ToString())
                    voter = move.Second.ToString();
            foreach (var fix in fixes_and_merges)
                if (fixes_and_merges.ContainsKey(voter))
                    voter = fixes_and_merges[voter];
            //if (oldvoter != voter)
            processed_users.Add(oldvoter, voter);
        }
        else voter = processed_users[voter];

        //if (merged_users.ContainsKey(voter))
        //voter = merged_users[voter];
        if (support)
            yes[election_id].Add(voter);
        else
            no[election_id].Add(voter);
    }
    static void gather_renamed_users()
    {
        string cont, query, apiout;
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&list=logevents&leprop=details%7Ctimestamp%7Ctitle&leaction=renameuser%2Frenameuser&lestart=2007-08-28T00%3A00%3A00.000Z&leend=2012-12-23T00%3A00%3A00.000Z&ledir=newer&lelimit=max"))))
            while (r.Read())
                if (r.Name == "item" && r.NodeType == XmlNodeType.Element)
                {
                    string ts = r.GetAttribute("timestamp");
                    string oldname = r.GetAttribute("title");
                    if (oldname == null || oldname.EndsWith(" old") || oldname.EndsWith("-old") || oldname.EndsWith("-Old") || oldname.EndsWith(" (old)") || oldname.EndsWith(" (usurped)") || oldname.EndsWith("~usurped") || oldname.EndsWith(" (usurp)"))
                        continue;
                    oldname = oldname.Substring(oldname.IndexOf(':') + 1);
                    //if (oldname.EndsWith("~ruwiki"))
                    //    oldname = oldname.Replace("~ruwiki", "");
                    r.Read(); r.Read(); r.Read();
                    if (!r.Value.StartsWith("--"))
                        try { renamed_users.Add(ts, new Pair() { First = oldname, Second = r.Value }); } catch { } //есть дублирующаяся запись в логах 2010-12-07T11:33:36Z
                }

        cont = ""; query = "/w/api.php?action=query&format=xml&list=logevents&leprop=details%7Ctimestamp&leaction=renameuser%2Frenameuser&lestart=2012-12-23T00%3A00%3A00.000Z&ledir=newer&lelimit=max";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&lecontinue=" + Uri.EscapeDataString(cont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item" && r.NodeType == XmlNodeType.Element)
                    {
                        string ts = r.GetAttribute("timestamp");
                        r.Read();
                        if (Convert.ToInt32(r.GetAttribute("edits")) >= 450)
                        {
                            string oldname = r.GetAttribute("olduser");
                            //if (oldname.EndsWith("~ruwiki") && new DateTime(Convert.ToInt16(ts.Substring(0, 4)), Convert.ToInt16(ts.Substring(5, 2)), Convert.ToInt16(ts.Substring(8, 2))) < SULfinalisation)
                            //    oldname = oldname.Replace("~ruwiki", "");
                            renamed_users.Add(ts, new Pair() { First = oldname, Second = r.GetAttribute("newuser") });
                        }
                    }
            }
        }

        site = new Site("https://meta.wikimedia.org", creds[0], creds[1]);
        cont = ""; query = "/w/api.php?action=query&format=xml&list=logevents&leprop=details%7Ctimestamp&leaction=gblrename%2Frename&ledir=newer&lelimit=max";
        while (cont != null)
        {
            apiout = (cont == "" ? site.GetWebPage(query) : site.GetWebPage(query + "&lecontinue=" + Uri.EscapeDataString(cont)));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("lecontinue");
                while (r.Read())
                    if (r.Name == "item" && r.NodeType == XmlNodeType.Element)
                    {
                        string ts = r.GetAttribute("timestamp");
                        r.Read();
                        string oldname = r.GetAttribute("olduser");
                        string newname = r.GetAttribute("newuser");
                        var pair = new Pair() { First = oldname, Second = newname };
                        if (!renamed_users.ContainsKey(ts) && oldname != null && newname != null && !renamed_users.ContainsValue(pair))
                            renamed_users.Add(ts, pair);
                    }
            }
        }
    }
    static void gather_rfabs()
    {
        var rfargx = new Regex(@"\{\{ЗСА/Архив\|([^|]*)\|");
        var rfbrgx = new Regex(@"{{/Строка\|[^|]+(\d{4})\s*\|[^|]+\|([^|]+)\|");
        var voteblockrgx = new Regex(@"За\s*==.*\n.*Против\s*==.*\n==", RegexOptions.Singleline);
        var opposergx = new Regex(@"Против\s*=="); //может быть картинка в начале
        var rfabs = new Dictionary<string, bool>();

        for (int year = earlieryear; year <= lateryear; year++)
        {
            var rfas_in_year = Getpage("Википедия:Заявки на статус администратора/Архив/" + year).Split('\n');
            foreach (var s in rfas_in_year)
                if (rfargx.IsMatch(s))
                {
                    elections.Add(year + "adm " + rfargx.Match(s).Groups[1].ToString());
                    rfabs.Add(year + "adm " + rfargx.Match(s).Groups[1].ToString(), false);
                }
        }
        var rfbs = Getpage("Википедия:Заявки на статус бюрократа/Архив").Split('\n');
        foreach (var s in rfbs)
            if (rfbrgx.IsMatch(s))
            {
                int year = Convert.ToInt16(rfbrgx.Match(s).Groups[1].ToString());
                if (year >= earlieryear && year <= lateryear && !rfbrgx.Match(s).Groups[2].ToString().Contains('['))
                {
                    elections.Add(year + "bur " + rfbrgx.Match(s).Groups[2].ToString());
                    rfabs.Add(year + "bur " + rfbrgx.Match(s).Groups[2].ToString(), true);
                }
            }

        foreach (var rfab_id in rfabs.Keys)
        {
            yes.Add(rfab_id, new HashSet<string>());
            no.Add(rfab_id, new HashSet<string>());
            string voteblock = voteblockrgx.Match(Getpage("ВП:Заявки на статус " + (rfabs[rfab_id] ? "бюрократа" : "администратора") + "/" + rfab_id.Substring(8))).Value.ToString();
            bool support = true;
            foreach (var @string in voteblock.Split('\n'))
            {
                if (opposergx.IsMatch(@string)) //если началась секция против
                    support = false;
                if (voterrgx.IsMatch(@string)) //если реплика написана с нулевым отступом
                {
                    string voter = voterrgx.Match(@string).Groups[2].Value;
                    Addvoter(voter, rfab_id, support);
                }
            }
        }
    }
    static void gather_arbvotings()
    {
        var signature = new Regex(@"(\d\d:\d\d, \d{1,2} \w+ \d{4}) \(UTC\)");
        var yearrgx = new Regex(@"\d{4}");
        var arbvotepage = new Regex(@"Википедия:Выборы арбитров/([^/]*/Голосование/\+/.+)");
        string cont = "", query = "/w/api.php?action=query&format=xml&list=allpages&apprefix=Выборы арбитров/&apnamespace=4&aplimit=max", apiout;
        while (cont != null)
        {
            apiout = (cont == "" ? Getapi(query, "ru.wikipedia") : Getapi(query + "&apcontinue=" + Uri.EscapeDataString(cont), "ru.wikipedia"));
            using (var r = new XmlTextReader(new StringReader(apiout)))
            {
                r.WhitespaceHandling = WhitespaceHandling.None;
                r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("apcontinue");
                while (r.Read())
                    if (r.Name == "p" && arbvotepage.IsMatch(r.GetAttribute("title")))//если существует страница /+/ по кандидату, обрабатываем +/- выборы по нему
                    {
                        var votes_by_time = new List<arbvote>();
                        var voted_users = new HashSet<string>();
                        string title = r.GetAttribute("title");
                        string varb_id = arbvotepage.Match(title).Groups[1].ToString().Replace("/Голосование/+/", "/");
                        int year = Convert.ToInt32(yearrgx.Match(varb_id.Substring(0, varb_id.IndexOf('/'))).ToString());
                        if (year < earlieryear || year > lateryear)
                            continue;
                        elections.Add(varb_id);
                        yes.Add(varb_id, new HashSet<string>());
                        no.Add(varb_id, new HashSet<string>());

                        foreach (var @string in Getpage(title).Split('\n')) // голоса за
                            if (signature.IsMatch(@string))
                                votes_by_time.Add(new arbvote() { voter = voterrgx.Match(@string).Groups[2].Value, timestamp = DateTime.Parse(signature.Match(@string).Groups[1].Value,
                                    System.Globalization.CultureInfo.GetCultureInfo("ru-RU")), support = true });

                        foreach (var @string in Getpage(title.Replace("/+/", "/-/")).Split('\n')) // голоса против
                            if (signature.IsMatch(@string))
                                votes_by_time.Add(new arbvote() { voter = voterrgx.Match(@string).Groups[2].Value, timestamp = DateTime.Parse(signature.Match(@string).Groups[1].Value,
                                    System.Globalization.CultureInfo.GetCultureInfo("ru-RU")), support = false });

                        foreach (var vote in votes_by_time.OrderByDescending(vote => vote.timestamp))
                            if (!voted_users.Contains(vote.voter) && vote.voter != "")
                            {
                                Addvoter(vote.voter, varb_id, vote.support);
                                voted_users.Add(vote.voter);
                            }
                    }
            }
        }
    }
    static string Getpage(string pagename)
    {
        Console.WriteLine(pagename);
        //Thread.Sleep(800);
        return Encoding.UTF8.GetString(cl.DownloadData("https://ru.wikipedia.org/wiki/" + pagename.Replace("&#61;", "=") + "?action=raw"));
    }
    static string Getapi(string api, string domain)
    {
        return Encoding.UTF8.GetString(cl.DownloadData("https://" + domain + ".org" + api));
    }
}
