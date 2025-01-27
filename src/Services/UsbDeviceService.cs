using System;
using System.Collections.Generic;
using System.Management;
using MicroDude.Models;
using System.Linq;
using MicroDude.Core;
using System.Text.RegularExpressions;
using System.Threading;

namespace MicroDude.Services
{
    #region ManagementObjectSearcher
    public interface IManagementObjectSearcher : IDisposable
    {
        IEnumerable<IManagementObject> Get();
    }

    public interface IManagementObject
    {
        object GetPropertyValue(string propertyName);
    }

    public class ManagementObjectSearcherWrapper : IManagementObjectSearcher
    {
        private readonly ManagementObjectSearcher _searcher;

        public ManagementObjectSearcherWrapper()
        {
            _searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0");
        }

        public IEnumerable<IManagementObject> Get()
        {
            foreach (ManagementObject obj in _searcher.Get())
            {
                yield return new ManagementObjectWrapper(obj);
            }
        }

        public void Dispose()
        {
            _searcher.Dispose();
        }
    }

    public class ManagementObjectWrapper : IManagementObject
    {
        private readonly ManagementObject _managementObject;

        public ManagementObjectWrapper(ManagementObject managementObject)
        {
            _managementObject = managementObject;
        }

        public object GetPropertyValue(string propertyName)
        {
            return _managementObject[propertyName];
        }
    }

    #endregion

    public class UsbDeviceService : IDisposable
    {
        private List<Programmer> _supportedProgrammers;
        private ManagementEventWatcher _usbWatcher;
        private readonly IManagementObjectSearcher _managementObjectSearcher;
        private Dictionary<string, DeviceInfo> knownDevices = new Dictionary<string, DeviceInfo>();
        private static object lockObj = new object();
        private static TimeSpan debounceInterval = TimeSpan.FromMilliseconds(1000);
        private static Dictionary<string, DateTime> lastEventTimes = new Dictionary<string, DateTime>();
        private HashSet<string> connectedProgrammerIds = new HashSet<string>(); // Add this line
        public event EventHandler<UsbDeviceChangeEventArgs> UsbDeviceChanged;

        public UsbDeviceService(List<Programmer> supportedProgrammers, IManagementObjectSearcher searcher = null)
        {
            _supportedProgrammers = supportedProgrammers;
            _managementObjectSearcher = searcher ?? new ManagementObjectSearcherWrapper();
            InitializeUsbWatcher();
        }

        private void InitializeUsbWatcher()
        {
            _usbWatcher = new ManagementEventWatcher();
            var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2 or EventType = 3");
            _usbWatcher.EventArrived += UsbWatcher_EventArrived;
            _usbWatcher.Query = query;
            _usbWatcher.Start();
        }

        private void UsbWatcher_EventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                Console.WriteLine($"### DeviceChangeEvent");
                UInt16 eventType = (UInt16)e.NewEvent["EventType"];
                string eventKey = $"{eventType}";

                if (ShouldProcessEvent(eventKey))
                {
                    Thread.Sleep(500);
                    ProcessUsbDevices();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in event handler: {ex.Message}");
            }
        }

        private void ProcessUsbDevices()
        {
            try
            {
                Dictionary<string, DeviceInfo> newDevices = GetCurrentUsbDevices();
                HashSet<string> newConnectedProgrammerIds = new HashSet<string>();
                List<Programmer> connectedProgrammers = new List<Programmer>();
                List<Programmer> disconnectedProgrammers = new List<Programmer>();

                // Log all found USB devices for debugging
                foreach (var device in newDevices.Values)
                {
                    var vidPidInfo = ExtractVidPidFromDeviceId(device.DeviceID);
                    if (vidPidInfo != null)
                    {
                        Logger.Log($"Found USB device: {device.Name}");
                        Logger.Log($"  DeviceID: {device.DeviceID}");
                        Logger.Log($"  VID: {vidPidInfo.Vid}, PID: {vidPidInfo.Pid}");
                    }
                }

                // Check for new connections
                foreach (string deviceId in newDevices.Keys)
                {
                    var device = newDevices[deviceId];
                    var programmer = _supportedProgrammers.FirstOrDefault(p => IsProgrammerMatch(p, device));
                    if (programmer != null)
                    {
                        newConnectedProgrammerIds.Add(programmer.Id);
                        if (!connectedProgrammerIds.Contains(programmer.Id))
                        {
                            connectedProgrammers.Add(programmer);
                            Logger.Log($"New programmer connected: {programmer.Id}");
                            var vidPidInfo = ExtractVidPidFromDeviceId(device.DeviceID);
                            Logger.Log($"  VID: {vidPidInfo?.Vid}, PID: {vidPidInfo?.Pid}");
                        }
                    }
                }

                // Check for disconnections
                foreach (string programmerId in connectedProgrammerIds)
                {
                    if (!newConnectedProgrammerIds.Contains(programmerId))
                    {
                        var programmer = _supportedProgrammers.FirstOrDefault(p => p.Id == programmerId);
                        if (programmer != null)
                        {
                            disconnectedProgrammers.Add(programmer);
                            Logger.Log($"Programmer disconnected: {programmer.Id}");
                        }
                    }
                }

                // Raise event if there were any changes
                if (connectedProgrammers.Any() || disconnectedProgrammers.Any())
                {
                    connectedProgrammerIds = newConnectedProgrammerIds;
                    Logger.Log($"Device change event - Connected: {connectedProgrammers.Count}, Disconnected: {disconnectedProgrammers.Count}");
                    UsbDeviceChanged?.Invoke(this, new UsbDeviceChangeEventArgs(
                        connectedProgrammers,
                        disconnectedProgrammers,
                        newConnectedProgrammerIds
                    ));
                }

                knownDevices = newDevices;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in ProcessUsbDevices: {ex.Message}");
            }
        }

        public void CheckForConnectedProgrammers()
        {
            ProcessUsbDevices();
        }

        private Dictionary<string, DeviceInfo> GetCurrentUsbDevices()
        {
            Dictionary<string, DeviceInfo> devices = new Dictionary<string, DeviceInfo>();

            using (var pnpEntitySearcher = new ManagementObjectSearcher(@"SELECT DeviceID, Name, Description FROM Win32_PnPEntity"))
            {
                foreach (ManagementObject entity in pnpEntitySearcher.Get())
                {
                    string deviceId = entity["DeviceID"] as string;
                    if (string.IsNullOrEmpty(deviceId) || !IsUsbDevice(deviceId))
                        continue;

                    string name = (entity["Name"] as string) ??
                                  (entity["Description"] as string) ??
                                  "Unknown USB Device";

                    devices[deviceId] = new DeviceInfo
                    {
                        Name = name,
                        DeviceID = deviceId,
                        VidPid = ExtractVidPid(deviceId)
                    };
                    //Logger.Log(" device: " + deviceId + " Name: " + name + " VID/PID : " + ExtractVidPid(deviceId));
                }
            }

            return devices;
        }

        private bool IsUsbDevice(string deviceId)
        {
            return deviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ||
                   (deviceId.Contains("\\VID_") && deviceId.Contains("&PID_"));
        }

        private string ExtractVidPid(string deviceId)
        {
            Match match = Regex.Match(deviceId, @"VID_([0-9A-F]{4})&PID_([0-9A-F]{4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string vid = match.Groups[1].Value;
                string pid = match.Groups[2].Value;
                return $"VID: {vid}, PID: {pid}";
            }
            return "VID/PID not found";
        }

        private bool IsProgrammerMatch(Programmer programmer, DeviceInfo device)
        {
            try
            {
                if (string.IsNullOrEmpty(programmer.UsbVid) || !programmer.UsbPids.Any())
                {
                    return false;
                }

                // Extract VID/PID from device ID
                var deviceVidPid = ExtractVidPidFromDeviceId(device.DeviceID);
                if (deviceVidPid == null)
                {
                    return false;
                }

                // Normalize programmer VID (ensure 4 digits, uppercase)
                string normalizedVid = programmer.UsbVid.PadLeft(4, '0').ToUpperInvariant();

                // Compare VID and check if any PID matches
                bool isMatch = deviceVidPid.Vid.Equals(normalizedVid, StringComparison.OrdinalIgnoreCase) &&
                              programmer.UsbPids.Any(pid =>
                                  deviceVidPid.Pid.Equals(pid.PadLeft(4, '0'), StringComparison.OrdinalIgnoreCase));

                if (isMatch)
                {
                    Logger.Log($"Programmer match found: {programmer.Id}");
                    Logger.Log($"VID: {deviceVidPid.Vid} (expected: {normalizedVid})");
                    Logger.Log($"PID: {deviceVidPid.Pid} (matching one of: {string.Join(", ", programmer.UsbPids)})");
                }

                return isMatch;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in IsProgrammerMatch: {ex.Message}");
                return false;
            }
        }

        private class VidPidInfo
        {
            public string Vid { get; set; }
            public string Pid { get; set; }
        }

        private VidPidInfo ExtractVidPidFromDeviceId(string deviceId)
        {
            try
            {
                // Support both standard Windows VID/PID format and potential variations
                var patterns = new[]
                {
            @"VID_([0-9A-Fa-f]{4}).*?PID_([0-9A-Fa-f]{4})",  // Standard format
            @"vid:([0-9A-Fa-f]{4}).*?pid:([0-9A-Fa-f]{4})",  // Alternative format
            @"vid([0-9A-Fa-f]{4}).*?pid([0-9A-Fa-f]{4})"     // Compact format
        };

                foreach (string pattern in patterns)
                {
                    Match match = Regex.Match(deviceId, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return new VidPidInfo
                        {
                            Vid = match.Groups[1].Value.ToUpperInvariant(),
                            Pid = match.Groups[2].Value.ToUpperInvariant()
                        };
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error extracting VID/PID: {ex.Message}");
                return null;
            }
        }

        //private bool IsProgrammerMatch(Programmer programmer, DeviceInfo device)
        //{
        //    if (string.IsNullOrEmpty(programmer.UsbVid) || !programmer.UsbPids.Any())
        //        return false;

        //    string vidPattern = $"VID_{programmer.UsbVid.PadLeft(4, '0')}";
        //    var pidPatterns = programmer.UsbPids.Select(pid => $"PID_{pid.PadLeft(4, '0')}");

        //    bool vidPidMatch = pidPatterns.Any(pid =>
        //        device.DeviceID.IndexOf(vidPattern, StringComparison.OrdinalIgnoreCase) >= 0
        //        && device.DeviceID.IndexOf(pid, StringComparison.OrdinalIgnoreCase) >= 0);

        //    // Check if the device name contains the programmer ID or if the VID/PID matches
        //    bool nameMatch = device.Name.IndexOf(programmer.Id, StringComparison.OrdinalIgnoreCase) >= 0;

        //    //Logger.Log($"Checking programmer match: {programmer.Id}");
        //    //Logger.Log($"Device: {device.DeviceID}, Name: {device.Name}");
        //    //Logger.Log($"VID/PID Match: {vidPidMatch}, Name Match: {nameMatch}");

        //    return vidPidMatch || nameMatch;
        //}
        static bool ShouldProcessEvent(string eventKey)
        {
            lock (lockObj)
            {
                DateTime now = DateTime.Now;
                if (!lastEventTimes.ContainsKey(eventKey) ||
                    now - lastEventTimes[eventKey] > debounceInterval)
                {
                    lastEventTimes[eventKey] = now;
                    return true;
                }
                return false;
            }
        }

        public void Dispose()
        {
            _usbWatcher?.Stop();
            _usbWatcher?.Dispose();
        }
    }

    public class UsbDeviceChangeEventArgs : EventArgs
    {
        public List<Programmer> ConnectedProgrammers { get; }
        public List<Programmer> DisconnectedProgrammers { get; }
        public HashSet<string> CurrentlyConnectedProgrammerIds { get; }

        public UsbDeviceChangeEventArgs(List<Programmer> connectedProgrammers, List<Programmer> disconnectedProgrammers, HashSet<string> currentlyConnectedProgrammerIds)
        {
            ConnectedProgrammers = connectedProgrammers;
            DisconnectedProgrammers = disconnectedProgrammers;
            CurrentlyConnectedProgrammerIds = currentlyConnectedProgrammerIds;
        }
    }

    public class DeviceInfo
    {
        public string Name { get; set; }
        public string DeviceID { get; set; }
        public string VidPid { get; set; }
    }
}