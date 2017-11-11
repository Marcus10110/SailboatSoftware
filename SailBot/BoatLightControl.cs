using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SerialLedWriter;
using AnimationLibrary;

namespace SailBot
{
    public class BoatLightControl
    {

        LedWriter Writer = new LedWriter();

        List<AnimationDescriptor> CurrentAnimations = new List<AnimationDescriptor>();

        private const int PortSide = 0;
        private const int StarbordSide = 1;

        string ComPort = null;

        public void Init( string com_port )
        {
            if( String.IsNullOrEmpty( com_port ) )
                return;
            Writer.ChainCount = 2;
            Writer.SetLedsOnChain(0, 200);
            Writer.SetLedsOnChain(1, 200);
            Writer.FrameRate = 30;
            Writer.RegisterUpdateCallback( CreateFrame );
            Writer.Start( com_port );
            ComPort = com_port;
        }

        public void InteriorLightsOff()
        {
            SetInteriorColor( System.Windows.Media.Brushes.Black );
        }

        public void SetInteriorColor( System.Windows.Media.SolidColorBrush brush )
        {
            bool was_running = Writer.IsRunning();

            if( was_running )
                Writer.Stop();

            //change out the color!
            CurrentAnimations.Clear();

            AnimationDescriptor desc = new AnimationDescriptor();
            desc.Animation = new SolidColor(new Color(brush.Color.R, brush.Color.G, brush.Color.B), 50);
            desc.CurrentFrame = 0;
            desc.Chain = PortSide;
            CurrentAnimations.Add(desc);

            desc = new AnimationDescriptor();
            desc.Animation = new SolidColor(new Color(brush.Color.R, brush.Color.G, brush.Color.B), 50);
            desc.CurrentFrame = 0;
            desc.Chain = StarbordSide;
            CurrentAnimations.Add(desc);

            if( was_running )
                Writer.Start(ComPort);
        }

        public void SetRainbow()
        {

            bool was_running = Writer.IsRunning();

            if( was_running )
                Writer.Stop();

            CurrentAnimations.Clear();

            AnimationDescriptor desc = new AnimationDescriptor();
            desc.Animation = new RainbowCircle();
            desc.CurrentFrame = 0;
            desc.Chain = PortSide;
            CurrentAnimations.Add(desc);

            desc = new AnimationDescriptor();
            desc.Animation = new RainbowCircle();
            desc.CurrentFrame = 0;
            desc.Chain = StarbordSide;

            CurrentAnimations.Add(desc);


            if( was_running )
                Writer.Start(ComPort);
        }



        public List<Tuple<byte, byte, byte>> CreateFrame(int ChainIndex, int LedCount)
        {
            List<Tuple<byte, byte, byte>> data = new List<Tuple<byte, byte, byte>>();

            if (!CurrentAnimations.Any(x => x.Chain == ChainIndex))
            {
                for (int i = 0; i < LedCount; ++i)
                {
                    data.Add(new Tuple<byte, byte, byte>(0, 0, 0));


                }
                return data;
            }

            Dictionary<int, int> blended_led_counts = new Dictionary<int, int>();
            for (int i = 0; i < LedCount; ++i)
            {
                blended_led_counts.Add(i, 0);
                data.Add(new Tuple<byte, byte, byte>(0, 0, 0));
            }

            foreach (var descriptor in CurrentAnimations)
            {
                if (descriptor.Chain != ChainIndex)
                    continue;

                for (int i = 0; i < LedCount; ++i)
                {
                    var color = descriptor.Animation.Animate(descriptor.CurrentFrame, i);

                    if (blended_led_counts[i] > 0)
                    {
                        data[i] = AverageColors(data[i], new Tuple<byte, byte, byte>(color.Red, color.Green, color.Blue), blended_led_counts[i]);
                    }
                    else
                    {
                        data[i] = new Tuple<byte, byte, byte>(color.Red, color.Green, color.Blue);
                    }

                    blended_led_counts[i]++;

                }

                descriptor.CurrentFrame++;
                if (descriptor.CurrentFrame > descriptor.Animation.FrameCount)
                    descriptor.CurrentFrame = 0;

            }

            return data;

        }

        private Tuple<byte, byte, byte> AverageColors(Tuple<byte, byte, byte> old_color, Tuple<byte, byte, byte> new_color, int num_existing_averages)
        {
            double r = old_color.Item1;
            double g = old_color.Item2;
            double b = old_color.Item3;

            r = r * num_existing_averages / (num_existing_averages + 1.0) + new_color.Item1 / (num_existing_averages + 1.0);

            g = g * num_existing_averages / (num_existing_averages + 1.0) + new_color.Item2 / (num_existing_averages + 1.0);

            b = b * num_existing_averages / (num_existing_averages + 1.0) + new_color.Item3 / (num_existing_averages + 1.0);


            if (r > 255)
                r = 255;
            if (r < 0)
                r = 0;
            if (g > 255)
                g = 255;
            if (g < 0)
                g = 0;
            if (b > 255)
                b = 255;
            if (b < 0)
                b = 0;

            return new Tuple<byte, byte, byte>((byte)r, (byte)g, (byte)b);


        }
    }
    
}
