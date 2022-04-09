namespace KWP
{
    public class KWPReadEcuIdentificationRequest : KWPRequest
    {
        public override KWPServiceId ServiceId => KWPServiceId.READ_ECU_IDENTIFICATION;

        public enum EcuIdentification
        {
            UNIT_NAME = 0x80,
            VIN = 0x90,
            HARDWARE_NUMBER = 0x91,
            IMMOBILIZER_CODE = 0x92,
            SOFTWARE_PART_NUMBER = 0x94,
            SOFTWARE_VERSION = 0x95,
            ENGINE_TYPE = 0x97
        }
        // 90, 92, 93, 94, 95
        public KWPReadEcuIdentificationRequest(EcuIdentification ecuId) : base(new byte[] { (byte)ecuId }) {}

        public KWPReadEcuIdentificationRequest(byte ecuId) : base(new byte[] { ecuId }) { }
    }
}
