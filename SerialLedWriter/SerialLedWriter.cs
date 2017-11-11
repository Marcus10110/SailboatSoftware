using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO.Ports;
using System.Threading;

namespace SerialLedWriter
{
    public class LedWriter
    {
        Thread WriteThread;
        SerialPort Port = new SerialPort();
        Dictionary<int, int> LedsPerChain = new Dictionary<int,int>();

        ContentCalculation Callback;

        public delegate List<Tuple<byte, byte, byte>> ContentCalculation(int ChainIndex, int LedCount);

        public int ChainCount { get; set; }
        public int FrameRate { get; set; }


        //all serial settings are fixed.


        public void RegisterUpdateCallback(ContentCalculation callback)
        {
            Callback = callback;
        }

        public int GetLedsOnChain(int chain)
        {
            return LedsPerChain[chain];
        }


        public void SetLedsOnChain(int chain, int count)
        {
            if (!LedsPerChain.ContainsKey(chain))
                LedsPerChain.Add(chain, count);
            else
                LedsPerChain[chain] = count;
        }
        

        public static List<String> GetSerialPorts()
        {
            var ports = SerialPort.GetPortNames().ToList();
            return ports;
        }

        public void Start(String port)
        {
            if (!GetSerialPorts().Contains(port))
                throw new Exception("Serial port does not exist.");

            if ((FrameRate < 1) || (FrameRate > 30))
                throw new Exception("invalid frame rate");

            if (ChainCount < 1 || ChainCount > 6)
                throw new Exception("Invalid number of chains.");

            for (int i = 0; i < ChainCount; ++i)
            {
                if (!LedsPerChain.ContainsKey(i) || LedsPerChain[i] < 0 || LedsPerChain[i] > 512)
                    throw new Exception("Invalid Chain configuration.");
            }

            if (Callback == null)
                throw new Exception("A callback must be registered to work!");

            Port.BaudRate = 3000000;
            Port.PortName = port;

            if (WriteThread != null)
            {
                if (WriteThread.IsAlive)
                    WriteThread.Join();
            }


            Port.Open();

            WriteThread = new Thread(new ThreadStart(WorkerThread));
            WriteThread.IsBackground = true;
            WriteThread.Start();

        }

        void WorkerThread()
        {


            DateTime start_time = DateTime.Now;
            int ms = 1000 / FrameRate;
            int frame_count = 0;

            while (true)
            {

                //get data
                byte[] data = GenerateByteArray(true);

                //write data
                try
                {
                    Port.Write(data, 0, data.Length);
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("ERROR: writing LED data failed.");
                    if (Port.IsOpen)
                        Port.Close();
                    return;
                }
                


                //check for upper channels.
                if (ChainCount > 3)
                {
                    data = GenerateByteArray(false);

                    try
                    {
                        Port.Write(data, 0, data.Length);
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine("ERROR: writing LED data failed.");
                        if (Port.IsOpen)
                            Port.Close();
                        return;
                    }
                    
                }

                //compute sleep time
                TimeSpan span = start_time.AddMilliseconds((frame_count) * ms).Subtract(DateTime.Now);


                if (span.Ticks < 0)
                {
                    frame_count = 2;
                    start_time = DateTime.Now;
                }
                else
                {
                    Thread.Sleep(span);
                }

                frame_count++;
            }


        }

        byte[] GenerateByteArray( bool lower_chains )
        {
            List<int> chains_to_request = new List<int>();

            if (lower_chains)
            {
                for (int i = 0; i < (ChainCount > 3 ? 3 : ChainCount); ++i)
                {
                    chains_to_request.Add(i);
                }
            }
            else
            {
                for (int i = 3; i < ChainCount; ++i)
                {
                    chains_to_request.Add(i);
                }
            }

            Dictionary<int, List<Tuple<byte, byte, byte>>> results = new Dictionary<int, List<Tuple<byte, byte, byte>>>();

            foreach (int index in chains_to_request)
            {

                results.Add(index, Callback(index, LedsPerChain[index]));

            }

            int longest_chain = results.Max( x => LedsPerChain[x.Key]);
            int size = longest_chain * 9;
            byte[] output = new byte[size+1];

            foreach (var result in results)
            {
                for (int i = 0; i < LedsPerChain[result.Key]; ++i)
                {
                    output[(i * 9) + (result.Key % 3) + 1] = result.Value[i].Item1;
                    output[(i * 9) + (result.Key % 3) + 3 + 1] = result.Value[i].Item2;
                    output[(i * 9) + (result.Key % 3) + 6 + 1] = result.Value[i].Item3;
                }

            }
            output[0] = (byte)(lower_chains ? 0 : 1);
            return output;
            
        }

      


    }
}
