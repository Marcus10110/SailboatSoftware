using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SailBot
{
    
    public class BatteryReadDataStruct
    {
        public const int CELL_COUNT = 15;
        public const int RELAY_COUNT = 5;

        public const int Size = 84+8;

        private UInt16[] mCellVoltages = new UInt16[CELL_COUNT];
        private UInt16[] mCellTemperatures = new UInt16[CELL_COUNT];

        private UInt16[] mPrechargeVoltages = new UInt16[RELAY_COUNT];
        private UInt16 MasterCurrent;
        private UInt16 ChargeCurrent;
        private UInt16 PackVoltage;
        private UInt16 MasterTemperature;
        private UInt16 MasterHumidity;
        private UInt16 ValidCellReadCount;
        private UInt16 CriticalFlags;
        private Int64 CoulombCount;

        public UInt16 GetRawChargeAdcValue()
        {
            return ChargeCurrent;
        }

        public double[] CellVoltages 
        { 
            get 
            { 
                double[] cell_voltages = new double[CELL_COUNT];
                for( int i = 0; i < CELL_COUNT; ++i )
                    cell_voltages[ i ] = GetCellVoltage( i );
                return cell_voltages;
            } 
        }

        public double[] CellTemperatures
        {
            get
            {
                double[] cell_temperatures = new double[ CELL_COUNT ];
                for( int i = 0; i < CELL_COUNT; ++i )
                    cell_temperatures[ i ] = GetCellTemperature( i );
                return cell_temperatures;
            }
        }

        public double[] PrechargeVoltages
        {
            get
            {
                double[] voltages = new double[ RELAY_COUNT ];
                for( int i = 0; i < RELAY_COUNT; ++i )
                    voltages[ i ] = GetRelayPrechargeVoltage( i );
                return voltages;
            }
        }

        public double GetCellVoltage( int cell_id )
        {
            double adc_value = mCellVoltages[cell_id] / 1023.0 * BatteryCom.CellReferenceVoltage;

            //batt is 232k over 100k.
            return adc_value * (60.4 + 100) / 100;
        }

        public static void TestTemp()
        {
            List<int> adc_values = new List<int>();
            for (int i = 0; i < 1024; ++i)
                adc_values.Add(i);


            Dictionary<int, double> temperatures = new Dictionary<int, double>();

            foreach( int adc in adc_values )
            {
                temperatures.Add(adc, 0.0);

                double node_voltage = (adc / 1023.0) * BatteryCom.CellReferenceVoltage;
                double voltage_across_ntc = BatteryCom.CellReferenceVoltage - node_voltage;
                double current_through_ntc = node_voltage / 100000.0; //bottom resistor is 100K fixed.
                double ntc_resistance = voltage_across_ntc / current_through_ntc;

                double r = ntc_resistance;
                double t_0 = 25.0 + 273.15; //in kelvin.
                double r_0 = 100000;
                double b = 4250;

                double t_inv = 1.0 / t_0 + 1.0 / b * Math.Log(r / r_0);
                double t = 1.0 / t_inv;


                t -= 273.15;
                t -= 0.39684763068221; //office temp offset. (1 data point)

                double f = t * 1.8 + 32;

                temperatures[adc] = f;
            }

            Console.WriteLine("done");


        }

        public double GetCellTemperature( int cell_id )
        {
            //conversion to C using 25-50 temp constant.
            double node_voltage = (mCellTemperatures[cell_id] / 1023.0) * BatteryCom.CellReferenceVoltage;
            double voltage_across_ntc = BatteryCom.CellReferenceVoltage - node_voltage;
            double current_through_ntc = node_voltage / 100000.0; //bottom resistor is 100K fixed.
            double ntc_resistance = voltage_across_ntc / current_through_ntc;

            double r = ntc_resistance;
            double t_0 = 25.0 + 273.15; //in kelvin.
            double r_0 = 100000;
            double b = 4250;

            double t_inv = 1.0 / t_0 + 1.0 / b * Math.Log(r / r_0);
            double t = 1.0 / t_inv;


            t -= 273.15;
            t -= 0.39684763068221; //office temp offset. (1 data point)

            double f = t * 1.8 + 32;

            return f;
        }

        public double GetRelayPrechargeVoltage(int index)
        {
            //1Meg over 21.5K
            //referenced to the BMS master reference voltage of 1.5V

            double pin_voltage = (double)mPrechargeVoltages[ index ] / 1023.0 * 1.5;

            return pin_voltage * ( 1000.0 + 21.5 ) / ( 21.5 );

        }

        public double GetPackVoltage()
        {
            //VBATT is 1M over 21.5k resistor divider

            //single point calibration, resistors are way off.
            double k = 50.07 / 730;


            //double node_voltage = adc_value / 1023.0 * 1.5;

            //double pack_voltage = node_voltage * (1000 + 21.5) / 21.5;

            double pack_voltage = PackVoltage * k;

            return pack_voltage;
        }

        public double GetMasterCurrent()
        {
            //master shunt: 200A / 50mV
            //charge shunt: 50A / 50mV
            //V = I * R
            //I = V / R
            //R = V / I
            decimal master_shunt_resistance = 0.05m / 200.0m;

            return ( double )GetCurrent( (( decimal )MasterCurrent ) - 32768m, master_shunt_resistance );
        }

        public double GetChargeCurrent()
        {
            //master shunt: 200A / 50mV
            //charge shunt: 50A / 50mV
            //V = I * R
            //I = V / R
            //R = V / I
            decimal charge_shunt_resistance = 0.05m / 50.0m;

            return ( double )GetCurrent( ( ( decimal )ChargeCurrent ) - 32768m, charge_shunt_resistance );
        }

        private double GetCurrent( decimal adc_value, decimal shunt_resistance )
        {
            decimal adc_voltage = ( (decimal)adc_value ) / 32768.0m;


            decimal v_ref = 1.25m;

            //scale by range for actual diff voltage at input.
            adc_voltage *= v_ref;

            //compensate op-amp gain.
            //old gain: 110.0m / 4.42m
            //new gain: 
            decimal op_amp_gain = 110.0m / 4.42m;

            adc_voltage /= op_amp_gain;

            double master_current = Convert.ToDouble( adc_voltage / shunt_resistance );

            return master_current;
            //32768 • (VIN+ – VIN–)/VREF + 32768
        }

        public double GetMasterTemperature()
        {
            //conversion to C using 25-50 temp constant.
            double node_voltage = (MasterTemperature / 1023.0) * 1.5;
            double voltage_across_ntc = 1.25 - node_voltage;
            double current_through_ntc = node_voltage / 100000.0; //bottom resistor is 100K fixed.
            double ntc_resistance = voltage_across_ntc / current_through_ntc;

            double r = ntc_resistance;
            double t_0 = 25.0 + 273.15; //in kelvin.
            double r_0 = 100000;
            double b = 4250;

            double t_inv = 1.0 / t_0 + 1.0 / b * Math.Log(r / r_0);
            double t = 1.0 / t_inv;


            t -= 273.15;
            t -= 2.35338992308643; //office temp offset. (1 data point)

            double f = t * 1.8 + 32;

            return f;
        }

        public Double GetMasterHumidity()
        {
            double supply_voltage = 3.0;
            double measured_voltage = (MasterHumidity / 1023.0) * 1.5;
            double m = 0.00656; //mx+b (ish)
            double b = 0.1515;
            //humidity goes into a 40.2K over 20K divider.
            //actual output voltage swings from 
            double v = measured_voltage * 60.2 / 20;
            //from datasheet.
            //VOUT=(VSUPPLY)(0.00636(sensor RH) + 0.1515), typical at 25 C
            //True RH = (Sensor RH)/(1.0546 0.00216T), T in C
            double rh = ((v / supply_voltage) - b) / m;

            return rh;

        }

        public int GetValidCellReadCount()
        {
            return ValidCellReadCount;
        }

        public Int64 GetRawCoulombCount()
        {
            return CoulombCount;
        }

        public double GetCoulombCount()
        {
            return GetAmpHours() * 3600;
        }

        public double GetAmpHours()
        {
            //amp hours. one amp hour = 3600 coulomb.
            //1 amp for 1 second = 1 coulomb.
            //sampled at 100 hz.
            //each sample is 0.01 seconds.

            decimal master_shunt_resistance = 0.05m / 200.0m;

            //convert ADC samples to amps.
            double amp_hours = GetCurrent( ( decimal )CoulombCount, master_shunt_resistance );

            //convert to amp seconds.
            amp_hours /= 100.0;

            //convert to amp hours.
            amp_hours /= ( 60 * 60 );

            return amp_hours;
        }

        public List<String> GetErrors()
        {
            List<String> errors = new List<string>();

            if ((CriticalFlags & 1) != 0)
                errors.Add("CONDITION_CRITICAL_CELL_VOLTAGE");
            if ((CriticalFlags & 2) != 0)
                errors.Add("CONDITION_CRITICAL_CELL_TEMP");
            if ((CriticalFlags & 4) != 0)
                errors.Add("CONDITION_CRITICAL_PACK_VOLTAGE");
            if ((CriticalFlags & 8) != 0)
                errors.Add("CONDITION_CRITICAL_TEMP");
            if ((CriticalFlags & 16) != 0)
                errors.Add("CONDITION_CRITICAL_MASTER_CURRENT");
            if ((CriticalFlags & 32) != 0)
                errors.Add("CONDITION_CRITICAL_CHARGE_CURRENT");
            if ((CriticalFlags & 64) != 0)
                errors.Add("CONDITION_CRITICAL_COMS_LOST");

            return errors;

        }

        public static BatteryReadDataStruct Deserialize( byte[] data)
        {

            List<byte> dat = new List<byte>(data);

            if( !(data.Last() != 0xDE) )
            {
                //throw execption?
                Console.WriteLine("Failed to deserialize Battery Read Data Struct.");
                return null;
            }

            BatteryReadDataStruct read_data = new BatteryReadDataStruct();
            
            for( int i = 0; i < CELL_COUNT; ++i)
            {
                read_data.mCellVoltages[i] = BitConverter.ToUInt16( dat.Take(2).ToArray(),0 );
                dat.RemoveRange(0,2);
            }

            for( int i = 0; i < CELL_COUNT; ++i)
            {
                read_data.mCellTemperatures[i] = BitConverter.ToUInt16( dat.Take(2).ToArray(),0 );
                dat.RemoveRange(0,2);
            }

            for( int i = 0; i < RELAY_COUNT; ++i)
            {
                read_data.mPrechargeVoltages[i] = BitConverter.ToUInt16( dat.Take(2).ToArray(),0 );
                dat.RemoveRange(0,2);
            }

            
            //SWAPPED BECUASE ADC 2 is not working, and we want charge!
            read_data.MasterCurrent = BitConverter.ToUInt16( dat.Take( 2 ).ToArray(), 0 );
            dat.RemoveRange(0,2);

            read_data.ChargeCurrent = BitConverter.ToUInt16(dat.Take(2).ToArray(), 0);
            dat.RemoveRange(0, 2);

            read_data.PackVoltage = BitConverter.ToUInt16( dat.Take(2).ToArray(),0 );
            dat.RemoveRange(0,2);

            read_data.MasterTemperature = BitConverter.ToUInt16( dat.Take(2).ToArray(),0 );
            dat.RemoveRange(0,2);

            read_data.MasterHumidity = BitConverter.ToUInt16( dat.Take(2).ToArray(),0 );
            dat.RemoveRange(0,2);

            read_data.ValidCellReadCount = BitConverter.ToUInt16( dat.Take(2).ToArray(),0 );
            dat.RemoveRange(0,2);

            read_data.CriticalFlags = BitConverter.ToUInt16( dat.Take( 2 ).ToArray(), 0 ); //enums are 16 bit on MSP430.
            dat.RemoveRange(0, 2);

            read_data.CoulombCount = BitConverter.ToInt64( dat.Take( 8 ).ToArray(), 0 );
            dat.RemoveRange( 0, 8 );

            if (dat.Count() > 0)
                throw new Exception("padding detected.");

            if (read_data.mCellVoltages.Any(x => x == 0xFFFF) || read_data.mCellTemperatures.Any( x => x == 0xFFFF))
            {
                //issue detected.
                Console.WriteLine("cell read error detected");
            }

            return read_data;
        }

        public BmsDisplayValues ConvertToDisplayData()
        {
            BmsDisplayValues display = new BmsDisplayValues()
            {
                CellTemp = new double[CELL_COUNT],
                CellVoltage = new double[CELL_COUNT],
                ChargeCurrent = GetChargeCurrent(),
                Humidity = GetMasterHumidity(),
                PackVoltage = GetPackVoltage(),
                PrimaryCurrent = GetMasterCurrent(),
                Temperture = GetMasterTemperature(),
                PackAmpHours = GetAmpHours()
            };

            for( int i = 0; i < CELL_COUNT; ++i)
            {
                display.CellTemp[i] = GetCellTemperature(i);
                display.CellVoltage[i] = GetCellVoltage(i);
            }

            return display;
        }

    }

    public class BatteryWriteDataStruct
    {
        private object lock_object = new object();

        private const int CELL_COUNT = 15;
        private const int RELAY_COUNT = 5;

        private UInt16[] mCellCurrents = new UInt16[CELL_COUNT];
	    private bool[]	mRelayStates = new bool[RELAY_COUNT];
        private bool[] mRelayPrechargeStates = new bool[RELAY_COUNT];
        private bool mDisableBuzzer = false;

        public int CellCount { get { return CELL_COUNT; } }

        public Double GetCellCurrent(int index)
        {
            lock(lock_object)
            {
                return (double)mCellCurrents[index] / 1023.0 * BatteryCom.CellRegulatorVoltage;
            }
        }

        public void StopCellDischarges()
        {
            lock(lock_object)
            {
                for( int i = 0; i < CELL_COUNT; ++i)
                {
                    mCellCurrents[ i ] = 0;
                }
            }
        }

        public List<Double> GetCellCurrents()
        {
            List<Double> currents = new List<double>();
            lock(lock_object)
            {
                for( int i = 0; i < CELL_COUNT; ++i)
                {
                    currents.Add( GetCellCurrent( i ) );
                }
            }
            return currents;
            
        }

        public void SetCellCurrent(int index, double current)
        {
            if( ( current < 0 ) || ( current > 1.0) )
            {
                throw new Exception("Current out of range.");
            }
            lock (lock_object)
            {
                mCellCurrents[ index ] = ( UInt16 )( current / BatteryCom.CellRegulatorVoltage * 1023.0 );
            }
        }

        public bool GetRelayState( int index )
        {
            lock (lock_object)
            {
                return mRelayStates[ index ];
            }
        }

        public void SetRelayState( int index, bool contacted )
        {
            lock (lock_object)
            {
                mRelayStates[ index ] = contacted;
            }
        }

        public bool GetRelayPrechargeState(int index)
        {
            lock (lock_object)
            {
                return mRelayPrechargeStates[ index ];
            }
        }

        public void SetRelayPrechargeState(int index, bool contacted)
        {
            lock (lock_object)
            {
                mRelayPrechargeStates[ index ] = contacted;
            }
        }

        public bool DisableBuzzer
        {
            get
            {
                lock(lock_object)
                {
                    return mDisableBuzzer;
                }
            }
            set
            {
                lock(lock_object)
                {
                    mDisableBuzzer = value;
                }
            }
        }

        public BatteryWriteDataStruct()
        {
            for( int i = 0; i < CELL_COUNT; ++i )
            {
                mCellCurrents[i] = 0;
            }

            for( int i = 0; i < RELAY_COUNT; ++i)
            {
                mRelayStates[i] = false;
                mRelayPrechargeStates[i] = false;
            }
        }


        public byte[] Serialize()
        {


            List<byte> data = new List<byte>();

            lock (lock_object)
            {

                for (int i = 0; i < CELL_COUNT; ++i)
                {
                    data.AddRange(BitConverter.GetBytes(mCellCurrents[i]));
                }

                for (int i = 0; i < RELAY_COUNT; ++i)
                {
                    data.AddRange(BitConverter.GetBytes(mRelayStates[i]));
                }

                for (int i = 0; i < RELAY_COUNT; ++i)
                {
                    data.AddRange(BitConverter.GetBytes(mRelayPrechargeStates[i]));
                }

                data.AddRange(BitConverter.GetBytes(mDisableBuzzer));
                data.Add( 0x00 ); //padding
                //fake checksum, used for now.
                data.Add(0xDE);
            }

            return data.ToArray();
        }
    }



    class BatteryCom : BoatSerialDevice
    {
        private const int NormalCellTimeout = 6000;

        public const double CellReferenceVoltage = 2.5;
        public const double CellRegulatorVoltage = 2.8;

        public BatteryCom()
            : base(9615)
        {
        }

        public BatteryReadDataStruct BmsUpdate( BatteryWriteDataStruct write_struct )
        {
            var write_data = write_struct.Serialize();

            byte[] rx_buffer = RoundTripCommand(write_data, BatteryReadDataStruct.Size, NormalCellTimeout);

            BatteryReadDataStruct read_data = BatteryReadDataStruct.Deserialize(rx_buffer);

            return read_data;
        }
    }

    public class BmsDisplayValues
    {
        public double Temperture { get; set; }
        public double Humidity { get; set; }
        public double[] CellTemp { get; set; }
        public double[] CellVoltage { get; set; }
        public double PrimaryCurrent { get; set; }
        public double ChargeCurrent { get; set; }
        public double PackVoltage { get; set; }
        public double PackAmpHours { get; set; }

        public string GetJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
