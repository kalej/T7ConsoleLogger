using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CombiLib
{
    internal class CombiUsbPacket
    {
        public static byte TERM_ACK = 0x0;
        public static byte TERM_NACK = (byte)0xff;

        public static byte CMD_BRD_FWVERSION = 0x20;
        //public static byte CMD_BRD_ADCFILTER = 0x21;
        //public static byte CMD_BRD_ADC = 0x22;
        //public static byte CMD_BRD_EGT = 0x23;

        public static byte CMD_CAN_OPEN = (byte)0x80;
        public static byte CMD_CAN_BITRATE = (byte)0x81;
        public static byte CMD_CAN_FRAME = (byte)0x82;
        public static byte CMD_CAN_TXFRAME = (byte)0x83;

        //public static byte CMD_CAN_ECUCONNECT = (byte)0x89;
        //public static byte CMD_CAN_READFLASH = (byte)0x8a;
        //public static byte CMD_CAN_WRITEFLASH = (byte)0x8b;

        public static int CMD_CODE_FIELD_LENGTH = 1;
        public static int COUNT_BYTES_FIELD_LENGTH = 2;
        public static int ACK_FIELD_LENGTH = 1;
        //public static int MIN_PACKET_LENGTH = CMD_CODE_FIELD_LENGTH + COUNT_BYTES_FIELD_LENGTH + ACK_FIELD_LENGTH;

        public CombiUsbPacket(byte cmdCode)
        {
            this.CommandCode = cmdCode;
            this.CommandData = null;
            this.Ack = TERM_ACK;
        }

        public CombiUsbPacket(byte cmdCode, byte[] cmdData)
        {
            this.CommandCode = cmdCode;
            this.CommandData = cmdData;
            this.Ack = TERM_ACK;
        }

        private CombiUsbPacket(byte cmdCode, byte[] cmdData, byte ack)
        {
            this.CommandCode = cmdCode;
            this.CommandData = cmdData;
            this.Ack = ack;
        }

        public int SizeInBytes()
        {
            return CMD_CODE_FIELD_LENGTH + COUNT_BYTES_FIELD_LENGTH + (CommandData != null ? CommandData.Length : 0) + ACK_FIELD_LENGTH;
        }
        public byte[] ToBytes()
        {
            byte[] result;
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(CommandCode);

                    byte[] lengthBytes = BitConverter.GetBytes((short)(CommandData == null ? 0 : CommandData.Length));
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);

                    writer.Write(lengthBytes);

                    if (CommandData != null)
                        writer.Write(CommandData);

                    writer.Write(Ack);
                }

                result = stream.ToArray();
            }

            return result;
        }

        public static CombiUsbPacket FromBytes(byte[] data)
        {
            byte cmdCode;
            UInt16 cmdLength;
            byte[] cmdData;
            byte ack;

            using (MemoryStream stream = new MemoryStream(data))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    cmdCode = reader.ReadByte();

                    byte[] lengthBytes = reader.ReadBytes(COUNT_BYTES_FIELD_LENGTH);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(lengthBytes);
                    cmdLength = BitConverter.ToUInt16(lengthBytes, 0);
                    cmdData = reader.ReadBytes(cmdLength);

                    ack = reader.ReadByte();
                }
            }
            
            return new CombiUsbPacket(cmdCode, cmdData, ack);
        }

        public byte CommandCode
        {
            get;
            private set;
        }

        public byte[] CommandData
        {
            get;
            private set;
        }

        public byte Ack
        {
            get;
            private set;
        }
    }

    internal class CombiUsbPacketEventArgs : EventArgs
    {
        public CombiUsbPacketEventArgs(CombiUsbPacket packet)
        {
            this.Packet = packet;
        }

        public CombiUsbPacket Packet
        {
            get;
            private set;
        }
    }
}
