using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MicroDude.Models;
using MicroDude.Core;

namespace MicroDude.Services
{
    public class FuseBitsService : INotifyPropertyChanged
    {
        private readonly object _lock = new object();
        private byte _lowFuse;
        private byte _highFuse;
        private byte _extendedFuse;
        private byte _lockBits;
        private bool _hasReadFuses;
        private Dictionary<string, List<FuseBitOption>> _fuseBitOptions;
        private Dictionary<string, ValueGroup> _valueGroups;
        private Microcontroller _currentMicrocontroller;

        public event PropertyChangedEventHandler PropertyChanged;

        public FuseBitsService()
        {
            InitializeFuseBitData();
        }

        private void InitializeFuseBitData()
        {
            _lowFuse = 0;
            _highFuse = 0;
            _extendedFuse = 0;
            _lockBits = 0;
            _hasReadFuses = false;
            _fuseBitOptions = new Dictionary<string, List<FuseBitOption>>();
            _valueGroups = new Dictionary<string, ValueGroup>();
        }

        public byte LowFuse
        {
            get { lock (_lock) return _lowFuse; }
            private set
            {
                lock (_lock)
                {
                    if (_lowFuse != value)
                    {
                        _lowFuse = value;
                        OnPropertyChanged(nameof(LowFuse));
                    }
                }
            }
        }

        public byte HighFuse
        {
            get { lock (_lock) return _highFuse; }
            private set
            {
                lock (_lock)
                {
                    if (_highFuse != value)
                    {
                        _highFuse = value;
                        OnPropertyChanged(nameof(HighFuse));
                    }
                }
            }
        }

        public byte ExtendedFuse
        {
            get { lock (_lock) return _extendedFuse; }
            private set
            {
                lock (_lock)
                {
                    if (_extendedFuse != value)
                    {
                        _extendedFuse = value;
                        OnPropertyChanged(nameof(ExtendedFuse));
                    }
                }
            }
        }

        public byte LockBits
        {
            get { lock (_lock) return _lockBits; }
            private set
            {
                lock (_lock)
                {
                    if (_lockBits != value)
                    {
                        _lockBits = value;
                        OnPropertyChanged(nameof(LockBits));
                    }
                }
            }
        }

        public bool HasReadFuses
        {
            get { lock (_lock) return _hasReadFuses; }
            private set
            {
                lock (_lock)
                {
                    if (_hasReadFuses != value)
                    {
                        _hasReadFuses = value;
                        OnPropertyChanged(nameof(HasReadFuses));
                    }
                }
            }
        }

        public Dictionary<string, List<FuseBitOption>> FuseBitOptions
        {
            get
            {
                lock (_lock)
                {
                    return _fuseBitOptions;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _fuseBitOptions = value;
                    OnPropertyChanged(nameof(FuseBitOptions));
                    OnPropertyChanged(nameof(AvailableFuses));
                }
            }
        }

        public List<string> AvailableFuses
        {
            get
            {
                lock (_lock)
                {
                    return _fuseBitOptions?.Keys.ToList() ?? new List<string>();
                }
            }
        }

        public void UpdateDevice(Microcontroller microcontroller)
        {
            lock (_lock)
            {
                _currentMicrocontroller = microcontroller;
                UpdateFuseBitConfiguration();
                ClearFuseValues();
            }
        }

        private void UpdateFuseBitConfiguration()
        {
            lock (_lock)
            {
                if (_currentMicrocontroller == null)
                {
                    FuseBitOptions = new Dictionary<string, List<FuseBitOption>>();
                    _valueGroups = new Dictionary<string, ValueGroup>();
                    return;
                }

                Dictionary<string, List<FuseBitOption>> newOptions = new Dictionary<string, List<FuseBitOption>>();
                _valueGroups = _currentMicrocontroller.ValueGroups;

                // Process fuse registers
                foreach (FuseRegister register in _currentMicrocontroller.FuseRegisters)
                {
                    List<FuseBitOption> optionsList = new List<FuseBitOption>();
                    foreach (Bitfield bitfield in register.Bitfields)
                    {
                        ValueGroup foundGroup;
                        if (_valueGroups.TryGetValue(bitfield.ValueGroupName, out foundGroup))
                        {
                            foreach (ConfigValue configValue in foundGroup.Values)
                            {
                                optionsList.Add(new FuseBitOption
                                {
                                    Caption = configValue.Caption,
                                    Name = configValue.Name,
                                    Value = configValue.Value.ToString("X2")
                                });
                            }
                        }
                    }
                    if (optionsList.Count > 0)
                    {
                        newOptions[register.Name] = optionsList;
                    }
                }

                FuseBitOptions = newOptions;
                Logger.Log(string.Format("Updated fuse bit configuration for {0}", _currentMicrocontroller.Id));
            }
        }

        public void UpdateFuseValues(Dictionary<string, byte> fuseValues)
        {
            lock (_lock)
            {
                byte value;
                if (fuseValues.TryGetValue("lfuse", out value))
                    LowFuse = value;
                if (fuseValues.TryGetValue("hfuse", out value))
                    HighFuse = value;
                if (fuseValues.TryGetValue("efuse", out value))
                    ExtendedFuse = value;
                if (fuseValues.TryGetValue("lockb", out value))
                    LockBits = value;

                HasReadFuses = true;
                Logger.Log(string.Format("Fuse values updated - Low: 0x{0:X2}, High: 0x{1:X2}, Extended: 0x{2:X2}, Lock: 0x{3:X2}",
                    LowFuse, HighFuse, ExtendedFuse, LockBits));
            }
        }

        public void ClearFuseValues()
        {
            lock (_lock)
            {
                LowFuse = 0;
                HighFuse = 0;
                ExtendedFuse = 0;
                LockBits = 0;
                HasReadFuses = false;
                Logger.Log("Fuse values cleared");
            }
        }


        public List<FuseBitOption> GetFuseBitOptions(string fuseName)
        {
            lock (_lock)
            {
                List<FuseBitOption> foundOptions;
                if (_fuseBitOptions != null && _fuseBitOptions.TryGetValue(fuseName, out foundOptions))
                {
                    return foundOptions;
                }
                return new List<FuseBitOption>();
            }
        }


        public string GetFuseDescription(string fuseName)
        {
            lock (_lock)
            {
                if (_currentMicrocontroller?.FuseRegisters == null)
                    return string.Empty;

                var register = _currentMicrocontroller.FuseRegisters.FirstOrDefault(r => r.Name == fuseName);
                return register?.Caption ?? string.Empty;
            }
        }

        public void ApplyFuseOption(string fuseName, string optionValue)
        {
            lock (_lock)
            {
                byte parsedValue;
                if (byte.TryParse(optionValue, System.Globalization.NumberStyles.HexNumber, null, out parsedValue))
                {
                    switch (fuseName.ToLower())
                    {
                        case "lfuse":
                            LowFuse = parsedValue;
                            break;
                        case "hfuse":
                            HighFuse = parsedValue;
                            break;
                        case "efuse":
                            ExtendedFuse = parsedValue;
                            break;
                        default:
                            Logger.Log(string.Format("Unknown fuse type: {0}", fuseName));
                            break;
                    }
                    Logger.Log(string.Format("Applied {0} option: 0x{1:X2}", fuseName, parsedValue));
                }
            }
        }

        public byte GetFuseValue(string fuseName)
        {
            lock (_lock)
            {
                switch (fuseName.ToLower())
                {
                    case "lfuse":
                        return LowFuse;
                    case "hfuse":
                        return HighFuse;
                    case "efuse":
                        return ExtendedFuse;
                    case "lockb":
                        return LockBits;
                    default:
                        return 0;
                }
            }
        }

        public Dictionary<string, byte> GetAllFuseValues()
        {
            lock (_lock)
            {
                return new Dictionary<string, byte>
                {
                    { "lfuse", LowFuse },
                    { "hfuse", HighFuse },
                    { "efuse", ExtendedFuse },
                    { "lockb", LockBits }
                };
            }
        }

        public byte GetInvertedFuseBits(string fuseType)
        {
            lock (_lock)
            {
                switch (fuseType.ToLower())
                {
                    case "lfuse":
                        return (byte)~LowFuse;
                    case "hfuse":
                        return (byte)~HighFuse;
                    case "efuse":
                        return (byte)~ExtendedFuse;
                    case "lockb":
                        return (byte)~LockBits;
                    default:
                        throw new ArgumentException($"Invalid fuse type: {fuseType}");
                }
            }
        }

        public void SetFuseBit(string fuseType, int bitPosition, bool value)
        {
            lock (_lock)
            {
                byte currentValue;
                switch (fuseType.ToLower())
                {
                    case "lfuse":
                        currentValue = LowFuse;
                        if (value)
                            currentValue |= (byte)(1 << bitPosition);
                        else
                            currentValue &= (byte)~(1 << bitPosition);
                        LowFuse = currentValue;
                        break;

                    case "hfuse":
                        currentValue = HighFuse;
                        if (value)
                            currentValue |= (byte)(1 << bitPosition);
                        else
                            currentValue &= (byte)~(1 << bitPosition);
                        HighFuse = currentValue;
                        break;

                    case "efuse":
                        currentValue = ExtendedFuse;
                        if (value)
                            currentValue |= (byte)(1 << bitPosition);
                        else
                            currentValue &= (byte)~(1 << bitPosition);
                        ExtendedFuse = currentValue;
                        break;

                    case "lockb":
                        currentValue = LockBits;
                        if (value)
                            currentValue |= (byte)(1 << bitPosition);
                        else
                            currentValue &= (byte)~(1 << bitPosition);
                        LockBits = currentValue;
                        break;

                    default:
                        throw new ArgumentException($"Invalid fuse type: {fuseType}");
                }
                Logger.Log($"Set {fuseType} bit {bitPosition} to {value}");
            }
        }

        public bool GetFuseBit(string fuseType, int bitPosition)
        {
            lock (_lock)
            {
                byte value = GetFuseValue(fuseType);
                return (value & (1 << bitPosition)) != 0;
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}