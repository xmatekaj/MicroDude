using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using MicroDude.Models;
using MicroDude.Services;
using System;
using System.Linq;
using System.Threading;

namespace MicroDude.Tests
{
    [TestClass]
    public class UsbDeviceServiceTests
    {
        [TestMethod]
        public void UsbDeviceService_DetectsUsbAsp()
        {
            // Arrange
            var programmer = new Programmer
            {
                Id = "usbasp",
                UsbVid = "16C0",
                UsbPids = new List<string> { "05DC" }
            };
            var mockSearcher = new Mock<IManagementObjectSearcher>();
            var deviceList = new List<MockDevice>
            {
                new MockDevice { DeviceID = @"USB\VID_05E3&PID_0610\6&158044BE&0&2", Name = "Some Other Device" },
                new MockDevice { DeviceID = @"USB\VID_16C0&PID_05DC\6&158044BE&0&1", Name = "USBasp" }
            };
            mockSearcher.Setup(x => x.Get()).Returns(GetMockDevices(deviceList));

            List<Programmer> detectedProgrammers = null;
            UsbDeviceService service = null;

            try
            {
                service = new UsbDeviceService(new List<Programmer> { programmer }, mockSearcher.Object);
                service.UsbDeviceChanged += (sender, args) =>
                {
                    detectedProgrammers = args.ConnectedProgrammers;
                };

                // Allow time for COM object initialization
                Thread.Sleep(100);

                // Assert
                Assert.IsNotNull(detectedProgrammers, "UsbDeviceChanged event should be fired");
                Assert.AreEqual(1, detectedProgrammers.Count, "One programmer should be detected");
                Assert.AreEqual("usbasp", detectedProgrammers.First().Id, "USBasp should be the detected programmer");
            }
            finally
            {
                service?.Dispose();
            }
        }

        [TestMethod]
        public void UsbDeviceService_DoesNotDetectNonMatchingDevice()
        {
            // Arrange
            var programmer = new Programmer
            {
                Id = "usbasp",
                UsbVid = "16C0",
                UsbPids = new List<string> { "05DC" }
            };
            var mockSearcher = new Mock<IManagementObjectSearcher>();
            var deviceList = new List<MockDevice>
            {
                new MockDevice { DeviceID = @"USB\VID_05E3&PID_0610\6&158044BE&0&2", Name = "Some Other Device" },
            };
            mockSearcher.Setup(x => x.Get()).Returns(GetMockDevices(deviceList));

            List<Programmer> detectedProgrammers = null;
            UsbDeviceService service = null;

            try
            {
                service = new UsbDeviceService(new List<Programmer> { programmer }, mockSearcher.Object);
                service.UsbDeviceChanged += (sender, args) =>
                {
                    detectedProgrammers = args.ConnectedProgrammers;
                };

                // Allow time for COM object initialization
                Thread.Sleep(100);

                // Assert
                Assert.IsNotNull(detectedProgrammers, "UsbDeviceChanged event should be fired");
                Assert.AreEqual(0, detectedProgrammers.Count, "No programmers should be detected");
            }
            finally
            {
                service?.Dispose();
            }
        }

        private static IEnumerable<IManagementObject> GetMockDevices(List<MockDevice> deviceList)
        {
            foreach (var device in deviceList)
            {
                var mockDevice = new Mock<IManagementObject>();
                mockDevice.Setup(m => m.GetPropertyValue("DeviceID")).Returns(device.DeviceID);
                mockDevice.Setup(m => m.GetPropertyValue("Name")).Returns(device.Name);
                yield return mockDevice.Object;
            }
        }

        private class MockDevice
        {
            public string DeviceID { get; set; }
            public string Name { get; set; }
        }
    }
}