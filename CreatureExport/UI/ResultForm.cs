using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace CompendiumImport.UI
{
    internal partial class ResultForm : Form
    {
        public ResultForm()
        {
            InitializeComponent();
        }

        public void Open(string result, List<string> errors)
        {
            OutputBox.Text = result;
            ErrorBox.Lines = errors.ToArray();
            this.ShowDialog();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string export = Path.Combine(Path.GetTempPath(), "export" + Guid.NewGuid() + "export.txt");
            using (StreamWriter sw = new StreamWriter(export))
            {
                sw.WriteLine(OutputBox.Text);
            }
            Process.Start(export);
        }
    }
}
