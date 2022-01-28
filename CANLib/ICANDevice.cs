using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CANLib
{
    public abstract class ICANDevice
    {
        public abstract void Open(int bitrate);
        public abstract void Close();
        public abstract void SendMessage(CANMessage message);

        public abstract string GetVersion();

        public void InitCanListener(UInt32 Id)
        {
            // just add an entry to easily += and -= delegates later
            if (!OnCANMessageWithId.ContainsKey(Id))
            {
                OnCANMessageWithId.Add(Id, null);
            }
        }

        public EventHandler<CANMessageEventArgs> OnAnyCANMessage;
        public EventHandler<CANMessageEventArgs> OnCANMessageSent;
        public Dictionary<UInt32, EventHandler<CANMessageEventArgs>> OnCANMessageWithId = new Dictionary<UInt32, EventHandler<CANMessageEventArgs>>();
    }
}
