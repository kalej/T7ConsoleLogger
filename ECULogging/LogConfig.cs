using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace ECULogging
{
    [Serializable]
    [XmlRoot("config")]
    public class LogConfig
    {
        public LogConfig()
        {
            PeriodMs = 1000;
            VarDefinitions = null;
            CANGuards = null;
        }

        public LogConfig(int period, VarDefinition[] varDefinitions, CANGuard[] guards)
        {
            PeriodMs = period;
            VarDefinitions = varDefinitions;
            CANGuards = guards;
        }

        public static LogConfig LoadXML(string path)
        {
            LogConfig result = null;
            XmlSerializer serializer = new XmlSerializer(typeof(LogConfig), "");
            
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                result = (LogConfig)serializer.Deserialize(fs);
            }

            return result;
        }

        public int TotalVarCount
        {
            get { return VarDefinitions.Length; }
        }

        public int TotalVarLength
        {
            get
            {
                int result = 0;
                foreach (VarDefinition vardef in VarDefinitions)
                    result += vardef.Length;

                return result;
            }
        }

        public bool Validate()
        {
            if (TotalVarCount > 60)
                throw new Exception("Total variable count is above 60");

            if (TotalVarLength > 248)
                throw new Exception("Total variable size is above 248");

            foreach (VarDefinition vardef in VarDefinitions)
                if (!vardef.Validate())
                    throw new Exception($"Variable {vardef.Name} definition is invalid");

            foreach (CANGuard canGuard in CANGuards)
                if (!canGuard.Validate())
                    throw new Exception($"CAN guard for message {canGuard.HexId} definition is invalid");

            return true;
        }

        [XmlAttribute("period")]
        public int PeriodMs;
        
        [XmlArray("vars")]
        public VarDefinition[] VarDefinitions;

        [XmlArray("guards")]
        public CANGuard[] CANGuards;
    }
}
