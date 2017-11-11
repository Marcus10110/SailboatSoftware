using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SailBot
{
    class DebugWriter: TextWriter
    {
        ListBox OutputListBox = null;

        String CurrentString = "";
        String DebugFilePath;
        Dispatcher SrcThread;
        

        public DebugWriter(ListBox output, Dispatcher dispatcher, String debug_file_path)
        {
            OutputListBox = output;
            DebugFilePath = debug_file_path;
            SrcThread = dispatcher;
        }

        public static string GetDefaultLogLocation()
        {
            string file_path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            file_path = file_path + @"\SailBot Log\";

            if (!Directory.Exists(file_path))
            {
                Directory.CreateDirectory(file_path);
            }

            string file_name = "SailBotLog.txt";

            file_path += file_name;
            return file_path;
        }
 
        public override void Write(char value)
        {
            base.Write(value);

            CurrentString += value;

            if (CurrentString.EndsWith(Environment.NewLine))
            {
                Action<string> sync_update = (s) =>
                {
                    AddListText(s);
                    SaveString(s);
                };


                string closure_string = CurrentString.Trim();
                SrcThread.BeginInvoke(sync_update, closure_string);


                
                CurrentString = "";
            }

        }


        public void AddListText(string line)
        {
            AddListText(line, Brushes.Green);
        }

        public void AddListText(string line, Brush color)
        {
            TextBlock block = new TextBlock();
            block.Text = line;
            block.Foreground = color;
			block.FontSize = 14;
            OutputListBox.Items.Add(block);
        }

        public void SaveString( string line )
        {
            System.IO.StreamWriter wr = new StreamWriter(DebugFilePath, true);

            wr.WriteLine( DateTime.Now.ToString() + ":  " + line );

            wr.Close();
        }
 
        public override Encoding Encoding
        {
            get { return System.Text.Encoding.UTF8; }
        }
    }
}
