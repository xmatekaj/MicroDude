using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using MicroDude.Models;
using MicroDude.Parsers;
using MicroDude.Properties;
using System.IO;

namespace MicroDude.Core
{
    public class FileHashInfo
    {
        public string FilePath { get; set; }
        public string Hash { get; set; }
        public DateTime LastModified { get; set; }
    }

    public sealed class ProgrammingStateService : INotifyPropertyChanged
    {
        private static readonly object _lock = new object();
        private static volatile ProgrammingStateService _instance;

        private string _manualProgrammer;
        private string _port;
        private XmlParser _xmlParser;
        private Programmer _detectedProgrammer;
        private Dictionary<string, FileHashInfo> _fileHashes = new Dictionary<string, FileHashInfo>();
        private DateTime _lastProgrammingTime;
        private MemoryUsageInfo _memoryUsage;
        private Microcontroller _currentMicrocontroller;

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<string> DeviceChanged;
        public event EventHandler<Programmer> ProgrammerChanged;
        public event EventHandler<string> PortChanged;

        public static ProgrammingStateService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ProgrammingStateService();
                        }
                    }
                }
                return _instance;
            }
        }

        public bool HasFileChanged(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return true;

            var fileInfo = new FileInfo(filePath);
            FileHashInfo hashInfo;
            if (!_fileHashes.TryGetValue(filePath, out hashInfo))
                return true;

            // Check if file was modified since last hash calculation
            if (fileInfo.LastWriteTime != hashInfo.LastModified)
                return true;

            // Calculate new hash and compare
            string currentHash = CalculateFileHash(filePath);
            return currentHash != hashInfo.Hash;
        }

        public void UpdateFileHash(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            var fileInfo = new FileInfo(filePath);
            _fileHashes[filePath] = new FileHashInfo
            {
                FilePath = filePath,
                Hash = CalculateFileHash(filePath),
                LastModified = fileInfo.LastWriteTime
            };
        }
        public void ClearFileHashes()
        {
            _fileHashes.Clear();
        }

        private string CalculateFileHash(string filePath)
        {
            try
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error calculating file hash: {ex.Message}");
                return string.Empty;
            }
        }

        public Microcontroller CurrentMicrocontroller
        {
            get
            {
                lock (_lock)
                {
                    return _currentMicrocontroller;
                }
            }
        }

        public string DetectedDevice
        {
            get
            {
                lock (_lock)
                {
                    return _currentMicrocontroller?.Signature;
                }
            }
        }

        public string CurrentDevice
        {
            get
            {
                lock (_lock)
                {
                    return _currentMicrocontroller?.Id;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_currentMicrocontroller?.Id != value)
                    {
                        UpdateDevice(value);
                        OnPropertyChanged(nameof(CurrentDevice));
                        OnDeviceChanged(value);
                    }
                }
            }
        }
        private ProgrammingStateService()
        {
            _port = "usb";  // Default port
            _manualProgrammer = MicroDudeSettings.Default.Programmer;
            try
            {
                _xmlParser = new XmlParser();
                Logger.Log("XML Parser initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to initialize XML Parser: {ex.Message}");
            }
            Logger.Log("ProgrammingStateService initialized");
        }

        private void UpdateDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName) || _xmlParser == null)
            {
                Logger.Log("UpdateDevice: deviceName is empty or _xmlParser is null");
                return;
            }

            try
            {
                if (_xmlParser.IsDeviceSupported(deviceName))
                {
                    // Store previous MCU info
                    var previousMcu = _currentMicrocontroller;

                    // Update MCU
                    _currentMicrocontroller = _xmlParser.ParseDevice(deviceName);

                    // Log MCU info (safely)
                    LogMicrocontrollerInfo(_currentMicrocontroller);

                    // Initialize or update memory usage
                    if (_currentMicrocontroller != null)
                    {
                        MemoryUsage = new MemoryUsageInfo
                        {
                            FlashUsed = _memoryUsage?.FlashUsed ?? 0,
                            FlashTotal = _currentMicrocontroller.FlashSize,
                            EepromUsed = _memoryUsage?.EepromUsed ?? 0,
                            EepromTotal = _currentMicrocontroller.EepromSize
                        };

                        Logger.Log($"Memory usage initialized: Flash={MemoryUsage.FlashTotal}B, EEPROM={MemoryUsage.EepromTotal}B");

                        // Notify about all relevant changes
                        OnPropertyChanged(nameof(CurrentDevice));
                        OnPropertyChanged(nameof(CurrentMicrocontroller));
                        OnPropertyChanged(nameof(MemoryUsage));
                        OnDeviceChanged(deviceName);
                    }
                    else
                    {
                        Logger.Log("Warning: ParseDevice returned null");
                    }
                }
                else
                {
                    Logger.Log($"Device {deviceName} not found in ATDF files");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating device information: {ex.Message}");
                Logger.Log($"Stack trace: {ex.StackTrace}");
            }
        }

        public Programmer DetectedProgrammer
        {
            get
            {
                lock (_lock)
                {
                    return _detectedProgrammer;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_detectedProgrammer != value)
                    {
                        _detectedProgrammer = value;
                        Logger.Log(string.Format("Detected programmer changed to: {0}",
                            value != null ? value.Id : "none"));
                        OnPropertyChanged("DetectedProgrammer");
                        OnProgrammerChanged(value);
                    }
                }
            }
        }

        public string ManualProgrammer
        {
            get
            {
                lock (_lock)
                {
                    return _manualProgrammer;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_manualProgrammer != value)
                    {
                        _manualProgrammer = value;
                        MicroDudeSettings.Default.Programmer = value;
                        MicroDudeSettings.Default.Save();
                        Logger.Log(string.Format("Manual programmer set to: {0}", value ?? "none"));
                        OnPropertyChanged("ManualProgrammer");
                    }
                }
            }
        }

        public string Port
        {
            get
            {
                lock (_lock)
                {
                    return _port;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_port != value)
                    {
                        _port = value;
                        Logger.Log(string.Format("Port changed to: {0}", value));
                        OnPropertyChanged("Port");
                        OnPortChanged(value);
                    }
                }
            }
        }

        public bool IsReadyToProgram()
        {
            lock (_lock)
            {
                bool hasDevice = _currentMicrocontroller != null;
                bool hasProgrammer = _detectedProgrammer != null ||
                                   !string.IsNullOrEmpty(_manualProgrammer);
                bool hasPort = !string.IsNullOrEmpty(_port);

                Logger.Log($"Programming readiness check - Device: {hasDevice}, Programmer: {hasProgrammer}, Port: {hasPort}");

                return hasDevice && hasProgrammer && hasPort;
            }
        }

        public ProgrammingParameters GetProgrammingParameters()
        {
            lock (_lock)
            {
                return new ProgrammingParameters
                {
                    DeviceName = _currentMicrocontroller?.Id,
                    ProgrammerName = _detectedProgrammer != null ?
                        _detectedProgrammer.Id : _manualProgrammer,
                    Port = _port,
                    IsAutoDetectedProgrammer = _detectedProgrammer != null
                };
            }
        }

        public MemoryUsageInfo MemoryUsage
        {
            get
            {
                lock (_lock)
                {
                    return _memoryUsage;
                }
            }
            private set
            {
                lock (_lock)
                {
                    if (_memoryUsage != value)
                    {
                        _memoryUsage = value;
                        Logger.Log($"MemoryUsage updated: {(_memoryUsage == null ? "null" : $"Flash: {_memoryUsage.FlashTotal}B, EEPROM: {_memoryUsage.EepromTotal}B")}");
                        OnPropertyChanged(nameof(MemoryUsage));
                    }
                }
            }
        }

        public bool HasMcuConflict
        {
            get
            {
                lock (_lock)
                {
                    return _currentMicrocontroller != null &&
                           !string.IsNullOrEmpty(DetectedDevice) &&
                           !_currentMicrocontroller.Signature.Equals(DetectedDevice, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public string[] GetAvailableDevices()
        {
            try
            {
                return _xmlParser?.GetAvailableDevices() ?? new string[0];
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting available devices: {ex.Message}");
                return new string[0];
            }
        }

        public void UpdateMemoryUsage(long flashUsed, long eepromUsed)
        {
            lock (_lock)
            {
                if (_currentMicrocontroller == null)
                {
                    Logger.Log("Cannot update memory usage: no microcontroller selected");
                    return;
                }

                MemoryUsage = new MemoryUsageInfo
                {
                    FlashUsed = (int)flashUsed,
                    FlashTotal = _currentMicrocontroller.FlashSize,
                    EepromUsed = (int)eepromUsed,
                    EepromTotal = _currentMicrocontroller.EepromSize
                };

                Logger.Log($"Memory usage updated:");
                Logger.Log($"Flash: {_memoryUsage.FlashUsed}/{_currentMicrocontroller.FlashSize}B ({_memoryUsage.FlashPercent:F1}%)");
                Logger.Log($"EEPROM: {_memoryUsage.EepromUsed}/{_currentMicrocontroller.EepromSize}B ({_memoryUsage.EepromPercent:F1}%)");
            }
        }

        private void LogMicrocontrollerInfo(Microcontroller mcu)
        {
            Logger.Log("=== Microcontroller Information ===");
            Logger.Log($"  Device: {mcu.Id}");
            Logger.Log($"  Description: {mcu.Description}");
            Logger.Log($"  Signature: {mcu.Signature}");

            // Memory Configuration
            Logger.Log("\n=== Memory Configuration ===");
            Logger.Log($"  Flash Size: {mcu.FlashSize} bytes");
            Logger.Log($"  Flash Page Size: {mcu.FlashPageSize} bytes");
            Logger.Log($"  EEPROM Size: {mcu.EepromSize} bytes");
            Logger.Log($"  EEPROM Page Size: {mcu.EepromPageSize} bytes");

            // Fuse Registers
            //Logger.Log("\n=== Fuse Configuration ===");
            //foreach (var fuse in mcu.FuseRegisters)
            //{
            //    Logger.Log($"\nFuse Register: {fuse.Name}");
            //    Logger.Log($"  Caption: {fuse.Caption}");
            //    Logger.Log($"  Initial Value: 0x{fuse.InitValue:X2}");

            //    foreach (var bitfield in fuse.Bitfields)
            //    {
            //        Logger.Log($"\n  Bitfield: {bitfield.Name}");
            //        Logger.Log($"  Caption: {bitfield.Caption}");
            //        Logger.Log($"  Mask: 0x{bitfield.Mask:X2}");

            //        ValueGroup group;
            //        if (mcu.ValueGroups.TryGetValue(bitfield.ValueGroupName, out group))
            //        {
            //            Logger.Log("  Available Values:");
            //            foreach (var value in group.Values)
            //            {
            //                Logger.Log($"    {value.Caption} = 0x{value.Value:X2}");
            //            }
            //        }
            //    }
            //}

            //// Lockbit Registers
            //Logger.Log("\n=== Lock Bit Configuration ===");
            //foreach (var lockbit in mcu.LockbitRegisters)
            //{
            //    Logger.Log($"\nLock Register: {lockbit.Name}");
            //    Logger.Log($"Caption: {lockbit.Caption}");
            //    Logger.Log($"Initial Value: 0x{lockbit.InitValue:X2}");

            //    foreach (var bitfield in lockbit.Bitfields)
            //    {
            //        Logger.Log($"\n  Bitfield: {bitfield.Name}");
            //        Logger.Log($"  Caption: {bitfield.Caption}");
            //        Logger.Log($"  Mask: 0x{bitfield.Mask:X2}");

            //        ValueGroup group;
            //        if (mcu.ValueGroups.TryGetValue(bitfield.ValueGroupName, out group))
            //        {
            //            if (group != null)
            //            {
            //                Logger.Log("  Available Values:");
            //                foreach (var value in group.Values)
            //                {
            //                    Logger.Log($"    {value.Caption} = 0x{value.Value:X2}");
            //                }
            //            }
            //        }
            //    }
            //}

            //Logger.Log("\n=== Additional Properties ===");
            //foreach (var prop in mcu.Properties)
            //{
            //    Logger.Log($"{prop.Key}: {prop.Value}");
            //}
            Logger.Log("===============================\n");
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                try
                {
                    handler(this, new PropertyChangedEventArgs(propertyName));
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Error in property change notification: {0}", ex.Message));
                }
            }
        }

        private void OnDeviceChanged(string newDevice)
        {
            EventHandler<string> handler = DeviceChanged;
            if (handler != null)
            {
                try
                {
                    handler(this, newDevice);
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Error in device change notification: {0}", ex.Message));
                }
            }
        }

        private void OnProgrammerChanged(Programmer newProgrammer)
        {
            EventHandler<Programmer> handler = ProgrammerChanged;
            if (handler != null)
            {
                try
                {
                    handler(this, newProgrammer);
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Error in programmer change notification: {0}", ex.Message));
                }
            }
        }

        private void OnPortChanged(string newPort)
        {
            EventHandler<string> handler = PortChanged;
            if (handler != null)
            {
                try
                {
                    handler(this, newPort);
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("Error in port change notification: {0}", ex.Message));
                }
            }
        }

        public sealed class ProgrammingParameters
        {
            public string DeviceName { get; set; }
            public string ProgrammerName { get; set; }
            public string Port { get; set; }
            public bool IsAutoDetectedProgrammer { get; set; }
        }
    }
}