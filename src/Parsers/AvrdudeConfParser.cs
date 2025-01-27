using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MicroDude.Models;
using System;

namespace MicroDude.Parsers
{
    public class AvrdudeConfParser
    {
        public List<Programmer> Programmers { get; private set; }
        public List<Microcontroller> Microcontrollers { get; private set; }

        private Dictionary<string, Microcontroller> _microcontrollerDict;


        public AvrdudeConfParser()
        {
            Programmers = new List<Programmer>();
            Microcontrollers = new List<Microcontroller>();
            _microcontrollerDict = new Dictionary<string, Microcontroller>();
        }

        public void ParseFile(string avrdudeExePath)
        {
            Logger.Log($"ParseFile called with path: {avrdudeExePath}");

            string confFilePath = Path.Combine(Path.GetDirectoryName(avrdudeExePath), AvrdudeConfConstants.AvrdudeConfFileName);
            if (!File.Exists(confFilePath))
            {
                //Logger.Log($"{AvrdudeConfConstants.AvrdudeConfFileName} file not found at: {confFilePath}");
                throw new FileNotFoundException($"{AvrdudeConfConstants.AvrdudeConfFileName} file not found", confFilePath);
            }

            string content = File.ReadAllText(confFilePath);
            //Logger.Log($"{AvrdudeConfConstants.AvrdudeConfFileName} file read, content length: {content.Length}");
            ParseContent(content);
            //LogProgrammers();
        }

        private void ParseContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Content is null or empty", nameof(content));
            }

            Programmer currentProgrammer = null;
            Microcontroller currentMicrocontroller = null;
            bool insideProgrammerBlock = false;
            bool insideMicrocontrollerBlock = false;
            bool skipCurrentSection = false;

            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    try
                    {
                        line = line.Trim();

                        // Check for section start
                        if (line.StartsWith(AvrdudeConfConstants.HeaderSection))
                        {
                            string nextLine = reader.ReadLine()?.Trim();
                            if (nextLine != null && nextLine.Contains(AvrdudeConfConstants.CommonValuesSection))
                            {
                                skipCurrentSection = true;
                                // Skip the ending "#----" line
                                reader.ReadLine();
                                continue;
                            }
                            else if (skipCurrentSection)
                            {
                                // We've reached the next major section, stop skipping
                                skipCurrentSection = false;
                            }
                        }

                        if (skipCurrentSection)
                        {
                            continue;
                        }

                        if (line.StartsWith(AvrdudeConfConstants.ProgrammerStartWithHash) || line.Equals(AvrdudeConfConstants.ProgrammerStart))
                        {
                            insideProgrammerBlock = true;
                            insideMicrocontrollerBlock = false;
                            currentProgrammer = new Programmer();
                        }
                        else if (line.StartsWith(AvrdudeConfConstants.MicrocontrollerStart))
                        {
                            insideMicrocontrollerBlock = true;
                            insideProgrammerBlock = false;
                            currentMicrocontroller = ParseMicrocontrollerStart(line);
                        }
                        else if (line == AvrdudeConfConstants.BlockEnd && (insideProgrammerBlock || insideMicrocontrollerBlock))
                        {
                            if (currentProgrammer != null)
                            {
                                Programmers.Add(currentProgrammer);
                                currentProgrammer = null;
                            }
                            if (currentMicrocontroller != null)
                            {
                                _microcontrollerDict[currentMicrocontroller.Id] = currentMicrocontroller;
                                currentMicrocontroller = null;
                            }
                            insideProgrammerBlock = false;
                            insideMicrocontrollerBlock = false;
                        }
                        else if (insideProgrammerBlock)
                        {
                            ParseProgrammerLine(line, ref currentProgrammer);
                        }
                        else if (insideMicrocontrollerBlock)
                        {
                            ParseMicrocontrollerLine(line, currentMicrocontroller);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error parsing line: {line}. Exception: {ex.Message}");
                    }
                }
            }
            ProcessMicrocontrollerParents();
        }
        
        private Microcontroller ParseMicrocontrollerStart(string line)
        {
            var match = Regex.Match(line, AvrdudeConfConstants.MicrocontrollerPattern);
            if (match.Success)
            {
                string parentId = match.Groups[1].Value.Trim();
                string id = match.Groups[2].Value.Trim();
                return new Microcontroller { Id = id, ParentId = parentId };
            }

            match = Regex.Match(line, AvrdudeConfConstants.MicrocontrollerStart + @"(.+)");
            if (match.Success)
            {
                return new Microcontroller { Id = match.Groups[1].Value.Trim() };
            }

            throw new FormatException($"Invalid microcontroller start line: {line}");
        }

        private void ProcessMicrocontrollerParents()
        {
            foreach (var mc in _microcontrollerDict.Values.ToList())
            {
                if (!string.IsNullOrEmpty(mc.ParentId))
                {
                    Microcontroller parent;
                    if (_microcontrollerDict.TryGetValue(mc.ParentId, out parent))
                    {
                        // Inherit properties from parent
                        foreach (var prop in parent.Properties)
                        {
                            if (!mc.Properties.ContainsKey(prop.Key))
                            {
                                mc.Properties[prop.Key] = prop.Value;
                            }
                        }

                        // Inherit signature if not already set
                        if (string.IsNullOrEmpty(mc.Signature) && !string.IsNullOrEmpty(parent.Signature))
                        {
                            mc.Signature = parent.Signature;
                        }

                        // Inherit description if not already set
                        if (string.IsNullOrEmpty(mc.Description) && !string.IsNullOrEmpty(parent.Description))
                        {
                            mc.Description = parent.Description;
                        }
                    }
                    else
                    {
                        //Logger.Log($"Parent microcontroller not found: {mc.ParentId} for {mc.Id}");
                    }
                }
            }

            // Filter out microcontrollers without signatures
            Microcontrollers = _microcontrollerDict.Values
                .Where(m => !string.IsNullOrEmpty(m.Signature))
                .ToList();
        }

        private void ParseProgrammerLine(string line, ref Programmer currentProgrammer)
        {
            if (string.IsNullOrWhiteSpace(line) || currentProgrammer == null)
            {
                return;
            }

            try
            {
                if (line.StartsWith("id"))
                {
                    Match idMatch = Regex.Match(line, @"id\s*=\s*(.+);");
                    if (idMatch.Success)
                    {
                        string[] ids = idMatch.Groups[1].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        ids = ids.Select(id => id.Trim().Trim('"')).ToArray();

                        if (ids.Length > 0)
                        {
                            currentProgrammer.Id = ids[0];
                            currentProgrammer.AlternativeIds.AddRange(ids.Skip(1));
                        }
                    }
                }
                else if (line.StartsWith("desc"))
                {
                    Match descMatch = Regex.Match(line, @"desc\s*=\s*""([^""]+)"";");
                    if (descMatch.Success)
                    {
                        currentProgrammer.Description = descMatch.Groups[1].Value;
                    }
                }
                else if (line.StartsWith("usbvid"))
                {
                    Match vidMatch = Regex.Match(line, @"usbvid\s*=\s*0x([0-9A-Fa-f]+);");
                    if (vidMatch.Success)
                    {
                        // Ensure 4-digit representation with leading zeros
                        currentProgrammer.UsbVid = vidMatch.Groups[1].Value.PadLeft(4, '0');
                    }
                }
                else if (line.StartsWith("usbpid"))
                {
                    Match pidMatch = Regex.Match(line, @"usbpid\s*=\s*(.+);");
                    if (pidMatch.Success)
                    {
                        string[] pids = pidMatch.Groups[1].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string pid in pids)
                        {
                            Match singlePidMatch = Regex.Match(pid.Trim(), @"0x([0-9A-Fa-f]+)");
                            if (singlePidMatch.Success)
                            {
                                string formattedPid = singlePidMatch.Groups[1].Value.PadLeft(4, '0');
                                currentProgrammer.UsbPids.Add(formattedPid);
                            }
                        }
                    }
                }
                else if (line.StartsWith("type"))
                {
                    Match typeMatch = Regex.Match(line, @"type\s*=\s*""([^""]+)"";");
                    if (typeMatch.Success)
                    {
                        currentProgrammer.Type = typeMatch.Groups[1].Value.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing programmer line: {line}. Exception: {ex.Message}");
            }
        }


        private void ParseMicrocontrollerLine(string line, Microcontroller currentMicrocontroller)
        {
            if (string.IsNullOrWhiteSpace(line) || currentMicrocontroller == null)
            {
                return;
            }

            try
            {
                if (line.StartsWith(AvrdudeConfConstants.DescKey))
                {
                    Match descMatch = Regex.Match(line, AvrdudeConfConstants.DescPattern);
                    if (descMatch.Success)
                    {
                        currentMicrocontroller.Description = descMatch.Groups[1].Value;
                    }
                }
                else if (line.StartsWith("signature"))
                {
                    Match signatureMatch = Regex.Match(line, @"signature\s*=\s*0x([0-9A-Fa-f]{2})\s*0x([0-9A-Fa-f]{2})\s*0x([0-9A-Fa-f]{2});");
                    if (signatureMatch.Success)
                    {
                        currentMicrocontroller.Signature = signatureMatch.Groups[1].Value +
                                                           signatureMatch.Groups[2].Value +
                                                           signatureMatch.Groups[3].Value;
                    }
                }
                else
                {
                    Match propertyMatch = Regex.Match(line, AvrdudeConfConstants.PropertyPattern);
                    if (propertyMatch.Success)
                    {
                        string key = propertyMatch.Groups[1].Value.Trim();
                        string value = propertyMatch.Groups[2].Value.Trim();
                        currentMicrocontroller.Properties[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing microcontroller line: {line}. Exception: {ex.Message}");
            }
        }

        private void LogProgrammers()
        {
            Logger.Log($"Total programmers found: {Programmers.Count}");
            foreach (var programmer in Programmers)
            {
                Logger.Log($"Programmer: {programmer.Id}");
                Logger.Log($"  Description: {programmer.Description}");
                Logger.Log($"  VID: {programmer.UsbVid}");
                Logger.Log($"  PIDs: {string.Join(", ", programmer.UsbPids)}");
                Logger.Log("  ---");
            }
        }
    }
}