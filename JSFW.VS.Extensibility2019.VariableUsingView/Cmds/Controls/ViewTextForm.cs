using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using FastColoredTextBoxNS;

namespace JSFW.VS.Extensibility.Cmds.Controls
{
    public partial class ViewTextForm : Form
    {  
        public ViewTextForm()
        {
            InitializeComponent();  
        }
         
        private void fctb_DoubleClick(object sender, EventArgs e)
        {
            var hit = textBox1.GetFirstCharIndexOfCurrentLine();
            MessageBox.Show("" + hit);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            Results = null;
            CurrentSelection = null;
        }
         
        private void ViewTextForm_Load(object sender, EventArgs e)
        {

        }

        SortedDictionary<int, int> Results { get; set; }
          
        private void Print(SortedDictionary<int, int> results, string[] lines)
        {
            textBox1.ResetText();
            foreach (KeyValuePair<int, int> key in Results)//.OrderBy(o => o.Key))
            {
                textBox1.AppendText(lines[key.Key]);
                for (int loop = 0; loop < key.Value; loop++)
                {
                    textBox1.AppendText(Environment.NewLine);
                }
            }
        }

        TextSelection CurrentSelection;
        private void textBox1_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                int hit = textBox1.GetFirstCharIndexOfCurrentLine();
                hit = textBox1.GetLineFromCharIndex(hit);
                for (int loop = 0; loop < hit && loop < Results.Count; loop++)
                {
                    if (1 < Results.ElementAt(loop).Value)
                        hit--;
                }
                int line = Results.ElementAt(hit).Key;

                if (Results != null)
                {
                    if (CurrentSelection != null) CurrentSelection.GotoLine(line + 1, false);
                }
            }
            catch {

            }
        }

        internal void SetTextResult(SortedDictionary<int, int> results, string[] lines, TextSelection currentSelection)
        {
            Results = results; 
            Print(results, lines);
            CurrentSelection = currentSelection;
        }
    }
}
