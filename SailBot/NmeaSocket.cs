using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;

namespace SailBot
{
    class NmeaSocket
    {
        private const int Port = 2114;
        private TcpListener Listener = null;
        private string ComPort = null;

        private byte[] RxBuffer = new byte[ 1024 ];

        void Start()
        {

            Listener = new TcpListener( Port );


            Listener.Start();

            Listener.BeginAcceptTcpClient( new AsyncCallback( AcceptTcpClientEvent ), Listener );



        }

        void SetSerialPort( string port )
        {
            bool started = false;

            if( Listener != null )
                started = true;
            

            if( started )
                Stop();

            ComPort = port;

            if( started )
                Start();

        }


        void Stop()
        {
            if( Listener == null )
                return;

            Listener.Stop();

            Listener = null;

        }

        void AcceptTcpClientEvent( IAsyncResult ar )
        {
            TcpClient client;
            try
            {
                TcpListener listener = ( TcpListener )ar.AsyncState;

                client = listener.EndAcceptTcpClient( ar );

                Listener.BeginAcceptTcpClient( new AsyncCallback( AcceptTcpClientEvent ), Listener );
            }
            catch( Exception ex )
            {
                Console.WriteLine( ex.ToString() );
                return;
            }

            try
            {
                client.GetStream().BeginRead( RxBuffer, 0, RxBuffer.Length, new AsyncCallback( SocketReadEvent ), client );
            }
            catch( Exception ex )
            {
                Console.WriteLine( ex.ToString() );
            }

        }

        void SocketReadEvent( IAsyncResult ar )
        {
            //start another read event.

            try
            {
                TcpClient client = ar.AsyncState as TcpClient;

                int bytes_read = client.GetStream().EndRead( ar );

                byte[] new_data = RxBuffer.Take( bytes_read ).ToArray();

                SendSerialData( new_data ); 

                client.GetStream().BeginRead( RxBuffer, 0, RxBuffer.Length, new AsyncCallback( SocketReadEvent ), client );
            }
            catch( Exception ex)
            {
                Console.WriteLine( ex.ToString() );
            }

        }

        void SendSerialData( byte[] data )
        {
            if( !SerialPort.GetPortNames().Contains( ComPort ) )
                return;

            try
            {
                SerialPort port = new SerialPort( ComPort );

                port.Open();

                port.Write( data, 0, data.Length );

                port.Close();

            }
            catch( Exception ex)
            {
                Console.WriteLine( ex.ToString() );
            }

        }

        static void SetManualBearing( double degrees_true, string comport )
        {
            string utc_fix = DateTime.UtcNow.ToString("HHmmss");

            double variation = -14.3; //14.3 east.

            double degrees_magnetic = degrees_true + variation;

            if( degrees_magnetic < 0.0 )
                degrees_magnetic += 360;


            string nmea_string = "GPBWC,"; //Bearing and distance to waypoint - great circle

            nmea_string += utc_fix + ","; //UTC time of fix 22:54:44

            nmea_string += "4917.24,N,"; //Latitude of waypoint
            nmea_string += "12309.57,W,"; //Longitude of waypoint
            nmea_string += degrees_true.ToString("000.0") +  ",T,"; //Bearing to waypoint, degrees true
            nmea_string += degrees_magnetic.ToString("000.0") + ",M,"; //Bearing to waypoint, degrees magnetic
            nmea_string += "001.0,N,"; //Distance to waypoint, Nautical miles
            nmea_string += "001"; //Waypoint ID

            //checksum calc
            byte[] byte_version = System.Text.Encoding.ASCII.GetBytes( nmea_string );

            byte checksum = 0;
            for( int i = 0; i < byte_version.Length; ++i)
            {
                checksum ^= byte_version[ i ];
            }


            string checksum_string = BitConverter.ToString( new byte[ 1 ] { checksum } ); ;
            nmea_string = "$" + nmea_string + "*" + checksum_string;
            Console.WriteLine( "NMEA Bearing String: " + nmea_string );

            if( !SerialPort.GetPortNames().Contains( comport ) )
            {
                Console.WriteLine( "NMEA com port not connected" );
                return;
            }

            try
            {

                SerialPort port = new SerialPort( comport );

                port.Open();

                port.Write( nmea_string );

                port.Close();

            }
            catch( Exception ex)
            {
                Console.WriteLine( ex.ToString() );
            }
        }

    }
}
