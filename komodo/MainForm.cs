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
    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
        }

        public UsbConnection usb = null;
        public int Handle = 0;

        bool match(byte[] mem, int h, string str) {
            if (h + str.Length > mem.Length)
                return false;

            for (int i = 0; i < str.Length; i++) {
                if ((char)mem[h + i] != str[i])
                    return false;
            }
            return true;
        }
        bool match(byte[] mem, int h, byte[] str) {
            if (h + str.Length > mem.Length)
                return false;

            for (int i = 0; i < str.Length; i++) {
                if (mem[h + i] != str[i])
                    return false;
            }
            return true;
        }

        private void toolStripButton1_Click(object sender, EventArgs e) {
            Form_ProcessList frm = new Form_ProcessList();
            frm.TopMost = true;

            Console.WriteLine("Looking for processes...");
            long[] pids = usb.CmdListProcesses();
            Console.WriteLine("Found!");
            for (int i = 0; i < pids.Length - 1; i++) {
                frm.listBox_Processes.Items.Add("PID: " + pids[i].ToString("X8"));
            }

            if (frm.ShowDialog(this) == System.Windows.Forms.DialogResult.OK) {
                Handle = usb.CmdAttachProcess(pids[frm.listBox_Processes.SelectedIndex]);
                String name = "<no name>";
                for (int i = 0; i < 1000; i++) {
                    KDebugEvent Event = usb.CmdGetDbgEvent(Handle);
                    if (Event.EventType == 0) {
                        name = "";

                        for (int f = 0; f < Event.ProcessName.Length - 1; f++)
                            name += Event.ProcessName[f];

                        break;
                    }
                }
                Console.WriteLine("name: " + name + "!");
                this.Text = "komodo - (PID: " + pids[frm.listBox_Processes.SelectedIndex].ToString("X") + ")" + " - " + name + " [" + Handle + "]";

                dataGridViewPages.Rows.Clear();

                string[] MemTypes = new string[] {
                    "NONE", "IO", "NORMAL", "CODE-STATIC", "CODE", "HEAP", "SHARED-MEM", "WEIRD-SHARED-MEM", "MODULE-CODE-STATIC", "MODULE-CODE", "IPC-BUF-0", "MEM-MAP", "THREAD-LOCAL-STORAGE", "TRANSFER-MEMORY-ISOLATED", "TRANSFER-MEMORY", "PROCESS-MEMORY", "RESERVED", "IPC-BUF-1", "IPC-BUF-3", "KERN-STACK"
                };

                int times = 0;
                long pointer = 0;
                do {
                    KMemPage memInfo = usb.CmdQueryMemory(Handle, pointer);
                    if ((memInfo.Permissions & 0x3) == 3 || memInfo.Type == 5 || memInfo.Type == 4)
                    {
                        /*if (nonzero) {
                            memAreas[memAreasCount].start = memInfo.addr;
                            memAreas[memAreasCount].size = memInfo.size;
                            memAreasCount++;
                        }*/
                        dataGridViewPages.Rows.Add(new String[] { "0x" + memInfo.Address.ToString("X"), "0x" + memInfo.Size.ToString("X"), 
                            ((memInfo.Permissions & 0x1) == 0x1 ? "R" : "") +
                            ((memInfo.Permissions & 0x2) == 0x2 ? "W" : "") +
                            ((memInfo.Permissions & 0x4) == 0x4 ? "X" : "") +
                            ((memInfo.Permissions & 0x7) == 0x0 ? "<none>" : ""), MemTypes[memInfo.Type] });
                    }

                    toolStripStatusLabel1.Text = "Loading pages... (" + times + " / " + 128 + ")";
                    toolStripProgressBar1.Value = (int)(((double)times / 128) * 100);
                    this.Refresh();

                    pointer = memInfo.Address + memInfo.Size;
                    times++;
                    if (memInfo.Size == 0)
                        break;
                } while (times < 128);

                toolStripStatusLabel1.Text = "Done.";
                toolStripProgressBar1.Value = 100;
                toolStrip1.Refresh();
            }
        }

        private void MainForm_Load(object sender, EventArgs e) {
            usb = new UsbConnection();
            this.Enabled = true;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e) {
            usb.CmdClose();
        }

        private void buttonFirstScan_Click(object sender, EventArgs e) {
            ulong readAt = 0;
            ulong st = (readAt & 0xFF0) + 0x10;
            readAt = (readAt & 0xFFFFFFFFFFFFF000);
            bool found = false;
            byte[] byteField = new byte[0x1000];

            if (dataGridViewPages.SelectedRows.Count == 0) {
                MessageBox.Show("No pages selected.");
                return;
            }

            string startAddrStr = dataGridViewPages.SelectedRows[0].Cells[0].Value.ToString().Substring(2);
            string startSizeStr = dataGridViewPages.SelectedRows[0].Cells[1].Value.ToString().Substring(2);

            Console.WriteLine(startAddrStr);
            Console.WriteLine(startSizeStr);

            ulong startAddr = Convert.ToUInt64(startAddrStr, 16);
            ulong startSize = Convert.ToUInt64(startSizeStr, 16);

            /*Console.WriteLine("High");
            Console.WriteLine(dataGridViewPages.SelectedRows[0].Cells[0].Value.ToString().Substring(2));
            Console.WriteLine(dataGridViewPages.SelectedRows[0].Cells[1].Value.ToString().Substring(2));*/

            string fileName = "dump_0x" + startAddrStr + ".dat";

            //string searchQuery = "Weed";

            int searchnumber = 0x3F800000;
            byte[] searchQuery = BitConverter.GetBytes(searchnumber);

            dataGridView1.Rows.Clear();

            toolStripStatusLabel1.Text = "Searching through page...(0 / " + startSizeStr + ")";
            toolStripProgressBar1.Value = (int)((0.0 / startSize) * 100);
            this.Refresh();

            using(FileStream fileStream = new FileStream(fileName, FileMode.Create))
            while (readAt < startSize - 0x100) {
                byteField = usb.CmdReadMemory(Handle, (long)(startAddr + readAt), 0x1000);
                
                for (int i = 0; i < byteField.Length; i++)
                    fileStream.WriteByte(byteField[i]);

                if (byteField.Length != 0x1000) {
                    MessageBox.Show("Bytefield length is: " + byteField.Length, "Bytefield incorrect length!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                for (ulong h = st; h < 0x1000; h++) {
                    if (h < 0x1000) {
                        if (match(byteField, (int)h, searchQuery)) {
                            //svcWriteDebugProcessMemory(DbgHandle, (char*)"Yeet", (u64)startP + readAt + h, 4);
                            //readAt += (ulong)(h & 0xFFF0);
                            //found = true;
                            if (searchQuery.GetType() == typeof(string)) {
                                //dataGridView1.Rows.Add(new String[] { "0x" + (readAt + h).ToString("X"), searchQuery, searchQuery });
                            }
                            else {
                                //dataGridView1.Rows.Add(new String[] { "0x" + (startAddr + readAt + h).ToString("X"), BitConverter.ToUInt32(searchQuery, 0).ToString("X"), BitConverter.ToUInt32(searchQuery, 0).ToString("X") });

                                usb.CmdWriteMemory(Handle, startAddr + readAt + h, 0x40000000);
                                dataGridView1.Rows.Add(new String[] { "0x" + (startAddr + readAt + h).ToString("X"), "40000000", BitConverter.ToUInt32(searchQuery, 0).ToString("X") });
                            }
                        }
                    }
                }

                toolStripStatusLabel1.Text = "Searching through page...(" + readAt.ToString("X") + " / " + startSizeStr + ")";
                toolStripProgressBar1.Value = (int)(((double)readAt / startSize) * 100);
                this.Refresh();

                if (found)
                    break;

                readAt = (readAt & 0xFFFFFFFFFFFFF000) + 0x1000;
                st = 0;
            }

            toolStripStatusLabel1.Text = "";
            toolStripProgressBar1.Value = 100;
            this.Refresh();

            /*if (ghjkl != 0)
                readAt = ghjkl;

            if (readAt > startSize - 0x100)
                readAt = startSize - 0x100;

            svcReadDebugProcessMemory(byteField, DbgHandle, (u64)startP + (readAt & (~0xFFF)), 0x1000);*/
        }

        private void dataGridView1_CellContextMenuStripNeeded(object sender, DataGridViewCellContextMenuStripNeededEventArgs e)
        {
            
        }

        public static DialogResult InputBox(string title, string promptText, ref string value)
        {
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;

            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 20, 372, 13);
            textBox.SetBounds(12, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }

        private void dataGridView1_DoubleClick(object sender, EventArgs e)
        {
            
        }

        private void dataGridView1_Click(object sender, EventArgs e)
        {
        }

        private void dataGridView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Right)
                return;


            string startAddrStr = dataGridView1.SelectedRows[0].Cells[0].Value.ToString().Substring(2);
            string startValueStr = dataGridView1.SelectedRows[0].Cells[1].Value.ToString().Substring(0);

            ulong startAddr = Convert.ToUInt64(startAddrStr, 16);
            uint startValue = Convert.ToUInt32(startValueStr, 16);

            string defaultValueStr = startValueStr;
            uint defaultValue = startValue;

            if (InputBox("Enter a value.", "Value?", ref defaultValueStr) == DialogResult.OK)
            {

                toolStripStatusLabel1.Text = "Replacing addresses...";
                toolStripProgressBar1.Value = 0;
                this.Refresh();

                defaultValue = Convert.ToUInt32(defaultValueStr, 16);

                for (int i = 0; i < dataGridView1.SelectedRows.Count; i++)
                {
                    string addrStr = dataGridView1.SelectedRows[i].Cells[0].Value.ToString().Substring(2);
                    ulong addr = Convert.ToUInt64(addrStr, 16);

                    usb.CmdWriteMemory(Handle, addr, defaultValue);

                    toolStripStatusLabel1.Text = "Replacing addresses... (" + i + " / " + dataGridView1.SelectedRows.Count + ")";
                    toolStripProgressBar1.Value = (int)(((double)i / dataGridView1.SelectedRows.Count) * 100);
                    this.Refresh();
                }
            }

            toolStripStatusLabel1.Text = "";
            toolStripProgressBar1.Value = 100;
            this.Refresh();
        }
    }
}
