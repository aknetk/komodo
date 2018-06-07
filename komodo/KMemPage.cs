using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace komodo {
    public class KMemPage {
        public long Address;
        public long Size;
        public int Permissions;
        public int Type;
        public KMemPage(long address, long size, int permissions, int type) {
            Address = address;
            Size = size;
            Permissions = permissions;
            Type = type;
        }
    }
}
