using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace komodo {
    public class KDebugEvent {
        enum EventTypes : int {
            AttachProcess = 0,
            AttachThread,
            None,
            Exiting,
            Exception,
        }

        public int EventType = 2;
        public int Flags = 0;
        public long ThreadID = 0;

        public long TitleID = 0;
        public long ProcessID = 0;
        public String ProcessName = "<no name>";
        public int MnuFlags = 0;
        public long UserExceptionContextAddr = 0;

        public long ThreadID2 = 0;
        public long TLSPointer = 0;
        public long Entrypoint = 0;

        public long Type = 0;

        public long ExceptionType = 0;
        public long FaultRegister1 = 0;
        public long FaultRegister2 = 0;

        public KDebugEvent(byte[] data) {
            EventType = BitConverter.ToInt32(data, 0x00);
            Flags = BitConverter.ToInt32(data, 0x04);
            ThreadID = BitConverter.ToInt64(data, 0x08);

            switch (EventType) {
                case 0: {
                    TitleID = BitConverter.ToInt64(data, 0x10);
                    ProcessID = BitConverter.ToInt64(data, 0x18);
                    ProcessName = new String(Encoding.Default.GetString(data, 0x20, 0xC).ToCharArray());
                    MnuFlags = BitConverter.ToInt32(data, 0x2C);
                    UserExceptionContextAddr = BitConverter.ToInt64(data, 0x30);
                }
                break;

                case 1: {
                    ThreadID2 = BitConverter.ToInt64(data, 0x10);
                    TLSPointer = BitConverter.ToInt64(data, 0x18);
                    Entrypoint = BitConverter.ToInt64(data, 0x20);
                }
                break;

                case 3: {
                    Type = BitConverter.ToInt64(data, 0x10);
                }
                break;

                case 4: {
                    ExceptionType = BitConverter.ToInt64(data, 0x10);
                    FaultRegister1 = BitConverter.ToInt64(data, 0x18);
                    FaultRegister2 = BitConverter.ToInt64(data, 0x20);
                }
                break;

                default:
                break;
            }
        }
    }
}
