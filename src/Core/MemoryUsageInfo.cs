using System;

namespace MicroDude.Models
{
    public class MemoryUsageInfo
    {
        public int FlashUsed { get; set; }
        public int FlashTotal { get; set; }
        public int EepromUsed { get; set; }
        public int EepromTotal { get; set; }

        public double FlashPercent => FlashTotal > 0 ? (double)FlashUsed / FlashTotal * 100 : 0;
        public double EepromPercent => EepromTotal > 0 ? (double)EepromUsed / EepromTotal * 100 : 0;

        public string GetFlashUsageString()
        {
            return $"{FlashUsed:N0}B / {FlashTotal:N0}B ({FlashPercent:F1}%)";
        }

        public string GetEepromUsageString()
        {
            return $"{EepromUsed:N0}B / {EepromTotal:N0}B ({EepromPercent:F1}%)";
        }
    }
}