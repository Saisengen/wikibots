using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;
using System.Linq;
using System.IO;

class Program
{
    static void Main()
    {
        var thanked = new Dictionary<string, int>();
        var thankers = new Dictionary<string, int>();
        var users = new HashSet<string>();
        MySqlDataReader r;
        MySqlCommand command;
        string vars = Environment.GetEnvironmentVariable("QUERY_STRING");
        if (!vars.Contains("user="))
        {
            sendresponse("", "");
            return;
        }
        string user = Uri.UnescapeDataString(vars.Substring(5).Replace('+', ' '));

        var creds = new StreamReader("../p").ReadToEnd().Split('\n');
        var connect = new MySqlConnection("Server=ruwiki.labsdb;Database=ruwiki_p;Uid=" + creds[2] + ";Pwd=" + creds[3] + ";CharacterSet=utf8mb4;SslMode=none;");
        connect.Open();

        command = new MySqlCommand("select cast(replace (log_title, '_', ' ') as char) from logging where log_type=\"thanks\" and log_actor=(select actor_id from actor where actor_name=\"" + user +
            "\");", connect)        { CommandTimeout = 9999 };
        r = command.ExecuteReader();
        while (r.Read())
        {
            string name = r.GetString(0);
            if (!thanked.ContainsKey(name))
                thanked.Add(name, 1);
            else
                thanked[name]++;
        }
        r.Close();

        command = new MySqlCommand("select cast(actor_name as char) source from (select log_actor from logging where log_type=\"thanks\" and log_title=\"" + user.Replace(' ', '_') + "\") log join " +
            "actor on actor_id=log_actor;", connect)        { CommandTimeout = 9999 };
        r = command.ExecuteReader();
        while (r.Read())
        {
            string name = r.GetString(0);
            if (!thankers.ContainsKey(name))
                thankers.Add(name, 1);
            else
                thankers[name]++;
        }

        string response = "<table border=\"1\" cellspacing=\"0\">";
        foreach (var t in thanked.OrderByDescending(t => t.Value))
            response += "<tr><td>" + user + " <a href=\"https://ru.wikipedia.org/w/index.php?title=special:log&type=thanks&user=" + Uri.EscapeDataString(user) + "&page=" + t.Key + "\">⇨</a>" +
                " <a href=\"https://tools.wmflabs.org/mbh/likes.cgi?user=" + Uri.EscapeDataString(t.Key) + "\">" + t.Key + "</a></td><td>" + t.Value + "</td></tr>\n";
        response += "</table></td><td valign=\"top\"><table border=\"1\" cellspacing=\"0\">";
        foreach (var t in thankers.OrderByDescending(t => t.Value))
            response += "<tr><td><a href=\"https://tools.wmflabs.org/mbh/likes.cgi?user=" + Uri.EscapeDataString(t.Key) + "\">" + t.Key + "</a>" +
                "<a href=\"https://ru.wikipedia.org/w/index.php?title=special:log&type=thanks&user=" + t.Key + "&page=" + Uri.EscapeDataString(user) + "\">⇨</a>" + user +" </td><td>" + t.Value + "</td></tr>\n";
        sendresponse(response + "</table>", user);
    }

    static void sendresponse(string response, string user)
    {
        var sr = new StreamReader("thanks-template.txt");
        Console.WriteLine(sr.ReadToEnd().Replace("%response%", response).Replace("%user%", user));
        Console.WriteLine();
        return;
    }
}
