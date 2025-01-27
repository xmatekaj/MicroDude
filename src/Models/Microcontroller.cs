using System.Collections.Generic;

namespace MicroDude.Models
{
    public class Microcontroller
    {
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Description { get; set; }
        public string Signature { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        // Memory configuration
        public int FlashSize { get; set; }  // Total flash size in bytes
        public int FlashPageSize { get; set; }  // Flash page size for programming
        public int EepromSize { get; set; }  // Total EEPROM size in bytes
        public int EepromPageSize { get; set; }  // EEPROM page size for programming

        // Fuse and Lock bit configuration
        public List<FuseRegister> FuseRegisters { get; set; }
        public List<LockbitRegister> LockbitRegisters { get; set; }
        public Dictionary<string, ValueGroup> ValueGroups { get; set; }

        public Microcontroller()
        {
            Properties = new Dictionary<string, string>();
            FuseRegisters = new List<FuseRegister>();
            LockbitRegisters = new List<LockbitRegister>();
            ValueGroups = new Dictionary<string, ValueGroup>();
        }
    }

    public abstract class ConfigRegister
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        public int Offset { get; set; }
        public int Size { get; set; }
        public byte InitValue { get; set; }
        public List<Bitfield> Bitfields { get; set; } = new List<Bitfield>();
    }

    public class FuseRegister : ConfigRegister
    {
    }

    public class LockbitRegister : ConfigRegister
    {
    }

    public class Bitfield
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        public byte Mask { get; set; }
        public string ValueGroupName { get; set; }
    }

    public class ValueGroup
    {
        public string Name { get; set; }
        public List<ConfigValue> Values { get; set; } = new List<ConfigValue>();
    }

    public class ConfigValue
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        public byte Value { get; set; }
    }
}