using System;
using System.Text.RegularExpressions;
using MicroDude.Models;

namespace MicroDude.Parsers
{
    public class MemoryUsageParser
    {
        private static readonly Regex ProgramMemoryRegex = new Regex(@"Program Memory Usage\s*:\s*(\d+)\s*bytes\s*([\d,.]+)\s*%\s*Full", RegexOptions.Compiled);
        private static readonly Regex DataMemoryRegex = new Regex(@"Data Memory Usage\s*:\s*(\d+)\s*bytes\s*([\d,.]+)\s*%\s*Full", RegexOptions.Compiled);
        private static readonly Regex WarningRegex = new Regex(@"Warning:\s*Memory Usage estimation", RegexOptions.Compiled);

        public static MemoryUsageInfo ParseBuildOutput(string buildOutput, int flashTotal, int eepromTotal)
        {
            if (string.IsNullOrEmpty(buildOutput))
                return null;

            var info = new MemoryUsageInfo
            {
                FlashTotal = flashTotal,
                EepromTotal = eepromTotal
            };

            // Parse Program Memory (Flash) Usage
            var programMatch = ProgramMemoryRegex.Match(buildOutput);
            if (programMatch.Success)
            {
                info.FlashUsed = int.Parse(programMatch.Groups[1].Value);
            }

            // Parse Data Memory (EEPROM) Usage
            var dataMatch = DataMemoryRegex.Match(buildOutput);
            if (dataMatch.Success)
            {
                info.EepromUsed = int.Parse(dataMatch.Groups[1].Value);
            }

            return info;
        }

        public static string FormatMemoryUsage(MemoryUsageInfo info)
        {
            if (info == null)
                return "Memory usage information not available";

            return $"Memory Usage:{Environment.NewLine}" +
                   $"Flash: {info.GetFlashUsageString()}{Environment.NewLine}" +
                   $"EEPROM: {info.GetEepromUsageString()}";
        }
    }
}