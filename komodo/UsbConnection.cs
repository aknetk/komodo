using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.IO.Compression;
using System.Diagnostics;
using zlib;
using BitMiracle.LibJpeg.Classic;
using System.Reflection;

namespace komodo {

    public class UsbConnection {

        public Socket serv;
        public bool useUSB = false;
        public int Handle = 0;
        public SerialPort sp;

        public byte[] Combine(byte[] first, byte[] second) {
            /*byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);*/

            IEnumerable<byte> rv = first.Concat(second);

            return rv.ToArray();
        }
        public byte[] Combine(byte[] a, byte[] b, byte[] c) {
            /*byte[] ret = new byte[a.Length + b.Length + c.Length];
            int off = 0;
            Buffer.BlockCopy(a, 0, ret, off, a.Length);
            off += a.Length;
            Buffer.BlockCopy(b, 0, ret, off, b.Length);
            off += b.Length;
            Buffer.BlockCopy(c, 0, ret, off, c.Length);*/
            IEnumerable<byte> ret = a.Concat(b).Concat(c);
            return ret.ToArray();
        }
        public byte[] Combine(byte[] a, byte[] b, byte[] c, byte[] d) {
            /*byte[] ret = new byte[a.Length + b.Length + c.Length + d.Length];
            int off = 0;
            Buffer.BlockCopy(a, 0, ret, off, a.Length);
            off += a.Length;
            Buffer.BlockCopy(b, 0, ret, off, b.Length);
            off += b.Length;
            Buffer.BlockCopy(c, 0, ret, off, c.Length);
            off += c.Length;
            Buffer.BlockCopy(d, 0, ret, off, d.Length);*/
            IEnumerable<byte> ret = a.Concat(b).Concat(c).Concat(d);
            return ret.ToArray();
        }
        public byte[] Combine(byte[] a, byte[] b, byte[] c, byte[] d, byte[] e) {
            /*byte[] ret = new byte[a.Length + b.Length + c.Length + d.Length + e.Length];
            int off = 0;
            Buffer.BlockCopy(a, 0, ret, off, a.Length);
            off += a.Length;
            Buffer.BlockCopy(b, 0, ret, off, b.Length);
            off += b.Length;
            Buffer.BlockCopy(c, 0, ret, off, c.Length);
            off += c.Length;
            Buffer.BlockCopy(d, 0, ret, off, d.Length);
            off += d.Length;
            Buffer.BlockCopy(e, 0, ret, off, e.Length);*/
            IEnumerable<byte> ret = a.Concat(b).Concat(c).Concat(d).Concat(e);
            return ret.ToArray();
        }

        public UsbConnection() {
            if (useUSB) {
                sp = new SerialPort("Nintendo Switch", 115200);
                //sp.Write(writebytes, 0, writebytes.Length);

                if (sp == null) {
                    Console.WriteLine("Komodo over USB not found.");
                }
                else {
                    Console.WriteLine("Komodo over USB found!");
                    //sp.Close();
                }

                /*byte[] readbytes = new byte[sp.BytesToRead];
                sp.Read(readbytes, 0, readbytes.Length);*/
            }
            else {

                IPAddress ipAddr = Dns.GetHostEntry("192.168.1.9").AddressList[0]; //new IPAddress(new byte[] { 192, 168, 1, 13 });

                serv = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                serv.Connect(ipAddr, 0xDEAF);

                Console.WriteLine("Komodo over TCP/IP found!");
            }
        }

        public byte[] Read(int size) {
            byte[] buffer = new byte[size];
            try {
                int total = 0;
                int offset = 0;
                while (total < size) {
                    int count = 0;
                    if (useUSB) {
                        count = sp.Read(buffer, offset, size - total);
                    }
                    else {
                        count = serv.Receive(buffer, offset, size - total, SocketFlags.None);
                    }
                    if (count <= 0)
                        return null;
                    total += count;
                    offset += count;
                }
                //Console.WriteLine("KConn << Read " + total + " bytes.");
            }
            catch (Exception e) {
                //Application.Exit();
            }
            return buffer;
        }
        public void Write(byte[] buffer) {
            try {
                int size = buffer.Length;
                int total = 0;
                int offset = 0;
                while (total < size) {
                    int count = 0;
                    if (useUSB) {
                        sp.Write(buffer, offset, size - total);
                        count = size - total;
                    }
                    else {
                        count = serv.Send(buffer, offset, size - total, SocketFlags.None);
                    }
                    if (count <= 0)
                        return;
                    total += count;
                    offset += count;
                }
                //Console.WriteLine("KConn >> Sent " + total + " bytes.");
            }
            catch (Exception e) {
                //Application.Exit();
            }
        }

        public int  GetU8(byte[] buffer, int from) {
            return buffer[from + 0];
        }
        public int  GetU16(byte[] buffer, int from) {
            return buffer[from + 0] + buffer[from + 1] * 0x100;
        }
        public int  GetU32(byte[] buffer, int from) {
            return buffer[from + 0] + buffer[from + 1] * 0x100 + buffer[from + 2] * 0x10000 + buffer[from + 3] * 0x1000000;
        }
        public long GetU64(byte[] buffer, int from) {
            return buffer[from + 0] + 
                (buffer[from + 1] << 8) +
                (buffer[from + 2] << 16) +
                (buffer[from + 3] << 24) +
                (buffer[from + 4] << 32) +
                (buffer[from + 5] << 40) +
                (buffer[from + 6] << 48) +
                (buffer[from + 7] << 56);
        }

        public KResponse ReadResponse() {
            byte[] r = Read(0x8);

            int    Result = r[0] + r[1] * 0x100 + r[2] * 0x10000 + r[3] * 0x1000000;
            int    Size   = r[4] + r[5] * 0x100 + r[6] * 0x10000 + r[7] * 0x1000000;
            byte[] Data   = Read(Size);

            return new KResponse(Result, Data);
        }
        public bool CheckResult(KResponse resp) {
            if (resp.RC != 0) {
                Console.WriteLine("SwitchError 0x" + resp.RC.ToString("X5"));
                return false;
            }
            return true;
        }

        public void CmdClose() {
            if (Handle != 0)
                CmdDetachProcess(Handle);

            KResponse resp;
            // Send code for "Attach Processes"
            Write(BitConverter.GetBytes(128));
        }
        public long[] CmdListProcesses() {
            KResponse resp;

            long[] pids = null;

            // Send code for "List Processes"
            Write(BitConverter.GetBytes(0));
            // Get response
            resp = ReadResponse();
            CheckResult(resp);
            // Parse data
            int p = 0;
            pids = new long[resp.Data.Length / 8];
            for (int i = 0; i < resp.Data.Length; i += 8) {
                pids[p++] = GetU64(resp.Data, i);
            }
            return pids;
        }
        public int CmdAttachProcess(long PID) {
            KResponse resp;

            // Send code for "Attach Processes"
            Write(BitConverter.GetBytes(1));
            Write(BitConverter.GetBytes(PID));
            // Get response
            resp = ReadResponse();
            CheckResult(resp);
            // Parse data
            this.Handle = BitConverter.ToInt32(resp.Data, 0);
            return Handle;
        }
        public void CmdDetachProcess(int handle) {
            KResponse resp;

            // Send code for "Detach Processes"
            Write(BitConverter.GetBytes(2));
            Write(BitConverter.GetBytes(handle));
            // Get response
            resp = ReadResponse();
            CheckResult(resp);
        }

        public KDebugEvent CmdGetDbgEvent(int handle) {
            KResponse resp;

            // Send code for "Get Debug Event"
            Write(BitConverter.GetBytes(4));
            Write(BitConverter.GetBytes(handle));
            // Get response
            resp = ReadResponse();
            CheckResult(resp);
            // Parse data
            return new KDebugEvent(resp.Data);
        }
        
        public KMemPage CmdQueryMemory(int handle, long addr) {
            KResponse resp;

            // Send code for "Query Memory"
            Write(BitConverter.GetBytes(3));
            byte[] hdl = BitConverter.GetBytes(handle);
            byte[] adr = BitConverter.GetBytes(addr);
            Write(new byte[] { hdl[0], hdl[1], hdl[2], hdl[3], 0, 0, 0, 0, adr[0], adr[1], adr[2], adr[3], adr[4], adr[5], adr[6], adr[7] });
            // Get response
            resp = ReadResponse();
            CheckResult(resp);
            // Parse data
            long address = BitConverter.ToInt64(resp.Data, 0);
            long size = BitConverter.ToInt64(resp.Data, 8);
            int permissions = BitConverter.ToInt32(resp.Data, 16);
            int type = BitConverter.ToInt32(resp.Data, 20);

            return new KMemPage(address, size, permissions, type);
        }
        public byte[] CmdReadMemory(int handle, long addr, int size) {
            KResponse resp;

            // Send code for "Read Memory"
            Write(BitConverter.GetBytes(5));
            byte[] hdl = BitConverter.GetBytes(handle);
            byte[] sze = BitConverter.GetBytes(size);
            byte[] adr = BitConverter.GetBytes(addr);
            Write(Combine(hdl, sze, adr));
            // Get response
            resp = ReadResponse();
            CheckResult(resp);

            return resp.Data;
        }
        public void CmdWriteMemory(int handle, ulong addr, uint value)
        {
            KResponse resp;

            // Send code for "Write Memory"
            Write(BitConverter.GetBytes(9));
            byte[] hdl = BitConverter.GetBytes(handle);
            byte[] sze = BitConverter.GetBytes(value);
            byte[] adr = BitConverter.GetBytes(addr);
            Write(Combine(hdl, sze, adr));
            // Get response
            resp = ReadResponse();
            CheckResult(resp);
        }

        /*

    def cmdWriteMemory32(self, handle, addr, val): # Cmd9
        with self.lock:
            self.write(struct.pack('<I', 9))
            self.write(struct.pack('<IIQ', handle, val, addr))
            resp = self.readResponse()

        self.checkResult(resp)

    def cmdContinueDbgEvent(self, handle, flags, thread_id): # Cmd6
        with self.lock:
            self.write(struct.pack('<I', 6))
            self.write(struct.pack('<IIQ', handle, flags, thread_id))
            resp = self.readResponse()

        self.checkResult(resp)

    def cmdGetThreadContext(self, handle, thread_id, flags): # Cmd7
        with self.lock:
            self.write(struct.pack('<I', 7))
            self.write(struct.pack('<IIQ', handle, flags, thread_id))
            resp = self.readResponse()

        self.checkResult(resp)
        return resp['data']

    def cmdBreakProcess(self, handle): # Cmd8
        with self.lock:
            self.write(struct.pack('<I', 8))
            self.write(struct.pack('<I', handle))
            resp = self.readResponse()

        self.checkResult(resp)

    def cmdListenForAppLaunch(self): # Cmd10
        with self.lock:
            self.write(struct.pack('<I', 10))
            resp = self.readResponse()

        self.checkResult(resp)

    def cmdGetAppPid(self): # Cmd11
        with self.lock:
            self.write(struct.pack('<I', 11))
            resp = self.readResponse()

        try:
            self.checkResult(resp)
        except:
            return None

        return struct.unpack('<Q', resp['data'])[0]

    def cmdStartProcess(self, pid): # Cmd12
        with self.lock:
            self.write(struct.pack('<I', 12))
            self.write(struct.pack('<Q', pid))
            resp = self.readResponse()

        self.checkResult(resp)

    def cmdGetTitlePid(self, titleid): # Cmd13
        with self.lock:
            self.write(struct.pack('<I', 13))
            self.write(struct.pack('<Q', titleid))
            resp = self.readResponse()

        self.checkResult(resp)
        return struct.unpack('<Q', resp['data'])[0]

        */
    }
}
