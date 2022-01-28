using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KWP
{
    public abstract class KWPRequest
    {
        public KWPRequest(byte[] data)
        {
            this.data = data;
        }

        public KWPRequest()
        {
            this.data = null;
        }

        public abstract KWPServiceId ServiceId { get; }

        public byte[] ToBytes()
        {
            byte[] result = null;
            using (MemoryStream stream = new MemoryStream(1024))
            {
                if (this.data != null)
                    stream.WriteByte((byte)(this.data.Length + 1));
                else
                    stream.WriteByte(1);

                stream.WriteByte((byte)this.ServiceId);

                if (this.data != null)
                    stream.Write(this.data, 0, this.data.Length);

                result = stream.ToArray();
            }
            
            return result;
        }

        private byte[] data;
    }
}
