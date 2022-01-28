using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CANLib
{
    public class CANMessage
    {
        public CANMessage(UInt32 id, byte[] data)
        {
            this.Id = id;
            this.Data = data;
        }

        public CANMessage(UInt32 id)
        {
            this.Id = id;
            this.data = new byte[DEFAULT_LENGTH];
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0:X03}: ", this.Id);
            foreach(byte b in this.Data)
                sb.AppendFormat("{0:X02}", b);

            return sb.ToString();
        }

        public UInt32 Id
        {
            get;
        }

        public byte[] Data
        {
            get
            {
                return data;
            }

            set 
            {
                this.data = new byte[value.Length];
                Array.Copy(value, this.data, value.Length);
            }
        }

        private byte[] data;
        private static int DEFAULT_LENGTH = 8;
    }

    public class CANMessageEventArgs : EventArgs
    {
        public CANMessageEventArgs(CANMessage message)
        {
            this.Message = message;
        }

        public CANMessage Message
        {
            get;
            private set;
        }
    }
}
