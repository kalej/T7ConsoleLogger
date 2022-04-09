using CANLib;
using CombiLib;
using KWP;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCMDumper
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ICANDevice canAdapter = new CombiAdapter();
            canAdapter.Open(500000);

            TCMKWPCANAdapter kwpAdapter = new TCMKWPCANAdapter(canAdapter);
            KWPResponse response = kwpAdapter.SendReceive(new KWPStartCommunicationRequest());

            if (response is KWPPositiveResponse)
            {
                for (byte ecuId = 0x80; ecuId < 0xA0; ecuId++)
                {
                    response = kwpAdapter.SendReceive(new KWPReadEcuIdentificationRequest(ecuId));
                    if (response is KWPPositiveResponse)
                    {
                        KWPPositiveResponse positiveResponse = response as KWPPositiveResponse;
                        byte[] respData = positiveResponse.Data;
                        byte[] unitData = new byte[respData.Length - 1];
                        Array.Copy(respData, 1, unitData, 0, unitData.Length);

                        Console.Write($"{ecuId:X02}: ");
                        Console.WriteLine(Encoding.ASCII.GetString(unitData));
                    }
                }
                
                response = kwpAdapter.SendReceive(new KWPSecurityAccessRequest(1));

                if (response is KWPPositiveResponse)
                {
                    KWPPositiveResponse positiveResponse = response as KWPPositiveResponse;
                    byte[] seed = new byte[2];
                    Array.Copy(positiveResponse.Data, 1, seed, 0, seed.Length);

                    int key = SecAccTcm(seed);

                    if (positiveResponse != null)
                    {
                        response = kwpAdapter.SendReceive(new KWPSecurityAccessRequest(2, key));

                        if (response is KWPPositiveResponse)
                        {
                            positiveResponse = response as KWPPositiveResponse;

                            if (positiveResponse.Data[1] == 0x34)
                            {
                                using (FileStream fs = new FileStream("tmp_tcm.bin", FileMode.Create))
                                {
                                    DateTime start = DateTime.Now;

                                    int total = 3 * 128 * 1024;
                                    int received = 0;
                                    response = kwpAdapter.SendReceive(new KWPRequestUpload(0, total));

                                    if (response is KWPPositiveResponse)
                                    {
                                        do
                                        {
                                            response = kwpAdapter.SendReceive(new KWPTransferData());

                                            positiveResponse = response as KWPPositiveResponse;

                                            if (positiveResponse == null)
                                            {
                                                KWPNegativeResponse negativeResponse = response as KWPNegativeResponse;
                                                if (negativeResponse != null)
                                                {
                                                    Console.WriteLine($"Error: {negativeResponse.Code}");
                                                }
                                                else
                                                {
                                                    Console.WriteLine("WTF?!");
                                                }
                                                break;
                                            }

                                            byte[] respData = positiveResponse.Data;
                                            fs.Write(respData, 3, respData.Length - 3);
                                            received += respData.Length - 3;

                                            Console.WriteLine($"Total: {received}");
                                        } while (received < total);
                                    }

                                    /*

                                    for (int i = 0; i < 3*128*1024; i+= 128)
                                    {
                                        response = kwpAdapter.SendReceive(new KWPReadMemoryByAddressRequest(i, 128));

                                        positiveResponse = response as KWPPositiveResponse;

                                        if (positiveResponse == null)
                                        {
                                            KWPNegativeResponse negativeResponse = response as KWPNegativeResponse;
                                            if (negativeResponse != null)
                                            {
                                                Console.WriteLine($"Error: {negativeResponse.Code}");
                                            }
                                            else
                                            {
                                                Console.WriteLine("WTF?!");
                                            }
                                            break;
                                        }

                                        byte[] respData = positiveResponse.Data;
                                        fs.Write(respData, 0, respData.Length);
                                        totalBytes += respData.Length;

                                        Console.WriteLine($"Total: {totalBytes}");
                                    }
                                    */
                                    TimeSpan span = DateTime.Now - start;
                                    Console.WriteLine($"Dumped in {span.TotalMinutes}m ({span.TotalSeconds})");
                                }
                            }
                            else
                                Console.WriteLine($"Error: {positiveResponse.Data[1]:X02}");
                        }
                    }

                    /*
                    if (positiveResponse.Data[1] == 0x4D && positiveResponse.Data[2] == 0x4C)
                    {
                        response = kwpAdapter.SendReceive(new KWPSecurityAccessRequest(2, 0x4257));

                        if (response is KWPPositiveResponse)
                        {
                            positiveResponse = response as KWPPositiveResponse;

                            if (positiveResponse.Data[1] == 0x34)
                                Console.WriteLine("Got security access");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Got security access seed: {positiveResponse.Data[1]:X02}{positiveResponse.Data[2]:X02}");
                    }
                    */


                }
                else
                {

                }
            }
            canAdapter.Close();
            while (true) ;
        }

        static int SecAccT7_1(byte[] seed)
        {
            UInt16 key = (ushort)((seed[0] << 8) | seed[1]);
            key <<= 2;
            key ^= 0x4081;
            key -= 0x1F6F;

            return key;
        }

        static int SecAccT7_2(byte[] seed)
        {
            UInt16 key = (ushort)((seed[0] << 8) | seed[1]);
            key <<= 2;
            key ^= 0x8142;
            key -= 0x2356;

            return key;
        }

        static int SecAccTcm(byte[] seed)
        {
            return 0x4257;
        }
    }
}
