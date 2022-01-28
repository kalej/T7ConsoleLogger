namespace KWP
{
    public class KWPPositiveResponse : KWPResponse
    {
        public KWPPositiveResponse(KWPServiceId serviceId, byte[] data) : base(serviceId)
        {
        
            this.Data = data;
        }

        public byte[] Data
        {
            get;
            private set;
        }
    }
}
