using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SailBot
{
	/// <summary>
	/// Interaction logic for BatteryViewControl.xaml
	/// </summary>
	public partial class BatteryViewControl : UserControl
	{
        private int mCellId = 0;
        private double mVoltage = 0;
        private double mTemp = 0;
        private bool mIsBypassed = false;
        private bool mIsOverheated = false;
        private bool mIsCritical = false;

        public int CellId
        {
            get { return mCellId; }
            set { mCellId = value; IdLbl.Content = "CELL " + value.ToString(); }
        }

        public double Voltage
        {
            get { return mVoltage; }
            set { mVoltage = value; VoltageLbl.Content = value.ToString("0.00") + "V"; }
        }

        public double Temp
        {
            get { return mTemp; }
            set { mTemp = value; TempLbl.Content = value.ToString("0") + "F"; }
        }

        public bool IsBypassed
        {
            get { return mIsBypassed; }
            set { mIsBypassed = value; BypassLbl.Visibility = (value ? Visibility.Visible : Visibility.Hidden); }
        }

        public bool IsOverheated
        {
            get { return mIsOverheated; }
            set { mIsOverheated = value; OverheatLbl.Visibility = (value ? Visibility.Visible : Visibility.Hidden); }
        }

        public bool IsCritical
        {
            get { return mIsCritical; }
            set { mIsCritical = value; CriticalLbl.Visibility = (value ? Visibility.Visible : Visibility.Hidden); }
        }


		public BatteryViewControl()
		{
			this.InitializeComponent();
            IsBypassed = false;
            IsOverheated = false;
            IsCritical = false;
            Temp = 0;
		}


	}
}