using Microsoft.VisualStudio.TestTools.UnitTesting;
using MicroDude.Parsers;
using MicroDude.Models;
using System.IO;
using System.Linq;
using System.Reflection;
using System;

namespace MicroDude.Tests
{
    [TestClass]
    public class AvrdudeConfParserTests
    {
        private AvrdudeConfParser _parser;
        private string _avrdudeExePath;
        private string _avrdudeConfPath;
        private const int NumberOfProgrammerForV8 = 114;
        private const int NumberOfMicrocontrollersWithSignatureForV8 = 355;

        [TestInitialize]
        public void TestInitialize()
        {
            _parser = new AvrdudeConfParser();

            // Get the directory of the current assembly (test project)
            string testProjectDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Navigate up to the solution directory
            string solutionDirectory = Directory.GetParent(testProjectDirectory).Parent.Parent.FullName;

            // Construct paths to avrdude executable and config file
            _avrdudeExePath = Path.Combine(solutionDirectory, "src", "AvrDude", "avrdude.exe");
            _avrdudeConfPath = Path.Combine(solutionDirectory, "src", "AvrDude", "avrdude.conf");

            // Verify that the files exist
            Assert.IsTrue(File.Exists(_avrdudeExePath), $"avrdude.exe not found at {_avrdudeExePath}");
            Assert.IsTrue(File.Exists(_avrdudeConfPath), $"avrdude.conf not found at {_avrdudeConfPath}");

            System.Diagnostics.Debug.WriteLine($"AvrDude exe path: {_avrdudeExePath}");
            System.Diagnostics.Debug.WriteLine($"AvrDude conf path: {_avrdudeConfPath}");
        }

        [TestMethod]
        public void CheckParsedMicrocontrollersAndProgrammers()
        {
            _parser.ParseFile(_avrdudeExePath);

            // Check if Programmers and Microcontrollers are not null
            {
                Assert.IsNotNull(_parser.Programmers, "Programmers list is null");
                Assert.IsNotNull(_parser.Microcontrollers, "Microcontrollers list is null");
            }

            // Test numbers
            {
                Assert.AreEqual(_parser.Programmers.Count, NumberOfProgrammerForV8);
                Assert.AreEqual(_parser.Microcontrollers.Count, NumberOfMicrocontrollersWithSignatureForV8);
            }

            // Test some basic data
            {
                Assert.IsNotNull(_parser.Programmers.FirstOrDefault(p => p.Id.Equals("arduino", StringComparison.OrdinalIgnoreCase)), "Arduino programmer should exist");
                Assert.IsNotNull(_parser.Microcontrollers.FirstOrDefault(m => m.Id.Equals("m328p", StringComparison.OrdinalIgnoreCase)), "ATmega328P should exist");
                Assert.IsNotNull(_parser.Microcontrollers.FirstOrDefault(m => m.Id.Equals("m8", StringComparison.OrdinalIgnoreCase)), "ATmega8 should exist");
            }

            // Test Atmel-ICE
            {
                var atmelIce = _parser.Programmers.FirstOrDefault(p =>
                                p.Id.Equals("atmelice", StringComparison.OrdinalIgnoreCase) ||
                                p.AlternativeIds.Any(id => id.Equals("atmelice", StringComparison.OrdinalIgnoreCase)));
                Assert.IsNotNull(atmelIce, "Atmel-ICE should exist");
                Assert.IsTrue(atmelIce.Id.Equals("atmelice", StringComparison.OrdinalIgnoreCase) ||
                  atmelIce.AlternativeIds.Any(id => id.Equals("atmelice", StringComparison.OrdinalIgnoreCase)),
                  "Main ID or one of alternative IDs should be 'atmelice'");
                Assert.AreEqual(1, atmelIce.AlternativeIds.Count, "Atmel-ICE should have 1 alternative ID");
                Assert.IsTrue(atmelIce.Id.Equals("atmelice_jtag", StringComparison.OrdinalIgnoreCase) ||
                              atmelIce.AlternativeIds.Any(id => id.Equals("atmelice_jtag", StringComparison.OrdinalIgnoreCase)),
                              "Main ID or one of alternative IDs should be 'atmelice_jtag'");
                Assert.AreEqual("03eb", atmelIce.UsbVid, "Atmel-ICE should have VID 03eb");
                Assert.AreEqual(1, atmelIce.UsbPids.Count, "Atmel-ICE should have 1 PID");
                Assert.AreEqual("2141", atmelIce.UsbPids[0], "Atmel-ICE should have PID 2141");
            }

            // Test Usbasp 
            {
                var usbasp = _parser.Programmers.FirstOrDefault(p => p.Id.Equals("usbasp", StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(usbasp, "USBASP should exist");
                Assert.AreEqual("16c0", usbasp.UsbVid, "USBASP should have VID 16c0");
                Assert.AreEqual(1, usbasp.UsbPids.Count, "USBASP should have 1 PID");
                Assert.AreEqual("05dc", usbasp.UsbPids[0], "USBASP should have PID 05dc");
                Assert.IsNotNull(_parser.Programmers.FirstOrDefault(p => p.Id.Equals("usbasp-clone", StringComparison.OrdinalIgnoreCase)), "USBASP-clone should exist as a separate programmer, despite the fact it has the same VID&PID");
            }

            // test NIBObee
            {
                var nibobee = _parser.Programmers.FirstOrDefault(p => p.Id.Equals("nibobee", StringComparison.OrdinalIgnoreCase));

                Assert.IsNotNull(nibobee, "NIBObee programmer should exist");
                Assert.AreEqual("nibobee", nibobee.Id, "ID should be 'nibobee'");
                Assert.AreEqual("NIBObee", nibobee.Description, "Description should be 'NIBObee'");
                Assert.AreEqual("usbasp", nibobee.Type, "Type should be 'usbasp'");
                Assert.AreEqual("16c0", nibobee.UsbVid, "USB VID should be 16c0");
                Assert.AreEqual(1, nibobee.UsbPids.Count, "Should have 1 USB PID");
                Assert.AreEqual("092f", nibobee.UsbPids[0], "USB PID should be 092f");

                // Check that it's a separate programmer from the standard USBASP
                var usbasp = _parser.Programmers.FirstOrDefault(p => p.Id.Equals("usbasp", StringComparison.OrdinalIgnoreCase));
                Assert.AreNotEqual(usbasp, nibobee, "NIBObee should be a separate programmer from USBASP");
            }

            // Test non USB programmer
            {
                var avr109 = _parser.Programmers.FirstOrDefault(p => p.Id.Equals("avr109", StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(avr109, "AVR109 should exist");
                Assert.IsNull(avr109.UsbVid, "AVR109 should not have a USB VID");
                Assert.AreEqual(0, avr109.UsbPids.Count, "AVR109 should not have any USB PIDs");
            }

            // Test PICkit5
            {
                var pickit5 = _parser.Programmers.FirstOrDefault(p => p.Id.Equals("pickit5_updi", StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(pickit5, "PICkit5 should exist");
                Assert.AreEqual("04d8", pickit5.UsbVid, "PICkit5 should have VID 04d8");
                Assert.AreEqual(3, pickit5.UsbPids.Count, "PICkit5 should have 3 PIDs");
                CollectionAssert.AreEqual(new[] { "9036", "9012", "9018" }, pickit5.UsbPids, "PICkit5 should have PIDs 9036, 9012, and 9018");
            }

            // Test PL2303
            {
                var pl2303 = _parser.Programmers.FirstOrDefault(p => p.Id.Equals("pl2303", StringComparison.OrdinalIgnoreCase));
                Assert.IsNotNull(pl2303, "pl2303 should exist");
                Assert.AreEqual("067B", pl2303.UsbVid, "pl2303 should have VID 067B");
                Assert.AreEqual(8, pl2303.UsbPids.Count, "pl2303 should have 3 PIDs");
                CollectionAssert.AreEqual(new[] { "2303", "2304", "23A3", "23B3", "23C3", "23D3", "23E3", "23F3" }, pl2303.UsbPids, "pl2303 should have PIDs 2303, 2304, 23a3, 23b3, 23c3, 23d3, 23e3 and 23f3");
            }

            System.Diagnostics.Debug.WriteLine($"Parsed {_parser.Programmers.Count} programmers and {_parser.Microcontrollers.Count} microcontrollers");
        }
    }
}