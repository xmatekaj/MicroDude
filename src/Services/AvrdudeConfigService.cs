using System;
using System.IO;
using System.Security.Cryptography;
using MicroDude.Parsers;
using MicroDude.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MicroDude.Services
{
    public class AvrdudeConfigService
    {
        private static AvrdudeConfigService _instance;
        private AvrdudeConfParser _parser;
        private string _avrdudeExePath;
        private string _lastFileHash;

        public List<Programmer> Programmers { get; private set; }
        public List<Microcontroller> Microcontrollers { get; private set; }

        private AvrdudeConfigService()
        {
            _parser = new AvrdudeConfParser();
            Programmers = new List<Programmer>();
            Microcontrollers = new List<Microcontroller>();
        }

        public static AvrdudeConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AvrdudeConfigService();
                }
                return _instance;
            }
        }

        public void InitializeSync(string avrdudeExePath)
        {
            _avrdudeExePath = avrdudeExePath;
            ParseConfigIfNeeded();
        }

        private void ParseConfigIfNeeded()
        {
            string confFilePath = Path.Combine(Path.GetDirectoryName(_avrdudeExePath), AvrdudeConfConstants.AvrdudeConfFileName);
            string currentHash = CalculateFileHash(confFilePath);

            if (_lastFileHash != currentHash)
            {
                _parser.ParseFile(_avrdudeExePath);
                Programmers = _parser.Programmers;
                Microcontrollers = _parser.Microcontrollers;
                _lastFileHash = currentHash;
            }
        }

        //public async Task InitializeAsync(string avrdudeExePath)
        //{
        //    _avrdudeExePath = avrdudeExePath;
        //    await ParseConfigIfNeededAsync();
        //}

        //public async Task ParseConfigIfNeededAsync()
        //{
        //    string confFilePath = Path.Combine(Path.GetDirectoryName(_avrdudeExePath), AvrdudeConfConstants.AvrdudeConfFileName);
        //    string currentHash = CalculateFileHash(confFilePath);

        //    if (_lastFileHash != currentHash)
        //    {
        //        await Task.Run(() =>
        //        {
        //            _parser.ParseFile(_avrdudeExePath);
        //            Programmers = _parser.Programmers;
        //            Microcontrollers = _parser.Microcontrollers;
        //        });
        //        _lastFileHash = currentHash;
        //    }
        //}

        private string CalculateFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}