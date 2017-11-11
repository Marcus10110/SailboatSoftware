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
using System.Windows.Shapes;
using FTD2XX_NET;

namespace SailBot
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        String MotorComPort// = "COM9";
        {
            get { return Properties.Settings.Default.MotorComPort; }
            set { Properties.Settings.Default.MotorComPort = value; Properties.Settings.Default.Save(); }
        }
        String BmsComPort// = "COM13";
        {
            get { return Properties.Settings.Default.BmsComPort; }
            set { Properties.Settings.Default.BmsComPort = value; Properties.Settings.Default.Save(); }
        }

        String LedComPort// = "COM13";
        {
            get { return Properties.Settings.Default.LedComPort; }
            set { Properties.Settings.Default.LedComPort = value; Properties.Settings.Default.Save(); }
        }

        String NmeaComPort// = "COM13";
        {
            get { return Properties.Settings.Default.NmeaComPort; }
            set { Properties.Settings.Default.NmeaComPort = value; Properties.Settings.Default.Save(); }
        }


        public static void ValidatePorts()
        {
            var avalible_ports = System.IO.Ports.SerialPort.GetPortNames().ToList();

            avalible_ports.RemoveAll(x => x == "COM1");
            avalible_ports.RemoveAll(x => x == "COM2");

            if (!avalible_ports.Contains(Properties.Settings.Default.MotorComPort))
                Properties.Settings.Default.MotorComPort = null;

            if (!avalible_ports.Contains(Properties.Settings.Default.BmsComPort))
                Properties.Settings.Default.BmsComPort = null;

            if (!avalible_ports.Contains(Properties.Settings.Default.LedComPort))
                Properties.Settings.Default.LedComPort = null;

            if( !avalible_ports.Contains( Properties.Settings.Default.NmeaComPort ) )
                Properties.Settings.Default.NmeaComPort = null;

            Properties.Settings.Default.Save();

        }

        public static void AutoIdentifyPorts()
        {

            Dictionary<string, string> ftdi_ports_detected = new Dictionary<string, string>();

            //clear out ports that don't exist first:
            ValidatePorts();

            UInt32 ftdiDeviceCount = 0;
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;

            FTDI myFtdiDevice = new FTDI();

            ftStatus = myFtdiDevice.GetNumberOfDevices( ref ftdiDeviceCount );
            // Check status
            if( ftStatus != FTDI.FT_STATUS.FT_OK )
                return;
            
            if( ftdiDeviceCount == 0 )
                return;

            FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ ftdiDeviceCount ];
            ftStatus = myFtdiDevice.GetDeviceList( ftdiDeviceList );

            if( ftStatus != FTDI.FT_STATUS.FT_OK )
                return;

            for( UInt32 i = 0; i < ftdiDeviceCount; i++ )
            {

                ftStatus = myFtdiDevice.OpenBySerialNumber( ftdiDeviceList[ i ].SerialNumber );
                if( ftStatus != FTDI.FT_STATUS.FT_OK )
                    return;

                string port = "";
                ftStatus = myFtdiDevice.GetCOMPort( out port );

                if( ftStatus != FTDI.FT_STATUS.FT_OK )
                    return;

                myFtdiDevice.Close();
                string serial_number = ftdiDeviceList[ i ].SerialNumber.ToString();
                Console.WriteLine( "FTDI detected. Serial Number: " + serial_number + " Port: " + port );
                ftdi_ports_detected.Add( ftdiDeviceList[ i ].SerialNumber.ToString(), port );
            }

            string motor_serial = "MOTORCOM";
            string bms_serial = "BMSCOM";
            string led_serial = "LEDCOM";
            string autopilot_serial = "NMEACOM";

            ValidatePorts();

            if( ftdi_ports_detected.ContainsKey( motor_serial ) )
                Properties.Settings.Default.MotorComPort = ftdi_ports_detected[ motor_serial ];

            if( ftdi_ports_detected.ContainsKey( bms_serial ) )
                Properties.Settings.Default.BmsComPort = ftdi_ports_detected[ bms_serial ];

            if( ftdi_ports_detected.ContainsKey( led_serial ) )
                Properties.Settings.Default.LedComPort = ftdi_ports_detected[ led_serial ];

            if( ftdi_ports_detected.ContainsKey( autopilot_serial ) )
                Properties.Settings.Default.NmeaComPort = ftdi_ports_detected[ autopilot_serial ];

            Properties.Settings.Default.Save();


            ValidatePorts(); //just in case something went wrong.
        }

        public SettingsDialog()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitSelections();
        }

        void InitSelections()
        {
            var avalible_ports = System.IO.Ports.SerialPort.GetPortNames().ToList();

            avalible_ports.RemoveAll( x => x == "COM1" );
            avalible_ports.RemoveAll( x => x == "COM2" );

            avalible_ports.ToList().ForEach( x => MotorComComboBox.Items.Add( x ) );
            avalible_ports.ToList().ForEach( x => BmsComComboBox.Items.Add( x ) );
            avalible_ports.ToList().ForEach( x => LedComComboBox.Items.Add( x ) );
            avalible_ports.ToList().ForEach( x => NmeaComComboBox.Items.Add( x ) );

            if( MotorComComboBox.Items.Contains( MotorComPort ) )
            {
                MotorComComboBox.SelectedIndex = MotorComComboBox.Items.IndexOf( MotorComPort );
            }

            if( BmsComComboBox.Items.Contains( BmsComPort ) )
            {
                BmsComComboBox.SelectedIndex = BmsComComboBox.Items.IndexOf( BmsComPort );
            }

            if( LedComComboBox.Items.Contains( LedComPort ) )
            {
                LedComComboBox.SelectedIndex = LedComComboBox.Items.IndexOf( LedComPort );
            }

            if( NmeaComComboBox.Items.Contains( NmeaComPort ) )
            {
                NmeaComComboBox.SelectedIndex = NmeaComComboBox.Items.IndexOf( NmeaComPort );
            }
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
        	// TODO: Add event handler implementation here.

            if (!String.IsNullOrEmpty(MotorComComboBox.SelectedItem as string))
            {
                MotorComPort = MotorComComboBox.SelectedItem as string;
            }

            if (!String.IsNullOrEmpty(BmsComComboBox.SelectedItem as string))
            {
                BmsComPort = BmsComComboBox.SelectedItem as string;
            }

            if (!String.IsNullOrEmpty(LedComComboBox.SelectedItem as string))
            {
                LedComPort = LedComComboBox.SelectedItem as string;
            }

            if( !String.IsNullOrEmpty( NmeaComComboBox.SelectedItem as string ) )
            {
                NmeaComPort = NmeaComComboBox.SelectedItem as string;
            }

			Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //reset.
            BmsComComboBox.SelectedIndex = -1;
            MotorComComboBox.SelectedIndex = -1;
            LedComComboBox.SelectedIndex = -1;
            NmeaComComboBox.SelectedIndex = -1;
        }

        private void Button_Click_2( object sender, RoutedEventArgs e )
        {
            AutoIdentifyPorts();
            InitSelections();
        }


    }
}
