using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Web;
using DotNetWikiBot;

class MyBot : Bot
{
    public static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        Site site = new Site("https://ru.wikipedia.org", creds[8], creds[9]);
        Page setting = new Page(site, "user:MBH/incubator.js");
        setting.Load();
        Regex on = new Regex(@"deleteredirects.?=.?true", RegexOptions.Singleline);
        if (on.Matches(setting.text).Count > 0) // if this bot is allowed
        {
            // find if we need to skip some redirects, maybe someones are useful)))
            Regex skip = new Regex(@"skipredirects.?=.*?" + Regex.Escape(";"), RegexOptions.Singleline);
            string toskip = skip.Matches(setting.text)[0].ToString();
            toskip = toskip.Substring(toskip.IndexOf("=") + 1).Trim();
            toskip = toskip.Substring(toskip.IndexOf("\"") + 1);
            toskip = toskip.Remove(toskip.IndexOf("\""));
            string pageURL = site.apiPath + "?action=query&list=allpages&apnamespace=102&apfilterredir=all&aplimit=1000&format=xml";
            string html = site.GetWebPage(pageURL);
            // парсим данные
            int i = 0;
            string[] redirs = new string[5000];
            XmlTextReader reader = new XmlTextReader(new StringReader(html));
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "p")
                    {
                        redirs[i] = reader.GetAttribute("title"); ;
                        i++;
                    }
                }
            }
            string pageURL2 = site.apiPath + "?action=query&list=allpages&apnamespace=103&apfilterredir=all&aplimit=1000&format=xml";
            string html2 = site.GetWebPage(pageURL2);
            XmlTextReader reader2 = new XmlTextReader(new StringReader(html2));
            while (reader2.Read())
            {
                if (reader2.NodeType == XmlNodeType.Element)
                {
                    if (reader2.Name == "p")
                    {
                        redirs[i] = reader2.GetAttribute("title"); ;
                        i++;
                    }
                }
            }
            for (int j = 0; j < i; j++)
            {
                bool delete = false;
                string red = "";
                string reason = "[[ВП:КБУ#П2|П2]]: межпространственное перенаправление ";
                if (toskip.IndexOf(redirs[j]) == -1)
                {
                    Page n = new Page(site, redirs[j]);
                    n.Load();
                    Regex r = new Regex("(REDIRECT|перенаправление) " + Regex.Escape("[[") + ".*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                    if (n.text.IndexOf("{nobots}") == -1)
                    {
                        if (n.text.IndexOf("Инкубатор, Статья перенесена в ОП") != -1)
                        {
                            DateTime dpage = n.timestamp; // дата и время последней правки
                            DateTime dnow = DateTime.UtcNow; // текущие дата и время
                            TimeSpan ddiff = dnow - dpage; // считаем разницу
                            if (ddiff.Days < 3)
                                delete = false;
                            else
                            {
                                delete = true;
                                red = n.text;
                            }
                        }
                        else
                        {
                            for (int qw = 0; qw < r.Matches(n.text).Count; qw++)
                            {
                                delete = true;
                                red = r.Matches(n.text)[qw].ToString();
                                if (red.IndexOf("Инкубатор") != -1)
                                {
                                    DateTime dpage = n.timestamp; // дата и время последней правки
                                    DateTime dnow = DateTime.UtcNow; // текущие дата и время
                                    TimeSpan ddiff = dnow - dpage; // считаем разницу
                                    if (ddiff.Days < 1)
                                        delete = false;
                                    else
                                        reason = "[[ВП:КБУ#П3|П3]]: перенаправление с ошибкой в названии ";
                                }
                                Regex db = new Regex(Regex.Escape("{{") + "(db|в инкубаторе|В инкубаторе).*?" + Regex.Escape("}}"), RegexOptions.Singleline);
                                string temp = n.text;
                                for (int qz = 0; qz < db.Matches(temp).Count; qz++)
                                {
                                    temp = temp.Replace(db.Matches(temp)[qz].ToString(), "");
                                }
                                if (temp.Length - red.Length > 15)
                                    delete = false;

                            }
                        }
                        if (delete == true)
                        {

                            string rd;
                            if (red.IndexOf("[[") != -1)
                            {
                                Regex rr = new Regex(Regex.Escape("[[") + ".*?" + Regex.Escape("]]"), RegexOptions.Singleline);
                                rd = rr.Matches(red)[0].ToString();
                            }
                            else if (red.IndexOf("Инкубатор, Статья перенесена в ОП|") != -1)
                            {
                                Regex rr = new Regex(Regex.Escape("|") + ".*?" + Regex.Escape("}}"), RegexOptions.Singleline);
                                rd = rr.Matches(red)[0].ToString();
                                rd = rd.Replace("|", "[[");
                                rd = rd.Replace("}}", "]]");
                            }
                            else if (red.IndexOf("Инкубатор, Статья перенесена в ОП") != -1)
                            {
                                rd = n.title;
                                rd = rd.Replace("Инкубатор:", "");
                            }
                            else
                                rd = red;
                            n.Delete(reason + "/* " + rd + " */");
                            Console.WriteLine(reason + "/* " + rd + " */");
                        }
                    }
                }
            }
        }
    }
}
