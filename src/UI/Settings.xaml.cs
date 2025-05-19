using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Win32;
using System.IO;
using MicroDude.Parsers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MicroDude.Models;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using MicroDude.Properties;
using System.Collections.Specialized;
using MicroDude.Services;
using System.Management;

namespace MicroDude.UI
{
    public partial class Settings : Window, INotifyPropertyChanged
    {
        #region Properties
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public OutputDestination SelectedOutputDestination
        {
            get { return (OutputDestination)MicroDudeSettings.Default.OutputDestination; }
            set
            {
                MicroDudeSettings.Default.OutputDestination = (int)value;
                OnPropertyChanged();
            }
        }

        public bool AutoDetectMicrocontroller
        {
            get { return MicroDudeSettings.Default.AutoDetectMicrocontroller; }
            set
            {
                if (MicroDudeSettings.Default.AutoDetectMicrocontroller != value)
                {
                    MicroDudeSettings.Default.AutoDetectMicrocontroller = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoDetectUsb
        {
            get { return MicroDudeSettings.Default.AutoDetectUsb; }
            set
            {
                if (MicroDudeSettings.Default.AutoDetectUsb != value)
                {
                    MicroDudeSettings.Default.AutoDetectUsb = value;
                    OnPropertyChanged();
                }
            }
        }

        private Programmer _selectedProgrammer;
        public Programmer SelectedProgrammer
        {
            get { return _selectedProgrammer; }
            set
            {
                if (_selectedProgrammer != value)
                {
                    _selectedProgrammer = value;
                    OnPropertyChanged();
                    if (_selectedProgrammer != null)
                    {
                        Programmer = _selectedProgrammer.Id;
                    }
                }
            }
        }

        private ObservableCollection<object> _programmersDisplay;
        public ObservableCollection<object> ProgrammersDisplay
        {
            get { return _programmersDisplay; }
            set
            {
                _programmersDisplay = value;
                OnPropertyChanged();
            }
        }

        private string _selectedPort;
        public string SelectedPort
        {
            get { return _selectedPort; }
            set
            {
                if (_selectedPort != value)
                {
                    _selectedPort = value;
                    MicroDudeSettings.Default.Port = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<PortInfo> _availablePorts;
        public ObservableCollection<PortInfo> AvailablePorts
        {
            get { return _availablePorts; }
            set
            {
                _availablePorts = value;
                OnPropertyChanged();
            }
        }

        public string Programmer
        {
            get { return MicroDudeSettings.Default.Programmer; }
            set
            {
                if (MicroDudeSettings.Default.Programmer != value)
                {
                    MicroDudeSettings.Default.Programmer = value;
                    OnPropertyChanged();
                    UpdateRecentlyUsedProgrammers(value);
                }
            }
        }

        private ObservableCollection<Programmer> _allProgrammers;
        public ObservableCollection<Programmer> AllProgrammers
        {
            get { return _allProgrammers; }
            set
            {
                _allProgrammers = value;
                OnPropertyChanged();
            }
        }

        private string _avrDudePath;
        public string AvrDudePath
        {
            get { return _avrDudePath; }
            private set
            {
                if (_avrDudePath != value)
                {
                    _avrDudePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoFlash
        {
            get { return MicroDudeSettings.Default.AutoFlash; }
            set
            {
                if (MicroDudeSettings.Default.AutoFlash != value)
                {
                    MicroDudeSettings.Default.AutoFlash = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ColoredOutputEnabled
        {
            get { return MicroDudeSettings.Default.ColoredOutputEnabled; }
            set
            {
                if (MicroDudeSettings.Default.ColoredOutputEnabled != value)
                {
                    MicroDudeSettings.Default.ColoredOutputEnabled = value;
                    OnPropertyChanged(nameof(ColoredOutputEnabled));
                }
            }
        }

        public bool Verbose
        {
            get { return MicroDudeSettings.Default.Verbose; }
            set
            {
                if (MicroDudeSettings.Default.Verbose != value)
                {
                    MicroDudeSettings.Default.Verbose = value;
                    OnPropertyChanged(nameof(Verbose));
                }
            }
        }

        private List<string> _recentlyUsedProgrammers;
        public List<string> RecentlyUsedProgrammers
        {
            get { return _recentlyUsedProgrammers; }
            set
            {
                _recentlyUsedProgrammers = value;
                OnPropertyChanged();
            }
        }

        #endregion

        public Settings()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize AvrDude path first
            InitializeAvrDudePath();

            // Load other settings
            LoadSettings();
            LoadProgrammers();
            InitializeProgrammersDisplay();

            // If we have a selected programmer, update port selection
            if (SelectedProgrammer != null)
            {
                UpdatePortSelection(SelectedProgrammer);
            }
        }

        private void InitializeAvrDudePath()
        {
            string extensionDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
            string currentAvrDudePath = Path.Combine(extensionDirectory, "AvrDude", "avrdude.exe");

            AvrDudePath = currentAvrDudePath;
            AvrdudePathTextBox.Text = currentAvrDudePath;

            if (MicroDudeSettings.Default.AvrDudePath != currentAvrDudePath)
            {
                MicroDudeSettings.Default.AvrDudePath = currentAvrDudePath;
                MicroDudeSettings.Default.Save();
            }
        }

        private void LoadSettings()
        {
            // Load other settings
            AutoFlash = MicroDudeSettings.Default.AutoFlash;
            AutoDetectUsb = MicroDudeSettings.Default.AutoDetectUsb;
            AutoDetectMicrocontroller = MicroDudeSettings.Default.AutoDetectMicrocontroller;
            OutputDestinationComboBox.ItemsSource = System.Enum.GetValues(typeof(OutputDestination));
            OutputDestinationComboBox.SelectedItem = SelectedOutputDestination;
            Programmer = MicroDudeSettings.Default.Programmer;
            RecentlyUsedProgrammers = new List<string>(
                MicroDudeSettings.Default.RecentlyUsedProgrammers?.Cast<string>() ??
                Enumerable.Empty<string>());
        }

        private void LoadProgrammers()
        {
            string avrdudeConfPath = Path.Combine(Path.GetDirectoryName(AvrDudePath), "avrdude.conf");
            Logger.Log($"Checking for avrdude.conf at: {avrdudeConfPath}");

            if (File.Exists(avrdudeConfPath))
            {
                string currentHash = CalculateFileHash(avrdudeConfPath);
                if (currentHash != MicroDudeSettings.Default.AvrdudeConfHash)
                {
                    var parser = new AvrdudeConfParser();
                    parser.ParseFile(avrdudeConfPath);
                    AllProgrammers = new ObservableCollection<Programmer>(parser.Programmers);
                    MicroDudeSettings.Default.AvrdudeConfHash = currentHash;
                    SaveProgrammersToSettings();
                    MicroDudeSettings.Default.Save();
                }
                else
                {
                    LoadProgrammersFromSettings();
                }
            }
            else
            {
                AllProgrammers = new ObservableCollection<Programmer>();
                Logger.Log($"avrdude.conf not found at path: {avrdudeConfPath}");
            }

            Logger.Log($"AllProgrammers count: {AllProgrammers.Count}");
        }

        private void UpdatePortSelection(Programmer programmer)
        {
            try
            {
                // Clear current ports
                AvailablePorts = new ObservableCollection<PortInfo>();

                if (programmer.PortType == ProgrammerPortType.USB)
                {
                    // For USB programmers, just add a single USB option
                    var usbPort = new PortInfo
                    {
                        Name = "usb",
                        Description = "Direct USB Connection"
                    };
                    AvailablePorts.Add(usbPort);
                    PortComboBox.SelectedItem = usbPort;
                    PortComboBox.IsEnabled = false;
                }
                else
                {
                    PortComboBox.IsEnabled = true;
                    // Get available ports based on programmer type
                    var ports = GetAvailablePorts(programmer.PortType);
                    foreach (var port in ports)
                    {
                        AvailablePorts.Add(port);
                    }

                    string portTypeName = programmer.PortType == ProgrammerPortType.COM ? "COM" : "LPT";

                    if (AvailablePorts.Any())
                    {
                        // Try to select previously used port or first available
                        var savedPort = MicroDudeSettings.Default.Port;
                        var portToSelect = AvailablePorts.FirstOrDefault(p => p.Name == savedPort)
                                         ?? AvailablePorts.First();
                        PortComboBox.SelectedItem = portToSelect;

                        //PortDescriptionTextBlock.Text = $"Select {portTypeName} port";
                    }
                    else
                    {
                        //PortDescriptionTextBlock.Text = $"No {portTypeName} ports detected";
                        PortComboBox.IsEnabled = false;
                    }
                }

                PortComboBox.ItemsSource = AvailablePorts;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating port selection: {ex.Message}");
                //PortDescriptionTextBlock.Text = "Error detecting ports";
                PortComboBox.IsEnabled = false;
            }
        }

        private List<PortInfo> GetAvailablePorts(ProgrammerPortType portType)
        {
            var ports = new List<PortInfo>();

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%{(portType == ProgrammerPortType.COM ? "COM" : "LPT")}%'"))
                {
                    foreach (ManagementObject port in searcher.Get())
                    {
                        string fullName = port["Name"]?.ToString();
                        if (string.IsNullOrEmpty(fullName)) continue;

                        string portName = ExtractPortName(fullName, portType == ProgrammerPortType.COM ? "COM" : "LPT");
                        if (!string.IsNullOrEmpty(portName))
                        {
                            ports.Add(new PortInfo
                            {
                                Name = portName,
                                Description = fullName
                            });
                        }
                    }
                }

                // Sort ports by number
                return ports.OrderBy(p =>
                {
                    int number;
                    return int.TryParse(new string(p.Name.Where(char.IsDigit).ToArray()),
                                       out number) ? number : 999;
                }).ToList();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting available ports: {ex.Message}");
                return new List<PortInfo>();
            }
        }

        private string ExtractPortName(string fullName, string portType)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                fullName, $@"({portType}\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Load saved port if available
            if (!string.IsNullOrEmpty(MicroDudeSettings.Default.Port))
            {
                SelectedPort = MicroDudeSettings.Default.Port;
            }
        }

        private void InitializeProgrammersDisplay()
        {
            SortProgrammers();
            Logger.Log($"ProgrammersDisplay count: {ProgrammersDisplay.Count}");
        }

        private string CalculateFileHash(string filePath)
        {
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void CheckAndLoadProgrammers()
        {
            string extensionDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
            string avrDudeDirectory = Path.Combine(extensionDirectory, "AvrDude");
            string avrdudeConfPath = Path.Combine(avrDudeDirectory, "avrdude.conf");

            Logger.Log($"Checking for avrdude.conf at: {avrdudeConfPath}");

            if (File.Exists(avrdudeConfPath))
            {
                string currentHash = CalculateFileHash(avrdudeConfPath);
                if (currentHash != MicroDudeSettings.Default.AvrdudeConfHash)
                {
                    LoadProgrammers(avrdudeConfPath);
                    MicroDudeSettings.Default.AvrdudeConfHash = currentHash;
                    SaveProgrammersToSettings();
                    MicroDudeSettings.Default.Save();
                }
                else
                {
                    LoadProgrammersFromSettings();
                }
            }
            else
            {
                AllProgrammers = new ObservableCollection<Programmer>();
                Logger.Log($"avrdude.conf not found at path: {avrdudeConfPath}");
            }
            Logger.Log($"AllProgrammers count: {AllProgrammers.Count}");
            SortProgrammers();
            Logger.Log($"ProgrammersDisplay count: {ProgrammersDisplay.Count}");
        }

        private void LoadProgrammers(string avrdudeConfPath)
        {
            var parser = new AvrdudeConfParser();
            parser.ParseFile(avrdudeConfPath);
            AllProgrammers = new ObservableCollection<Programmer>(parser.Programmers);
            SaveProgrammersToSettings();
        }

        private void SortProgrammers()
        {
            var recentProgrammers = RecentlyUsedProgrammers
                .Take(3)
                .Select(id => AllProgrammers.FirstOrDefault(p => p.Id == id))
                .Where(p => p != null)
                .ToList();

            var allSortedProgrammers = AllProgrammers.OrderBy(p => p.Id).ToList();

            ProgrammersDisplay = new ObservableCollection<object>();

            // Add recent programmers
            foreach (var programmer in recentProgrammers)
            {
                ProgrammersDisplay.Add(programmer);
            }

            // Add separator
            if (recentProgrammers.Any())
            {
                ProgrammersDisplay.Add(new Separator());
            }

            // Add all programmers
            foreach (var programmer in allSortedProgrammers)
            {
                ProgrammersDisplay.Add(programmer);
            }

            OnPropertyChanged(nameof(ProgrammersDisplay));

            // Update the ComboBox
            ProgrammerComboBox.ItemsSource = ProgrammersDisplay;

            // Set the selected item
            if (!string.IsNullOrEmpty(Programmer))
            {
                SelectedProgrammer = ProgrammersDisplay.OfType<Programmer>().FirstOrDefault(p => p.Id == Programmer);
            }
        }

        private void UpdateRecentlyUsedProgrammers(string programmerId)
        {
            if (!RecentlyUsedProgrammers.Contains(programmerId))
            {
                RecentlyUsedProgrammers.Insert(0, programmerId);
                if (RecentlyUsedProgrammers.Count > 3) // Keep only the 3 most recent
                {
                    RecentlyUsedProgrammers.RemoveAt(RecentlyUsedProgrammers.Count - 1);
                }
                MicroDudeSettings.Default.RecentlyUsedProgrammers = new StringCollection();
                MicroDudeSettings.Default.RecentlyUsedProgrammers.AddRange(RecentlyUsedProgrammers.ToArray());
                MicroDudeSettings.Default.Save();
                SortProgrammers();
            }
        }

        private void LoadProgrammersFromSettings()
        {
            var savedProgrammers = MicroDudeSettings.Default.Programmers;
            if (savedProgrammers != null && savedProgrammers.Count > 0)
            {
                AllProgrammers = new ObservableCollection<Programmer>(
                    savedProgrammers.Cast<string>()
                    .Select(s =>
                    {
                        var parts = s.Split('|');
                        var programmer = new Programmer
                        {
                            Id = parts[0],
                            Description = parts.Length > 1 ? parts[1] : string.Empty,
                            Type = parts.Length > 2 ? parts[2] : string.Empty
                        };

                // Set port type based on programmer type or ID
                if (!string.IsNullOrEmpty(programmer.UsbVid) && programmer.UsbPids.Any())
                        {
                            programmer.PortType = ProgrammerPortType.USB;
                        }
                        else if (programmer.Id.Contains("serial") || programmer.Id.Contains("arduino"))
                        {
                            programmer.PortType = ProgrammerPortType.COM;
                        }
                        else
                        {
                            programmer.PortType = ProgrammerPortType.USB; // Default to USB
                }

                        return programmer;
                    })
                );
            }
            else
            {
                AllProgrammers = new ObservableCollection<Programmer>();
            }
            SortProgrammers();
        }

        private void SaveProgrammersToSettings()
        {
            var programmerStrings = AllProgrammers.Select(p => $"{p.Id}|{p.Description}|{p.Type}");
            MicroDudeSettings.Default.Programmers = new StringCollection();
            MicroDudeSettings.Default.Programmers.AddRange(programmerStrings.ToArray());
            MicroDudeSettings.Default.Save();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AvrDudePath = openFileDialog.FileName;
                AvrdudePathTextBox.Text = AvrDudePath;
                MicroDudeSettings.Default.AvrDudePath = AvrDudePath;
                MicroDudeSettings.Default.Save();
                CheckAndLoadProgrammers();
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(AvrDudePath))
            {
                MicroDudeSettings.Default.AvrDudePath = AvrDudePath;
            }
            MicroDudeSettings.Default.AutoFlash = AutoFlash;
            MicroDudeSettings.Default.AutoDetectUsb = AutoDetectUsb;
            MicroDudeSettings.Default.AutoDetectMicrocontroller = AutoDetectMicrocontroller;
            MicroDudeSettings.Default.Programmer = Programmer;
            MicroDudeSettings.Default.OutputDestination = (int)SelectedOutputDestination;
            var selectedPort = PortComboBox.SelectedItem as PortInfo;
            if (selectedPort != null)
            {
                MicroDudeSettings.Default.Port = selectedPort.Name;
            }
            else if (SelectedProgrammer != null && SelectedProgrammer.PortType == ProgrammerPortType.USB)
            {
                MicroDudeSettings.Default.Port = "usb";
            }
            MicroDudeSettings.Default.Save();
            Close();
        }

        private void ProgrammerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedProgrammer = ProgrammerComboBox.SelectedItem as Programmer;
            if (selectedProgrammer != null)
            {
                SelectedProgrammer = selectedProgrammer;
                UpdatePortSelection(selectedProgrammer);
            }
        }

        private void ColorSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var colorSettings = new ColorSettings();
            colorSettings.Owner = this;
            colorSettings.ShowDialog();
        }

        

        private void PortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedPort = PortComboBox.SelectedItem as PortInfo;
            if (selectedPort != null)
            {
                SelectedPort = selectedPort.Name;
            }
        }

        private void BaudRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void BitClockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}