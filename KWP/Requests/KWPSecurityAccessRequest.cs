namespace KWP
{
    public class KWPSecurityAccessRequest : KWPRequest
    {
        public KWPSecurityAccessRequest(byte accessLevel) : base(new byte[] { accessLevel }) { }

        public KWPSecurityAccessRequest(byte accessLevel, int key) : base(new byte[] { accessLevel, (byte)(key >> 8), (byte)key }) { }
        
        public override KWPServiceId ServiceId => KWPServiceId.SECURITY_ACCESS;
    }
}
