using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace SailBot
{
    class MotorControllerCom :BoatSerialDevice
    {

        public enum Direction { Forward, Reverse, NoDirection };

        private const byte CMD_SET_SPEED = 0x01;
        private const byte CMD_SET_DIR = 0x02;
        private const byte CMD_GET_TEMP = 0x03;
        private const byte CMD_GET_HUMIDITY = 0x04;
        private const byte CMD_SET_KEY = 0x05;
        private const byte CMD_BEEP = 0x06;
        private const byte CMD_PING = 0x07;
        private const byte CMD_SET_LED = 0x08;
        

        /// <summary>
        /// Speed is between 0 and 1.0
        /// </summary>
        /// <param name="speed"></param>
        public void SetSpeed(double speed)
        {
            if (speed > 1.0)
                speed = 1.0;
            if (speed < 0.0)
                speed = 0.0;
            //dac is 12 bit.
            uint int_speed = (uint)(speed * 4096);

            byte[] buffer = new byte[3];
            buffer[0] = CMD_SET_SPEED;
            buffer[1] = (byte)((int_speed >> 8) & 0xFF); //MS Byte first.
            buffer[2] = (byte)(int_speed & 0xFF);

            SendCommand(buffer);

        }

        public void SetDirection(Direction dir)
        {
            byte[] buffer = new byte[2];
            buffer[0] = CMD_SET_DIR;
            switch (dir)
            {
                case Direction.Forward:
                    buffer[1] = 0x01;
                    break;
                case Direction.Reverse:
                    buffer[1] = 0x00;
                    break;
                case Direction.NoDirection:
                    buffer[1] = 0x02;
                    break;
            }
            
            SendCommand(buffer);
        }

        public Double GetTemp()
        {
            //the MotorController uses an ADC connected to the NTC. The NTC is the top part of a R divider, the bottom is 100K.
            //The ADC in the MSP430 uses an internal 1.5V reference. Design error prevents using the external reference.


            byte[] buffer = new byte[1];
            buffer[0] = CMD_GET_TEMP;
            byte[] rx_buffer = RoundTripCommand(buffer, 2);
            if (rx_buffer.Length != 2)
                throw new Exception("could not get Temp, wrong length");

            //temp is 10 bit.
            //LS Byte first
            int raw_adc_value = GetWord(rx_buffer);


            //conversion to C using 25-50 temp constant.
            double node_voltage = (raw_adc_value / 1023.0) * 1.5;
            double voltage_across_ntc = 1.237 - node_voltage;
            double current_through_ntc = node_voltage / 100000.0; //bottom resistor is 100K fixed.
            double ntc_resistance = voltage_across_ntc / current_through_ntc;

            double r = ntc_resistance;
            double t_0 = 25.0 + 273.15; //in kelvin.
            double r_0 = 100000;
            double b = 4250;

            double t_inv = 1.0 / t_0 + 1.0 / b * Math.Log(r / r_0);
            double t = 1.0 / t_inv;

            
            t -= 273.15;

            t -= 2.0; //office temp offset. (1 data point)

            double f = t * 1.8 + 32;

            return f;
        }

        public Double GetHumidity()
        {
            byte[] buffer = new byte[1];
            buffer[0] = CMD_GET_HUMIDITY;
            byte[] rx_buffer = RoundTripCommand(buffer, 2);
            if (rx_buffer.Length != 2)
                throw new Exception("could not get Humidity, wrong length");

            //temp is 10 bit.
            //LS Byte first
            int int_temp = GetWord(rx_buffer);

            double supply_voltage = 3.0;
            double measured_voltage = int_temp / 1023.0 * 1.5;
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

        public void SetKey( bool on )
        {
            byte[] buffer = new byte[2];
            buffer[0] = CMD_SET_KEY;
            buffer[1] = (byte)((on == true) ? 0x55 : 0x66);
            SendCommand(buffer);
        }

        public void Beep()
        {
            byte[] buffer = new byte[1];
            buffer[0] = CMD_BEEP;
            SendCommand(buffer);
        }

        public void SetLeds(bool red, bool yellow, bool green)
        {
            byte tmp = 0x00;
            if (red)
                tmp |= 0x01;
            if (yellow)
                tmp |= 0x02;
            if (green)
                tmp |= 0x04;

            byte[] buffer = new byte[2];
            buffer[0] = CMD_SET_LED;
            buffer[1] = tmp;
            SendCommand(buffer);
        }

        public bool Ping()
        {
            byte[] buffer = new byte[1];
            buffer[0] = CMD_PING;
            byte[] response = RoundTripCommand(buffer, 1);
            if( (response.Length == 1 ) && ( response[0] == 0xFF) )
                return true;
            else
                return false;
        }

        /*void SendCommand(Byte[] data)
        {
            Port.Write(data, 0, data.Length);

        }*/

    }


    public class BoatSerialDevice
    {
        protected SerialPort Port = new SerialPort();

        private int BaudRate = 6900;

        public BoatSerialDevice() : this(9600)
        {
        }

        public BoatSerialDevice(int baud_rate)
        {
            BaudRate = baud_rate;
        }



        public bool IsOpen()
        {
            return Port.IsOpen;
        }

        public bool Open(string serial_port)
        {

            if (!SerialPort.GetPortNames().ToList().Contains(serial_port))
                throw new Exception("Invalid serial port specified.");

            if (Port.IsOpen)
                return true; //it's already open.

            Port.PortName = serial_port;
            Port.BaudRate = 9600;
            Port.ReadTimeout = 100;
            

            
            try
            {
                Port.Open();
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        public void Close()
        {
            if (!Port.IsOpen)
                throw new Exception("port already closed");

            Port.Close();
        }

        protected Byte[] RoundTripCommand(byte[] data, int rx_size,int timeout_ms = 100)
        {
            int max_length = rx_size;
            //int original_timeout = Port.ReadTimeout;
            //Port.ReadTimeout = timeout_ms;
            Port.Write(data, 0, data.Length);
            byte[] rx_buffer = new byte[max_length];

            //read for 100ms.
            int bytes_read = 0;
            DateTime start_time = DateTime.Now;

            while (DateTime.Now.Subtract(start_time).TotalMilliseconds < timeout_ms)
            {
                try
                {
                    int bytes_remaining = max_length - bytes_read;
                    int new_bytes_read = Port.Read(rx_buffer, bytes_read, bytes_remaining);
                    bytes_read += new_bytes_read;
                }
                catch (TimeoutException ex)
                {
                    //if (bytes_read == 0)
                        //throw;
                }

                if (bytes_read >= rx_size)
                    break;
            }

            if (bytes_read == 0)
            {
                throw new TimeoutException("failed to read any data in " + timeout_ms + "ms");
            }

            //Port.ReadTimeout = original_timeout;

            

            return rx_buffer.Take(bytes_read).ToArray();
        }

        protected void SendCommand(params byte[] list)
        {
            Port.Write(list, 0, list.Length);
        }

        protected UInt16 GetWord(byte[] dat)
        {
            UInt16 val = (UInt16)((dat[0] << 8) | dat[1]);
            return val;
        }

        public static bool CheckComportConnection(string com_port)
        {
            var all_ports = SerialPort.GetPortNames().ToList();

            if (!all_ports.Contains(com_port))
                return false;

            SerialPort port = new SerialPort();

            port.PortName = com_port;
            port.BaudRate = 9600;
            port.ReadTimeout = 100;

            try
            {
                port.Open();
                port.Close();
            }
            catch (Exception ex)
            {
                return false;
                Console.WriteLine("Found port, but could not open it!");
            }

            return true;
        }

    }
}
