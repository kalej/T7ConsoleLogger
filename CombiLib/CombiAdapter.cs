using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CANLib;
using LibUsbDotNet;
using LibUsbDotNet.Main;
using LibUsbDotNet.WinUsb;

namespace CombiLib
{
    public class CombiAdapter : ICANDevice
    {
        public CombiAdapter()
        {
            UsbDeviceFinder usbDeviceFinder = new UsbDeviceFinder(0xFFFF, 0x0005);
            combiUsbDevice = UsbDevice.OpenUsbDevice(device => device.Vid == 0xFFFF && device.Pid == 0x0005);
            if (combiUsbDevice == null) throw new Exception("CombiAdapter Not Found.");

            WinUsbDevice winUsbDevice2 = combiUsbDevice as WinUsbDevice;
            WinUsbDevice winUsbDevice = null;
            if (winUsbDevice2 == null || !winUsbDevice2.GetAssociatedInterface(0, out winUsbDevice) || winUsbDevice == null)
            {
                throw new Exception("Failed to claim interface");
            }
            
            reader = winUsbDevice.OpenEndpointReader(ReadEndpointID.Ep02);
            reader.ReadFlush();
            usbReadBuffer = new ReadWriteBuffer(1024);
            reader.DataReceivedEnabled = true;
            reader.DataReceived += OnDataReceived;

            writer = winUsbDevice.OpenEndpointWriter(WriteEndpointID.Ep05);
        }

        public override string GetVersion()
        {
            string result = null;
            CombiUsbPacket response;
            if (SendUsbPacket(new CombiUsbPacket(CombiUsbPacket.CMD_BRD_FWVERSION), out response))
            {
                result = String.Format("v{0}.{1}", response.CommandData[0], response.CommandData[1]);
            }
            
            return result;
        }

        private void OnDataReceived(object sender, EndpointDataEventArgs e)
        {
            byte[] receivedData = new byte[e.Count];
            Array.Copy(e.Buffer, receivedData, e.Count);
            usbReadBuffer.Write(receivedData);

            while (true)
            {
                if (usbReadBuffer.Count < CombiUsbPacket.CMD_CODE_FIELD_LENGTH + CombiUsbPacket.COUNT_BYTES_FIELD_LENGTH + CombiUsbPacket.ACK_FIELD_LENGTH)
                {
                    break;
                }

                int length = (usbReadBuffer[1] << 8) | usbReadBuffer[2];

                if (usbReadBuffer.Count < CombiUsbPacket.CMD_CODE_FIELD_LENGTH + CombiUsbPacket.COUNT_BYTES_FIELD_LENGTH + length + CombiUsbPacket.ACK_FIELD_LENGTH)
                {
                    break;
                }

                byte[] packetData = usbReadBuffer.Read(CombiUsbPacket.CMD_CODE_FIELD_LENGTH + CombiUsbPacket.COUNT_BYTES_FIELD_LENGTH + length + CombiUsbPacket.ACK_FIELD_LENGTH);

                if (OnUsbPacket != null)
                {
                    OnUsbPacket(this, new CombiUsbPacketEventArgs(CombiUsbPacket.FromBytes(packetData)));
                }
            }
        }

        public override void Close()
        {
            SendUsbPacket(new CombiUsbPacket(CombiUsbPacket.CMD_CAN_OPEN, new byte[] { 0 }));
            OnUsbPacket -= OnCanFramePacket;
        }

        public override void Open(int bitrate)
        {
            byte[] bitrateBytes = BitConverter.GetBytes(bitrate);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bitrateBytes);
            SendUsbPacket(new CombiUsbPacket(CombiUsbPacket.CMD_CAN_BITRATE, bitrateBytes));
            SendUsbPacket(new CombiUsbPacket(CombiUsbPacket.CMD_CAN_OPEN, new byte[] { 1 }));

            OnUsbPacket += OnCanFramePacket;
        }

        private void OnCanFramePacket(object sender, CombiUsbPacketEventArgs e)
        {
            CombiUsbPacket packet = e.Packet;
            if (packet.CommandCode != CombiUsbPacket.CMD_CAN_FRAME)
                return;

            CANMessage message = UsbPacketToCANMessage(packet);

            if (OnAnyCANMessage != null)
                OnAnyCANMessage(this, new CANMessageEventArgs(message));

            if (OnCANMessageWithId.ContainsKey(message.Id) && (OnCANMessageWithId[message.Id] != null))
                OnCANMessageWithId[message.Id](this, new CANMessageEventArgs(message));
        }

        public override void SendMessage(CANMessage message)
        {
            bool result = SendUsbPacket(CANMessageToUsbPacket(message));
            if (!result)
            {
                throw new Exception("Failed to send CAN message");
            }

            if (OnCANMessageSent != null)
                OnCANMessageSent(this, new CANMessageEventArgs(message));

        }


        private Mutex writeMutex = new Mutex();
        private AutoResetEvent commandConfirmed = new AutoResetEvent(false);
        private bool SendUsbPacket(CombiUsbPacket request, out CombiUsbPacket response, int writeTimeout=100, int responseTimeout=100)
        {
            int transferedLength;
            bool result;
            byte[] transferedData = request.ToBytes();
            byte ack = CombiUsbPacket.TERM_NACK;
            CombiUsbPacket handlerResponse = null;

            EventHandler<CombiUsbPacketEventArgs> handler = delegate (object sender, CombiUsbPacketEventArgs args)
            {
                if (args.Packet.CommandCode.Equals(request.CommandCode))
                {
                    handlerResponse = args.Packet;
                    ack = args.Packet.Ack;

                    commandConfirmed.Set();
                }
            };

            writeMutex.WaitOne();
            OnUsbPacket += handler;
            ErrorCode ec = writer.Write(transferedData, writeTimeout, out transferedLength);
            result = (ec.Equals(ErrorCode.None) || ec.Equals(ErrorCode.Ok) || ec.Equals(ErrorCode.Success)) && (transferedData.Length == transferedLength);
            if (result)
            {
                result = result && commandConfirmed.WaitOne(responseTimeout) && (ack == CombiUsbPacket.TERM_ACK);
            }
            OnUsbPacket -= handler;
            writeMutex.ReleaseMutex();

            response = handlerResponse;
            return result;
        }

        private bool SendUsbPacket(CombiUsbPacket request, int writeTimeout = 1000, int responseTimeout = 1000)
        {
            CombiUsbPacket response;
            return SendUsbPacket(request, out response, writeTimeout, responseTimeout);
        }

        ~CombiAdapter()
        {
            if (combiUsbDevice != null)
            {
                Close();
                reader.DataReceivedEnabled = false;
                reader.DataReceived -= OnDataReceived;
                
                if (combiUsbDevice.IsOpen)
                {
                    IUsbDevice wholeUsbDevice = combiUsbDevice as IUsbDevice;
                    if (!ReferenceEquals(wholeUsbDevice, null))
                    {
                        wholeUsbDevice.ReleaseInterface(1);
                    }

                    combiUsbDevice.Close();
                }
                combiUsbDevice = null;

                UsbDevice.Exit();
            }
        }

        private CombiUsbPacket CANMessageToUsbPacket(CANMessage message)
        {
            byte[] packetBytes;
            using (MemoryStream stream = new MemoryStream(256))
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    byte[] idBytes = BitConverter.GetBytes((UInt32)message.Id);
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(idBytes);

                    writer.Write(idBytes);
                    if (message.Data != null)
                        writer.Write(message.Data);
                    writer.Write((byte)(message.Data != null ? message.Data.Length : 0));
                    writer.Write((byte)0); // extended
                    writer.Write((byte)0); // remote
                }

                packetBytes = stream.ToArray();
            }
            return new CombiUsbPacket(CombiUsbPacket.CMD_CAN_TXFRAME, packetBytes);
        }

        private CANMessage UsbPacketToCANMessage(CombiUsbPacket packet)
        {
            CANMessage result;
            using (MemoryStream stream = new MemoryStream(packet.CommandData))
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    byte[] idBytes = reader.ReadBytes(4);
                    if (!BitConverter.IsLittleEndian)
                        Array.Reverse(idBytes);
                    UInt16 id = BitConverter.ToUInt16(idBytes, 0);
                    byte[] tmpData = reader.ReadBytes(8);
                    byte length = reader.ReadByte();

                    byte[] data = new byte[length];
                    Array.Copy(tmpData, data, length);
                    byte extended = reader.ReadByte();
                    byte remote = reader.ReadByte();

                    result = new CANMessage(id, data);
                }
            }

            return result;
        }

        private UsbDevice combiUsbDevice = null;
        private UsbEndpointReader reader = null;
        private UsbEndpointWriter writer = null;

        private ReadWriteBuffer usbReadBuffer;

        private EventHandler<CombiUsbPacketEventArgs> OnUsbPacket;
    }

    // ReadWriteBuffer is copy-paste from stackoverflow. ain't it the way people write code now?
    internal class ReadWriteBuffer
    {
        private readonly byte[] _buffer;
        private int _startIndex, _endIndex;

        public ReadWriteBuffer(int capacity)
        {
            _buffer = new byte[capacity];
        }

        public int Count
        {
            get
            {
                if (_endIndex > _startIndex)
                    return _endIndex - _startIndex;
                if (_endIndex < _startIndex)
                    return (_buffer.Length - _startIndex) + _endIndex;
                return 0;
            }
        }

        public void Write(byte[] data)
        {
            if (Count + data.Length > _buffer.Length)
                throw new Exception("buffer overflow");
            if (_endIndex + data.Length >= _buffer.Length)
            {
                var endLen = _buffer.Length - _endIndex;
                var remainingLen = data.Length - endLen;

                Array.Copy(data, 0, _buffer, _endIndex, endLen);
                Array.Copy(data, endLen, _buffer, 0, remainingLen);
                _endIndex = remainingLen;
            }
            else
            {
                Array.Copy(data, 0, _buffer, _endIndex, data.Length);
                _endIndex += data.Length;
            }
        }

        public byte[] Read(int len, bool keepData = false)
        {
            if (len > Count)
                throw new Exception("not enough data in buffer");
            var result = new byte[len];
            if (_startIndex + len < _buffer.Length)
            {
                Array.Copy(_buffer, _startIndex, result, 0, len);
                if (!keepData)
                    _startIndex += len;
                return result;
            }
            else
            {
                var endLen = _buffer.Length - _startIndex;
                var remainingLen = len - endLen;
                Array.Copy(_buffer, _startIndex, result, 0, endLen);
                Array.Copy(_buffer, 0, result, endLen, remainingLen);
                if (!keepData)
                    _startIndex = remainingLen;
                return result;
            }
        }

        public byte this[int index]
        {
            get
            {
                if (index >= Count)
                    throw new ArgumentOutOfRangeException();
                return _buffer[(_startIndex + index) % _buffer.Length];
            }
        }

        public IEnumerable<byte> Bytes
        {
            get
            {
                for (var i = 0; i < Count; i++)
                    yield return _buffer[(_startIndex + i) % _buffer.Length];
            }
        }
    }
}
