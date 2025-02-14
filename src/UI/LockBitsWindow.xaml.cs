using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.RegularExpressions;
using MicroDude.Core;
using MicroDude.Models;

namespace MicroDude.UI
{
    public partial class LockBitsWindow : Window
    {
        private readonly AvrDudeWrapper _avrDudeWrapper;
        private readonly ProgrammingStateService _programmingState;
        private readonly Style _originalWriteStyle;
        private readonly Style _originalReadStyle;
        private readonly Brush _originalWriteBackground;
        private readonly Brush _originalWriteForeground;
        private readonly Brush _originalReadBackground;
        private readonly Brush _originalReadForeground;
        private const string WRITE_BUTTON_CONTENT = "Write";
        private const string READ_BUTTON_CONTENT = "Read";
        private readonly System.Windows.Threading.DispatcherTimer _writeButtonTimer;
        private readonly System.Windows.Threading.DispatcherTimer _readButtonTimer;
        private bool _isUpdatingUI;

        private class LockBitsConfig
        {
            public byte LPM { get; set; }     // Flash Protection Mode
            public byte BLB { get; set; }     // Boot Lock Bits
            public byte LB { get; set; }      // Lock Bits
            public string Description { get; set; }
        }

        public LockBitsWindow()
        {
            try
            {
                InitializeComponent();
                _programmingState = ProgrammingStateService.Instance;
                string extensionDirectory = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);
                string avrDudeExePath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.exe");
                string avrDudeConfigPath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.conf");
                _avrDudeWrapper = new AvrDudeWrapper(avrDudeExePath, avrDudeConfigPath);

                // Store original button states
                _originalWriteStyle = Write.Style;
                _originalWriteBackground = Write.Background;
                _originalWriteForeground = Write.Foreground;

                _originalReadStyle = Read.Style;
                _originalReadBackground = Read.Background;
                _originalReadForeground = Read.Foreground;

                // Initialize timers
                _writeButtonTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _writeButtonTimer.Tick += (s, e) => ResetButtonAppearance(Write, WRITE_BUTTON_CONTENT);

                _readButtonTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _readButtonTimer.Tick += (s, e) => ResetButtonAppearance(Read, READ_BUTTON_CONTENT);

                InitializeUI();
                UpdateCurrentConfigText();

                //if (_programmer == null)
                //{
                //    Write.IsEnabled = false;
                //    Read.IsEnabled = false;
                //    Write.ToolTip = "Programmer not available";
                //    Read.ToolTip = "Programmer not available";
                //}
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing LockBitsWindow: {ex}");
                MessageBox.Show($"Error initializing Lock Bits window: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeUI()
        {
            // Initialize Boot Section Size ComboBox
            BootSectionSize.Items.Clear();
            BootSectionSize.Items.Add(new ComboBoxItem { Content = "512 words" });
            BootSectionSize.Items.Add(new ComboBoxItem { Content = "1024 words" });
            BootSectionSize.Items.Add(new ComboBoxItem { Content = "2048 words" });
            BootSectionSize.Items.Add(new ComboBoxItem { Content = "4096 words" });
            BootSectionSize.SelectedIndex = 0;

            // Set default selections for protection modes
            var firstFlashOption = FlashProtectionPanel.Children.OfType<RadioButton>().FirstOrDefault();
            if (firstFlashOption != null) firstFlashOption.IsChecked = true;

            var firstEepromOption = EepromProtectionPanel.Children.OfType<RadioButton>().FirstOrDefault();
            if (firstEepromOption != null) firstEepromOption.IsChecked = true;

            // Hook up event handlers
            foreach (RadioButton rb in FlashProtectionPanel.Children.OfType<RadioButton>())
            {
                rb.Checked += Protection_Changed;
            }
            foreach (RadioButton rb in EepromProtectionPanel.Children.OfType<RadioButton>())
            {
                rb.Checked += Protection_Changed;
            }
            BootSectionEnabled.Checked += Protection_Changed;
            BootSectionEnabled.Unchecked += Protection_Changed;
            BootSectionSize.SelectionChanged += Protection_Changed;
            BootSectionProtection.Checked += Protection_Changed;
            BootSectionProtection.Unchecked += Protection_Changed;
        }

        private void Protection_Changed(object sender, EventArgs e)
        {
            if (!_isUpdatingUI)
            {
                UpdateCurrentConfigText();
            }
        }

        private LockBitsConfig GetCurrentConfiguration()
        {
            byte lpm = 0;
            byte blb = 0;
            byte lb = 0;

            // Get Flash Protection Mode
            var selectedFlash = FlashProtectionPanel.Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked == true);
            if (selectedFlash != null)
            {
                lpm = (byte)FlashProtectionPanel.Children.OfType<RadioButton>().ToList().IndexOf(selectedFlash);
            }

            // Get EEPROM Protection Mode
            var selectedEeprom = EepromProtectionPanel.Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked == true);
            if (selectedEeprom != null)
            {
                blb |= (byte)(EepromProtectionPanel.Children.OfType<RadioButton>().ToList().IndexOf(selectedEeprom) << 2);
            }

            // Get Boot Section Configuration
            if (BootSectionEnabled.IsChecked == true)
            {
                lb |= 0x04;  // Enable Boot Section
                lb |= (byte)(BootSectionSize.SelectedIndex << 2);  // Boot Size
                if (BootSectionProtection.IsChecked == true)
                {
                    lb |= 0x02;  // Boot Section Protection
                }
            }

            return new LockBitsConfig
            {
                LPM = lpm,
                BLB = blb,
                LB = lb,
                Description = GetConfigurationDescription()
            };
        }

        private void UpdateUIFromLockBits(Dictionary<string, byte> fuseValues)
        {
            _isUpdatingUI = true;
            try
            {
                if (!fuseValues.ContainsKey("lockb"))
                {
                    Logger.Log("Lock bits value not found in parsed values");
                    return;
                }

                byte lockBits = fuseValues["lockb"];

                // Update Flash Protection Mode
                byte lpmValue = (byte)(lockBits & 0x03);
                var flashButtons = FlashProtectionPanel.Children.OfType<RadioButton>().ToList();
                if (lpmValue < flashButtons.Count)
                {
                    flashButtons[lpmValue].IsChecked = true;
                }

                // Update EEPROM Protection Mode
                byte blbValue = (byte)((lockBits >> 2) & 0x03);
                var eepromButtons = EepromProtectionPanel.Children.OfType<RadioButton>().ToList();
                if (blbValue < eepromButtons.Count)
                {
                    eepromButtons[blbValue].IsChecked = true;
                }

                // Update Boot Section Configuration
                bool bootEnabled = (lockBits & 0x04) != 0;
                BootSectionEnabled.IsChecked = bootEnabled;

                if (bootEnabled)
                {
                    int bootSize = (lockBits >> 2) & 0x03;
                    if (bootSize < BootSectionSize.Items.Count)
                    {
                        BootSectionSize.SelectedIndex = bootSize;
                    }

                    BootSectionProtection.IsChecked = (lockBits & 0x02) != 0;
                }

                UpdateCurrentConfigText();
                ActualLockBitsText.Text = $"Lock Bits: 0x{lockBits:X2}\n" +
                                        GetLockBitsDescription(lockBits);
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private string GetLockBitsDescription(byte lockBits)
        {
            var description = new StringBuilder();
            description.AppendLine("\nLock Bits Breakdown:");

            // Flash Protection
            byte lpm = (byte)(lockBits & 0x03);
            description.AppendLine($"Flash Protection (LPM): {GetFlashProtectionDescription(lpm)}");

            // EEPROM Protection
            byte blb = (byte)((lockBits >> 2) & 0x03);
            description.AppendLine($"EEPROM Protection (BLB): {GetEepromProtectionDescription(blb)}");

            // Boot Section
            if ((lockBits & 0x04) != 0)
            {
                int bootSize = (lockBits >> 2) & 0x03;
                description.AppendLine($"Boot Section: Enabled");
                description.AppendLine($"Boot Size: {GetBootSizeDescription(bootSize)}");
                description.AppendLine($"Boot Protection: {((lockBits & 0x02) != 0 ? "Enabled" : "Disabled")}");
            }
            else
            {
                description.AppendLine("Boot Section: Disabled");
            }

            return description.ToString();
        }

        private string GetFlashProtectionDescription(byte mode)
        {
            switch (mode)
            {
                case 0: return "No Protection";
                case 1: return "Programming Disabled";
                case 2: return "Programming and Verification Disabled";
                default: return "Unknown Protection Mode";
            }
        }

        private string GetEepromProtectionDescription(byte mode)
        {
            switch (mode)
            {
                case 0: return "No Protection";
                case 1: return "Programming Disabled";
                default: return "Unknown Protection Mode";
            }
        }

        private string GetBootSizeDescription(int size)
        {
            switch (size)
            {
                case 0: return "512 words";
                case 1: return "1024 words";
                case 2: return "2048 words";
                case 3: return "4096 words";
                default: return "Unknown Size";
            }
        }

        private Dictionary<string, byte> ParseFuseValues(string output)
        {
            var fuseValues = new Dictionary<string, byte>();
            Logger.Log($"Parsing fuse values from output: {output}");

            try
            {
                // Look for lock bits value in the output
                var match = Regex.Match(output, @"lockb\s*=\s*0x([0-9A-Fa-f]{2})");
                if (match.Success)
                {
                    fuseValues["lockb"] = Convert.ToByte(match.Groups[1].Value, 16);
                    Logger.Log($"Found lock bits value: 0x{fuseValues["lockb"]:X2}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing fuse values: {ex.Message}");
            }

            return fuseValues;
        }

        private void UpdateCurrentConfigText()
        {
            var config = GetCurrentConfiguration();
            CurrentConfigText.Text = config.Description;
            ActualLockBitsText.Text = $"Lock Bits: 0x{config.LB:X2}\n" +
                                    GetLockBitsDescription(config.LB);
        }

        private void ShowButtonFeedback(Button button, bool success, string message)
        {
            var timer = button == Write ? _writeButtonTimer : _readButtonTimer;
            timer.Stop();

            button.Content = message;
            button.Background = new SolidColorBrush(success ? Colors.LightGreen : Colors.LightPink);
            button.Foreground = new SolidColorBrush(Colors.Black);

            timer.Start();
            button.IsEnabled = true;
        }

        private void ResetButtonAppearance(Button button, string content)
        {
            button.Style = button == Write ? _originalWriteStyle : _originalReadStyle;
            button.Background = button == Write ? _originalWriteBackground : _originalReadBackground;
            button.Foreground = button == Write ? _originalWriteForeground : _originalReadForeground;
            button.Content = content;
            button.IsEnabled = true;
        }

        private void Write_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Write.IsEnabled = false;
                ResetButtonAppearance(Read, READ_BUTTON_CONTENT);
                _readButtonTimer.Stop();

                if (!_programmingState.IsReadyToProgram())
                {
                    ShowButtonFeedback(Write, false, "Not Ready");
                    return;
                }

                var config = GetCurrentConfiguration();
                var parameters = _programmingState.GetProgrammingParameters();

                var result = _avrDudeWrapper.WriteFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port,
                    null,  // Don't write low fuse
                    null,  // Don't write high fuse
                    $"0x{config.LB:X2}");  // Write lock bits

                if (result.Success)
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Lock bits configuration applied successfully");
                    ShowButtonFeedback(Write, true, "Success!");
                }
                else
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to write lock bits: {result.Error}");
                    ShowButtonFeedback(Write, false, "Failed!");
                }
            }
            catch (Exception ex)
            {
                OutputPaneHandler.PrintTextToOutputPane($"Error applying lock bits: {ex.Message}");
                ShowButtonFeedback(Write, false, "Error!");
            }
        }

        private void Read_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Read.IsEnabled = false;
                ResetButtonAppearance(Write, WRITE_BUTTON_CONTENT);
                _writeButtonTimer.Stop();

                if (!_programmingState.IsReadyToProgram())
                {
                    ShowButtonFeedback(Read, false, "Not Ready");
                    return;
                }

                var parameters = _programmingState.GetProgrammingParameters();
                var result = _avrDudeWrapper.ReadFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port);

                if (!result.Success)
                {
                    ShowButtonFeedback(Read, false, "Failed!");
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to read lock bits: {result.Error}");
                    return;
                }

                var fuseValues = ParseFuseValues(result.Output + "\n" + result.Error);
                if (fuseValues != null && fuseValues.Any())
                {
                    UpdateUIFromLockBits(fuseValues);
                    ShowButtonFeedback(Read, true, "Success!");
                    OutputPaneHandler.PrintTextToOutputPane("Lock bits read successfully");
                }
                else
                {
                    ShowButtonFeedback(Read, false, "Failed!");
                    OutputPaneHandler.PrintTextToOutputPane("Could not parse lock bits values");
                }
            }
            catch (Exception ex)
            {
                OutputPaneHandler.PrintTextToOutputPane($"Error reading lock bits: {ex.Message}");
                ShowButtonFeedback(Read, false, "Error!");
            }
        }

        private string GetConfigurationDescription()
        {
            var description = new StringBuilder();

            // Flash Protection
            var selectedFlash = FlashProtectionPanel.Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked == true);
            if (selectedFlash != null)
            {
                description.AppendLine($"Flash: {selectedFlash.Content}");
            }

            // EEPROM Protection
            var selectedEeprom = EepromProtectionPanel.Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked == true);
            if (selectedEeprom != null)
            {
                description.AppendLine($"EEPROM: {selectedEeprom.Content}");
            }

            // Boot Section
            if (BootSectionEnabled.IsChecked == true)
            {
                var bootSize = (BootSectionSize.SelectedItem as ComboBoxItem)?.Content.ToString();
                description.AppendLine($"Boot Section: Enabled, Size: {bootSize}");
                if (BootSectionProtection.IsChecked == true)
                {
                    description.AppendLine("Boot Section Protection: Enabled");
                }
            }
            else
            {
                description.AppendLine("Boot Section: Disabled");
            }

            return description.ToString();
        }

        private void SwitchToFuseBits_Click(object sender, RoutedEventArgs e)
        {
            var fuseBitsWindow = new FuseBitsWindow();
            fuseBitsWindow.Show();
            this.Close();
        }
    }
}