namespace KWP
{
    public class KWPTesterPresentRequest : KWPRequest
    {
        public KWPTesterPresentRequest() : base() { }
        public override KWPServiceId ServiceId => KWPServiceId.TESTER_PRESENT;
    }
}
