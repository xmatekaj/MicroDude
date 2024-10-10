using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using MicroDude.Core;
using MicroDude.Models;

namespace MicroDude.Parsers

{
    public class MicrocontrollerParser
    {
        private XDocument _doc;

        public MicrocontrollerParser(string xmlFilePath)
        {
            _doc = XDocument.Load(xmlFilePath);
        }

        public List<FuseBitOption> GetFuseBitOptions(string fuseName)
        {
            var options = _doc.Descendants("value-group")
                .Where(vg => vg.Attribute("caption")?.Value.Contains(fuseName) == true)
                .Descendants("value")
                .Select(v => new FuseBitOption
                {
                    Caption = v.Attribute("caption")?.Value,
                    Name = v.Attribute("name")?.Value,
                    Value = v.Attribute("value")?.Value
                })
                .ToList();

            return options;
        }

        public List<string> GetAvailableFuses()
        {
            return _doc.Descendants("value-group")
                .Select(vg => vg.Attribute("caption")?.Value)
                .Where(caption => caption != null && caption.Contains("Fuse"))
                .Distinct()
                .ToList();
        }

        public Dictionary<string, string> GetMicrocontrollerInfo()
        {
            var device = _doc.Descendants("device").FirstOrDefault();
            if (device == null) return null;

            return new Dictionary<string, string>
            {
                {"Name", device.Attribute("name")?.Value},
                {"Architecture", device.Attribute("architecture")?.Value},
                {"Family", device.Attribute("family")?.Value}
            };
        }

        public List<InterruptInfo> GetInterrupts()
        {
            return _doc.Descendants("interrupt")
                .Select(i => new InterruptInfo
                {
                    Index = int.Parse(i.Attribute("index")?.Value ?? "-1"),
                    Name = i.Attribute("name")?.Value,
                    Caption = i.Attribute("caption")?.Value
                })
                .ToList();
        }

        public List<ModuleInfo> GetModules()
        {
            return _doc.Descendants("module")
                .Select(m => new ModuleInfo
                {
                    Name = m.Attribute("name")?.Value,
                    Caption = m.Attribute("caption")?.Value
                })
                .ToList();
        }
    }


    public class InterruptInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Caption { get; set; }
    }

    public class ModuleInfo
    {
        public string Name { get; set; }
        public string Caption { get; set; }
    }
}