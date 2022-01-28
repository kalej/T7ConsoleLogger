using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ECULogging
{
    [Serializable]
    [XmlType("var")]
    public class VarDefinition
    {
        public VarDefinition() : this(null, null, false, null, -1) { }
        public VarDefinition(string name, string type, bool signed, string method, int value)
        {
            this.Name = name;
            this.Type = type;
            this.Signed = signed;
            this.Method = method;
            this.Value = value;
        }

        public long FromBytes(byte[] data, int offset)
        {
            if (Type == "byte")
            {
                if (Signed)
                {
                    return (sbyte)data[offset];
                }
                else
                {
                    return data[offset];
                }
            }

            int length = Length;
            byte[] tmp = new byte[length];
            Array.Copy(data, offset, tmp, 0, length);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tmp);

            switch (Type)
            {
                case "long":
                    return Signed ? (long)BitConverter.ToInt32(tmp, 0) : (long)BitConverter.ToUInt32(tmp, 0);
                case "word":
                    return Signed ? (long)BitConverter.ToInt16(tmp, 0) : (long)BitConverter.ToUInt16(tmp, 0);
            }
            
            throw new Exception($"Unknown data type {Type} declared for variable {Name}");
        }

        public bool Validate()
        {
            switch (this.Method)
            {
                case "address":
                    return (0xF00000 <= this.Value) && (this.Value < 0xF10000);
                case "locid":
                    return (0 <= this.Value) && (this.Value < 256);
                case "symbol":
                    return (0 < this.Value) && (this.Value < 65536);
            }

            return false;
        }

        public int Length
        {
            get
            {
                if (length != 0)
                    return length;

                switch (Type)
                {
                    case "long":
                        length = 4;
                        break;
                    case "word":
                        length = 2;
                        break;
                    case "byte":
                        length = 1;
                        break;
                    default:
                        throw new Exception($"Variable {Name} has undefined type {Type}");
                }

                return length;
            }
        }
        [XmlIgnore]
        private int length = 0;

        [XmlAttribute("name")]
        public string Name;
        [XmlAttribute("type")]
        public string Type;
        [XmlAttribute("signed")]
        public bool Signed;
        [XmlAttribute("method")]
        public string Method;

        [XmlIgnore]
        public int Value;

        [XmlAttribute("value")]
        public string HexValue
        {
            get
            {
                return Value.ToString("X");
            }
            set
            {
                if (value.ToLower().StartsWith("0x"))
                    Value = Convert.ToInt32(value, 16);
                else
                    Value = Convert.ToInt32(value, 10);
            }
        }
    }
}
