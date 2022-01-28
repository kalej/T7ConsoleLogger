using ECULogging;
using System.IO;

namespace KWP
{
    public class KWPDynamicallyDefineLocalIdRequest : KWPRequest
    {
        public KWPDynamicallyDefineLocalIdRequest(byte definedId, VarDefinition varDef) : base(GetKWPDefinition(definedId, varDef)) { }
        public override KWPServiceId ServiceId => KWPServiceId.DYNAMICALLY_DEFINE_LOCAL_IDENTIFIER;
        private static byte[] GetKWPDefinition(byte definedId, VarDefinition varDef)
        {
            byte[] result;

            using (MemoryStream stream = new MemoryStream(1024))
            {
                stream.WriteByte(0xF0);

                switch (varDef.Method)
                {
                    case "address":
                        stream.WriteByte(0x03);
                        stream.WriteByte(definedId);
                        stream.WriteByte((byte)varDef.Length);
                        stream.WriteByte((byte)(varDef.Value >> 16));
                        stream.WriteByte((byte)(varDef.Value >> 8));
                        stream.WriteByte((byte)(varDef.Value >> 0));
                        break;
                    case "locid":
                        stream.WriteByte(0x01);
                        stream.WriteByte(definedId);
                        stream.WriteByte(0x00);
                        stream.WriteByte((byte)varDef.Value);
                        stream.WriteByte(0x00);
                        break;
                    case "symbol":
                        stream.WriteByte(0x03);
                        stream.WriteByte(definedId);
                        stream.WriteByte(0x00);
                        stream.WriteByte(0x80);
                        stream.WriteByte((byte)(varDef.Value >> 8));
                        stream.WriteByte((byte)(varDef.Value >> 0));
                        break;
                }
                result = stream.ToArray();
            }

            return result;
        }
    }
}
