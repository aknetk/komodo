using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace komodo {
    public partial class Form_ProcessList : Form {
        public Form_ProcessList() {
            InitializeComponent();
        }

        private void listBox_Processes_DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0)
                return;

            e.DrawBackground();
            e.DrawFocusRectangle();
            Rectangle newBounds = e.Bounds;
            newBounds.Y += 3;
            e.Graphics.DrawString(
                 (string)listBox_Processes.Items[e.Index],
                 e.Font,
                 new SolidBrush(e.ForeColor),
                 newBounds);
        }

        private void listBox_Processes_MeasureItem(object sender, MeasureItemEventArgs e) {
            e.ItemHeight = 20;
        }

    }
}
