using System;
using System.Collections.Generic;
using System.Linq;
internal class Program
{
    static void Main()
    {
        Console.WriteLine("Content-type: text/html\n");
        var allvars = Environment.GetEnvironmentVariables();
        var dict = new Dictionary<string, string>();
        Console.WriteLine("<body><table><tr><th>Env var</th><th>Value</th><tr>");
        foreach (var v in allvars.Keys)
            dict.Add(v.ToString(), allvars[v].ToString());

        foreach (var key in dict.Keys.OrderBy(d => d))
            Console.WriteLine("<tr><td>" + key + "</td><td>" + ((key.Contains("PASS") || key.Contains("CRED")) ? "" : dict[key]) + "</td></tr>\n");
        Console.WriteLine("</table></body>");
    }
}
