using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimationLibrary
{

    public struct Color
    {
        public Color(byte r, byte g, byte b)
            : this()
        {
            Red = r;
            Green = g;
            Blue = b;
        }

        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
    }

    public struct HslColor
    {
        public double Hue { get; set; }
        public double Luminosity { get; set; }
        public double Saturation { get; set; }

        public Color ToRGB()
        {
            byte r, g, b;
            if (Saturation == 0)
            {
                r = (byte)Math.Round(Luminosity * 255d);
                g = (byte)Math.Round(Luminosity * 255d);
                b = (byte)Math.Round(Luminosity * 255d);
            }
            else
            {
                double t1, t2;
                double th = Hue / 6.0d;

                if (Luminosity < 0.5d)
                {
                    t2 = Luminosity * (1d + Saturation);
                }
                else
                {
                    t2 = (Luminosity + Saturation) - (Luminosity * Saturation);
                }
                t1 = 2d * Luminosity - t2;

                double tr, tg, tb;
                tr = th + (1.0d / 3.0d);
                tg = th;
                tb = th - (1.0d / 3.0d);

                tr = ColorCalc(tr, t1, t2);
                tg = ColorCalc(tg, t1, t2);
                tb = ColorCalc(tb, t1, t2);
                r = (byte)Math.Round(tr * 255d);
                g = (byte)Math.Round(tg * 255d);
                b = (byte)Math.Round(tb * 255d);
            }
            return new Color(r, g, b);
        }
        private static double ColorCalc(double c, double t1, double t2)
        {

            if (c < 0) c += 1d;
            if (c > 1) c -= 1d;
            if (6.0d * c < 1.0d) return t1 + (t2 - t1) * 6.0d * c;
            if (2.0d * c < 1.0d) return t2;
            if (3.0d * c < 2.0d) return t1 + (t2 - t1) * (2.0d / 3.0d - c) * 6.0d;
            return t1;
        }


    }



    public interface Animation
    {
        int FrameCount { get; set; }
        int FrameRate { get; set; }
        int LedCount { get; set; }

        Color Animate(int frame, int led);
    }

    public class AnimationDescriptor
    {
        public Animation Animation { get; set; }
        public int CurrentFrame { get; set; }
        public int Chain { get; set; }
    }




    public class Animation1 : Animation
    {

        public int FrameCount { get; set; }
        public int FrameRate { get; set; }
        public int LedCount { get; set; }

        public Animation1()
        {
            FrameCount = 10;
            LedCount = 10;
            FrameRate = 10;
        }

        public Color Animate(int frame, int led)
        {
            if (led == frame)
                return new Color(255, 255, 255);
            else
                return new Color(0, 0, 0);
        }
    }

    public class AllWhilte : Animation
    {

        public int FrameCount { get; set; }
        public int FrameRate { get; set; }
        public int LedCount { get; set; }

        private byte brightness;

        public AllWhilte()
        {
            FrameCount = 1;
            LedCount = 150;
            FrameRate = 1;

            brightness = 255;
        }

        public AllWhilte(byte power, int num_leds/* = 150*/)
            : this()
        {
            LedCount = num_leds;
            brightness = power;

        }

        public Color Animate(int frame, int led)
        {
            //if (led < 120)
            //return new Color(0, 0, 0);
            return new Color(brightness, brightness, brightness);
        }
    }

    public class WhiteRabbit : Animation
    {

        public int FrameCount { get; set; }
        public int FrameRate { get; set; }
        public int LedCount { get; set; }

        double total_time = 4.0;

        public WhiteRabbit()
        {

            LedCount = 150;
            FrameRate = 30;

            //time end to end. 5 seconds.

            FrameCount = (int)(FrameRate * total_time);

        }

        public Color Animate(int frame, int led)
        {
            double location = LedCount * ((double)frame / FrameCount);
            location *= 2;

            if (location > LedCount)
            {
                location = (LedCount*2) - location;
            }

            double distance = Math.Abs(location - led);
            //min 0, max 10

            int max_distance = 30;

            double brightness = (max_distance - distance);

            if (brightness < 0)
                brightness = 0;



            brightness /= max_distance;
            brightness = Math.Pow(brightness, 3.0);


            if (brightness > 1.0)
                brightness = 1.0;
            else if (brightness < 0)
                brightness = 0;

            byte level = (byte)(brightness * 255);

            return new Color(level, level, level);
        }
    }


    public class RainbowCircle : Animation
    {

        public int FrameCount { get; set; }
        public int FrameRate { get; set; }
        public int LedCount { get; set; }

        private double TotalTime = 3.0;

        public RainbowCircle()
        {

            LedCount = 150;
            FrameRate = 30;

            FrameCount = (int)(FrameRate * TotalTime);
        }

        public RainbowCircle(double total_time)
            : this()
        {
            TotalTime = total_time;
        }

        public Color Animate(int frame, int led)
        {

            double hue = led / (double)LedCount;
            hue %= 1.0;
            hue += 1.0 - ((double)frame / FrameCount);

            return new HslColor { Hue = (hue % 1.0) * 6, Luminosity = 0.5, Saturation = 1 }.ToRGB();
        }
    }

    public class Animation3 : Animation
    {

        public int FrameCount { get; set; }
        public int FrameRate { get; set; }
        public int LedCount { get; set; }

        double total_time = 3.0;

        public Animation3()
        {

            LedCount = 150;
            FrameRate = 60;

            //time end to end. 5 seconds.

            FrameCount = (int)(FrameRate * total_time);


        }

        public Color Animate(int frame, int led)
        {
            double location = LedCount * ((double)frame / FrameCount);
            /*location *= 2;

            if (location > 150)
            {
                location = 300 - location;
            }*/

            double distance = Math.Abs(location - led);
            //min 0, max 10

            int max_distance = 15;

            double brightness = (max_distance - distance);

            if (brightness < 0)
                brightness = 0;



            brightness /= max_distance;
            brightness = Math.Pow(brightness, 3.0);


            if (brightness > 1.0)
                brightness = 1.0;
            else if (brightness < 0)
                brightness = 0;

            byte level = (byte)(brightness * 255);

            double hue = led / (double)LedCount;
            hue += 1.0 - ((double)frame / FrameCount);


            return new HslColor { Hue = (hue % 1.0) * 6, Luminosity = 0.05 /*brightness / 2.0 + 0.05*/, Saturation = 1 }.ToRGB();

            //return new HslColor { Hue = 0, Luminosity = .5, Saturation = 1 }.ToRGB();


            //return new Color(level, level, level);
        }
    }
}
