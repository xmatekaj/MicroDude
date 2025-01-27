using System;
using System.Collections.Generic;
using System.ComponentModel;
using MicroDude.Parsers;
using MicroDude.Models;

namespace MicroDude.Core
{
    public class FuseBitProgrammer : INotifyPropertyChanged
    {
        private MicrocontrollerParser _parser;
        private Dictionary<string, byte> _currentFuseValues;

        public event PropertyChangedEventHandler PropertyChanged;

        public List<string> AvailableFuses { get; private set; }
        public Dictionary<string, List<FuseBitOption>> FuseBitOptions { get; private set; }

        public FuseBitProgrammer()
        {
        }

        public FuseBitProgrammer(string xmlFilePath)
        {
            _parser = new MicrocontrollerParser(xmlFilePath);
            _currentFuseValues = new Dictionary<string, byte>();
            AvailableFuses = _parser.GetAvailableFuses();
            FuseBitOptions = new Dictionary<string, List<FuseBitOption>>();

            foreach (var fuse in AvailableFuses)
            {
                FuseBitOptions[fuse] = _parser.GetFuseBitOptions(fuse);
                _currentFuseValues[fuse] = 0;
            }
        }

        public void SetFuseValue(string fuseName, byte value)
        {
            if (_currentFuseValues.ContainsKey(fuseName))
            {
                _currentFuseValues[fuseName] = value;
                OnPropertyChanged(nameof(GetFuseValue));
                OnPropertyChanged(nameof(GetFuseBitStatus));
            }
        }

        public byte GetFuseValue(string fuseName)
        {
            return _currentFuseValues.ContainsKey(fuseName) ? _currentFuseValues[fuseName] : (byte)0;
        }

        public bool GetFuseBitStatus(string fuseName, int bitPosition)
        {
            if (_currentFuseValues.ContainsKey(fuseName))
            {
                return (_currentFuseValues[fuseName] & (1 << bitPosition)) != 0;
            }
            return false;
        }

        public void SetFuseBitStatus(string fuseName, int bitPosition, bool value)
        {
            if (_currentFuseValues.ContainsKey(fuseName))
            {
                if (value)
                    _currentFuseValues[fuseName] |= (byte)(1 << bitPosition);
                else
                    _currentFuseValues[fuseName] &= (byte)~(1 << bitPosition);

                OnPropertyChanged(nameof(GetFuseValue));
                OnPropertyChanged(nameof(GetFuseBitStatus));
            }
        }

        public void ReadFusesFromMicrocontroller()
        {
            foreach (var fuse in AvailableFuses)
            {
                SetFuseValue(fuse, (byte)new Random().Next(256));
            }
        }

        public void WriteFusesToMicrocontroller()
        {
            // Tu dodaj kod do zapisu fuse bitów do mikrokontrolera
            Console.WriteLine("Writing fuses to microcontroller:");
            foreach (var fuse in AvailableFuses)
            {
                Console.WriteLine($"{fuse}: 0x{GetFuseValue(fuse):X2}");
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}