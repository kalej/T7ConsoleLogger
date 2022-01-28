using System;

namespace KWP
{
    public class KWPResponse
    {
        protected KWPResponse(KWPServiceId serviceId)
        {
            this.ServiceId = serviceId;
        }

        public static KWPResponse FromBytes(byte[] bytes)
        {
            int totalLength = bytes[0];

            if (bytes[1] == 0x7F)
            {
                return new KWPNegativeResponse((KWPServiceId)bytes[2], (KWPNegativeResponseCode)bytes[3]);
            }
            else
            {
                byte[] data = new byte[totalLength - 1];
                Array.Copy(bytes, 2, data, 0, totalLength - 1);
                return new KWPPositiveResponse((KWPServiceId)((byte)(bytes[1] & ~0x40)),  data);
            }

        }

        public KWPServiceId ServiceId
        {
            private set;
            get;
        }
    }
}
