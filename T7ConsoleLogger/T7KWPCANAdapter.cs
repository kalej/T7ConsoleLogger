using CANLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KWP
{
    public class T7KWPCANAdapter
    {
        public T7KWPCANAdapter(ICANDevice canDevice)
        {
            this.canDevice = canDevice;
            this.canDevice.InitCanListener(INIT_RESP_ID);
            this.canDevice.InitCanListener(REQ_CHUNK_CONF_ID);
        }

        public KWPResponse SendReceive(KWPRequest request, int timeout=100)
        {
            ManualResetEventSlim gotMessage = new ManualResetEventSlim(false);
            if (request is KWPStartCommunicationRequest)
            {
                CANMessage initResponse = null;
                EventHandler<CANMessageEventArgs> initHandler = delegate (object sender, CANMessageEventArgs args)
                {
                    initResponse = args.Message;
                    gotMessage.Set();
                };

                canDevice.OnCANMessageWithId[INIT_RESP_ID] += initHandler;
                gotMessage.Reset();
                canDevice.SendMessage(
                    new CANMessage(
                        INIT_MSG_ID, new byte[]
                        {
                            0x3F, (byte)KWPServiceId.START_COMMUNICATION, 0x00, 0x11, (byte)(REQ_MSG_ID >> 8), (byte)(REQ_MSG_ID >> 0), 0x00, 0x00
                        }
                    )
                );

                if (!gotMessage.Wait(timeout))
                {
                    canDevice.OnCANMessageWithId[INIT_RESP_ID] -= initHandler;
                    return null;
                }

                canDevice.OnCANMessageWithId[INIT_RESP_ID] -= initHandler;
                if (initResponse.Data[3] != ((byte)KWPServiceId.START_COMMUNICATION | 0x40))
                    return new KWPNegativeResponse(KWPServiceId.START_COMMUNICATION, KWPNegativeResponseCode.GENERAL_REJECT);

                responseMessageId = (UInt32)((initResponse.Data[6] << 8) | initResponse.Data[7]); // example: 0x238: 40 BF 21 C1 00 11 02 58
                canDevice.InitCanListener(responseMessageId);

                return new KWPPositiveResponse(KWPServiceId.START_COMMUNICATION, null);
            }
            else
            {
                Queue<CANMessage> reqChunkQueue = SplitRequest(request);
                CANMessage requestChunk = null;
                CANMessage chunkConfirmation = null;

                EventHandler<CANMessageEventArgs> requestChunkConfirmationHandler = delegate (object sender, CANMessageEventArgs args)
                {
                    chunkConfirmation = args.Message;

                    gotMessage.Set();
                };

                // send all chunks of request with confirmation except the last one
                while (reqChunkQueue.Count > 1)
                {
                    requestChunk = reqChunkQueue.Dequeue();
                    gotMessage.Reset();
                    canDevice.OnCANMessageWithId[REQ_CHUNK_CONF_ID] += requestChunkConfirmationHandler;
                    canDevice.SendMessage(requestChunk);

                    if (!gotMessage.Wait(timeout))
                    {
                        canDevice.OnCANMessageWithId[REQ_CHUNK_CONF_ID] -= requestChunkConfirmationHandler;
                        return new KWPNegativeResponse(request.ServiceId, KWPNegativeResponseCode.GENERAL_REJECT);
                    }

                    // TODO: check confirmation chunk order

                    canDevice.OnCANMessageWithId[REQ_CHUNK_CONF_ID] -= requestChunkConfirmationHandler;
                }

                byte[] kwpResponseData = null;
                bool isFirstResponseChunk = true;
                int chunkCount = 0;
                int currentChunkNumber = 0;
                CANMessage responseChunk = null;
                EventHandler<CANMessageEventArgs> responseChunkCollector = delegate (object sender, CANMessageEventArgs args)
                {
                    responseChunk = args.Message;
                    gotMessage.Set();
                };

                // we don't need confirmation for the last chunk as the first chunk of the reply would be the confirmation
                requestChunk = reqChunkQueue.Dequeue();
                gotMessage.Reset();
                canDevice.OnCANMessageWithId[responseMessageId] += responseChunkCollector;
                canDevice.SendMessage(requestChunk);

                do
                {
                    if (!gotMessage.Wait(timeout))
                    {
                        canDevice.OnCANMessageWithId[responseMessageId] -= responseChunkCollector;
                        return new KWPNegativeResponse(request.ServiceId, KWPNegativeResponseCode.GENERAL_REJECT);
                    }
                    canDevice.OnCANMessageWithId[responseMessageId] -= responseChunkCollector;

                    currentChunkNumber = responseChunk.Data[0] & 0x3F;
                    if (isFirstResponseChunk)
                    {
                        chunkCount = currentChunkNumber + 1;
                        kwpResponseData = new byte[responseChunk.Data[2] + 1];

                        isFirstResponseChunk = false;
                    }

                    int position = 6 * (chunkCount - currentChunkNumber - 1);
                    int remainingCapacity = kwpResponseData.Length - position;

                    Array.Copy(responseChunk.Data, 2, kwpResponseData, position, remainingCapacity > 6 ? 6 : remainingCapacity);
                    // TODO: check response chunk order

                    //if ((data0 & 0x80) != 0)
                    //{
                    CANMessage confirmation = new CANMessage(RESP_CHUNK_CONF_ID, new byte[] { 0x40, (byte)0xA1, 0x3F, (byte)(responseChunk.Data[0] & ~0x40), 0x00, 0x00, 0x00, 0x00 });
                    gotMessage.Reset();
                    canDevice.OnCANMessageWithId[responseMessageId] += responseChunkCollector;
                    canDevice.SendMessage(confirmation);
                    //}
                } while ((currentChunkNumber & 0x3F) != 0);

                return KWPResponse.FromBytes(kwpResponseData);
            }
        }

        private static Queue<CANMessage> SplitRequest(KWPRequest request)
        {
            Queue<CANMessage> result = new Queue<CANMessage>();
            byte[] data = request.ToBytes();

            int msgCount = (data.Length + 6 - 1) / 6;
            byte flag;

            for (int i = 0; i < msgCount; i++)
            {
                byte[] msgData = new byte[8];
                
                flag = 0;

                if (i == 0)
                    flag |= 0x40; // this is the first data chunk 

                if (i != msgCount - 1)
                    flag |= 0x80; // we want confirmation for every chunk except the last one

                msgData[0] = (byte)(flag | ((msgCount - i - 1) & 0x3F)); // & 0x3F is not necessary, only to show that this field is 6-bit wide
                msgData[1] = 0xA1;


                int start = 6 * i;
                int count = (data.Length - start) < 6 ? (data.Length - start) : 6;

                Array.Copy(data, start, msgData, 2, count);
                for (int j = 0; j < count; j++)
                {
                    msgData[2 + j] = data[start + j];
                }

                CANMessage msg = new CANMessage(REQ_MSG_ID, msgData);
                result.Enqueue(msg);
            }

            return result;
        }

        private ICANDevice canDevice;
        private UInt32 responseMessageId;

        private static UInt32 INIT_MSG_ID = 0x222; // most of CAN units expect message 0x22*, 0x220 works for Trionic, but 0x222 is a more strict value
        private static UInt32 REQ_MSG_ID = 0x242; // same here, 0x24* would be ok, but 0x242 is a little bit more correct
        private static UInt32 INIT_RESP_ID = 0x238; // strictly 0x238
        private static UInt32 REQ_CHUNK_CONF_ID = 0x270;
        private static UInt32 RESP_CHUNK_CONF_ID = 0x266;
    }
}
