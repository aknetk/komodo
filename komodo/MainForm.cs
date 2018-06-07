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
    public partial class MainForm : Form {
        public MainForm() {
            InitializeComponent();
        }

        public UsbConnection usb = null;
        public int Handle = 0;

        bool match(byte[] mem, int h, string str) {
            for (int i = 0; i < str.Length; i++) {
                if ((char)mem[h + i] != str[i])
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
                        name = Event.ProcessName;
                        break;
                    }
                }
                Console.WriteLine("name: " + name + "!");
                this.Text = "komodo - (PID: " + pids[frm.listBox_Processes.SelectedIndex].ToString("X") + ") [" + Handle + "]" + " - " + name;

                int times = 0;
                long pointer = 0;
                do {
                    KMemPage memInfo = usb.CmdQueryMemory(Handle, pointer);
                    if ((memInfo.Permissions & 0x3) == 3) {
                        /*if (nonzero) {
                            memAreas[memAreasCount].start = memInfo.addr;
                            memAreas[memAreasCount].size = memInfo.size;
                            memAreasCount++;
                        }*/
                        dataGridViewPages.Rows.Add(new String[] { "0x" + memInfo.Address.ToString("X"), "0x" + memInfo.Size.ToString("X"), 
                            ((memInfo.Permissions & 0x1) == 0x1 ? "R" : "") +
                            ((memInfo.Permissions & 0x2) == 0x2 ? "W" : "") +
                            ((memInfo.Permissions & 0x4) == 0x4 ? "X" : "") +
                            ((memInfo.Permissions & 0x7) == 0x0 ? "<none>" : ""), memInfo.Type.ToString() });
                    }

                    pointer = memInfo.Address + memInfo.Size;
                    times++;
                    if (memInfo.Size == 0)
                        break;
                } while (times < 64);
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

            ulong startAddr = 0; //Convert.ToUInt64(dataGridViewPages.SelectedRows[0].Cells[0].Value.ToString().Substring(2));
            ulong startSize = 0; //Convert.ToUInt64(dataGridViewPages.SelectedRows[0].Cells[1].Value.ToString().Substring(2));

            /*Console.WriteLine("High");
            Console.WriteLine(dataGridViewPages.SelectedRows[0].Cells[0].Value.ToString().Substring(2));
            Console.WriteLine(dataGridViewPages.SelectedRows[0].Cells[1].Value.ToString().Substring(2));*/

            while (readAt < startSize - 0x100) {
                byteField = usb.CmdReadMemory(Handle, (long)(startAddr + readAt), 0x1000);

                for (ulong h = st; h < 0x1000; h++) {
                    if (h < 0x1000 - 10) {
                        if (match(byteField, (int)h, "Santa")) {
                            //svcWriteDebugProcessMemory(DbgHandle, (char*)"Yeet", (u64)startP + readAt + h, 4);
                            //readAt += (ulong)(h & 0xFFF0);
                            //found = true;
                            dataGridView1.Rows.Add(new String[] { "0x" + (readAt + h).ToString("X"), "Santa", "Santa" });
                            break;
                        }
                    }
                }

                if (found)
                    break;

                readAt = (readAt & 0xFFFFFFFFFFFFF000) + 0x1000;
                st = 0;
            }

            /*if (ghjkl != 0)
                readAt = ghjkl;

            if (readAt > startSize - 0x100)
                readAt = startSize - 0x100;

            svcReadDebugProcessMemory(byteField, DbgHandle, (u64)startP + (readAt & (~0xFFF)), 0x1000);*/
        }
    }
}
