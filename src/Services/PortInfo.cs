using System;
using System.Collections.Generic;
using System.Management;
using System.Linq;
using MicroDude.Models;

namespace MicroDude.Services
{
    /// <summary>
    /// Port Info model. 
    /// </summary>
    public class PortInfo
    {
        /// <summary>
        /// Name of the port, example COM1 or LPT1.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Full description from Windows.
        /// </summary>
        public string Description { get; set; }     

        /// <summary>
        /// Returns port in format, ex. "COM1 (description)"
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Name} ({Description})";
        }
    }

    /// <summary>
    /// Service to get avaiable serial and parallel ports visible in the system.
    /// </summary>
    public class PortService
    {
        private static readonly object _lock = new object();
        private static volatile PortService _instance;

        public static PortService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PortService();
                        }
                    }
                }
                return _instance;
            }
        }

        public List<PortInfo> GetAvailablePorts(ProgrammerPortType portType)
        {
            var ports = new List<PortInfo>();

            try
            {
                string portPattern = portType == ProgrammerPortType.COM ? "COM" : "LPT";

                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%{portPattern}%'"))
                {
                    foreach (ManagementObject port in searcher.Get())
                    {
                        string name = port["Name"]?.ToString();
                        if (string.IsNullOrEmpty(name)) continue;

                        string portName = ExtractPortName(name, portPattern);
                        if (!string.IsNullOrEmpty(portName))
                        {
                            ports.Add(new PortInfo
                            {
                                Name = portName,
                                Description = name
                            });

                            Logger.Log($"Found {portType} port: {portName} ({name})");
                        }
                    }
                }

                // Sort ports by number
                ports = ports.OrderBy(p =>
                {
                    int number;
                    return int.TryParse(p.Name.Substring(portPattern.Length), out number) ? number : 999;
                }).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting available ports: {ex.Message}");
            }

            return ports;
        }

        private string ExtractPortName(string fullName, string portType)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                fullName, $@"({portType}\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}