using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace SailBot
{
    public class WebSocketService
    {

        

        private WebSocketServer Server;
        const int Port = 1337;

        Func<BmsDisplayValues> GetBmsStatusFunc = null;
        Action<string> SetLedFunc = null;
        public WebSocketService( Func<BmsDisplayValues> get_bms_status_func, Action<string> set_led_func )
        {
            GetBmsStatusFunc = get_bms_status_func;
            SetLedFunc = set_led_func;
        }

        public void Start()
        {
            Server = new WebSocketServer(Port);
            Server.AddWebSocketService<ProcessMessage>("/", (x) => { x.Init(GetBmsStatusFunc, SetLedFunc); });
            Server.Start();
            if (Server.IsListening)
            {
                Console.WriteLine("Listening on port " + Server.Port + " and providing WebSocket services:");
                foreach (var path in Server.WebSocketServices.Paths)
                    Console.WriteLine(path);
            }

        }

    }

    public class ProcessMessage : WebSocketBehavior
    {
        Func<BmsDisplayValues> GetBmsStatusFunc { get; set; }
        Action<string> SetLedFunc { get; set; }


        public void Init(Func<BmsDisplayValues> get_bms_status_func, Action<string> set_led_func)
        {
            GetBmsStatusFunc = get_bms_status_func;
            SetLedFunc = set_led_func;
        }



        protected override void OnMessage(MessageEventArgs e)
        {
            if( e.Data == "STATUS")
            {
                Random rand = new Random();
                BmsDisplayValues bms_data = GetBmsStatusFunc();
                if (bms_data == null)
                {
                    bms_data = new BmsDisplayValues() //test data.
                    {
                        CellTemp = new double[15],
                        CellVoltage = new double[15],
                        ChargeCurrent = 0.124 + (rand.NextDouble() / 45),
                        Humidity = 0.34,
                        PackAmpHours = 39.2452,
                        PackVoltage = 48.62,
                        PrimaryCurrent = 15.46,
                        Temperture = 81.6
                    };
                    for(int i = 0; i < bms_data.CellTemp.Length; ++i)
                    {
                        bms_data.CellTemp[i] = rand.Next(55, 65);
                        bms_data.CellVoltage[i] = rand.Next(310, 350) / 100.0;
                    }
                }
                if (bms_data != null)
                    Send(bms_data.GetJson());
                else
                    Send(JsonConvert.SerializeObject(new { error = "BMS data not loaded" }));
                return;
            }

            if( e.Data.StartsWith("LED"))
            {
                string command = e.Data.Substring("LED".Length).Trim();
                SetLedFunc(command);
                Send(JsonConvert.SerializeObject(new { success = true }));
                return;
            }

            Send(JsonConvert.SerializeObject(new { error = "unknown command: " + e.Data }));
            return;
        }
    }
}
