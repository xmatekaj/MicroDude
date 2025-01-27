namespace MicroDude.Parsers
{
    public static class AvrdudeConfConstants
    {
        public const string ProgrammerStart = "programmer";
        public const string ProgrammerStartWithHash = "programmer # ";
        public const string MicrocontrollerStart = "part";
        public const string MicrocontrollerPattern = @"part\s+parent\s+""(.+)""\s+#\s+(.+)";
        public const string CommonValuesSection = "Common values";
        public const string BlockEnd = ";";
        public const string IdKey = "id";
        public const string DescKey = "desc";
        public const string UsbVid = "usbvid";
        public const string UsbPid = "usbpid";
        public const string IdPattern = @"id\s*=\s*(.+?);";
        public const string DescPattern = @"desc\s*=\s*""([^""]+)"";";
        public const string PropertyPattern = @"(\w+)\s*=\s*(.+);";
        public const string UsbVidPattern = @"usbvid\s*=\s*0x([0-9A-Fa-f]+);";
        public const string UsbPidPattern = @"usbpid\s*=\s*(0x[0-9A-Fa-f]+)(?:,\s*0x[0-9A-Fa-f]+)*;";
        public const string AvrdudeConfFileName = "avrdude.conf";
        public const string HeaderSection = "#----";
        public const string TypeKey = "type";
        public const string TypePattern = @"type\s*=\s*(.+);";
    }
}