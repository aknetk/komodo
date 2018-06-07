using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace komodo {
    public partial class MemoryReader : Form {
        public MemoryReader() {
            InitializeComponent();
        }

        int     byteGroupSize = 1;
        int     byteRowSize = 16;

        long    fileSize = 0;
        byte[]  fileBytes;

        int     lineHeight = 3;

        long    startingAddress = 0x100000000;

        private void Form1_Load(object sender, EventArgs e) {
            String fileName = "C:/Users/Justin/Dropbox/sonic3/source/Resource/Sprites/Player/ManiaKnux.spr";

            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);
            fileSize = new FileInfo(fileName).Length;
            fileBytes = br.ReadBytes((int)fileSize);

            RefreshForm();
        }

        private void RefreshForm() {
            dataGridView.AllowDrop = false;
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.AllowUserToResizeRows = false;
            dataGridView.AllowUserToResizeColumns = false;
            dataGridView.AllowUserToOrderColumns = false;

            dataGridView.Font = new Font("Consolas", 11);
            dataGridView.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dataGridView.BorderStyle = BorderStyle.None;
            dataGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridView.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridView.EnableHeadersVisualStyles = false;
            dataGridView.MultiSelect = false;
            
            dataGridView.DefaultCellStyle.BackColor = Color.FromArgb(0x28, 0x2C, 0x34);
            dataGridView.DefaultCellStyle.ForeColor = Color.FromArgb(0xAB, 0xB2, 0xBF);

            dataGridView.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0x28, 0x2C, 0x34);
            dataGridView.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0x98, 0xC3, 0x79);

            dataGridView.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(0x28, 0x2C, 0x34);
            dataGridView.RowHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0x98, 0xC3, 0x79);

            dataGridView.RowHeadersDefaultCellStyle.Padding = new Padding(dataGridView.RowHeadersWidth);
            dataGridView.RowPostPaint += new DataGridViewRowPostPaintEventHandler(dataGridView_RowPostPaint);
            dataGridView.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;

            dataGridView.ColumnCount = byteRowSize / byteGroupSize;
            for (int i = 0; i < byteRowSize / byteGroupSize; i++) {
                dataGridView.Columns[i].HeaderText = String.Format("{0,2:X2}", i * byteGroupSize);
                dataGridView.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            for (long off = 0; off < fileSize / byteRowSize; off++) {
                String[] row = new String[byteRowSize / byteGroupSize];
                for (int i = 0; i < byteRowSize / byteGroupSize && off * byteRowSize + i < fileSize; i++) {
                    row[i] = String.Format("{0," + (byteGroupSize * 2) + ":X" + (byteGroupSize * 2) + "}", fileBytes[off * byteRowSize + i]);
                }
                dataGridView.Rows.Add(row);
                dataGridView.Rows[dataGridView.Rows.Count - 1].HeaderCell.Value = "" + String.Format("{0,8:X8}", off * byteRowSize + startingAddress) + "";
                dataGridView.Rows[dataGridView.Rows.Count - 1].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
            dataGridView.AutoResizeRowHeadersWidth(DataGridViewRowHeadersWidthSizeMode.AutoSizeToDisplayedHeaders);
            dataGridView.AutoResizeColumns();
            dataGridView.Columns.Add("s", "");
            dataGridView.Columns[dataGridView.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;


            /*lineHeight = TextRenderer.MeasureText("0", labelAddressLabels.Font).Height;
            
            //// Text setting

            // Address Labels
            temp = "";
            for (long off = 0; off < fileSize; off += byteRowSize) {
                temp += String.Format("{0,10:X8}", off + startingAddress) + "\n";
            }
            labelAddressLabels.Text = temp;

            // Bytes
            temp = "";
            for (long off = 0; off < fileSize; off++) {
                if (off % byteRowSize == 0 && off > 0)
                    temp += "\n";

                if (off % byteGroupSize == 0 && off > 0)
                    temp += " ";

                temp += String.Format("{0,2:X2}", fileBytes[off]);
            }
            textBoxByteArea.Text = temp;

            // Literals
            temp = "";
            for (long off = 0; off < fileSize; off++){
                if (off % byteRowSize == 0 && off > 0)
                    temp += "\n";

                String str = Encoding.UTF8.GetString(new byte[] { fileBytes[off] });

                if (fileBytes[off] >= 0x30 && fileBytes[off] <= 0x58)
                    temp += str;
                else
                    temp += ".";
            }
            textBoxLiterals.Text = temp;

            //// Resizes
            size = TextRenderer.MeasureText(labelAddressLabels.Text, labelAddressLabels.Font);
            labelAddressLabels.Width = size.Width;
            labelAddressLabels.Height = size.Height + lineHeight;

            size = TextRenderer.MeasureText(textBoxByteArea.Text, textBoxByteArea.Font);
            textBoxByteArea.Width = size.Width;
            textBoxByteArea.Height = size.Height + lineHeight;

            size = TextRenderer.MeasureText(textBoxLiterals.Text, textBoxLiterals.Font);
            textBoxLiterals.Width = size.Width;
            textBoxLiterals.Height = size.Height + lineHeight;
            
            // Positions
            textBoxByteArea.Left = labelAddressLabels.Width;
            textBoxLiterals.Left = textBoxByteArea.Left + textBoxByteArea.Width;*/

            //panel1.VerticalScroll.SmallChange = lineHeight;
            //panel1.VerticalScroll.LargeChange = lineHeight * 16;
        }

        void dataGridView_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e) {
            object o = dataGridView.Rows[e.RowIndex].HeaderCell.Value;

            e.Graphics.DrawString(
                o != null ? o.ToString() : "",
                dataGridView.Font,
                new SolidBrush(dataGridView.RowHeadersDefaultCellStyle.ForeColor),
                new PointF((float)e.RowBounds.Left + 2, (float)e.RowBounds.Top + 4));
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e) {

        }
    }
}
