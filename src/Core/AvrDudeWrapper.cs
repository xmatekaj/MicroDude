using MicroDude.Properties;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MicroDude.Core
{
    public class AvrDudeResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public string Command { get; set; }  

    }

    public class AvrDudeWrapper
    {
        private string _avrDudePath;
        private string _configFilePath;

        public AvrDudeWrapper(string avrDudePath, string configFilePath)
        {
            if (!File.Exists(avrDudePath))
                throw new FileNotFoundException("AvrDude executable not found", avrDudePath);

            if (!File.Exists(configFilePath))
                throw new FileNotFoundException("AvrDude configuration file not found", configFilePath);

            _avrDudePath = avrDudePath;
            _configFilePath = configFilePath;
        }

        public AvrDudeResult ExecuteCommand(string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.FileName = _avrDudePath;
                process.StartInfo.Arguments = $"-C \"{_configFilePath}\" {arguments}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                process.OutputDataReceived += (sender, e) => {
                    if (e.Data != null)
                        output.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) => {
                    if (e.Data != null)
                        error.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (MicroDudeSettings.Default.Verbose)
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Calling AvrDude:\n{_avrDudePath} -C \"{_configFilePath}\" {arguments}");
                }

                return new AvrDudeResult
                {
                    Success = process.ExitCode == 0,
                    Output = output.ToString(),
                    Error = error.ToString(),
                    Command = $"{_avrDudePath} -C \"{_configFilePath}\" {arguments}"  
                };
            }
        }

        public AvrDudeResult ReadFuses(string partNo, string programmer, string port)
        {
            string arguments = string.Format("-p {0} -c {1} -P {2} -U lfuse:r:-:h -U hfuse:r:-:h -U efuse:r:-:h", partNo, programmer, port);
            return ExecuteCommand(arguments);
        }

        public AvrDudeResult WriteFuses(string partNo, string programmer, string port, string lfuse = null, string hfuse = null, string efuse = null)
        {
            var argumentBuilder = new System.Text.StringBuilder();

            argumentBuilder.AppendFormat("-p {0} -c {1} -P {2}", partNo, programmer, port);

            if (!string.IsNullOrEmpty(lfuse))
                argumentBuilder.AppendFormat(" -U lfuse:w:{0}:m", lfuse);

            if (!string.IsNullOrEmpty(hfuse))
                argumentBuilder.AppendFormat(" -U hfuse:w:{0}:m", hfuse);

            if (!string.IsNullOrEmpty(efuse))
                argumentBuilder.AppendFormat(" -U efuse:w:{0}:m", efuse);

            return ExecuteCommand(argumentBuilder.ToString());
        }

        public AvrDudeResult FlashFirmware(string partNo, string programmer, string port, string firmwarePath)
        {
            string arguments = string.Format("-p {0} -c {1} -P {2} -U flash:w:\"{3}\":i", partNo, programmer, port, firmwarePath);
            return ExecuteCommand(arguments);
        }

        public AvrDudeResult VerifyFirmware(string partNo, string programmer, string port, string firmwarePath)
        {
            string arguments = string.Format("-p {0} -c {1} -P {2} -U flash:v:\"{3}\":i", partNo, programmer, port, firmwarePath);
            return ExecuteCommand(arguments);
        }

        public AvrDudeResult EraseChip(string partNo, string programmer, string port)
        {
            string arguments = string.Format("-p {0} -c {1} -P {2} -e", partNo, programmer, port);
            return ExecuteCommand(arguments);
        }

        public AvrDudeResult GetSignature(string programmer, string port)
        {
            string arguments = string.Format("-p m8 -c {0} -P {1} -v", programmer, port);
            return ExecuteCommand(arguments);
        }

        public AvrDudeResult CalibrateOscillator(string partNo, string programmer, string port)
        {
            string arguments = string.Format("-p {0} -c {1} -P {2} -O", partNo, programmer, port);
            return ExecuteCommand(arguments);
        }
    }
}