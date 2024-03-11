using System;
internal class Program
{
    static void Main()
    {
        Console.WriteLine("Content-type: text/html\n");
        var a = Environment.GetEnvironmentVariables();
        foreach (var v in a.Keys)
            Console.WriteLine(v.ToString() + " = " + Environment.GetEnvironmentVariable(v.ToString()));
        Console.ReadLine();
    }
}
