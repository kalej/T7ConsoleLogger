namespace KWP
{
    public class KWPReadDataByLocalIdRequest : KWPRequest
    {
        public KWPReadDataByLocalIdRequest(byte localId) : base(new byte[] { localId }) { }

        public override KWPServiceId ServiceId => KWPServiceId.READ_DATA_BY_LOCAL_IDENTIFIER;
    }
}
