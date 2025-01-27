using System.Collections.Generic;
using System.Linq;

namespace MicroDude.Models
{
    public enum ProgrammerPortType
    {
        USB,   
        COM    
        //LPT     // LPT is not supported on Windows by AvrDude
    }

    public class Programmer
    {
        public string Id { get; set; }
        public List<string> AlternativeIds { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string UsbVid { get; set; }
        public List<string> UsbPids { get; set; }
        public ProgrammerPortType PortType { get; set; }
        public int BaudRate { get; set; }          
        public int BitClockCount { get; set; }   
        public int Order { get; set; }

        public Programmer()
        {
            AlternativeIds = new List<string>();
            UsbPids = new List<string>();
            PortType = ProgrammerPortType.USB;
            BaudRate = 0;
            BitClockCount = 0;
        }

        public override string ToString()
        {
            return $"{Id} - {Description}";
        }

        public List<string> GetVidPidStrings()
        {
            if (string.IsNullOrEmpty(UsbVid) || !UsbPids.Any())
                return new List<string>();

            return UsbPids.Select(pid => string.Format("VID_{0}&PID_{1}", UsbVid, pid)).ToList();
        }
    }
}