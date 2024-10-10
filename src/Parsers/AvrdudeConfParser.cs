
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MicroDude.Models;

namespace MicroDude.Parsers
{
    public class AvrdudeConfParser
    {
        public List<Programmer> Programmers { get; private set; }

        public AvrdudeConfParser()
        {
            Programmers = new List<Programmer>();
        }

        public void ParseFile(string avrdudeExePath)
        {
            Logger.Log($"ParseFile called with path: {avrdudeExePath}");

            string confFilePath = Path.Combine(Path.GetDirectoryName(avrdudeExePath), "avrdude.conf");
            if (!File.Exists(confFilePath))
            {
                Logger.Log($"avrdude.conf file not found at: {confFilePath}");
                throw new FileNotFoundException("avrdude.conf file not found", confFilePath);
            }

            string content = File.ReadAllText(confFilePath);
            Logger.Log($"avrdude.conf file read, content length: {content.Length}");
            ParseProgrammers(content);
            Logger.Log($"Parsing complete. Number of programmers found: {Programmers.Count}");
        }

        public void ParseProgrammers(string content)
        {
            Programmer currentProgrammer = null;
            bool insideProgrammerBlock = false;

            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (line.StartsWith("programmer # ") || line.Trim().Equals("programmer"))
                    {
                        insideProgrammerBlock = true;
                    }
                    else if (line == ";" && insideProgrammerBlock)
                    {
                        // End of programmer block
                        if (currentProgrammer != null)
                        {
                            Programmers.Add(currentProgrammer);
                            currentProgrammer = null;
                        }
                        insideProgrammerBlock = false;
                    }
                    else if (insideProgrammerBlock)
                    {
                        if (line.StartsWith("id"))
                        {
                            Match descMatch = Regex.Match(line, @"id\s*=\s*""([^""]+)"";");
                            if (descMatch.Success) {
                                currentProgrammer = new Programmer { Id = descMatch.Groups[1].Value };
                            }
                        }
                        // Parse properties
                        else if (line.StartsWith("desc") && currentProgrammer != null)
                        {
                            Match descMatch = Regex.Match(line, @"desc\s*=\s*""([^""]+)"";");
                            if (descMatch.Success)
                            {
                                currentProgrammer.Description = descMatch.Groups[1].Value;
                            }
                        }
                    }
                }
            }
            Logger.Log($"Number of programmers found: {Programmers.Count}");
        }
    }

   
}