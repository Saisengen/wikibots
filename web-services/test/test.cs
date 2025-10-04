using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
internal class Program
{
    static void Main()
    {
        Thread.Sleep(6*60*1000);
        Console.WriteLine("Content-type: text/html\n");
        var allvars = Environment.GetEnvironmentVariables();
        var dict = new Dictionary<string, string>();
        Console.WriteLine("<body><table border=1><tr><th>Env var</th><th>Value</th><tr>");
        foreach (var v in allvars.Keys)
            dict.Add(v.ToString(), allvars[v].ToString());

        foreach (var record in dict.OrderBy(d => d.Key))
            Console.WriteLine("<tr><td>" + record.Key + "</td><td>" + ((record.Key.Contains("PASS") || record.Key.Contains("CRED")) ? "" : record.Value) + "</td></tr>\n");
        Console.WriteLine("</table></body>");
    }
}
