using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MicroDude.Models
{
    public class Programmer
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string ConnectionType { get; set; }

        public override string ToString()
        {
            //return $"{Id} - {Description}";
            return Id;
        }
    }
}
