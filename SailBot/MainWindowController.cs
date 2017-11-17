using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SailBot
{
    public partial class MainWindow
    {
        //New system here:
        //synchronous functions for background thread.

        void BmsUpdate( BatteryWriteDataStruct write_data )
        {
            BatteryReadDataStruct read_data = null;


            if (!String.IsNullOrEmpty(BmsComPort) && BmsCom.Open(BmsComPort))
            {
                try
                {
                    //usually throws a timeout if the FTDI part has power and is connected but 48V is off.
                    read_data = BmsCom.BmsUpdate(write_data);
                }
                catch( Exception ex)
                {
                    //we still need to close the comport if we left it open, so the next attempt works properly.
                    if( BmsCom.IsOpen() )
                        BmsCom.Close();
                    throw ex; //rethrow, so the top catches it.
                }

                BmsCom.Close();     
            }
            

            if( read_data != null )
            {
                BatteryReadData = read_data;
                Dispatcher.BeginInvoke((Action)(() => { BmsUpdateGui(); }));
            }
        }

        void BmsUpdateGui()
        {
            LastSuccessfullBmsMessage = DateTime.Now;

            //only update cell info if we have new, complete cell information.
            if( BatteryReadData.GetValidCellReadCount() != ValidCellCountHistory )
            {
                Console.WriteLine("Amp Hours: " + BatteryReadData.GetAmpHours().ToString("0.000"));
                LastValidCellDataTime = DateTime.Now;
                Console.WriteLine("Updating with new valid cell data");
                LogCellVoltages();
                for (int i = 0; i < BatteryReadDataStruct.CELL_COUNT; ++i)
                {
                    BatteryViews[ i ].Voltage = BatteryReadData.GetCellVoltage( i );
                    BatteryViews[ i ].Temp = BatteryReadData.GetCellTemperature( i );
                }

                UpdateCellStats();
                ChargeUpdate(); //only does stuff if charge is enabled.
                //log charge current.
                if( ChargeActive )
                {
                    //log charge current!
                    WriteLineToChargeLog("Charge Current: " + BatteryReadData.GetChargeCurrent().ToString("0.000") + " Raw ADC value: " + BatteryReadData.GetRawChargeAdcValue().ToString());
                }
            }

            for( int i = 0; i < 5; ++i )
            {
                RelayPrechargeCheckboxes[ i ].Content = BatteryReadData.GetRelayPrechargeVoltage( i );
            }

            ValidCellCountHistory = BatteryReadData.GetValidCellReadCount();

            UpdatePackPower();
            UpdateHumidityDisplay();

            BmsMasterTempLbl.Content = "BMS BOARD TEMP: " + BatteryReadData.GetMasterTemperature().ToString( "0.0" ) + " F";


            //Console.WriteLine( "Valid Cell Read Count: " + BatteryReadData.GetValidCellReadCount().ToString() );

            var errors = BatteryReadData.GetErrors();
            foreach( string error in errors )
            {
                Console.WriteLine( "BMS ERROR: " + error );
            }

            if( BatteryReadData.GetErrors().Any() )
                AlarmCheckBox.Foreground = System.Windows.Media.Brushes.Red;
            else
                AlarmCheckBox.Foreground = System.Windows.Media.Brushes.White;

           
            WriteLineToSystemLog("Master Current, " + BatteryReadData.GetMasterCurrent() + ", Amp Hours, " + BatteryReadData.GetAmpHours());

        }

        void UpdateMotorConnectedStatus()
        {

            lock (MotorLock)
            {
                IsMotorConnected = BoatSerialDevice.CheckComportConnection(MotorComPort);
            }

            string status = "";
            if (IsMotorConnected)
                status += "Motor Board Connected.";
            else
                status += "Motor Board NOT Connected.";

            status += " ";
            if (IsBmsConnected)
                status += "BMS Board Connected.";
            else
                status += "BMS Board NOT Connected.";

            Dispatcher.BeginInvoke((Action<string>)((x) => { RangeLbl.Content = x; }), status);

        }

        void UpdateBmsConnectedStatus()
        {

            lock (BmsLock)
            {

                bool new_state = BoatSerialDevice.CheckComportConnection(BmsComPort);

                IsBmsConnected = new_state;
            }



            string status = "";
            if (IsMotorConnected)
                status += "Motor Board Connected.";
            else
                status += "Motor Board NOT Connected.";

            status += " ";
            if (IsBmsConnected)
                status += "BMS Board Connected.";
            else
                status += "BMS Board NOT Connected.";

            Dispatcher.BeginInvoke((Action<string>)((x) => { RangeLbl.Content = x; }), status);

        }

        /// <summary>
        /// Set motor Speed between 0 and 1.0.
        /// </summary>
        /// <param name="speed"></param>
        void SetSpeed(double speed)
        {
            Task.Run( () =>
            {
                bool worked = false;

                if( speed < 0 )
                    speed = 0;

                lock( MotorLock )
                {
                    if( MotorCom.Open( MotorComPort ) )
                    {
                        MotorCom.SetSpeed( speed );
                        MotorCom.Close();
                        worked = true;
                    }
                    else
                    {
                        Console.WriteLine( "failed to SetSpeed" );
                    }
                }

                if( worked )
                {
                    Action<double> update_action = ( s ) => { BoatSpeedLbl.Content = speed.ToString( "0.00" ); };
                    Dispatcher.BeginInvoke( update_action, speed );
                }
            } );
        }

        void SetDirection(MotorControllerCom.Direction direction)
        {
            Task.Run( () =>
            {
                bool worked = false;

                lock( MotorLock )
                {
                    if( MotorCom.Open( MotorComPort ) )
                    {
                        MotorCom.SetDirection( direction );

                        MotorCom.Close();
                        worked = true;
                    }
                    else
                    {
                        Console.WriteLine( "failed to SetDirection" );
                    }
                }

                if( worked )
                {
                    string dir_string = "ERROR";
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

                    Action<MotorControllerCom.Direction> update_action = ( d ) => { UpdateDirection( d ); };
                    Dispatcher.BeginInvoke( update_action, direction );
                }
            } );
        }

        void SetKey(bool key_on)
        {
            Task.Run( () =>
            {
                bool worked = false;

                lock( MotorLock )
                {
                    if( MotorCom.Open( MotorComPort ) )
                    {
                        MotorCom.SetKey( key_on );

                        MotorCom.Close();
                        worked = true;
                    }
                    else
                    {
                        Console.WriteLine( "failed to SetKey" );
                    }
                }

                if( worked )
                {


                    Action<bool> update_action = ( k ) => { KeyLbl.Content = k ? "KEY ON" : "KEY OFF"; };
                    Dispatcher.BeginInvoke( update_action, key_on );
                }
            } );
        }

        void GetMotorHumidity()
        {

            Task.Run( () =>
            {

                bool worked = false;
                double humidity = 0;

                lock( MotorLock )
                {
                    if( MotorCom.Open( MotorComPort ) )
                    {
                        humidity = MotorCom.GetHumidity();

                        MotorCom.Close();

                        worked = true;
                    }
                    else
                    {
                        Console.WriteLine( "unable to open comport for GetMotorHumidity" );
                        return;
                    }
                }

                if( worked )
                {

                    //TODO: handle dual humidities for MotorHumidityLbl
                    //double MotorBoardHumidity = 0;
                    //double BmsBoardHumidity = 0;
                    Action<double> update_action = ( h ) => { MotorBoardHumidity = h; UpdateHumidityDisplay(); };
                    Dispatcher.BeginInvoke( update_action, humidity );
                }
            } );
        }

        void GetMotorTemp()
        {

            Task.Run( () =>
            {

                bool worked = false;
                double temperature = 0;

                lock( MotorLock )
                {
                    if( MotorCom.Open( MotorComPort ) )
                    {
                        temperature = MotorCom.GetTemp();

                        MotorCom.Close();

                        worked = true;
                    }
                    else
                    {
                        Console.WriteLine( "unable to open comport for GetMotorTemp" );
                        return;
                    }
                }

                if( worked )
                {
                    //TODO: handle dual humidities for MotorHumidityLbl
                    Action<double> update_action = ( t ) => { MotorBoardTempLbl.Content = "MOTOR BOARD TEMP: " + t.ToString( "0.00" ) + "C/F?"; };
                    Dispatcher.BeginInvoke( update_action, temperature );
                }
            } );
        }

        void MotorBeep()
        {
            //todo: add this.


            lock (MotorLock)
            {
                if (MotorCom.Open(MotorComPort))
                {
                    MotorCom.Beep();

                    MotorCom.Close();


                }
                else
                {
                    Console.WriteLine("unable to open comport for MotorBeep");
                    return;
                }
            }
        }

        void PingMotor()
        {
            //TODO: add gui for motor ping.
            Task.Run( () =>
            {

                bool worked = false;
                bool pinged = false;

                lock( MotorLock )
                {
                    if( MotorCom.Open( MotorComPort ) )
                    {
                        pinged = MotorCom.Ping();

                        MotorCom.Close();

                        worked = true;
                    }
                    else
                    {
                        Console.WriteLine( "unable to open comport for GetMotorTemp" );
                        return;
                    }
                }

                if( worked )
                {
                    //if (pinged)
                    //Console.WriteLine("Motor ping success");
                }
            } );
        }

    }
}
