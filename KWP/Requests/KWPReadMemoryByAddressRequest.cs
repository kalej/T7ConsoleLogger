namespace KWP
{
    public class KWPReadMemoryByAddressRequest : KWPRequest
    {
        public KWPReadMemoryByAddressRequest(int address, int length) : 
            base(
                new byte[]{
                    (byte)(address >> 16),
                    (byte)(address >> 8),
                    (byte)(address >> 0),
                    (byte)(length),
                    1,
                    0
                }
            ) { }

        public override KWPServiceId ServiceId => KWPServiceId.READ_MEMORY_BY_ADDRESS;
    }
}
