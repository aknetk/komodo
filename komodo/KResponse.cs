using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace komodo {
    public class KResponse {
        public int RC;
        public byte[] Data;
        public KResponse(int rc, byte[] data) {
            RC = rc;
            Data = data;
        }
    }
}
