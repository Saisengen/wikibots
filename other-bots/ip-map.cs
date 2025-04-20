using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Runtime.CompilerServices;
class IPv4BitMap
{
    private readonly byte[] bits;
    public IPv4BitMap()
    {
        bits = new byte[(((long)uint.MaxValue + 1) / 64) * 7];// 7/8 from full ipv4 space
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(uint ip)
    {
        bits[ip >> 3] |= (byte)(1 << (int)(ip & 7));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(uint ip)
    {
        return (bits[ip >> 3] & (1 << (int)(ip & 7))) != 0;
    }
    public static uint Parse(char a, char b, char c, char d)
    {
        return ((uint)(byte)a << 24) | ((uint)(byte)b << 16) | ((uint)(byte)c << 8) | (uint)(byte)d;
    }
}
class Program
{
    static Regex iprgx = new Regex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$");
    static HttpClient Site(string lang, string login, string password)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = true, CookieContainer = new CookieContainer() });
        client.DefaultRequestHeaders.Add("User-Agent", login);
        var result = client.GetAsync("https://" + lang + ".wikipedia.org/w/api.php?action=query&meta=tokens&type=login&format=xml").Result;
        if (!result.IsSuccessStatusCode)
            return null;
        var doc = new XmlDocument();
        doc.LoadXml(result.Content.ReadAsStringAsync().Result);
        var logintoken = doc.SelectSingleNode("//tokens/@logintoken").Value;
        result = client.PostAsync("https://" + lang + ".wikipedia.org/w/api.php", new FormUrlEncodedContent(new Dictionary<string, string> { { "action", "login" }, { "lgname", login }, { "lgpassword",
                password }, { "lgtoken", logintoken }, { "format", "xml" } })).Result;
        if (!result.IsSuccessStatusCode)
            return null;
        return client;
    }
    static void Main()
    {
        string lang = "en";
        var date = new DateTime(2002, 2, 1);
        var now = DateTime.Now;
        //var creds = new StreamReader((Environment.OSVersion.ToString().Contains("Windows") ? @"..\..\..\..\" : "") + "p").ReadToEnd().Split('\n');
        //var site = Site(lang, creds[0], creds[1]);
        //var repeatedIPs = new HashSet<string>();
        //do
        //{
        //    var w = new BinaryWriter(File.Create(date.ToString("yyyyMM") + ".txt"));
        //    string cont = "", query = "https://" + lang + ".wikipedia.org/w/api.php?action=query&format=xml&list=allrevisions&arvprop=user&arvlimit=max&arvend=" + date.ToString("yyyy-MM") + "-01T00:00:00" +
        //        "&arvstart=" + date.AddMonths(1).ToString("yyyy-MM") + "-01T00:00:00";
        //    while (cont != null)
        //        using (var r = new XmlTextReader(new StringReader(cont == "" ? site.GetStringAsync(query).Result : site.GetStringAsync(query + "&arvcontinue=" + cont).Result)))
        //        {
        //            r.Read(); r.Read(); r.Read(); cont = r.GetAttribute("arvcontinue");
        //            while (r.Read())
        //                if (r.Name == "rev" && r.GetAttribute("anon") == "")
        //                {
        //                    string user = r.GetAttribute("user");
        //                    if (iprgx.IsMatch(user) && !repeatedIPs.Contains(user))
        //                    {
        //                        var octets = user.Split('.');
        //                        byte o1, o2, o3, o4;
        //                        try { o1 = Convert.ToByte(octets[0]); o2 = Convert.ToByte(octets[1]); o3 = Convert.ToByte(octets[2]); o4 = Convert.ToByte(octets[3]); } catch { continue; }
        //                        w.Write(o1); w.Write(o2); w.Write(o3); w.Write(o4);
        //                        if (repeatedIPs.Count > 300000)
        //                            repeatedIPs.Clear();
        //                        repeatedIPs.Add(user);
        //                    }
        //                }
        //        }
        //    w.Close();
        //    date = date.AddMonths(1);

            var colors = new Dictionary<int, Color>();
        for (int intensity = 0; intensity < 256; intensity++)
        {
            if (intensity < 51)
                colors.Add(intensity + 1, Color.FromArgb(0, intensity * 5, 255));
            else if (intensity < 102)
                colors.Add(intensity + 1, Color.FromArgb(0, 255, 255 - (intensity - 51) * 5));
            else if (intensity < 153)
                colors.Add(intensity + 1, Color.FromArgb((intensity - 102) * 5, 255, 0));
            else if (intensity < 204)
                colors.Add(intensity + 1, Color.FromArgb(255, 255 - (intensity - 153) * 5, 0));
            else
                colors.Add(intensity + 1, Color.FromArgb(255, (intensity - 204) * 5, (intensity - 204) * 5));
        }
        var bitmap = new IPv4BitMap();
        var base_image = new Bitmap(4096, 4096);
        for (int w = 0; w < 4096; w++)
            for (int h = 0; h < 4096; h++)
                base_image.SetPixel(w, h, Color.Black);
        do
        {
                var r = new BinaryReader(File.OpenRead(date.ToString("yyyyMM") + ".txt"));
                if (r.BaseStream.Length == 0)
                {
                    date = date.AddMonths(1);
                    continue;
                }
                var slice = new Bitmap(base_image);

                while (r.BaseStream.Position < r.BaseStream.Length)
                    bitmap.Set(IPv4BitMap.Parse((char)r.ReadByte(), (char)r.ReadByte(), (char)r.ReadByte(), (char)r.ReadByte()));

                for (int o1 = 1; o1 < 224; o1++)
                    for (int o2 = 0; o2 < 256; o2++)
                        for (int o3 = 0; o3 < 256; o3++)
                        {
                            byte intensity = 0;
                            for (int o4 = 0; o4 < 256; o4++)
                                if (bitmap.Get((uint)((o1 << 24) | (o2 << 16) | (o3 << 8) | o4)))
                                intensity++;
                            if (intensity != 0)
                            {
                                int big1 = o1 / 16; int smol1 = o1 % 16; int big2 = o2 / 16; int smol2 = o2 % 16; int big3 = o3 / 16; int smol3 = o3 % 16;
                                int height = 256 * big1 + 16 * big2 + big3; int width = 256 * smol1 + 16 * smol2 + smol3;
                                slice.SetPixel(width, height, colors[intensity]);
                            }
                        }

                Graphics draw_text = Graphics.FromImage(slice);
                for (int w = 0; w < 16; w++)
                    for (int h = 0; h < 14; h++)
                        draw_text.DrawString((h * 16 + w).ToString(), new Font("Arial", 80), new SolidBrush(Color.FromArgb(64, Color.Red)), 256 * w + 32, 256 * h + 96);
                draw_text.DrawString(lang + "wiki " + date.ToString("yyyy-MM"), new Font("Arial", 400), Brushes.White, 150, 3500);
                slice.Save(lang + date.ToString("yyyyMM") + ".png", System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine(date.ToString("yyyyMM"));
                date = date.AddMonths(1);
            } while (date < now);
    }
}
