using DotNetWikiBot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Newtonsoft.Json;

public class Limits
{
    public int recentchanges;
}
public class Damaging
{
    public double @true, @false;
}
public class Goodfaith
{
    public double @true, @false;
}
public class Oresscores
{
    public Damaging damaging;
    public Goodfaith goodfaith;
}
public class Recentchanges
{
    public string type, title, user, anon;
    public int ns, pageid, revid, old_revid, rcid;
    public Oresscores oresscores;
}
public class Query
{
    public List<Recentchanges> recentchanges;
}
public class Root
{
    public string batchcomplete;
    public Limits limits;
    public Query query;
}
class Program
{
    static int Main()
    {
        var goodanons = new HashSet<string>();
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        Site site = new Site("https://ru.wikipedia.org", creds[4], creds[5]);
        var reportedusersrx = new Regex(@"\| вопрос = u/(.*)");
        var rowrx = new Regex(@"\|-");
        string csrftoken = "", rollbacktoken = "", apiout = "";
        double lowlimit = 1, mediumlimit = 1;
        int currminute = -1, newlasteditid = 0, lasteditid = 0;
        var badusers = new HashSet<string>();
        while (true)
        {
            try
            {
                var dtn = DateTime.UtcNow.AddSeconds(-15);
                if (currminute != dtn.Minute / 10)
                {
                    currminute = dtn.Minute / 10;
                    using (var r = new XmlTextReader(new StringReader(site.GetWebPage("/w/api.php?action=query&format=xml&meta=tokens&type=csrf%7Crollback"))))
                        while (r.Read())
                            if (r.Name == "tokens")
                            {
                                rollbacktoken = Uri.EscapeDataString(r.GetAttribute("rollbacktoken"));
                                csrftoken = Uri.EscapeDataString(r.GetAttribute("csrftoken"));
                                break;
                            }
                    var limits = site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=user:MBH/limits.css&action=raw").Split('\n');
                    lowlimit = Convert.ToDouble(limits[0]);
                    mediumlimit = Convert.ToDouble(limits[1]);//точка в запятую для отладки
                    var ga_raw = site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=user:MBH/goodanons.css&action=raw").Split('\n');
                    foreach (var g in ga_raw)
                        goodanons.Add(g);
                }
                bool newle = false;
                apiout = site.GetWebPage("/w/api.php?action=query&format=json&list=recentchanges&utf8=1&rcend=" + dtn.Year + "-" + dtn.ToString("MM") + "-" + dtn.ToString("dd") + "T" +
                    dtn.ToString("HH") + "%3A" + dtn.ToString("mm") + "%3A" + dtn.ToString("ss") + ".000Z&rcnamespace=0&rcprop=title%7Cuser%7Coresscores%7Cids&rcshow=oresreview&rclimit=max&rctype=edit");
                var data = JsonConvert.DeserializeObject<Root>(apiout);
                foreach (var record in data.query.recentchanges)
                {
                    double damaging = Math.Round(record.oresscores.damaging.@true, 3);
                    if (damaging < lowlimit)
                        continue;
                    string user = record.user;
                    if (goodanons.Contains(user))
                        continue;
                    bool user_is_anon = record.anon != null;
                    double goodfaith = Math.Round(record.oresscores.goodfaith.@true, 3);
                    string title = record.title;
                    int editid = record.revid;
                    if (!newle)
                    {
                        newlasteditid = editid;
                        newle = true;
                    }
                    if (editid == lasteditid)
                        break;

                    if (badusers.Contains(user))
                    {
                        string zkab = site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=ВП:Запросы_к_администраторам/Быстрые&action=raw");
                        var reportedusers = reportedusersrx.Matches(zkab);
                        bool reportedyet = false;
                        foreach (Match r in reportedusers)
                            if (user == r.Groups[1].Value)
                                reportedyet = true;
                        if (!reportedyet)
                            site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=ВП:Запросы к администраторам/Быстрые&summary=[[special:contribs/" + user +
                            "]] - новый запрос&appendtext=\n\n{{subst:t:preload/ЗКАБ/subst|участник=" + user + "|пояснение=}}&token=" + csrftoken);
                    }
                    else badusers.Add(user);

                    if (damaging < mediumlimit)
                    {
                        string text = site.GetWebPage("https://ru.wikipedia.org/w/index.php?title=user:Рейму_Хакурей/Проблемные_правки&action=raw").Replace("!Дифф!!Статья!!Автор!!damaging!!" +
                            "goodfaith", "!Дифф!!Статья!!Автор!!damaging!!goodfaith\n|-\n|[[special:diff/" + editid + "|diff]]||[//ru.wikipedia.org/w/index.php?title=" + title.Replace(' ', '_') +
                            "&action=history " + title + "]||[[special:contribs/" + user + "|" + user + "]]||" + damaging + "||" + goodfaith);
                        var rows = rowrx.Matches(text);
                        text = text.Substring(0, rowrx.Matches(text)[rowrx.Matches(text).Count - 1].Index);
                        string answer = site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=u:Рейму_Хакурей/Проблемные_правки&summary=[[special:diff/" + editid + "|diff]], " +
                            "[[special:history/" + title + "|" + title + "]], [[special:contribs/" + user + "|" + user + "]], " + damaging + "/" + goodfaith + "&text=" + Uri.EscapeDataString(text) +
                            "&token=" + csrftoken);
                    }
                    else
                    {
                        string answer = site.PostDataAndGetResult("/w/api.php?action=rollback&format=xml", "title=" + title + "&user=" + user + "&summary=[[u:Рейму Хакурей|" +
                            "автоматическая отмена]] правки участника [[special:contribs/" + user + "|" + user + "]] (" + damaging + "/" + goodfaith + ")" + "&token=" + rollbacktoken);
                        if (answer.Contains("<rollback title="))
                            site.PostDataAndGetResult("/w/api.php?action=edit&format=xml", "title=user talk:" + user + "&section=new&summary=" + (user_is_anon ? "Правка с вашего IP-адреса" :
                                "Ваша правка") + " в статье [[" + title + "]] " + "автоматически отменена&text={{subst:u:Рейму_Хакурей/Уведомление|" + editid + "|" + title + "|" + damaging + "|" +
                                goodfaith + "|" + (user_is_anon ? "1" : "") + "}}&token=" + csrftoken);
                        else
                        {
                            Console.WriteLine(title);
                            Console.WriteLine(answer);
                        }
                    }
                }
                lasteditid = newlasteditid;
            }
            catch (Exception e)
            {
                if (e.ToString().Contains("deserialize"))
                    Console.WriteLine("\n" + apiout);
                else
                    Console.WriteLine(e.ToString());
            }
            Thread.Sleep(10000);
        }
    }
}
