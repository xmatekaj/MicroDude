
using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Data;
using MicroDude.Core;
using MicroDude.Models;
using System.Linq;
using System.Text.RegularExpressions;

namespace MicroDude.UI
{
    public partial class FuseBitsWindow : Window
    {
        private readonly ProgrammingStateService _programmingState;
        private readonly AvrDudeWrapper _avrDudeWrapper;
        private bool _isUpdatingUI;
        private Dictionary<string, List<CheckBox>> _fuseCheckboxes;
        private Dictionary<string, TextBox> _fuseValueBoxes;

        public FuseBitsWindow()
        {
            InitializeComponent();
            _programmingState = ProgrammingStateService.Instance;
            _fuseCheckboxes = new Dictionary<string, List<CheckBox>>();
            _fuseValueBoxes = new Dictionary<string, TextBox>();

            string extensionDirectory = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);
            string avrDudeExePath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.exe");
            string avrDudeConfigPath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.conf");
            _avrDudeWrapper = new AvrDudeWrapper(avrDudeExePath, avrDudeConfigPath);

            InitializeManualConfiguration();
        }

        private void InitializeManualConfiguration()
        {
            try
            {
                var mcu = _programmingState.CurrentMicrocontroller;
                if (mcu == null || mcu.FuseRegisters == null)
                {
                    MessageBox.Show("No microcontroller selected", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Initialize collections
                _fuseCheckboxes.Clear();
                _fuseValueBoxes.Clear();

                // Create controls for each fuse register
                foreach (var register in mcu.FuseRegisters)
                {
                    // Create list for checkboxes if not exists
                    if (!_fuseCheckboxes.ContainsKey(register.Name))
                    {
                        _fuseCheckboxes[register.Name] = new List<CheckBox>();
                    }

                    var panel = GetFusePanel(register.Name);
                    if (panel != null)
                    {
                        panel.Children.Clear();
                        CreateBitCheckboxes(panel, register);
                    }

                    // Map textbox
                    var textBox = GetFuseValueBox(register.Name);
                    if (textBox != null)
                    {
                        _fuseValueBoxes[register.Name] = textBox;
                        textBox.Text = "00"; // Initialize with 0
                        textBox.TextChanged += FuseValue_TextChanged;
                    }
                }

                // Try to read current values
                if (_programmingState.IsReadyToProgram())
                {
                    ReadCurrentValues();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing manual configuration: {ex.Message}");
                MessageBox.Show($"Error initializing manual configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private StackPanel GetFusePanel(string registerName)
        {
            switch (registerName.ToLower())
            {
                case "low": return LowFusePanel;
                case "high": return HighFusePanel;
                case "extended": return ExtendedFusePanel;
                default: return null;
            }
        }

        private TextBox GetFuseValueBox(string registerName)
        {
            switch (registerName.ToLower())
            {
                case "low": return LowFuseValue;
                case "high": return HighFuseValue;
                case "extended": return ExtendedFuseValue;
                default: return null;
            }
        }

        private void CreateBitCheckboxes(StackPanel panel, FuseRegister register)
        {
            // Create checkboxes for each bit, starting from bit 7 down to 0
            for (int bit = 7; bit >= 0; bit--)
            {
                var bitfield = register.Bitfields.FirstOrDefault(b => (b.Mask & (1 << bit)) != 0);

                var checkbox = new CheckBox
                {
                    Content = bitfield != null ? bitfield.Caption ?? bitfield.Name : $"Bit {bit}",
                    Tag = new { Register = register.Name, Bit = bit, Mask = bitfield?.Mask ?? (1 << bit) },
                    Margin = new Thickness(5),
                    ToolTip = bitfield?.Caption
                };

                checkbox.Checked += BitCheckbox_CheckedChanged;
                checkbox.Unchecked += BitCheckbox_CheckedChanged;

                _fuseCheckboxes[register.Name].Add(checkbox);
                panel.Children.Add(checkbox);
            }
        }

        private void BitCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;

            var checkbox = sender as CheckBox;
            if (checkbox?.Tag == null) return;

            dynamic tag = checkbox.Tag;
            string register = tag.Register;
            int bit = tag.Bit;

            var textBox = _fuseValueBoxes[register];
            if (textBox == null) return;

            byte currentValue;
            if (byte.TryParse(textBox.Text, System.Globalization.NumberStyles.HexNumber, null, out currentValue))
            {
                if (checkbox.IsChecked == true)
                    currentValue |= (byte)(1 << bit);
                else
                    currentValue &= (byte)~(1 << bit);

                textBox.Text = currentValue.ToString("X2");
            }
        }

        private void FuseValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI) return;

            var textBox = sender as TextBox;
            if (textBox?.Tag == null) return;

            string register = textBox.Tag.ToString();
            byte value;
            if (byte.TryParse(textBox.Text, System.Globalization.NumberStyles.HexNumber, null, out value))
            {
                UpdateFuseDisplay(register, value);
            }
        }

        private void UpdateFuseDisplay(string register, byte value)
        {
            try
            {
                _isUpdatingUI = true;

                // Update checkboxes
                if (_fuseCheckboxes.ContainsKey(register))
                {
                    foreach (var checkbox in _fuseCheckboxes[register])
                    {
                        dynamic tag = checkbox.Tag;
                        int bit = tag.Bit;
                        checkbox.IsChecked = (value & (1 << bit)) != 0;
                    }
                }
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private void ReadCurrentValues()
        {
            try
            {
                if (!_programmingState.IsReadyToProgram())
                {
                    MessageBox.Show("Device or programmer not ready", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var parameters = _programmingState.GetProgrammingParameters();
                var result = _avrDudeWrapper.ReadFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port);

                if (result.Success)
                {
                    OutputPaneHandler.PrintTextToOutputPane("Fuses read successfully");
                    ParseAndUpdateFuseValues(result.Output + "\n" + result.Error);
                }
                else
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to read fuses: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading fuses: {ex.Message}");
                MessageBox.Show($"Error reading fuses: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ParseAndUpdateFuseValues(string output)
        {
            Logger.Log($"Parsing fuse values from output: {output}");
            try
            {
                Dictionary<string, byte> fuseValues = new Dictionary<string, byte>();
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string line in lines)
                {
                    if (line.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        string hexValue = line.Trim();
                        byte value;
                        if (byte.TryParse(hexValue.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value))
                        {
                            // Assign values in order: low, high, extended
                            if (!fuseValues.ContainsKey("low"))
                                fuseValues["low"] = value;
                            else if (!fuseValues.ContainsKey("high"))
                                fuseValues["high"] = value;
                            else if (!fuseValues.ContainsKey("extended"))
                                fuseValues["extended"] = value;
                        }
                    }
                }

                // Update displays
                foreach (var kvp in fuseValues)
                {
                    if (_fuseValueBoxes.ContainsKey(kvp.Key))
                    {
                        _fuseValueBoxes[kvp.Key].Text = kvp.Value.ToString("X2");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing fuse values: {ex.Message}");
            }
        }

        private void ReadFuses_Click(object sender, RoutedEventArgs e)
        {
            ReadCurrentValues();
        }

        private void WriteFuses_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_programmingState.IsReadyToProgram())
                {
                    MessageBox.Show("Device or programmer not ready", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get values from textboxes
                Dictionary<string, string> fuseValues = new Dictionary<string, string>();
                foreach (var kvp in _fuseValueBoxes)
                {
                    if (!string.IsNullOrEmpty(kvp.Value.Text))
                    {
                        fuseValues[kvp.Key] = kvp.Value.Text;
                    }
                }

                var parameters = _programmingState.GetProgrammingParameters();
                var result = _avrDudeWrapper.WriteFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port,
                    fuseValues.ContainsKey("low") ? $"0x{fuseValues["low"]}" : null,
                    fuseValues.ContainsKey("high") ? $"0x{fuseValues["high"]}" : null,
                    fuseValues.ContainsKey("extended") ? $"0x{fuseValues["extended"]}" : null);

                if (result.Success)
                {
                    OutputPaneHandler.PrintTextToOutputPane("Fuses written successfully");
                }
                else
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to write fuses: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error writing fuses: {ex.Message}");
                MessageBox.Show($"Error writing fuses: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}