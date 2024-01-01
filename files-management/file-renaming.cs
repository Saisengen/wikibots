using System;
using System.IO;
using System.Text.RegularExpressions;
using DotNetWikiBot;

class MyBot : Bot
{
    public static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        Site ru = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        DateTime now = DateTime.UtcNow;
        string log = "";

        string xml = ru.GetWebPage(ru.apiPath + "?action=query&list=logevents&letype=move&lestart=" + now.ToString("yyyyMMddHHmmss") + "&lelimit=5000&format=xml");
        // RegExps
        Regex items = new Regex("<item [^<]*?" + Regex.Escape("ns=\"6\"") + ".*?</item>", RegexOptions.Singleline);
        Regex title = new Regex("(?<=title..).*?(?=" + Regex.Escape("\"") + ")", RegexOptions.Singleline);
        Regex user = new Regex("(?<=user..).*?(?=" + Regex.Escape("\"") + ")", RegexOptions.Singleline);
        Regex comment = new Regex("(?<=comment..).*?(?=" + Regex.Escape("\"") + ")", RegexOptions.Singleline);
        Regex new_title = new Regex("(?<=target_title..).*?(?=" + Regex.Escape("\"") + ")", RegexOptions.Singleline);
        int n = 0;
        string[,] filemoves = new string[500, 4];
        // Add data from RegExps to file-array 
        foreach (Match m in items.Matches(xml))
        {
            filemoves[n, 0] = title.Matches(m.ToString())[0].ToString();
            filemoves[n, 1] = user.Matches(m.ToString())[0].ToString();
            filemoves[n, 2] = comment.Matches(m.ToString())[0].ToString();
            filemoves[n, 3] = new_title.Matches(m.ToString())[0].ToString();
            n++;
        }
        // look for usage of files
        for (int i = 0; i < n; i++)
        {
            string pageURL2 = ru.apiPath + "?action=query&list=imageusage&iutitle=" + Uri.EscapeUriString(filemoves[i, 0]) + "&format=xml";
            string usage = ru.GetWebPage(pageURL2);

            //string usage = t.resultHTM(ru, "api.php?action=query&list=imageusage&iutitle=" + fname_code + "&format=xml");
            string[] filelinks = new string[500];
            Regex iu = new Regex("<iu [^<]*? />", RegexOptions.Singleline);
            int j = 0;
            // add pages with filelinks to array
            foreach (Match m in iu.Matches(usage))
            {
                filelinks[j] = title.Matches(m.ToString())[0].ToString();
                j++;
            }
            // replace moved files in pages
            for (int k = 0; k < j; k++)
            {
                bool done = false;
                Page imagelink = new Page(ru, filelinks[k]);
                imagelink.Load();
                string filename = filemoves[i, 0].Replace("Файл:", "").Replace("&quot;", "\"").Replace("&amp;", "&").Replace("&#039;", "\'");
                string filenewname = filemoves[i, 3];
                // для Regex важно убрать плюсы и пробелы, а для вывода комментария в конце важно их оставить
                string oldfilename = filename.Replace(" ", ".").Replace("+", ".").Replace("?", ".").Replace("*", ".").Replace("\\", ".").Replace("$", ".").Replace("(", ".").Replace(")", ".");
                Regex fname = new Regex(@"(\n(\s)*|=(\s)*|:)" + oldfilename, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match m in fname.Matches(imagelink.text))
                {
                    if (imagelink.text.IndexOf(m.ToString()) == -1)
                        done = false;

                    filenewname = filemoves[i, 3].Replace("Файл:", "");
                    for (int zxc = 0; zxc < 2; zxc++)
                        filenewname = filenewname.Replace("&quot;", "\"").Replace("&#039;", "\'").Replace("&amp;", "&");
                    if (m.ToString().IndexOf("=", 0, 2) != -1)
                    {
                        imagelink.text = imagelink.text.Replace(m.ToString(), "= " + filenewname);
                        done = true;
                    }
                    else if (m.ToString().IndexOf(":", 0, 2) != -1)
                    {
                        imagelink.text = imagelink.text.Replace(m.ToString(), ":" + filenewname);
                        done = true;
                    }
                    else if (m.ToString().IndexOf("\n", 0, 2) != -1)
                    {
                        imagelink.text = imagelink.text.Replace(m.ToString(), "\n" + filenewname);
                        done = true;
                    }
                }
                if (!done)
                    log = log + "\n# [[:File:" + filenewname + "]] (" + now.ToString("dd MMMM yyyy") + ") - не удалось выполнить замену в [[" + imagelink.title + "]].";
                string savecomment = "[[File:" + filename + "]] переименован [[User:" + filemoves[i, 1] + "|" + filemoves[i, 1] + "]] в [[File:" + filenewname + "]]";
                if (filemoves[i, 2].Length > 0)
                    savecomment = savecomment + " (" + filemoves[i, 2] + ")";
                Console.WriteLine(savecomment);
                imagelink.Save(savecomment, true);
            }
        }
        if (log.Length > 2)
        {
            Page logpage = new Page(ru, "Участник:Bot89/Переименования файлов");
            logpage.Load();
            logpage.Save(logpage.text += log, "ошибки обработки", false);
        }
    }
}
