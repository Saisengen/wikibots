using System;
internal class Program
{
    static void Main()
    {
        Console.WriteLine("Content-type: text/html\n");
        var a = Environment.GetEnvironmentVariables();
        foreach (var v in a.Keys)
        {
            if (v.ToString().Contains("PASS") || v.ToString().Contains("CRED"))
                continue;

            Console.WriteLine(v.ToString() + " = " + Environment.GetEnvironmentVariable(v.ToString()));
        }
    }
}
