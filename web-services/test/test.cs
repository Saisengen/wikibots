using System;
internal class Program
{
    static void Main()
    {
        Console.WriteLine("Content-type: text/html\n");
        var a = Environment.GetEnvironmentVariables();
        Console.WriteLine("<body><table><tr><th>Env var</th><th>Value</th><tr>");
        foreach (var v in a.Keys)
        {
            if (v.ToString().Contains("PASS") || v.ToString().Contains("CRED"))
                continue;

            Console.WriteLine("<tr><td>" + v.ToString() + "</td><td>" + Environment.GetEnvironmentVariable(v.ToString()) + "</td></tr>\n");
        }
        Console.WriteLine("</table></body>");
    }
}
