using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using DotNetWikiBot;

class MyBot : Bot
{
    static string[] creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
    public string[] Settings(byte num, Site site)
    {
        string[] ar = new string[num];
        Page setting = new Page(site, "user:MBH/incubator.js");
        setting.Load();
        Regex all = new Regex(@"all.?=.?true", RegexOptions.Singleline);
        Regex inc = new Regex(@"incubator.?=.?true", RegexOptions.Singleline);
        Regex cat = new Regex(@"main_cat.?=.?.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex inc_wp = new Regex(@"inc_wp.?=.?true*", RegexOptions.Singleline);
        Regex inc_inc = new Regex(@"inc_inc.?=.?true*", RegexOptions.Singleline);
        Regex inc_cat = new Regex(@"inc_cat.?=.?true*", RegexOptions.Singleline);
        Regex inc_iwiki = new Regex(@"inc_iwiki.?=.?true*", RegexOptions.Singleline);
        Regex inc_max = new Regex(@"inc_max.?=.?\d*" + Regex.Escape(";"), RegexOptions.Singleline);
        Regex inc_except = new Regex(@"inc_except.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
        if (all.Matches(setting.text).Count > 0)
        {
            if (inc.Matches(setting.text).Count > 0)
            {
                ar[0] = "1";
                if (inc_wp.Matches(setting.text).Count > 0)
                {
                    ar[1] = "1";
                }
                if (inc_inc.Matches(setting.text).Count > 0)
                {
                    ar[2] = "1";
                }
                if (inc_cat.Matches(setting.text).Count > 0)
                {
                    ar[3] = "1";
                }
                if (inc_iwiki.Matches(setting.text).Count > 0)
                {
                    ar[4] = "1";
                }
                if (inc_max.Matches(setting.text).Count > 0)
                {
                    string a = inc_max.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Replace(" ", "");
                    a = a.Replace(";", "");
                    ar[5] = a;
                }
                if (cat.Matches(setting.text).Count > 0)
                {
                    string a = cat.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Replace(";", "");
                    a = a.Replace("\"", "");
                    ar[6] = a;
                }
                if (inc_except.Matches(setting.text).Count > 0)
                {
                    string a = inc_except.Matches(setting.text)[0].ToString();
                    a = a.Substring(a.IndexOf("=") + 1).Trim();
                    a = a.Replace(";", "");
                    a = a.Replace("\"", "");
                    ar[7] = a;
                }
                return ar;
            }
            else
            { ar[0] = "0"; return ar; }
        }
        else
        { ar[0] = "0"; return ar; }
    }
    public static void Main()
    {
        Site site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        Page p = new Page(site, "Википедия:Проект:Инкубатор/Статьи");
        PageList pl = new PageList(site);
        MyBot bot = new MyBot();
        string[] set = new string[7];
        set = bot.Settings(8, site);
        if (set[0] == "1")
        {
            string[] pages = new string[Convert.ToInt32(set[5])];
            int q = 0;
            if (set[1] == "1")
            {
                string pURL = site.apiPath + "?action=query&list=allpages&apprefix=Проект:Инкубатор/Статьи&apnamespace=4&apfilterredir=nonredirects&aplimit=" + set[5] + "&format=xml";
                string h = site.GetWebPage(pURL);
                XmlTextReader rdr = new XmlTextReader(new StringReader(h));
                while (rdr.Read())
                {
                    if (rdr.NodeType == XmlNodeType.Element)
                    {
                        if (rdr.Name == "p")
                        {

                            string abc = rdr.GetAttribute("title");
                            if (abc != "Википедия:Проект:Инкубатор/Статьи Инкубатора")
                            {
                                if (abc != "Википедия:Проект:Инкубатор/Статьи/")
                                {
                                    pages[q] = abc;
                                    q++;
                                }
                            }
                            //pt = rdr.ReadString();
                        }
                    }
                }
            }
            if (set[2] == "1")
            {
                string pURL = site.apiPath + "?action=query&list=allpages&apnamespace=102&apfilterredir=nonredirects&aplimit=" + set[5] + "&format=xml";
                string h = site.GetWebPage(pURL);
                XmlTextReader rdr = new XmlTextReader(new StringReader(h));
                while (rdr.Read())
                {
                    if (rdr.NodeType == XmlNodeType.Element)
                    {
                        if (rdr.Name == "p")
                        {
                            pages[q] = rdr.GetAttribute("title");
                            q++;
                            //pt = rdr2.ReadString();
                        }
                    }
                }
            }
            Regex set_parser = new Regex(@".*?" + Regex.Escape("|"), RegexOptions.Singleline);
            int count = set_parser.Matches(set[7]).Count;
            string[] exceptions = new string[count];
            int kk = 0;
            foreach (Match m in set_parser.Matches(set[7])) // exceptions for bot
            {
                exceptions[kk] = m.ToString();
                exceptions[kk] = exceptions[kk].Remove(exceptions[kk].Length - 1); // remove "|" in the end of the string
                kk++;
            }
            for (int z = 0; z < q; z++)
            {
                string tttt = "";
                int except = 0;  // start of module for exception some pages
                try
                {
                    tttt = pages[z];
                    for (int ik = 0; ik < kk; ik++)
                    {
                        if (tttt == exceptions[ik])
                            except++;
                    }
                    if (except == 0) // end of exceptions
                    {
                        Page n = new Page(site, pages[z]);
                        bool e_inc, e_cat;
                        e_inc = e_cat = false;
                        string com = "";
                        n.Load();
                        string dbt = "";
                        string red = "";
                        if (n.text.IndexOf("nobots") == -1)
                        {
                            if (n.text.IndexOf("Инкубатор, Статья перенесена в ОП") == -1)
                            {
                                Regex r = new Regex(Regex.Escape("#") + "(REDIRECT|перенаправление) " + Regex.Escape("[[") + ".*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                                Regex db = new Regex(Regex.Escape("{{") + "db-.*?" + Regex.Escape("}}"), RegexOptions.Singleline);
                                for (int qw = 0; qw < r.Matches(n.text).Count; qw++)
                                {
                                    red = r.Matches(n.text)[qw].ToString();
                                }
                                for (int qw = 0; qw < db.Matches(n.text).Count; qw++)
                                {
                                    dbt = db.Matches(n.text)[qw].ToString();
                                }
                                string ttt = n.text;
                                while (ttt.IndexOf("\n") != -1)
                                    ttt = ttt.Replace("\n", "");
                                if (n.text.Length - red.Length - dbt.Length > 2)
                                {
                                    if (n.text.IndexOf("{{В инкубаторе") == -1)
                                    {
                                        if (n.text.IndexOf("{{в инкубаторе") == -1)
                                        {
                                            n.text = "{{В инкубаторе}}\n" + n.text;
                                            e_inc = true;
                                        }
                                    }
                                }
                                else if (n.text.Length == 0)
                                {
                                    n.text = "{{В инкубаторе}}\n" + n.text;
                                    e_inc = true;
                                }
                            }
                            string temp = n.text;
                            Regex comment = new Regex(Regex.Escape("<!--") + ".*?" + Regex.Escape("-->"), RegexOptions.Singleline);
                            foreach (Match m in comment.Matches(temp))
                            {
                                red = m.ToString();
                                while (temp.IndexOf(red) != -1)
                                {
                                    temp = temp.Replace(red, "");
                                }
                            }
                            if (set[3] == "1")
                            {
                                Regex cats = new Regex(Regex.Escape("[[") + "(Category|Категория).*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                                foreach (Match m in cats.Matches(temp))
                                {
                                    string replacer = m.ToString().Replace("[[", "[[:");
                                    n.text = n.text.Replace(m.ToString(), replacer);
                                    e_cat = true;
                                }
                            }
                            if (set[4] == "1")
                            {
                                Regex iwikis = new Regex(Regex.Escape("[[") + "(aa|ab|ace|af|ak|als|am|an|ang|ar|arc|arz|as|ast|av|ay|az|ba|bar|bat-smg|bcl|be|be-x-old|bg|bh|bi|bjn|bm|bn|bo|bpy|br|bs|bug|bxr|ca|cbk-zam|cdo|ce|ceb|ch|cho|chr|chy|ckb|co|cr|crh|cs|csb|cu|cv|cy|da|de|diq|dsb|dv|dz|ee|el|eml|en|eo|es|et|eu|ext|fa|ff|fi|fiu-vro|fj|fo|fr|frp|frr|fur|fy|ga|gag|gan|gd|gl|glk|gn|got|gu|gv|ha|hak|haw|he|hi|hif|ho|hr|hsb|ht|hu|hy|hz|ia|id|ie|ig|ii|ik|ilo|io|is|it|iu|ja|jbo|jv|ka|kaa|kab|kbd|kg|ki|kj|kk|kl|km|kn|ko|koi|kr|krc|ks|ksh|ku|kv|kw|ky|la|lad|lb|lbe|lez|lg|li|lij|lmo|ln|lo|lt|ltg|lv|map-bms|mdf|mg|mh|mhr|mi|mk|ml|mn|mo|mr|mrj|ms|mt|mus|mwl|my|myv|mzn|na|nah|nan|nap|nb|nds|nds-nl|ne|new|ng|nl|nn|no|nov|nrm|nso|nv|ny|oc|om|or|os|pa|pag|pam|pap|pcd|pdc|pfl|pi|pih|pl|pms|pnb|pnt|ps|pt|qu|rm|rmy|rn|ro|roa-rup|roa-tara|rue|rw|sa|sah|sc|scn|sco|sd|se|sg|sh|si|simple|sk|sl|sm|sn|so|sq|sr|srn|ss|st|stq|su|sv|sw|szl|ta|te|tet|tg|th|ti|tk|tl|tn|to|tokipona|tpi|tr|ts|tt|tum|tw|ty|udm|ug|uk|ur|uz|ve|vec|vep|vi|vls|vo|wa|war|wo|wuu|xal|xh|xmf|yi|yo|za|zea|zh|zh-classical|zh-cn|zh-min-nan|zh-tw|zh-yue|zu):.*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                                foreach (Match m in iwikis.Matches(temp))
                                {
                                    string replacer = m.ToString().Replace("[[", "[[:");
                                    n.text = n.text.Replace(m.ToString(), replacer);
                                    e_cat = true;
                                }
                            }
                            if (set[4] == "1")
                            {
                                Regex index = new Regex(Regex.Escape("__") + "(INDEX|ИНДЕКС)" + Regex.Escape("__"), RegexOptions.Singleline);
                                foreach (Match m in index.Matches(temp))
                                {
                                    n.text = n.text.Replace(m.ToString(), "");
                                    e_cat = true;
                                }
                            }
                            if (e_inc == true)
                            {
                                if (e_cat == true)
                                {
                                    com = "добавлен {{В инкубаторе}}, [[User:IncubatorBot/Скрытие категорий и интервик|скрытие категорий и/или интервик]]";
                                }
                                else
                                {
                                    com = "добавлен {{В инкубаторе}}";
                                }
                                try
                                {
                                    n.Save(com, true);
                                }
                                catch { Console.WriteLine(n.title + " - can't save;\n"); }
                            }
                            else if (e_cat == true)
                            {
                                com = "[[User:IncubatorBot/Скрытие категорий и интервик|скрытие категорий и/или интервик]]";
                                try
                                {
                                    n.Save(com, true);
                                }
                                catch { Console.WriteLine(n.title + " - can't save;\n"); }
                            }
                        }
                    }
                }
                catch
                {
                    Console.WriteLine(tttt + " - some strange error;\n");
                }
            }
        }
    }
}
