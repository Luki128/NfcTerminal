using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NfcDemo
{
    public static class Apdu
    {
        public static byte[] SelectByAid(byte[] aid,byte _class = 0x00, byte _inst = 0x00,byte _p1 = 0x00, byte _p2 = 0x00)
        {
            var cmd = new byte[5 + aid.Length];
            cmd[0] = _class; cmd[1] = _inst;   // CLA, INS
            cmd[2] = _p1; cmd[3] = _p2;   // P1,  P2
            cmd[4] = (byte)aid.Length;      // Lc
            Buffer.BlockCopy(aid, 0, cmd, 5, aid.Length);
            return cmd;
        }

        /// <summary>
        /// Extract form response status code and data
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static (byte[] Data, ushort SW) SplitResponse(byte[] resp)
        {
            if (resp.Length < 2) throw new ArgumentException("To short responce");
            var sw = (ushort)((resp[^2] << 8) | resp[^1]);
            return (resp[..^2], sw);
        }
    }
}
