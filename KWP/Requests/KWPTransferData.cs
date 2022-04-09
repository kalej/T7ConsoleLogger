using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KWP
{
    public class KWPTransferData : KWPRequest
    {
        public override KWPServiceId ServiceId => KWPServiceId.TRANSFER_DATA;
    }
}
