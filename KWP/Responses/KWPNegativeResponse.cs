namespace KWP
{
    public class KWPNegativeResponse : KWPResponse
    {
    
        public KWPNegativeResponse(KWPServiceId serviceId, KWPNegativeResponseCode code) : base(serviceId)
        {
            this.Code = code;
        }

        public KWPNegativeResponseCode Code
        {
            get;
            private set;
        }

    }
}
