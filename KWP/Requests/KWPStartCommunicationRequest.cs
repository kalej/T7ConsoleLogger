namespace KWP
{
    public class KWPStartCommunicationRequest : KWPRequest
    {
        public KWPStartCommunicationRequest() : base() { }
        public override KWPServiceId ServiceId => KWPServiceId.START_COMMUNICATION;
    }
}
