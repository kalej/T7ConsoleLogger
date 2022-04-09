using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KWP
{
    public class KWPRequestUpload : KWPRequest
    {
        public KWPRequestUpload(int start, int length) : 
            base(
                new byte[] 
                { 
                    (byte)(start >> 16), 
                    (byte)(start >> 8), 
                    (byte)start,
                    (byte)(length >> 24),
                    (byte)(length >> 16), 
                    (byte)(length >> 8), 
                    (byte)length 
                }
                ) { }
        public override KWPServiceId ServiceId => KWPServiceId.REQUEST_UPLOAD;
    }
}
