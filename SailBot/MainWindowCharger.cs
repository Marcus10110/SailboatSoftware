using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SailBot
{


    public partial class MainWindow
    {
        public bool ChargeActive = false;

        public bool DischrageInProgress = false;
        public DateTime DichargeEndTime;

        public int[] CellDischargeCount = new int[ 15 ];




        void ChargeUpdate()
        {
            TimeSpan discharge_time = TimeSpan.FromMinutes( 5 );
            double cell_bleed_voltage = 3.61;
            double cell_finished_voltage = 3.45;
            int cell_count = BatteryWriteData.CellCount;
            //double cell_bleed_current = 1.0;

            BatteryReadDataStruct read_data = BatteryReadData;

            if( ChargeActive == false )
            {
                AbortCharge();
                return;
            }


            if( DischrageInProgress )
            {
                if( DateTime.Now >= DichargeEndTime )
                {
                    //discharge done, reconnect the charger!
                    WriteLineToCellLog("Discharge timer complete, enableing charge relay again");
                    Console.WriteLine( "Bleed cycle complete" );
                    DischrageInProgress = false;
                    BatteryWriteData.StopCellDischarges();
                    SetChargeRelay( true );
                }
            }
            else
            {
                bool all_unbled_cells_done = true;
                List<int> cells_to_bleed = new List<int>();
                List<double> cell_bleed_currents = new List<double>();
                for( int i = 0; i < cell_count; ++i )
                {
                    double cell_bleed_current = 0;
                    double cell_voltage = read_data.CellVoltages[i];
                    if (cell_voltage > 3.50)
                    {
                        if (cell_voltage < 3.6)
                        {
                            cell_bleed_current = (cell_voltage - 3.50) * 5.0;
                        }
                        else
                        {
                            cell_bleed_current = .5;
                        }
                    }
                    cell_bleed_currents.Add(cell_bleed_current);


                    if( CellDischargeCount[ i ] == 0 && read_data.GetCellVoltage( i ) < cell_finished_voltage )
                        all_unbled_cells_done = false;

                    if( read_data.GetCellVoltage( i ) > cell_bleed_voltage )
                        cells_to_bleed.Add( i );
                }

                if( all_unbled_cells_done == true )
                {
                    //DONE!
                   
                    int non_bled_cell_count = CellDischargeCount.Count( x => x == 0);
                    Console.WriteLine( "Charge Complete. " + non_bled_cell_count + " cells never bled" );
                    //todo: set charge button
                    string unbled_cells = "";
                    for( int i = 0; i < CellDischargeCount.Count(); ++i )
                    {
                        if( CellDischargeCount[i] == 0)
                            unbled_cells += i.ToString() + ", ";
                        CellDischargeCount[ i ] = 0;

                    }
                    WriteLineToCellLog("All unbled cells complete! Ending Charge. Unbled Cells: " + unbled_cells);
                    ChargeActive = false;
                    DischrageInProgress = false;
                    SetChargeRelay( false );
                    ChargeBtn.Content = "Start Charge";
                    return;
                }

                if( (all_unbled_cells_done == false) && (cells_to_bleed.Any() ) )
                {
                    //start bleeding
                    WriteLineToCellLog("bleed started: " + String.Join(",", cells_to_bleed));
                    Console.WriteLine( "starting to bleed cells " + String.Join( ",", cells_to_bleed ) );
                    DischrageInProgress = true;
                    SetChargeRelay( false );
                    DichargeEndTime = DateTime.Now + discharge_time;

                    foreach( int i in cells_to_bleed )
                    {
                        CellDischargeCount[ i ]++;
                        //BatteryWriteData.SetCellCurrent( i, cell_bleed_current );
                    }
                    string log = "starting discharge: ";
                    for (int i = 0; i < cell_count; ++i)
                    {
                        BatteryWriteData.SetCellCurrent(i, cell_bleed_currents[i]);
                        log += i + "(" + cell_bleed_currents[i].ToString("F") + "A), ";
                    }

                    Console.WriteLine(log);

                    return;
                }

                //stay in bulk charge, no discharging.


            }

        }

        public void AbortCharge()
        {
            BatteryWriteData.StopCellDischarges();
            SetChargeRelay( false );
            ChargeActive = false;
            DischrageInProgress = false;
            ChargeBtn.Content = "Start Charge";
            
        }

        public void StartCharge()
        {
            WriteLineToChargeLog("Starting Charge. StartCharge() called");
            for( int i = 0; i < CellDischargeCount.Count(); ++i )
            {
                CellDischargeCount[ i ] = 0;
            }
            DischrageInProgress = false;
            ChargeActive = true;
            SetChargeRelay( true );
            ChargeBtn.Content = "ABORT CHARGE";
        }

        void SetChargeRelay(bool charge)
        {
            int charge_relay = 1;
            BatteryWriteData.SetRelayPrechargeState( charge_relay, false );
            BatteryWriteData.SetRelayState( charge_relay, charge );
            if (charge)
                WriteLineToChargeLog("enableing charge relay");
            else
                WriteLineToCellLog("disableing charge relay");
        }

    }
}
