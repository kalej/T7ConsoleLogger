using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CANLib;
using CombiLib;
using ECULogging;
using KWP;

namespace T7ConsoleLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Usage();
                return;
            }

            ICANDevice canAdapter = null;
            T7KWPCANAdapter kwpAdapter;
            KWPResponse getVarData;
            KWPPositiveResponse varData;

            CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs cancelArgs)
            {
                cts.Cancel();
                cancelArgs.Cancel = true;
            };

            try
            {
                
                LogConfig logConfig = LogConfig.LoadXML(args[0]);
                Console.WriteLine("Config loaded. Total vars: {0}; total length: {1}", logConfig.TotalVarCount, logConfig.TotalVarLength);

                canAdapter = new CombiAdapter();
                canAdapter.Open(500000);

                CANMonitor monitor = new CANMonitor(canAdapter, logConfig.CANGuards);
                monitor.CheckGuards();

                kwpAdapter = new T7KWPCANAdapter(canAdapter);
                KWPResponse initResponse = kwpAdapter.SendReceive(new KWPStartCommunicationRequest());
                if (initResponse == null)
                    throw new Exception("Did not receive reply for init message");

                if (initResponse is KWPNegativeResponse)
                    throw new Exception("Got negative response for init message");

                KWPResponse defineVariableResponse;
                for(int i = 0; i < logConfig.VarDefinitions.Length; i++)
                {
                    Console.WriteLine("Defining {0}", logConfig.VarDefinitions[i].Name);
                    defineVariableResponse = kwpAdapter.SendReceive(new KWPDynamicallyDefineLocalIdRequest((byte)i, logConfig.VarDefinitions[i]));

                    if (defineVariableResponse == null)
                        throw new Exception("Did not receive reply for dinamically define variable request");

                    if (defineVariableResponse is KWPNegativeResponse)
                        throw new Exception(String.Format("Could not dynamically define variable {0}. Check config.", logConfig.VarDefinitions[i].Name));
                }

                Console.WriteLine("Reading data");
                getVarData = kwpAdapter.SendReceive(new KWPReadDynamicallyDefinedDataRequest());

                if (getVarData == null)
                    throw new Exception("Did not receive reply for read dinamically defined variables request.");

                if (getVarData is KWPNegativeResponse)
                {
                    KWPNegativeResponse nr = getVarData as KWPNegativeResponse;
                    throw new Exception(String.Format("Could not read dynamically defined variables: {0}.", nr.Code.ToString()));
                }

                varData = getVarData as KWPPositiveResponse;
                if (varData.Data.Length != logConfig.TotalVarLength + 1)
                    throw new Exception("Reply length did not match the total length of variables from config.");

                ConcurrentQueue<LogEntryData> queue = new ConcurrentQueue<LogEntryData>();
                Thread kwpThread = new Thread(() => KWPThread(kwpAdapter, queue, logConfig, cts.Token));
                kwpThread.Priority = ThreadPriority.Highest;

                Thread sqliteThread = new Thread(() => SQLiteThread(Path.GetFileNameWithoutExtension(args[0]), logConfig, queue, cts.Token));
                monitor.StartMonitoring();
                sqliteThread.Start();
                kwpThread.Start();
                
                kwpThread.Join();
                sqliteThread.Join();

                if (!cts.IsCancellationRequested)
                    cts.Cancel();
            }
            catch(Exception ex)
            {
                Warn(ex.Message);
            }
            finally
            {
                if (canAdapter != null)
                {
                    try
                    {
                        canAdapter.Close();
                    }
                    catch (Exception)
                    {

                    }
                }

                Inform("Finished");

                Environment.Exit(0);
            }
        }

        private static void KWPThread(T7KWPCANAdapter kwpAdapter, ConcurrentQueue<LogEntryData> dataQueue, LogConfig logConfig, CancellationToken ct)
        {
            KWPReadDynamicallyDefinedDataRequest request = new KWPReadDynamicallyDefinedDataRequest();
            KWPResponse response;
            DateTime start;
       
            while(!ct.IsCancellationRequested)
            {
                start = DateTime.Now;
                response = kwpAdapter.SendReceive(request);

                if (response == null)
                {
                    Warn("No KWP response");
                    continue;
                }

                if (response is KWPNegativeResponse)
                {
                    KWPNegativeResponse nr = response as KWPNegativeResponse;
                    Warn($"Negative KWP response: {nr.Code}");
                    continue;
                }

                dataQueue.Enqueue(new LogEntryData() { Timestamp = DateTime.Now, Response = response as KWPPositiveResponse });


                int delta = (int)(DateTime.Now - start).TotalMilliseconds;
                int sleep = logConfig.PeriodMs - delta;
                Thread.Sleep(sleep > 0 ? sleep : 0);
            }

            Inform("KWP thread finished");
        }

        private static void SQLiteThread(string filename, LogConfig config, ConcurrentQueue<LogEntryData> dataQueue, CancellationToken ct)
        {
            SqliteCollector collector = new SqliteCollector(filename, config);
            LogEntryData entryData;
            while (true)
            {
                while (dataQueue.TryDequeue(out entryData))
                {
                    collector.InsertData(entryData.Timestamp, entryData.Response.Data, 1);
                }

                Thread.Sleep(1000);

                if ((dataQueue.Count == 0) && ct.IsCancellationRequested)
                    break;
            }

            Inform("SQLite thread finished");
        }

        public static void Inform(string text)
        {
            Console.Write("[+] ");
            Console.WriteLine(text);
        }

        public static void Warn(string text)
        {
            Console.Write("[-] ");
            Console.WriteLine(text);
        }

        private static void Usage()
        {
            Console.WriteLine($"Usage: {typeof(Program).Assembly.GetName().Name} <config file>");
        }

        private class LogEntryData 
        {
            public DateTime Timestamp { get; set; }  
            public KWPPositiveResponse Response { get; set; }
        }
    }
}
