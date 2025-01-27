using System;
using System.IO;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using MicroDude.Models;

namespace MicroDude.Parsers
{
    /// <summary>
    /// Class to parse and handle Device Files (xml/atdf)
    /// </summary>
    public class XmlParser
    {
        private const string REGISTRY_KEY_PATTERN = @"SOFTWARE\Wow6432Node\Atmel\AtmelStudio\{0}";
        private const string REGISTRY_VALUE_NAME = "InstallDir";
        private const string RELATIVE_PACKS_PATH = @"packs\atmel";
        private readonly string _rootPath;
        private Dictionary<string, string> _deviceFiles;

        public XmlParser()
        {
            _rootPath = FindAtmelStudioPath();
            if (string.IsNullOrEmpty(_rootPath))
            {
                throw new DirectoryNotFoundException("Atmel/Microchip Studio installation not found");
            }

            _deviceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IndexDeviceFiles();
        }

        private string FindAtmelStudioPath()
        {
            try
            {
                // Check common versions from newest to oldest
                string[] versions = { "7.0", "6.2", "6.1", "6.0" };

                foreach (var version in versions)
                {
                    string registryKey = string.Format(REGISTRY_KEY_PATTERN, version);

                    // Try 64-bit registry first
                    using (var key = Registry.LocalMachine.OpenSubKey(registryKey))
                    {
                        if (key != null)
                        {
                            string installDir = key.GetValue(REGISTRY_VALUE_NAME) as string;
                            if (!string.IsNullOrEmpty(installDir))
                            {
                                string packsPath = Path.Combine(installDir, RELATIVE_PACKS_PATH);
                                if (Directory.Exists(packsPath))
                                {
                                    Logger.Log($"Found Atmel Studio {version} at: {installDir}");
                                    return packsPath;
                                }
                            }
                        }
                    }

                    // Try 32-bit registry if 64-bit failed
                    registryKey = registryKey.Replace("Wow6432Node\\", "");
                    using (var key = Registry.LocalMachine.OpenSubKey(registryKey))
                    {
                        if (key != null)
                        {
                            string installDir = key.GetValue(REGISTRY_VALUE_NAME) as string;
                            if (!string.IsNullOrEmpty(installDir))
                            {
                                string packsPath = Path.Combine(installDir, RELATIVE_PACKS_PATH);
                                if (Directory.Exists(packsPath))
                                {
                                    Logger.Log($"Found Atmel Studio {version} at: {installDir}");
                                    return packsPath;
                                }
                            }
                        }
                    }
                }

                // If registry lookup fails, try the default installation paths
                string[] defaultPaths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Atmel", "Studio", "7.0", RELATIVE_PACKS_PATH),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Atmel", "Studio", "7.0", RELATIVE_PACKS_PATH),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microchip", "Studio", "7.0", RELATIVE_PACKS_PATH),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microchip", "Studio", "7.0", RELATIVE_PACKS_PATH)
                };

                foreach (string path in defaultPaths)
                {
                    if (Directory.Exists(path))
                    {
                        Logger.Log($"Found Studio installation at default path: {path}");
                        return path;
                    }
                }

                Logger.Log("No Atmel/Microchip Studio installation found");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error finding Atmel Studio path: {ex.Message}");
                return null;
            }
        }

        private void IndexDeviceFiles()
        {
            try
            {
                if (!Directory.Exists(_rootPath))
                {
                    throw new DirectoryNotFoundException($"Root path does not exist: {_rootPath}");
                }

                // Use case-insensitive dictionary
                _deviceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var dfpDirectories = Directory.GetDirectories(_rootPath, "*_DFP", SearchOption.AllDirectories);
                Logger.Log($"Found {dfpDirectories.Length} DFP directories");

                foreach (var dfpDir in dfpDirectories)
                {
                    try
                    {
                        var atdfDir = Directory.GetDirectories(dfpDir, "atdf", SearchOption.AllDirectories).FirstOrDefault();
                        if (atdfDir != null)
                        {
                            Logger.Log($"Processing ATDF directory: {atdfDir}");

                            var atdfFiles = Directory.GetFiles(atdfDir, "*.atdf");
                            foreach (var file in atdfFiles)
                            {
                                string deviceName = Path.GetFileNameWithoutExtension(file);
                                // Store in original case but allow case-insensitive lookup
                                _deviceFiles[deviceName] = file;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error processing DFP directory {dfpDir}: {ex.Message}");
                    }
                }

                Logger.Log($"Successfully indexed {_deviceFiles.Count} ATDF device files");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error indexing ATDF files: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Logger.Log($"Inner exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        public Microcontroller ParseDevice(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
            {
                throw new ArgumentException("Device name cannot be null or empty", nameof(deviceName));
            }

            if (!_deviceFiles.ContainsKey(deviceName))
            {
                Logger.Log($"Device {deviceName} not found. Available devices: {string.Join(", ", _deviceFiles.Keys.Take(5))}...");
                throw new KeyNotFoundException($"Device {deviceName} not found in ATDF files");
            }

            var doc = XDocument.Load(_deviceFiles[deviceName]);

            // Create basic device info
            var microcontroller = new Microcontroller
            {
                Id = deviceName,
                Description = GetDeviceDescription(doc)
            };

            // Parse memory information
            ParseMemoryInfo(doc, microcontroller);

            // Parse configuration bits
            ParseConfigurationBits(doc, microcontroller);

            return microcontroller;
        }

        private string GetDeviceDescription(XDocument doc)
        {
            return doc.Descendants("device")
                     .FirstOrDefault()
                     ?.Attribute("name")
                     ?.Value;
        }

        private void ParseMemoryInfo(XDocument doc, Microcontroller device)
        {
            var addressSpaces = doc.Descendants("address-spaces").Elements("address-space");

            // Parse flash memory
            var progSpace = addressSpaces.FirstOrDefault(x => x.Attribute("name")?.Value == "prog");
            if (progSpace != null)
            {
                var flashSegment = progSpace.Elements("memory-segment")
                    .FirstOrDefault(x => x.Attribute("type")?.Value == "flash");
                if (flashSegment != null)
                {
                    device.FlashSize = ParseHexValue(flashSegment.Attribute("size")?.Value);
                    device.FlashPageSize = ParseHexValue(flashSegment.Attribute("pagesize")?.Value);
                    Logger.Log($"Parsed Flash: Size={device.FlashSize}B, Page={device.FlashPageSize}B");
                }
            }

            // Parse EEPROM
            var eepromSpace = addressSpaces.FirstOrDefault(x => x.Attribute("name")?.Value == "eeprom");
            if (eepromSpace != null)
            {
                var eepromSegment = eepromSpace.Elements("memory-segment")
                    .FirstOrDefault(x => x.Attribute("type")?.Value == "eeprom");
                if (eepromSegment != null)
                {
                    device.EepromSize = ParseHexValue(eepromSegment.Attribute("size")?.Value);
                    device.EepromPageSize = ParseHexValue(eepromSegment.Attribute("pagesize")?.Value);
                    Logger.Log($"Parsed EEPROM: Size={device.EepromSize}B, Page={device.EepromPageSize}B");
                }
            }
        }

        private void ParseConfigurationBits(XDocument doc, Microcontroller device)
        {
            var modules = doc.Descendants("modules").Elements("module");

            // Parse fuses
            var fuseModule = modules.FirstOrDefault(m => m.Attribute("name")?.Value == "FUSE");
            if (fuseModule != null)
            {
                ParseFuseModule(fuseModule, device);
            }

            // Parse lockbits
            var lockbitModule = modules.FirstOrDefault(m => m.Attribute("name")?.Value == "LOCKBIT");
            if (lockbitModule != null)
            {
                ParseLockbitModule(lockbitModule, device);
            }
        }

        private void ParseFuseModule(XElement fuseModule, Microcontroller device)
        {
            var registerGroup = fuseModule.Descendants("register-group")
                .FirstOrDefault(rg => rg.Attribute("name")?.Value == "FUSE");

            if (registerGroup != null)
            {
                foreach (var register in registerGroup.Elements("register"))
                {
                    var reg = new FuseRegister
                    {
                        Name = register.Attribute("name")?.Value,
                        Caption = register.Attribute("caption")?.Value,
                        Offset = Convert.ToInt32(register.Attribute("offset")?.Value ?? "0", 16),
                        Size = int.Parse(register.Attribute("size")?.Value ?? "1"),
                        InitValue = Convert.ToByte(register.Attribute("initval")?.Value ?? "0", 16)
                    };

                    foreach (var bitfield in register.Elements("bitfield"))
                    {
                        reg.Bitfields.Add(ParseBitfield(bitfield));
                    }

                    device.FuseRegisters.Add(reg);
                    Logger.Log($"Parsed Fuse Register: {reg.Name}");
                }

                // Parse value groups
                ParseValueGroups(fuseModule, device);
            }
        }

        private void ParseLockbitModule(XElement lockbitModule, Microcontroller device)
        {
            var registerGroup = lockbitModule.Descendants("register-group")
                .FirstOrDefault(rg => rg.Attribute("name")?.Value == "LOCKBIT");

            if (registerGroup != null)
            {
                foreach (var register in registerGroup.Elements("register"))
                {
                    var reg = new LockbitRegister
                    {
                        Name = register.Attribute("name")?.Value,
                        Caption = register.Attribute("caption")?.Value,
                        Offset = Convert.ToInt32(register.Attribute("offset")?.Value ?? "0", 16),
                        Size = int.Parse(register.Attribute("size")?.Value ?? "1"),
                        InitValue = Convert.ToByte(register.Attribute("initval")?.Value ?? "0", 16)
                    };

                    foreach (var bitfield in register.Elements("bitfield"))
                    {
                        reg.Bitfields.Add(ParseBitfield(bitfield));
                    }

                    device.LockbitRegisters.Add(reg);
                    Logger.Log($"Parsed Lockbit Register: {reg.Name}");
                }

                // Parse value groups
                ParseValueGroups(lockbitModule, device);
            }
        }

        private Bitfield ParseBitfield(XElement bitfield)
        {
            return new Bitfield
            {
                Name = bitfield.Attribute("name")?.Value,
                Caption = bitfield.Attribute("caption")?.Value,
                Mask = Convert.ToByte(bitfield.Attribute("mask")?.Value ?? "0", 16),
                ValueGroupName = bitfield.Attribute("values")?.Value
            };
        }

        private void ParseValueGroups(XElement module, Microcontroller device)
        {
            var valueGroups = module.Descendants("value-group");
            foreach (var group in valueGroups)
            {
                var valueGroup = new ValueGroup
                {
                    Name = group.Attribute("name")?.Value
                };

                foreach (var value in group.Elements("value"))
                {
                    valueGroup.Values.Add(new ConfigValue
                    {
                        Name = value.Attribute("name")?.Value,
                        Caption = value.Attribute("caption")?.Value,
                        Value = Convert.ToByte(value.Attribute("value")?.Value ?? "0", 16)
                    });
                }

                // Only add if not already present (can be shared between fuses and lockbits)
                if (!device.ValueGroups.ContainsKey(valueGroup.Name))
                {
                    device.ValueGroups[valueGroup.Name] = valueGroup;
                }
            }
        }

        private int ParseHexValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            value = value.TrimStart('0', 'x');
            return int.Parse(value, System.Globalization.NumberStyles.HexNumber);
        }

        public string[] GetAvailableDevices()
        {
            return _deviceFiles.Keys.OrderBy(k => k).ToArray();
        }

        public string GetNormalizedDeviceName(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return null;

            return _deviceFiles.Keys.FirstOrDefault(k => k.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsDeviceSupported(string deviceName)
        {
            if (string.IsNullOrEmpty(deviceName))
                return false;

            return _deviceFiles.ContainsKey(deviceName);
        }
    }
}