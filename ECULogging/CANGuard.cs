using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ECULogging
{
    [Serializable]
    [XmlType("canguard")]
    public class CANGuard
    {
        public CANGuard()
        {
            this.Id = 0xFFFFFFFF;
            this.MaxDelay = 0xFFFFFFF;
        }

        public CANGuard(UInt32 id, uint maxDelay)
        {
            this.Id = id;
            this.MaxDelay = maxDelay;
        }

        public bool Validate()
        {
            return (0 <= this.Id) && (this.Id < 0x800) && (this.MaxDelay > 0);
        }

        [XmlIgnore]
        public UInt32 Id;

        [XmlAttribute("id")]
        public string HexId
        {
            get
            {
                return Id.ToString("X");
            }
            set
            {
                Id = Convert.ToUInt32(value, 16); // int.Parse(value, System.Globalization.NumberStyles.HexNumber);
            }
        }

        [XmlAttribute("maxdelay")]
        public uint MaxDelay;
    }
}
