using System;
using System.Drawing.Text;
using System.Drawing;
using System.Windows.Forms;

namespace Printing_Multiple_Pages
{
	public partial class Form1 : Form
	{
		private int fontcount;
		private int fontposition = 1;
		private float ypos = 1;
		private PrintPreviewDialog previewDlg = null;


		public Form1()
		{
			InitializeComponent();
		}

		private void displayFontsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			//Create InstalledFontCollection objects  
			InstalledFontCollection ifc =
			 new InstalledFontCollection();
			//Get font families  
			FontFamily[] ffs = ifc.Families;
			Font f;
			//Make sure rich text box is empty  
			richTextBox1.Clear();
			//Read font families one by one,  
			//set font to some text,  
			//and add text to the text box  
			foreach (FontFamily ff in ffs)
			{
				if (ff.IsStyleAvailable(FontStyle.Regular))
					f = new Font(ff.GetName(1), 12, FontStyle.Regular);
				else if (ff.IsStyleAvailable(FontStyle.Bold))
					f = new Font(ff.GetName(1), 12, FontStyle.Bold);
				else if (ff.IsStyleAvailable(FontStyle.Italic))
					f = new Font(ff.GetName(1), 12, FontStyle.Italic);
				else
					f = new Font(ff.GetName(1), 12, FontStyle.Underline);
				richTextBox1.SelectionFont = f;
				richTextBox1.AppendText(ff.GetName(1) + "\r\n");
				richTextBox1.SelectionFont = f;
				richTextBox1.AppendText("abcdefghijklmnopqrstuvwxyz\r\n");
				richTextBox1.SelectionFont = f;
				richTextBox1.AppendText("ABCDEFGHIJKLMNOPQRSTUVWXYZ\r\n");
				richTextBox1.AppendText("==========================\r\n");
			}
		}
	}
}
