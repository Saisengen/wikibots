using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using System.IO;
using DotNetWikiBot;

class sliceinfo
{
    public int male, female;
}
class record
{
    public string flag, gender, id;
    public int edits;
}

class Program
{
    static void Main()
    {
        var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        var site = new Site("https://ru.wikipedia.org", creds[0], creds[1]);
        var p = new DotNetWikiBot.Page("user:MBH/genders");
        string text = "<center>";
        foreach (var lang in new List<string>() { "en", "de", "fr", "es", "pt", "it", "ru", "ja", "zh", "ar", "fa", "uk", "pl", "nl", "he", "tr" })
        {
            Console.WriteLine(lang);
            text += "\n==" + lang + "wiki==\n";
            var slices = new Dictionary<string, sliceinfo>
            {
                { "sysop", new sliceinfo() },
                { "all", new sliceinfo() },
                { "0", new sliceinfo() },
                { "1-3", new sliceinfo() },
                { "3-10", new sliceinfo() },
                { "10-30", new sliceinfo() },
                { "30-100", new sliceinfo() },
                { "100-300", new sliceinfo() },
                { "300-1k", new sliceinfo() },
                { "1k-3k", new sliceinfo() },
                { "3k-10k", new sliceinfo() },
                { "10k-30k", new sliceinfo() },
                { "30k-100k", new sliceinfo() },
                { "100k-300k", new sliceinfo() },
                { "300k+", new sliceinfo() }
            };
            var userdata = new List<record> ();
            var processed_users = new HashSet<string> ();
            var connect = new MySqlConnection("Server=" + lang + "wiki.labsdb;Database=" + lang + "wiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
            connect.Open();
            var command = new MySqlCommand("select up_value, ug_group, user_id, user_editcount from user left join user_groups on user.user_id = user_groups.ug_user join user_properties on user.user_id = user_properties.up_user where (up_property = 'gender' and up_value = 'male');", connect);
            command.CommandTimeout = 9999;
            MySqlDataReader r = command.ExecuteReader();
            while (r.Read())
                userdata.Add(new record() { id = r.GetString("user_id"), gender = r.GetString("up_value"), edits = r.GetInt32("user_editcount"), flag = r.IsDBNull(r.GetOrdinal("ug_group")) ? "" : r.GetString("ug_group") });
            foreach (var user in userdata)
            {
                if (user.flag == "sysop")
                    slices["sysop"].male++;
                if (!processed_users.Contains(user.id))
                {
                    slices["all"].male++;
                    if (user.edits == 0)
                        slices["0"].male++;
                    else if (user.edits <= 3)
                        slices["1-3"].male++;
                    else if (user.edits <= 10)
                        slices["3-10"].male++;
                    else if (user.edits <= 30)
                        slices["10-30"].male++;
                    else if (user.edits <= 100)
                        slices["30-100"].male++;
                    else if (user.edits <= 300)
                        slices["100-300"].male++;
                    else if (user.edits <= 1000)
                        slices["300-1k"].male++;
                    else if (user.edits <= 3000)
                        slices["1k-3k"].male++;
                    else if (user.edits <= 10000)
                        slices["3k-10k"].male++;
                    else if (user.edits <= 30000)
                        slices["10k-30k"].male++;
                    else if (user.edits <= 100000)
                        slices["30k-100k"].male++;
                    else if (user.edits <= 300000)
                        slices["100k-300k"].male++;
                    else slices["300k+"].male++;
                    processed_users.Add(user.id);
                }
            }
            r.Close();

            command = new MySqlCommand("select up_value, ug_group, user_id, user_editcount from user left join user_groups on user.user_id = user_groups.ug_user join user_properties on user.user_id = user_properties.up_user where (up_property = 'gender' and up_value = 'female');", connect);
            r = command.ExecuteReader();
            userdata.Clear();
            processed_users.Clear();
            while (r.Read())
                userdata.Add(new record() { id = r.GetString("user_id"), gender = r.GetString("up_value"), edits = r.GetInt32("user_editcount"), flag = r.IsDBNull(r.GetOrdinal("ug_group")) ? "" : r.GetString("ug_group") });
            foreach (var user in userdata)
            {
                if (user.flag == "sysop")
                    slices["sysop"].female++;
                if (!processed_users.Contains(user.id))
                {
                    slices["all"].female++;
                    if (user.edits == 0)
                        slices["0"].female++;
                    else if (user.edits <= 3)
                        slices["1-3"].female++;
                    else if (user.edits <= 10)
                        slices["3-10"].female++;
                    else if (user.edits <= 30)
                        slices["10-30"].female++;
                    else if (user.edits <= 100)
                        slices["30-100"].female++;
                    else if (user.edits <= 300)
                        slices["100-300"].female++;
                    else if (user.edits <= 1000)
                        slices["300-1k"].female++;
                    else if (user.edits <= 3000)
                        slices["1k-3k"].female++;
                    else if (user.edits <= 10000)
                        slices["3k-10k"].female++;
                    else if (user.edits <= 30000)
                        slices["10k-30k"].female++;
                    else if (user.edits <= 100000)
                        slices["30k-100k"].female++;
                    else if (user.edits <= 300000)
                        slices["100k-300k"].female++;
                    else slices["300k+"].female++;
                    processed_users.Add(user.id);
                }
            }

            text += "\n{|class=\"standard sortable\"\n!Число правок!!Мужчин!!Женщин!!Доля мужчин";
            foreach (var s in slices.OrderByDescending(s => s.Value.male))
                text += "\n|-\n|" + s.Key + "||" + s.Value.male + "||" + s.Value.female + "||" + (float)s.Value.male / (s.Value.male + s.Value.female);
            text += "\n|}";
        }
        p.Save(text);
    }
}
