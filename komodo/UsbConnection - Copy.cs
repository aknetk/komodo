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

    public class UsbConnectionCopy {

        public Socket serv;
        public bool useUSB = false;
        public int Handle = 0;

        int FINAL_WIDTH = 480;
        int FINAL_HEIGHT = 270;

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

        int WriteBitmapFile(string filename, int width, int height, byte[] imageData, int pxWidth) {
            byte[] newData = new byte[imageData.Length];

            for (int x = 0; x < imageData.Length; x += pxWidth) {
                byte[] pixel = new byte[pxWidth];
                Array.Copy(imageData, x, pixel, 0, pxWidth);

                byte r = pixel[0];
                byte g = pixel[1];
                byte b = pixel[2];
                byte a = 0xFF;// pixel[3];

                byte[] newPixel = new byte[] { b, g, r };

                Array.Copy(newPixel, 0, newData, x, pxWidth);
            }

            imageData = newData;

            using (var stream = new MemoryStream(imageData))
            using (var bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb)) {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0,
                                                                bmp.Width,
                                                                bmp.Height),
                                                  ImageLockMode.WriteOnly,
                                                  bmp.PixelFormat);

                IntPtr pNative = bmpData.Scan0;
                Marshal.Copy(imageData, 0, pNative, imageData.Length);

                bmp.UnlockBits(bmpData);

                bmp.Save(filename);
            }

            return 1;
        }

        public static byte[] Compress(byte[] uncompressedFileData) {
            byte[] compressedFileData = new byte[0];

            try {
                /*var stopwatch = new Stopwatch();
                stopwatch.Reset();
                stopwatch.Start();*/

                using (MemoryStream outStream = new MemoryStream()) {
                    using (ZOutputStream compress = new ZOutputStream(outStream, zlibConst.Z_BEST_COMPRESSION)) {
                        compress.Write(uncompressedFileData, 0, uncompressedFileData.Length);
                        compress.finish();
                        //Console.WriteLine("Compressed {0} bytes to {1}", uncompressedFileData.Length, outStream.Length);
                    }
                    compressedFileData = outStream.ToArray();
                }

                //stopwatch.Stop();
                //Console.WriteLine("Compressed {0} bytes to {1} bytes in {2} seconds", uncompressedFileData.Length, compressedFileData.Length, stopwatch.Elapsed.TotalSeconds);
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message, ex);
            }

            return compressedFileData;
        }
        public static byte[] Decompress(byte[] data) {
            /*const int BUFFER_SIZE = 256;
            byte[] tempArray = new byte[BUFFER_SIZE];
            List<byte[]> tempList = new List<byte[]>();
            int count = 0, length = 0;
            MemoryStream ms = new MemoryStream(data);
            DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress);
            while ((count = ds.Read(tempArray, 0, BUFFER_SIZE)) > 0) {
                if (count == BUFFER_SIZE) {
                    tempList.Add(tempArray);
                    tempArray = new byte[BUFFER_SIZE];
                }
                else {
                    byte[] temp = new byte[count];
                    Array.Copy(tempArray, 0, temp, 0, count);
                    tempList.Add(temp);
                }
                length += count;
            }
            byte[] retVal = new byte[length];
            count = 0;
            foreach (byte[] temp in tempList) {
                Array.Copy(temp, 0, retVal, count, temp.Length);
                count += temp.Length;
            }
            return retVal;*/
            return data;
        }

        public static byte[] CompressJPEG(byte[] uncompressedFileData, int vWidth, int vHeight) {
            using (MemoryStream stream = new MemoryStream()) {
                jpeg_compress_struct compressor = new jpeg_compress_struct(new jpeg_error_mgr());
                compressor.Image_height = vHeight;
                compressor.Image_width = vWidth;
                compressor.In_color_space = J_COLOR_SPACE.JCS_RGB;
                compressor.Input_components = 3;
                compressor.jpeg_set_defaults();

                compressor.Dct_method = J_DCT_METHOD.JDCT_IFAST;
                compressor.Smoothing_factor = 0;
                compressor.jpeg_set_quality(80, true);
                compressor.jpeg_simple_progression();

                compressor.Density_unit = DensityUnit.Unknown;
                compressor.X_density = (short)192;
                compressor.Y_density = (short)192;

                compressor.jpeg_stdio_dest(stream);
                compressor.jpeg_start_compress(true);

                byte[][] rowForDecompressor = new byte[1][];
                int bytesPerPixel = 3;
                byte[] row = new byte[vWidth * bytesPerPixel]; // wasteful, but gets you 0 bytes every time - content is immaterial.
                while (compressor.Next_scanline < compressor.Image_height) {

                    Buffer.BlockCopy(uncompressedFileData, compressor.Next_scanline * vWidth * bytesPerPixel, row, 0, vWidth * bytesPerPixel);

                    rowForDecompressor[0] = row;
                    compressor.jpeg_write_scanlines(rowForDecompressor, 1);
                }
                compressor.jpeg_finish_compress();

                return stream.ToArray();
            }
        }

        public UsbConnectionCopy() {
            if (useUSB) {
                SerialPort sp = new SerialPort("ATX", 115200);
                //sp.Write(writebytes, 0, writebytes.Length);

                if (sp == null) {
                    Console.WriteLine("Komodo over USB not found.");
                }
                else {
                    Console.WriteLine("Komodo over USB found!");
                    sp.Close();
                }

                /*byte[] readbytes = new byte[sp.BytesToRead];
                sp.Read(readbytes, 0, readbytes.Length);*/
            }
            else {

                /*IPAddress ipAddr = Dns.GetHostEntry("192.168.1.13").AddressList[0]; //new IPAddress(new byte[] { 192, 168, 1, 13 });

                serv = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                serv.Connect(ipAddr, 0xDEAD);

                Console.WriteLine("Komodo over TCP/IP found!");*/

                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();

                //VideoCapture Reader = new VideoCapture("F:/Movies & Media/Gurren Lagann - Childhood's End [1080p][Japanese].mkv");
                VideoCapture Reader = new VideoCapture("F:/Movies & Media/Fullmetal Alchemist Brotherhood [1080p][Dual Audio]/Fullmetal Alchemist Brotherhood [1080p][Dual Audio] - Episode 38 [35036DE6].mkv");

                //VideoCapture Reader = new VideoCapture("C:/Users/Justin/dropbox/sonic3/source/Resource/Movies/Mania LQ.mp4");

                Console.WriteLine("Frame Rate: " + Reader.GetCaptureProperty(CapProp.Fps));
                Console.WriteLine("Frame Count: " + Reader.GetCaptureProperty(CapProp.FrameCount));

                int FrameCountForPacket = (int)Math.Round(Reader.GetCaptureProperty(CapProp.Fps));
                int FrameCountTotal = (int)Reader.GetCaptureProperty(CapProp.FrameCount);

                Mat m = new Mat();

                /*byte[] data = new byte[m.Width * m.Height * m.NumberOfChannels];
                GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                using (Mat m2 = new Mat(new Size(m.Width, m.Height), DepthType.Cv8U, m.NumberOfChannels, handle.AddrOfPinnedObject(), m.Width * m.NumberOfChannels)) {
                    Reader.Read(m);
                    m.CopyTo(m2);
                }

                var stopwatch = new Stopwatch();
                stopwatch.Reset();
                stopwatch.Start();

                byte[] bbbbs = CompressJPEG(data, m.Width, m.Height);

                stopwatch.Stop();
                Console.WriteLine("Compressed {0} to {1} in {2} milliseconds", data.Length, bbbbs.Length, stopwatch.Elapsed.Milliseconds);

                File.WriteAllBytes("test.jpg", bbbbs);

                return;*/

                int vWidth = FINAL_WIDTH;
                int vHeight = FINAL_HEIGHT;


                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine();
                Thread.Sleep(10);

                IPAddress ipAddr = Dns.GetHostEntry("192.168.1.13").AddressList[0];

                serv = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                serv.Connect(ipAddr, 0xDEAF);

                Console.WriteLine("Binj over TCP/IP found!");

                WritePrePacket(1, 0, vWidth, vHeight, (int)Reader.GetCaptureProperty(CapProp.FrameCount));

                //SendFrames(Reader, FrameCountForPacket * 5);

                bool firstTime = true;

                byte sent = Read(1)[0];
                SendFrames(Reader, FrameCountForPacket * 5);
                for (int i = FrameCountForPacket * 5; i < FrameCountTotal && i < 200000; i += FrameCountForPacket) {
                    int shit = FrameCountTotal - i;
                    if (shit > FrameCountForPacket)
                        shit = FrameCountForPacket;

                    SendFrames(Reader, shit);
                }
                

                MessageBox.Show("sdfsdf");

                serv.Close();


                Application.Exit();
                return;
            }
        }

        public void SendFrames(VideoCapture Reader, int count) {
            Mat m = new Mat();

            int FrameCountForPacket = count;
            byte[] PacketData = new byte[0];

            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            byte[] FrameByteSizes = new byte[FrameCountForPacket * 2 * 4];

            int vWidth = Reader.Width;
            int vHeight = Reader.Height;
            int vChannels = 3; // m.NumberOfChannels

            vWidth = FINAL_WIDTH;
            vHeight = FINAL_HEIGHT;

            PacketData = new byte[vWidth * vHeight * vChannels * FrameCountForPacket];
            int PacketData_Length = 0;

            byte[] data = new byte[vWidth * vHeight * vChannels];
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);

            using (Mat m2 = new Mat(new Size(vWidth, vHeight), DepthType.Cv8U, vChannels, handle.AddrOfPinnedObject(), vWidth * vChannels)) {
                int offset = 0;

                for (int i = 0; i < FrameCountForPacket; i++) {
                    long m1, m3, m4, m5;
                    long milliseconds2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                    Reader.Read(m);
                    CvInvoke.Resize(m, m2, new Size(vWidth, vHeight));
                    m1 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - milliseconds2;

                    milliseconds2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    //m.CopyTo(m2);
                    m3 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - milliseconds2;

                    milliseconds2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    byte[] newData = CompressJPEG(data, vWidth, vHeight);
                    m4 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - milliseconds2;

                    milliseconds2 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    newData.CopyTo(PacketData, offset);
                    m5 = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - milliseconds2;

                    //Console.WriteLine("For frame {0} - Reader.Read(): {1}, m.CopyTo(): {2}, CompressJPEG(): {3}, newData.CopyTo(): {4}", i, m1, m3, m4, m5);

                    //File.WriteAllBytes("test" + i + ".jpg", newData);

                    //BitConverter.GetBytes(0).CopyTo(FrameByteSizes, i * 2 * 4 + 0); // decompressed size
                    BitConverter.GetBytes(newData.Length).CopyTo(FrameByteSizes, i * 4 + 0); // compressed size

                    offset += newData.Length;
                    PacketData_Length = offset;
                }

                //Console.WriteLine("Copied {0} frames in {1} milliseconds", FrameCountForPacket, stopwatch3.Elapsed.Milliseconds);
            }
            handle.Free();

            milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - milliseconds;

            Console.WriteLine("Uncompressed packet size: {0} in {1} milliseconds", PacketData_Length, milliseconds);

            int PacketUncSize = PacketData_Length;

            //PacketData = Compress(PacketData);

            int PacketComSize = PacketData_Length;
            Console.WriteLine();

            WritePrePacket(0, PacketData_Length, FrameCountForPacket, PacketUncSize, vHeight);

            int MICROPACKET_SIZE = 0x30000;

            for (int i = 0; i < PacketData_Length; i += MICROPACKET_SIZE) {
                int SizeToSend = PacketData_Length - i;
                if (SizeToSend > MICROPACKET_SIZE)
                    SizeToSend = MICROPACKET_SIZE;

                byte[] ret = new byte[SizeToSend];
                Buffer.BlockCopy(PacketData, i, ret, 0, SizeToSend);

                Write(ret);
            }

            byte[] ret2 = new byte[FrameByteSizes.Length / 2];
            Buffer.BlockCopy(FrameByteSizes, 0, ret2, 0, ret2.Length);

            Write(ret2);

            Console.WriteLine("Done.");

            //Write(FrameByteSizes);
        }
        public void SendFramesOld(VideoCapture Reader, int count) {
            Mat m = new Mat();

            int FrameCountForPacket = count;
            byte[] PacketData = new byte[0];

            var stopwatch = new Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            int[] FrameUncSize = new int[FrameCountForPacket];
            int[] FrameComSize = new int[FrameCountForPacket];

            byte[] FrameByteSizes = new byte[0];

            int vWidth = Reader.Width;
            int vHeight = Reader.Height;
            int vChannels = 3; // m.NumberOfChannels

            byte[] data = new byte[vWidth * vHeight * vChannels];
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            using (Mat m2 = new Mat(new Size(vWidth, vHeight), DepthType.Cv8U, vChannels, handle.AddrOfPinnedObject(), vWidth * vChannels)) {
                for (int i = 0; i < FrameCountForPacket; i++) {
                    Reader.Read(m);
                    m.CopyTo(m2);

                    FrameByteSizes = Combine(FrameByteSizes, BitConverter.GetBytes(data.Length));

                    FrameUncSize[i] = data.Length;

                    byte[] compressed = Compress(data);

                    FrameByteSizes = Combine(FrameByteSizes, BitConverter.GetBytes(compressed.Length));

                    FrameComSize[i] = compressed.Length;

                    PacketData = Combine(PacketData, compressed);
                }
            }
            handle.Free();
            stopwatch.Stop();
            Console.WriteLine("Uncompressed packet size: {0} in {1} milliseconds", PacketData.Length, stopwatch.Elapsed.Milliseconds);

            byte[] Type = BitConverter.GetBytes(0);
            byte[] PacketSize = BitConverter.GetBytes(PacketData.Length);
            byte[] Data1 = BitConverter.GetBytes(FrameCountForPacket);
            byte[] Data2 = BitConverter.GetBytes(vWidth);
            byte[] Data3 = BitConverter.GetBytes(vHeight);

            Write(Combine(Type, PacketSize, Data1, Data2, Data3));

            int MICROPACKET_SIZE = 0x4000;

            for (int i = 0; i < PacketData.Length; i += MICROPACKET_SIZE) {
                int SizeToSend = PacketData.Length - i;
                if (SizeToSend > MICROPACKET_SIZE)
                    SizeToSend = MICROPACKET_SIZE;

                byte[] ret = new byte[SizeToSend];
                Buffer.BlockCopy(PacketData, i, ret, 0, SizeToSend);

                Write(ret);
            }

            Write(FrameByteSizes);
        }

        public void WritePrePacket(int _Type, int _NextPacketSize, int _Data1, int _Data2, int _Data3) {
            byte[] Type = BitConverter.GetBytes(_Type);
            byte[] PacketSize = BitConverter.GetBytes(_NextPacketSize);
            byte[] Data1 = BitConverter.GetBytes(_Data1);
            byte[] Data2 = BitConverter.GetBytes(_Data2);
            byte[] Data3 = BitConverter.GetBytes(_Data3);

            Write(Combine(Type, PacketSize, Data1, Data2, Data3));
        }

        public byte[] Read(int size) {
            byte[] buffer = new byte[size];
            try {
                int total = 0;
                int offset = 0;
                while (total < size) {
                    int count = 0;
                    if (useUSB) {

                    }
                    else {
                        count = serv.Receive(buffer, offset, size - total, SocketFlags.None);
                    }
                    if (count <= 0)
                        return null;
                    total += count;
                    offset += count;
                }
            }
            catch (Exception e) {
                Application.Exit();
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

                    }
                    else {
                        count = serv.Send(buffer, offset, size - total, SocketFlags.None);
                    }
                    if (count <= 0)
                        return;
                    total += count;
                    offset += count;
                }
            }
            catch (Exception e) {
                Application.Exit();
            }
            //Console.WriteLine("KConn >> Sent " + total + " bytes.");
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
            Write(BitConverter.GetBytes(255));
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
            Write(new byte[] { hdl[0], hdl[1], hdl[2], hdl[3], sze[0], sze[1], sze[2], sze[3], adr[0], adr[1], adr[2], adr[3], adr[4], adr[5], adr[6], adr[7] });
            // Get response
            resp = ReadResponse();
            CheckResult(resp);

            return resp.Data;
        }

        /*

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

    def cmdWriteMemory32(self, handle, addr, val): # Cmd9
        with self.lock:
            self.write(struct.pack('<I', 9))
            self.write(struct.pack('<IIQ', handle, val, addr))
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
