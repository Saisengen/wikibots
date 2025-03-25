using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
class Program
{
    static Regex iprgx = new Regex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$");
    static void Main()
    {
        foreach (var lang in new StreamReader("ipmap_src.txt").ReadLine().Split('|'))
        {
            var creds = new StreamReader("p").ReadToEnd().Split('\n');
            int rows_in_current_run, step = 3200000;
            long offset = 0;
            var connect = new MySqlConnection(creds[2].Replace("%project%", lang + "wiki"));
            connect.Open();
            var raw_data = new Bitmap(4096, 4096);
            do
            {
                rows_in_current_run = 0;
                var command = new MySqlCommand("select actor_name from actor limit " + step + " offset " + offset + ";", connect) { CommandTimeout = 99999 };
                var rdr = command.ExecuteReader();
                while (rdr.Read())
                {
                    rows_in_current_run++;
                    var user = rdr.GetString("actor_name");
                    if (iprgx.IsMatch(user))
                    {
                        var octets = user.Split('.');
                        byte o1, o2, o3, o4;
                        try { o1 = Convert.ToByte(octets[0]); o2 = Convert.ToByte(octets[1]); o3 = Convert.ToByte(octets[2]); o4 = Convert.ToByte(octets[3]); } catch { return; }
                        int big1 = o1 / 16; int smol1 = o1 % 16; int big2 = o2 / 16; int smol2 = o2 % 16; int big3 = o3 / 16; int smol3 = o3 % 16;
                        int height = 256 * big1 + 16 * big2 + big3; int width = 256 * smol1 + 16 * smol2 + smol3;
                        var pixel = raw_data.GetPixel(width, height).G;
                        raw_data.SetPixel(width, height, Color.FromArgb(0, pixel == byte.MaxValue ? pixel : ++pixel, 0));
                    }
                }
                offset += rows_in_current_run;
                rdr.Close();
            } while (rows_in_current_run == step);
            var high_contrast = new Bitmap(4096, 4096);
            var true_colors = new Bitmap(4096, 4096);
            for (int w = 0; w < 4096; w++)
                for (int h = 0; h < 4096; h++)
                {
                    byte intensity = raw_data.GetPixel(w, h).G;
                    true_colors.SetPixel(w, h, getcolor(intensity));
                    if (intensity == 0)
                        high_contrast.SetPixel(w, h, Color.FromArgb(0, 0, 0));
                    else
                        high_contrast.SetPixel(w, h, Color.FromArgb(255, 255, 255));
                        
                }
            Graphics high = Graphics.FromImage(high_contrast), truec = Graphics.FromImage(true_colors);
            high.DrawString(lang + ".wikipedia", new Font("Arial", 400), Brushes.White, 500, 3500); truec.DrawString(lang + ".wikipedia", new Font("Arial", 400), Brushes.White, 500, 3500);
            high_contrast.Save("high contrast " + lang + ".png", System.Drawing.Imaging.ImageFormat.Png); true_colors.Save("true colors " + lang + ".png", System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    static Color getcolor (byte intensity)
    {
        if (intensity < 64)
            return Color.FromArgb(0, 0, intensity * 4);
        else if (intensity < 128)
            return Color.FromArgb(0, (intensity - 64) * 4, 255 - ((intensity - 64) * 4));
        else if (intensity < 192)
            return Color.FromArgb((intensity - 128) * 4, 255 - ((intensity - 128) * 4), 0);
        else
            return Color.FromArgb(intensity, (intensity - 192) * 4, (intensity - 192) * 4);
    }
}
