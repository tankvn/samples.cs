using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace externalapp
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

		private void btnLoad_Click(object sender, EventArgs e)
		{
			OpenFileDialog od = new OpenFileDialog();
			if (od.ShowDialog() == DialogResult.OK)
			{
				// Process proc = Process.Start(@"give your program address here");
				Process proc = Process.Start(od.FileName);
				proc.WaitForInputIdle();

				while (proc.MainWindowHandle == IntPtr.Zero)
				{
					Thread.Sleep(100);
					proc.Refresh();
				}

				SetParent(proc.MainWindowHandle, this.panel1.Handle);
			}
		}
	}
}
