using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Threading;
using System.Collections.Concurrent;
using System.Net.Mail;
using System.IO;

namespace SailBot
{

    public struct MotorDisplayValues
    {
        public Double TempF { get; set; }
        public Double Humidity { get; set; }
        public Double Rpm { get; set; }
    }


    public partial class MainWindow : Window
    {

        String[] Titles = { "SailBot", "Båtkapten", "Capitán del Barco", "kapitan łodzi", "capitaine de bateau" };



        MotorControllerCom MotorCom = new MotorControllerCom();
        BatteryCom BmsCom = new BatteryCom();
        BoatLightControl Leds = new BoatLightControl();
        WebSocketService WebSocketService;


        int ValidCellCountHistory = 0;

        String MotorComPort
        {
            get { return Properties.Settings.Default.MotorComPort; }
            set { Properties.Settings.Default.MotorComPort = value; Properties.Settings.Default.Save(); }
        }
        String BmsComPort
        {
            get { return Properties.Settings.Default.BmsComPort; }
            set { Properties.Settings.Default.BmsComPort = value; Properties.Settings.Default.Save(); }
        }

        String LedComPort
        {
            get { return Properties.Settings.Default.LedComPort; }
            set { Properties.Settings.Default.LedComPort = value; Properties.Settings.Default.Save(); }
        }

        String NmeaComPort
        {
            get { return Properties.Settings.Default.NmeaComPort; }
            set { Properties.Settings.Default.NmeaComPort = value; Properties.Settings.Default.Save(); }
        }

        List<BatteryViewControl> BatteryViews = new List<BatteryViewControl>();
        List<CheckBox> RelayPrechargeCheckboxes = new List<CheckBox>();
        List<Button> RelayBtns = new List<Button>();

        MotorControllerCom.Direction MotorDirection = MotorControllerCom.Direction.NoDirection;
        
        bool KeyOn = false;

        double MotorBoardHumidity = 0;

        private volatile bool IsMotorConnected = false;
        private volatile bool IsBmsConnected = false;

        const int CellCount = 15;

        private Double MotorSpeed = 0.0; //absolute value.


        //private Thread ComThread;


        private System.Timers.Timer ClockTimer;
        private System.Timers.Timer BmsUpdateTimer;
        private System.Timers.Timer MotorTimer;


        private object BmsLock = new object();
        private object MotorLock = new object();

        private DebugWriter DebugOut;

        private DateTime LastValidCellDataTime = DateTime.MinValue;
        private DateTime LastSuccessfullBmsMessage = DateTime.MinValue;

        private object BatteryReadDataStructLock = new object();
        private BatteryReadDataStruct mBatteryReadData;
        BatteryReadDataStruct BatteryReadData
        {
            get 
            { 
                lock( BatteryReadDataStructLock )
                {
                    return mBatteryReadData;
                }
            }
            set
            {
                lock( BatteryReadDataStructLock )
                {
                    mBatteryReadData = value;
                }
            }
        }

        public BatteryWriteDataStruct BatteryWriteData = new BatteryWriteDataStruct();

        public MainWindow()
        {
            InitializeComponent();

            BatteryViews.Add(Cell0);
            BatteryViews.Add(Cell1);
            BatteryViews.Add(Cell2);
            BatteryViews.Add(Cell3);
            BatteryViews.Add(Cell4);
            BatteryViews.Add(Cell5);
            BatteryViews.Add(Cell6);
            BatteryViews.Add(Cell7);
            BatteryViews.Add(Cell8);
            BatteryViews.Add(Cell9);
            BatteryViews.Add(Cell10);
            BatteryViews.Add(Cell11);
            BatteryViews.Add(Cell12);
            BatteryViews.Add(Cell13);
            BatteryViews.Add(Cell14);

            RelayPrechargeCheckboxes.Add( Relay1PrechargeChkBox );
            RelayPrechargeCheckboxes.Add( Relay2PrechargeChkBox );
            RelayPrechargeCheckboxes.Add( Relay3PrechargeChkBox );
            RelayPrechargeCheckboxes.Add( Relay4PrechargeChkBox );
            RelayPrechargeCheckboxes.Add( Relay5PrechargeChkBox );

            foreach( CheckBox box in RelayPrechargeCheckboxes )
            {
                box.Checked += box_Checked;
                box.Unchecked += box_Checked;
            }

            for (int i = 0; i < BatteryViews.Count(); ++i)
            {
                BatteryViews[i].Voltage = 0;
                BatteryViews[i].CellId = i;
                BatteryViews[i].Temp = 0;

                BatteryViews[i].MouseDoubleClick += BatteryView_MouseDoubleClick;
            }

            RelayBtns.Add(Relay1Btn);
            RelayBtns.Add(Relay2Btn);
            RelayBtns.Add(Relay3Btn);
            RelayBtns.Add(Relay4Btn);
            RelayBtns.Add(Relay5Btn);



            foreach (var btn in RelayBtns)
            {
                btn.Click += RelayBtn_Click;
            }
            UpdateRelayBtnText();

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            

            DebugOut = new DebugWriter(DebugListBox, this.Dispatcher, DebugWriter.GetDefaultLogLocation());

            Console.SetOut(DebugOut);

            Console.WriteLine("Sailbot loaded. Debug output re-directed.");
            Console.WriteLine(DateTime.Now.ToString());

            SettingsDialog.AutoIdentifyPorts();

            ClockTimer = new System.Timers.Timer();
            ClockTimer.Interval = 1000;
            ClockTimer.Elapsed += ClockTimer_Elapsed;
            ClockTimer.Start();


            UpdateMotorConnectedStatus();
            //UpdateBmsConnectedStatus();

            BmsUpdateTimer = new System.Timers.Timer();
            BmsUpdateTimer.Interval = 1000; //1 times a second
            BmsUpdateTimer.Elapsed += BmsUpdateTimer_Elapsed;
            BmsUpdateTimer.Start();

            Leds.SetInteriorColor(Brushes.White);
            Leds.Init(LedComPort);


            MotorTimer = new System.Timers.Timer();
            MotorTimer.Interval = 1000;
            MotorTimer.Elapsed += MotorTimer_Elapsed;
            MotorTimer.Start();

            //start websocket

            try
            {
                WebSocketService = new WebSocketService(GetDisplayValuesForSocket, ProcessLedSocketCommand);
                WebSocketService.Start();
            }
            catch( Exception ex)
            {
                Console.WriteLine("failed to start websocket. error: " + ex.Message);
            }
        }

        private BmsDisplayValues GetDisplayValuesForSocket()
        {
            if (BatteryReadData == null)
                return null;
            return BatteryReadData.ConvertToDisplayData();
        }

        private void ProcessLedSocketCommand( string command )
        {

            Dictionary<string, Action> LedCommands = new Dictionary<string, Action>();

            LedCommands.Add("white", () => { Leds.SetInteriorColor(Brushes.White); });
            LedCommands.Add("red", () => { Leds.SetInteriorColor(Brushes.White); });
            LedCommands.Add("rainbow", () => { Leds.SetRainbow(); });
            LedCommands.Add("off", () => { Leds.InteriorLightsOff(); });

            if (LedCommands.ContainsKey(command))
            {
                Console.WriteLine("LED command received: " + command);
                LedCommands[command]();
            }
 
        }



        private void BatteryView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BatteryViewControl control = sender as BatteryViewControl;

            if (control == null)
                throw new Exception();

            int index = BatteryViews.IndexOf(control);

            if (index < 0)
                throw new Exception();

            double current = double.Parse(CurrentTxtBox.Text);

            BatteryWriteData.SetCellCurrent( index, current );
           

        }

        private void ChargeBtn_Click(object sender, RoutedEventArgs e)
        {
            if( ChargeActive )
            {
                //abort charge
                Console.WriteLine( "Manually aborted charge." );
                WriteLineToChargeLog("Stopping Charge. AbortCharge() called, User clicked Stop Charge.");
                AbortCharge();
                
            }
            else
            {
                //start charge
                Console.WriteLine("Starting charge");
                StartCharge();
                
            }

        }

        void EmailDetails(string extra_data = "")
        {
            MailMessage message = new MailMessage("markgarrison@saleae.com", "5102069107@txt.att.net");
            message.CC.Add("mark.garrison@gmail.com");
            message.Subject = "batt updt";

            if (BatteryReadData == null)
            {
                Console.WriteLine("BMS not availible for SMS");
                return;
            }

            string body = "";

            if( IsBmsConnected )
                body += "connected\n";
            else
                body += "DISCONECTED\n";

            if (LastValidCellDataTime != default(DateTime))
                body += "lst updt:" + DateTime.Now.Subtract(LastValidCellDataTime).TotalMinutes.ToString("0") + "\n";
            else
                body += "lst updt ns!\n";


            double total_voltage = 0;
            double min_voltage = 0;
            double max_voltage = 0;


            total_voltage = BatteryViews.Take(CellCount).Sum(x => x.Voltage);
            min_voltage = BatteryViews.Take(CellCount).Min(x => x.Voltage);
            max_voltage = BatteryViews.Take(CellCount).Max(x => x.Voltage);

            double delta = max_voltage - min_voltage;
            double mean = total_voltage / CellCount;

            body += "av:" + mean.ToString("0.00") + "\n";
            body += "d:" + delta.ToString("0.00") + "\n";

            body += "pck:" + BatteryReadData.GetPackVoltage().ToString("0.00") + "\n";

            double max_temp = BatteryViews.Take(CellCount).Max(x => x.Temp);
            body += "mxtmp:" + max_temp.ToString("0") + "F\n";

            body += extra_data;
            message.Body = body;


            SmtpClient mail_client = new SmtpClient("smtp.gmail.com", 587);
            mail_client.EnableSsl = true;
            mail_client.DeliveryMethod = SmtpDeliveryMethod.Network;
            mail_client.Credentials = new System.Net.NetworkCredential("markgarrison@saleae.com", "M79t4uY2");

            Console.WriteLine("Trying to send status text/email at " + DateTime.Now.ToString());
            try
            {
                mail_client.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send email.");
            }
        }

        void BmsUpdateTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //10 times a second.
            try
            {
                BmsUpdateTimer.Stop();
                BmsUpdate( BatteryWriteData );
            }
            catch( Exception ex )
            {
                Console.WriteLine("error communicating with Battery BMS at regular interval. detail: " + ex.Message);
            }
            finally
            {
                BmsUpdateTimer.Start();
            }

        }

        void MotorTimer_Elapsed( object sender, System.Timers.ElapsedEventArgs e )
        {
            //we need to get to the main thread!
            if( this.Dispatcher.CheckAccess())
            {
                MotorTimerTask();
            }
            else
            {
                this.Dispatcher.BeginInvoke( ( Action )( () => { MotorTimerTask(); } ) );
            }


        }

        void MotorTimerTask()
        {

            Task.Run( () => {
                UpdateMotorConnectedStatus();

                if( !IsMotorConnected )
                    return;

                try
                {
                    PingMotor();
                    //GetMotorHumidity();
                    //GetMotorTemp();
                }
                catch( Exception ex )
                {
                    Console.WriteLine( "Motor COM failure: " + ex.Message );
                }
            
            } );


        }



        void ClockTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => { TimeLbl.Content = DateTime.Now.ToString("T"); }));
        }

        private void TextBtn_Click(object sender, RoutedEventArgs e)
        {
            EmailDetails();
        }

        private void SetupBtn_Click(object sender, RoutedEventArgs e)
        {
            string old_led_com = LedComPort;
            SettingsDialog dialog = new SettingsDialog();
            dialog.ShowDialog();

            if( String.IsNullOrWhiteSpace(old_led_com) && !String.IsNullOrWhiteSpace(LedComPort))
            {
                Leds.Init(LedComPort);
            }
            
        }

        private void CloseBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if( BmsUpdateTimer != null && BmsUpdateTimer.Enabled )
                BmsUpdateTimer.Stop();

            if( MotorTimer != null && MotorTimer.Enabled )
                MotorTimer.Stop();

            if (IsMotorConnected)
            {
                //stop the motor.
                SetSpeed(0.0);
            }

            Close();
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateMotorConnectedStatus();

            if (!IsMotorConnected)
                return;

            SpeedSlider.Value = 0;

            SetSpeed(0.0);
        }

        private void SpeedSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            UpdateMotorConnectedStatus();

            if (!IsMotorConnected)
                return;

            double speed = SpeedSlider.Value / SpeedSlider.Maximum;

            SetSpeed(speed);

            //this.Dispatcher.BeginInvoke( ( Action<double> )( x => { SetSpeed( x ); } ), speed );

        }

        void box_Checked( object sender, RoutedEventArgs e )
        {
            //we need to get our index.
            int index = -1;
            for( int i = 0; i < RelayPrechargeCheckboxes.Count(); ++i)
            {
                if( sender == RelayPrechargeCheckboxes[ i ] )
                    index = i;
            }

            if( index == -1 )
                throw new Exception();

            bool is_checked = RelayPrechargeCheckboxes[ index ].IsChecked.Value;

            BatteryWriteData.SetRelayPrechargeState( index, is_checked );

            if( is_checked )
            {
                RelayBtns[ index ].IsEnabled = true;
            }
            else if ((BatteryWriteData.GetRelayState(index) == false) && (index != 1)) //disable the button if the relay isn't currently on.
            {
                RelayBtns[ index ].IsEnabled = false;
            }

        }

        private void RelayBtn_Click(object sender, RoutedEventArgs e)
        {
  
            int relay_index = -1;

            for (int i = 0; i < RelayBtns.Count(); ++i)
            {
                if (RelayBtns[i] == sender)
                {
                    relay_index = i;
                    break;
                }
            }

            if (relay_index == -1)
                throw new Exception();

            //if the relay is currently off, the pre-charge box must be checked. lets automatically clear it here.
            if( BatteryWriteData.GetRelayState( relay_index ) == false )
            {
                BatteryWriteData.SetRelayPrechargeState( relay_index, false );
                
                BatteryWriteData.SetRelayState( relay_index, true );
                RelayPrechargeCheckboxes[relay_index].IsChecked = false;

            }
            else
            {
                //if the relay is currently on, turn it off.
                BatteryWriteData.SetRelayState( relay_index, false );
                if ((RelayPrechargeCheckboxes[relay_index].IsChecked.Value == false) && (relay_index != 1))
                    RelayBtns[ relay_index ].IsEnabled = false;

            }

            UpdateRelayBtnText();
        }

        private void UpdateRelayBtnText()
        {
            for (int i = 0; i < RelayBtns.Count(); ++i)
            {
                bool state = BatteryWriteData.GetRelayState( i );

                if (state == true)
                {
                    RelayBtns[i].Content = "Relay " + (i + 1) + " IS ON";
                }
                else
                {
                    RelayBtns[i].Content = "Relay " + (i + 1) + " is Off";
                }
            }
        }


        void UpdateDirection( MotorControllerCom.Direction direction )
        {
            string dir_string = "";
            switch( direction )
            {
                case MotorControllerCom.Direction.Forward:
                    dir_string = "FORWARD";
                    break;
                case MotorControllerCom.Direction.Reverse:
                    dir_string = "REVERSE";
                    break;
                case MotorControllerCom.Direction.NoDirection:
                    dir_string = "NO DIRECTION";
                    break;
            }

            DirLbl.Content = dir_string;
            

        }

        private void ForwardBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateMotorConnectedStatus();

            if (!IsMotorConnected)
                return;

            SetDirection(MotorControllerCom.Direction.Forward);

        }

        private void ReverseBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateMotorConnectedStatus();

            if (!IsMotorConnected)
                return;

            SetDirection(MotorControllerCom.Direction.Reverse);

        }

        private void NoDirectionBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateMotorConnectedStatus();

            if (!IsMotorConnected)
                return;

            SetDirection(MotorControllerCom.Direction.NoDirection);
        }

        private void KeyOnBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateMotorConnectedStatus();

            if (!IsMotorConnected)
                return;

            SetKey(true);
        }

        private void KeyOffBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateMotorConnectedStatus();

            if (!IsMotorConnected)
                return;

            SetKey(false);
        }



        private void UpdateHumidityDisplay()
        {

            double bms_humidity = 0;
            if( BatteryReadData  != null )
                bms_humidity = BatteryReadData.GetMasterHumidity();
            MotorHumidityLbl.Content = "HUMIDITY: " + MotorBoardHumidity.ToString( "0" ) + "%," + bms_humidity.ToString( "0" ) + "%";
        }

        private void UpdateCellStats()
        {
            double total_voltage = 0;
            double min_voltage = 0;
            double max_voltage = 0;

            BatteryReadDataStruct read_data = BatteryReadData;



            total_voltage = read_data.CellVoltages.Take( CellCount ).Sum( x => x );
            min_voltage = read_data.CellVoltages.Take( CellCount ).Min( x => x );
            max_voltage = read_data.CellVoltages.Take( CellCount ).Max( x => x );

            double delta = max_voltage - min_voltage;
            double mean = total_voltage / CellCount;

            PackVoltageDeltaLbl.Content = "Pack Δ " + delta.ToString("0.00") + "V";
            PackMeanVoltageLbl.Content = "x̄ Cell: " + mean.ToString("0.00") + "V";

            foreach (BatteryViewControl bat in BatteryViews)
            {
                if (bat.Voltage < 2.7 || bat.Voltage > 3.6)
                    bat.IsCritical = true;
                else
                    bat.IsCritical = false;

                if (bat.Temp > 100)
                    bat.IsOverheated = true;
                else
                    bat.IsOverheated = false;

            }


        }

        private void UpdatePackPower()
        {
            double pack_voltage = BatteryReadData.GetPackVoltage();
            double master_current = BatteryReadData.GetMasterCurrent();
            double charge_current = BatteryReadData.GetChargeCurrent();
            double pack_amp_hours = BatteryReadData.GetAmpHours();


            SystemCurrentLbl.Content = "Current: " + master_current.ToString( "0.000" ) + "A";
            SystemVoltageLbl.Content = "Voltage: " + pack_voltage.ToString( "0.00" ) + "V";

            double w = pack_voltage * master_current;
            double kw = w / 1000.0;
            double hp = kw * 1.34102209;

            MotorPowerLbl.Content = kw.ToString("0.00") + " kW";
            MotorHpLbl.Content = hp.ToString("0.00") + "HP";

            if( charge_current > 0.1 || charge_current < -0.1 )
            {
                PackStatusLbl.Content = "CHARGING";
            }
            else
            {
                if (w < 1.0)
                {
                    PackStatusLbl.Content = "NO LOAD";
                }
                else
                {
                    PackStatusLbl.Content = "UNDER LOAD";
                }

            }

            PackStatusLbl.Content = charge_current.ToString("0.000") + "A";

            double charge_percentage =  pack_amp_hours != 0 ? (pack_amp_hours / 40.0) : 0.0;
            PackChargeLevelLbl.Content = pack_amp_hours.ToString("0.0") + "%  " + pack_amp_hours.ToString("0.0") + "Ah";  //00.0%  0.00Ah

        }

        void LogCellVoltages()
        {
            BatteryReadDataStruct read_data = BatteryReadData;
            string line = DateTime.Now.ToString() + ", ";
            line += String.Join( ", ", read_data.CellVoltages.Select( x => x.ToString( "0.00" ) ) );
            line += ", ";
            line += String.Join( ", ", read_data.CellTemperatures.Select( x => x.ToString( "0.00" ) ) );
            line += ", ";
            line += String.Join( ", ", BatteryWriteData.GetCellCurrents().Select( x => x.ToString( "0.00" ) ) );

            WriteLineToCellLog( line );
        }

        void WriteLineToSystemLog(String line)
        {
            string file_path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            file_path = file_path + @"\SailBot Log\";

            if (!Directory.Exists(file_path))
            {
                Directory.CreateDirectory(file_path);
            }

            string file_name = "SystemLog.txt";

            file_path += file_name;

            bool write_header = false;
            if (!System.IO.File.Exists(file_path))
            {
                write_header = true;
            }

            System.IO.StreamWriter wr = new StreamWriter(file_path, true);

            if (write_header)
            {
                string header = "Date and Time, Event...";


                wr.WriteLine(header);
            }

            wr.WriteLine(DateTime.Now.ToString() + ", " + line);

            wr.Close();
        }

        void WriteLineToChargeLog( String line )
        {
            string file_path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            file_path = file_path + @"\SailBot Log\";

            if (!Directory.Exists(file_path))
            {
                Directory.CreateDirectory(file_path);
            }

            string file_name = "ChargeEventLog.txt";

            file_path += file_name;

            bool write_header = false;
            if (!System.IO.File.Exists(file_path))
            {
                write_header = true;
            }

            System.IO.StreamWriter wr = new StreamWriter(file_path, true);

            if (write_header)
            {
                string header = "Date and Time, Event...";


                wr.WriteLine(header);
            }

            wr.WriteLine( DateTime.Now.ToString() + ", " + line);

            wr.Close();
        }

        void WriteLineToCellLog( String line )
        {
            string file_path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            file_path = file_path + @"\SailBot Log\";

            if (!Directory.Exists(file_path))
            {
                Directory.CreateDirectory(file_path);
            }


            string file_name = "CellVoltageLog.txt";

            file_path += file_name;

            bool write_header = false;
            if (!System.IO.File.Exists(file_path))
            {
                write_header = true;
            }

            System.IO.StreamWriter wr = new StreamWriter(file_path, true);

            if (write_header)
            {
                int cell_count = BatteryWriteData.GetCellCurrents().Count(); //don't ask for the GUI's copy of cell count, that is used for aritificially limiting the number of cells.
                string header = "Date and Time, ";
                for( int i = 0; i < cell_count; ++i )
                    header += "Voltage " + i + ", ";
                for( int i = 0; i < cell_count; ++i )
                    header += "Temp " + i + ", ";
                for( int i = 0; i < cell_count; ++i )
                {
                    header += "Current " + i;
                    if( i < cell_count-1)
                        header += ", ";

                }


                wr.WriteLine(header);
            }

            wr.WriteLine(line);

            wr.Close();


        }

        private void AlarmCheckBox_Checked( object sender, RoutedEventArgs e )
        {
            BatteryWriteData.DisableBuzzer = AlarmCheckBox.IsChecked.Value;
        }


    }
}
