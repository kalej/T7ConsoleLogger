using CANLib;
using ECULogging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace T7ConsoleLogger
{
    class CANMonitor
    {
        public CANMonitor(ICANDevice canDevice, ICollection<CANGuard> guards)
        {
            foreach(CANGuard guard in guards)
            {
                maxTime.Add(guard.Id, guard.MaxDelay);
            }

            this.canDevice = canDevice;
        }

        public bool CheckGuards(int timeout=5000)
        {
            Dictionary<UInt32, ManualResetEvent> delayFound = new Dictionary<uint, ManualResetEvent>();
            Dictionary<UInt32, DateTime> firstAppearance = new Dictionary<uint, DateTime>();
            Dictionary<UInt32, UInt32> observedDelay = new Dictionary<uint, uint>();

            Program.Inform("Checking CAN guards");

            foreach(UInt32 id in maxTime.Keys)
            {
                delayFound.Add(id, new ManualResetEvent(false));
            }

            EventHandler<CANMessageEventArgs> handler = delegate (object sender, CANMessageEventArgs args)
            {
                CANMessage message = args.Message;
                if (!maxTime.ContainsKey(message.Id))
                    return;

                if (!firstAppearance.ContainsKey(message.Id))
                {
                    firstAppearance.Add(message.Id, DateTime.Now);
                }
                else if (!observedDelay.ContainsKey(message.Id))
                {
                    observedDelay.Add(message.Id, (uint)(DateTime.Now - firstAppearance[message.Id]).TotalMilliseconds);
                    delayFound[message.Id].Set();
                }
            };

            canDevice.OnAnyCANMessage += handler;
            WaitHandle.WaitAll(delayFound.Values.ToArray(), timeout);
            canDevice.OnAnyCANMessage -= handler;

            bool foundErrors = false;
            Dictionary<UInt32, UInt32> newMaxTimes = new Dictionary<uint, uint>();
            foreach (UInt32 id in maxTime.Keys)
            {
                if (!firstAppearance.ContainsKey(id))
                {
                    Program.Inform(String.Format("Never observed CAN message #{0:X03} in {1} ms.", id, timeout));
                    continue;
                }

                if (!observedDelay.ContainsKey(id))
                {
                    Program.Warn(String.Format("Never observed two CAN messages #{0:X03} in {1} ms. Increase timeout!", id, timeout));
                    newMaxTimes.Add(id, (uint)timeout);
                    foundErrors = true;
                    continue;
                }

                if (observedDelay[id] > maxTime[id])
                {
                    Program.Warn(String.Format("Observed longer delay between two CAN messages #{0:X03}: {1} > {2}. Revise your config!", id, observedDelay[id], maxTime[id]));
                    newMaxTimes.Add(id, observedDelay[id] + 5);
                    foundErrors = true;
                    continue;
                }
            }

            maxTime = newMaxTimes;

            Program.Inform("CAN guards check " + (foundErrors?"failed":"OK"));
            if (foundErrors)
            {
                Program.Inform("New guards assigned:");
                foreach(UInt32 id in maxTime.Keys)
                {
                    Program.Inform(String.Format("\t{0:X03}: {1}", id, maxTime[id]));
                }

            }

            return !foundErrors;
        }

        public void StartMonitoring()
        {
            handler = delegate (object sender, CANMessageEventArgs args)
            {
                CANMessage message = args.Message;

                if (!maxTime.ContainsKey(message.Id))
                    return;

                if (!previousTime.ContainsKey(message.Id))
                {
                    previousTime.TryAdd(message.Id, DateTime.Now);
                    return;
                }

                DateTime now = DateTime.Now;
                uint delta = (uint)(now - previousTime[message.Id]).TotalMilliseconds;
                previousTime[message.Id] = now;
                if (delta > maxTime[message.Id])
                {
                    Program.Warn(String.Format("Observed longer delay between two CAN messages #{0:X03}: {1}.", message.Id, delta));
                }
            };

            canDevice.OnAnyCANMessage += handler;
        }

        public void StopMonitoring()
        {
            canDevice.OnAnyCANMessage -= handler;
        }

        private EventHandler<CANMessageEventArgs> handler;

        private Dictionary<UInt32, UInt32> maxTime = new Dictionary<uint, uint>();
        private ConcurrentDictionary<UInt32, DateTime> previousTime = new ConcurrentDictionary<uint, DateTime>();
        private ICANDevice canDevice;
    }
}
