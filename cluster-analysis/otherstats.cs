using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class Program
{
    static void Main()
    {
        var rdr = new StreamReader("elections.txt");
        var output = new StreamWriter("result.txt");
        //string candidate;
        //while (!rdr.EndOfStream)
        //{
        //    string votename = rdr.ReadLine();//Весна 2008/Sairam или 2006adm Dsd2
        //    if (votename.StartsWith("2"))
        //        candidate = votename.Substring(8);
        //    else
        //        candidate = votename.Substring(votename.IndexOf('/') + 1);
        //    foreach (var yesvoter in rdr.ReadLine().Split('\t'))
        //        if (yesvoter == candidate)
        //            output.WriteLine(votename + "\t+");
        //    foreach (var novoter in rdr.ReadLine().Split('\t'))
        //        if (novoter == candidate)
        //            output.WriteLine(votename + "\t-");
        //}
        var results = new Dictionary<string, int>();
        var voters = new Dictionary<string, int>();
        int voterid = 0;

        rdr = new StreamReader("elections.txt");
        while (!rdr.EndOfStream)
        {
            rdr.ReadLine();
            foreach (var voter in rdr.ReadLine().Split('\t'))
                if (!voters.ContainsKey(voter))
                    voters.Add(voter, voterid++);
            foreach (var voter in rdr.ReadLine().Split('\t'))
                if (!voters.ContainsKey(voter))
                    voters.Add(voter, voterid++);
        }

        int[,] table = new int[voters.Count, voters.Count];

        rdr = new StreamReader("elections.txt");
        while (!rdr.EndOfStream)
        {
            rdr.ReadLine();
            var yesvoters = rdr.ReadLine().Split('\t');
            var novoters = rdr.ReadLine().Split('\t');
            foreach (var y1 in yesvoters)
                foreach (var y2 in yesvoters)
                    table[voters[y1], voters[y2]] += 1;
            foreach (var n1 in novoters)
                foreach (var n2 in novoters)
                    table[voters[n1], voters[n2]] += 1;
            foreach (var y in yesvoters)
                foreach (var n in novoters)
                    table[voters[y], voters[n]] -= 1;
        }

        foreach (var v1 in voters)
            foreach (var v2 in voters)
                if (v1.Key != "" && v2.Key != "" && v1.Key != v2.Key && (table[v1.Value, v2.Value] < -30 || table[v1.Value, v2.Value] > 80) && !results.ContainsKey(v1.Key + " / " + v2.Key) && !results.ContainsKey(v2.Key + " / " + v1.Key))
                    results.Add(v1.Key + " / " + v2.Key, table[v1.Value, v2.Value]);
        
        foreach(var r in results.OrderByDescending(x => x.Value))
            output.WriteLine(r.Key + '\t' + r.Value);
        output.Close();
    }
}
