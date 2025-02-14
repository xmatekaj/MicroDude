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

        // Dictionary to hold checkbox collections for each fuse type
        private readonly Dictionary<string, List<CheckBox>> _fuseCheckboxes;
        private readonly Dictionary<string, TextBox> _fuseValueBoxes;

        public List<FuseBitOption> SelectedFuseOptions { get; set; }

        public FuseBitsWindow()
        {
            InitializeComponent();
            _programmingState = ProgrammingStateService.Instance;

            string extensionDirectory = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);
            string avrDudeExePath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.exe");
            string avrDudeConfigPath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.conf");
            _avrDudeWrapper = new AvrDudeWrapper(avrDudeExePath, avrDudeConfigPath);

            _fuseCheckboxes = new Dictionary<string, List<CheckBox>>();
            _fuseValueBoxes = new Dictionary<string, TextBox>();

            InitializeFuseBits();
        }

        private void InitializeFuseBits()
        {
            try
            {
                // Initialize dictionaries
                _fuseCheckboxes["low"] = new List<CheckBox>();
                _fuseCheckboxes["high"] = new List<CheckBox>();
                _fuseCheckboxes["extended"] = new List<CheckBox>();

                // Map textboxes
                _fuseValueBoxes["low"] = LowFuseValue;
                _fuseValueBoxes["high"] = HighFuseValue;
                _fuseValueBoxes["extended"] = ExtendedFuseValue;

                // Create checkboxes for each fuse type
                CreateCheckboxes(LowFusePanel, "low");
                CreateCheckboxes(HighFusePanel, "high");
                CreateCheckboxes(ExtendedFusePanel, "extended");

                // Initialize hex value textboxes
                InitializeTextBoxes();

                // Initialize Presets section
                LoadFusePresets();

                // Try to read current values
                if (_programmingState.IsReadyToProgram())
                {
                    ReadCurrentValues();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing fuse bits: {ex.Message}");
                MessageBox.Show($"Error initializing fuse bits: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeTextBoxes()
        {
            foreach (var textBox in _fuseValueBoxes.Values)
            {
                textBox.MaxLength = 2;
                textBox.Text = "00";
            }
        }

        private void CreateCheckboxes(StackPanel panel, string fuseType)
        {
            Logger.Log($"Creating checkboxes for {fuseType} fuse");
            panel.Children.Clear();

            List<CheckBox> checkboxes = new List<CheckBox>();
            _fuseCheckboxes[fuseType] = checkboxes; // Ensure we store the list

            // Create 8 checkboxes for each bit, starting from bit 7
            for (int i = 7; i >= 0; i--)
            {
                var checkbox = new CheckBox
                {
                    Content = $"Bit {i}",
                    Tag = i,
                    Margin = new Thickness(5)
                };

                checkbox.Checked += (s, e) => OnCheckboxChanged(fuseType);
                checkbox.Unchecked += (s, e) => OnCheckboxChanged(fuseType);

                checkboxes.Add(checkbox);
                panel.Children.Add(checkbox);
            }
            Logger.Log($"Created {checkboxes.Count} checkboxes for {fuseType} fuse");
        }


        private void LoadFusePresets()
        {
            var mcu = _programmingState.CurrentMicrocontroller;
            if (mcu == null || mcu.FuseRegisters == null)
            {
                FuseSelector.IsEnabled = false;
                OptionSelector.IsEnabled = false;
                return;
            }

            // Add available fuse options to selector
            var fuseOptions = mcu.FuseRegisters
                .Select(f => f.Name)
                .ToList();

            FuseSelector.ItemsSource = fuseOptions;
            if (fuseOptions.Any())
            {
                FuseSelector.SelectedIndex = 0;
            }
        }

        private void OnCheckboxChanged(string fuseType)
        {
            if (_isUpdatingUI) return;

            try
            {
                var checkboxes = _fuseCheckboxes[fuseType];
                var valueBox = _fuseValueBoxes[fuseType];

                byte value = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (checkboxes[7 - i].IsChecked == true)
                    {
                        value |= (byte)(1 << i);
                    }
                }

                _isUpdatingUI = true;
                valueBox.Text = value.ToString("X2");
                _isUpdatingUI = false;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating fuse value: {ex.Message}");
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
                var parameters = _programmingState.GetProgrammingParameters();
                var result = _avrDudeWrapper.ReadFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port);


                if (result.Success)
                {
                    OutputPaneHandler.PrintTextToOutputPane("Fuses read successfully");
                    OutputPaneHandler.PrintTextToOutputPane(result.Output + "\n" + result.Error);
                    ParseAndUpdateFuseValues(result.Output + "\n" + result.Error);
                    
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading current fuse values: {ex.Message}");
            }
        }

        private void ParseAndUpdateFuseValues(string output, string targetFuse = null)
        {
            Logger.Log($"Parsing fuse values from output: {output}");
            try
            {
                // Split the output into lines
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                // The first three lines contain the hex values
                byte lowFuse = 0, highFuse = 0, extFuse = 0;
                bool foundLow = false, foundHigh = false, foundExt = false;

                // Try to find the hex values in the first few lines
                foreach (string line in lines)
                {
                    // Check if line is a hex value
                    if (line.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        string hexValue = line.Trim();
                        if (!foundLow)
                        {
                            foundLow = byte.TryParse(hexValue.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out lowFuse);
                        }
                        else if (!foundHigh)
                        {
                            foundHigh = byte.TryParse(hexValue.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out highFuse);
                        }
                        else if (!foundExt)
                        {
                            foundExt = byte.TryParse(hexValue.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out extFuse);
                        }
                    }
                }

                Logger.Log($"Parsed values - Low: {(foundLow ? $"0x{lowFuse:X2}" : "not found")}, " +
                          $"High: {(foundHigh ? $"0x{highFuse:X2}" : "not found")}, " +
                          $"Extended: {(foundExt ? $"0x{extFuse:X2}" : "not found")}");

                // Update displays based on what we found
                if ((targetFuse == null || targetFuse == "low") && foundLow)
                {
                    UpdateFuseDisplay("low", lowFuse);
                }

                if ((targetFuse == null || targetFuse == "high") && foundHigh)
                {
                    UpdateFuseDisplay("high", highFuse);
                }

                if ((targetFuse == null || targetFuse == "extended") && foundExt)
                {
                    UpdateFuseDisplay("extended", extFuse);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing fuse values: {ex.Message}");
            }
        }


        private void UpdateFuseDisplay(string fuseType, byte value)
        {
            if (_isUpdatingUI) return;

            try
            {
                _isUpdatingUI = true;
                Logger.Log($"Starting to update {fuseType} fuse display with value: 0x{value:X2}");

                // First verify we have the checkboxes
                if (!_fuseCheckboxes.ContainsKey(fuseType))
                {
                    Logger.Log($"ERROR: No checkboxes found for {fuseType} fuse");
                    return;
                }

                var checkboxes = _fuseCheckboxes[fuseType];
                Logger.Log($"Found {checkboxes.Count} checkboxes for {fuseType} fuse");

                // Update each checkbox
                for (int i = 0; i < 8; i++)
                {
                    bool bitSet = (value & (1 << i)) != 0;
                    int checkboxIndex = 7 - i;
                    if (checkboxIndex >= 0 && checkboxIndex < checkboxes.Count)
                    {
                        checkboxes[checkboxIndex].IsChecked = bitSet;
                        Logger.Log($"{fuseType} fuse bit {i} (checkbox {checkboxIndex}): {bitSet}");
                    }
                    else
                    {
                        Logger.Log($"ERROR: Invalid checkbox index {checkboxIndex} for {fuseType} fuse");
                    }
                }

                // Update the text box
                if (_fuseValueBoxes.ContainsKey(fuseType))
                {
                    _fuseValueBoxes[fuseType].Text = value.ToString("X2");
                    Logger.Log($"Updated {fuseType} fuse text box to: {value:X2}");
                }
                else
                {
                    Logger.Log($"ERROR: No text box found for {fuseType} fuse");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating fuse display: {ex.Message}");
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        #region Event Handlers
        private void FuseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Early exit checks
            if (FuseSelector == null || FuseSelector.SelectedItem == null)
                return;

            // Get the microcontroller
            var mcu = _programmingState.CurrentMicrocontroller;
            if (mcu == null)
                return;

            // Get the selected fuse
            string selectedFuse = FuseSelector.SelectedItem.ToString();
            if (string.IsNullOrEmpty(selectedFuse))
                return;

            // Find the matching register
            FuseRegister register = null;
            foreach (var reg in mcu.FuseRegisters)
            {
                if (reg.Name == selectedFuse)
                {
                    register = reg;
                    break;
                }
            }

            if (register == null)
                return;

            // Create options list
            var options = new List<FuseBitOption>();
            foreach (var bitfield in register.Bitfields)
            {
                if (string.IsNullOrEmpty(bitfield.ValueGroupName))
                    continue;

                ValueGroup group;
                if (mcu.ValueGroups.TryGetValue(bitfield.ValueGroupName, out group))
                {
                    foreach (var value in group.Values)
                    {
                        var option = new FuseBitOption
                        {
                            Caption = value.Caption ?? "",
                            Name = value.Name ?? "",
                            Value = value.Value.ToString("X2")
                        };
                        options.Add(option);
                    }
                }
            }

            // Update UI
            if (options.Count > 0)
            {
                OptionSelector.ItemsSource = options;
                OptionSelector.SelectedIndex = 0;
            }
            else
            {
                OptionSelector.ItemsSource = null;
                OptionSelector.SelectedIndex = -1;
            }
        }

        private void OptionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI)
                return;

            // Check for valid option selection
            FuseBitOption option = OptionSelector.SelectedItem as FuseBitOption;
            if (option == null)
                return;

            // Check for valid fuse selection
            if (FuseSelector.SelectedItem == null)
                return;

            string selectedFuse = FuseSelector.SelectedItem.ToString();
            if (string.IsNullOrEmpty(selectedFuse))
                return;

            // Parse hex value
            byte value;
            if (byte.TryParse(option.Value,
                              System.Globalization.NumberStyles.HexNumber,
                              System.Globalization.CultureInfo.InvariantCulture,
                              out value))
            {
                UpdateFuseDisplay(selectedFuse.ToLower(), value);
            }
            else
            {
                Logger.Log($"Invalid hex value in option: {option.Value}");
            }
        }

        private void ValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI)
                return;

            // Validate sender
            TextBox textBox = sender as TextBox;
            if (textBox == null)
                return;

            // Find corresponding fuse type
            string fuseType = null;
            foreach (KeyValuePair<string, TextBox> pair in _fuseValueBoxes)
            {
                if (pair.Value == textBox)
                {
                    fuseType = pair.Key;
                    break;
                }
            }

            if (string.IsNullOrEmpty(fuseType))
            {
                Logger.Log("Could not find matching fuse type for text box");
                return;
            }

            // Parse hex value
            string text = textBox.Text ?? "";
            byte value;
            if (byte.TryParse(text,
                              System.Globalization.NumberStyles.HexNumber,
                              System.Globalization.CultureInfo.InvariantCulture,
                              out value))
            {
                UpdateFuseDisplay(fuseType, value);
            }
            else
            {
                Logger.Log($"Invalid hex value entered: {text}");
            }
        }

        private void ReadFuses_Click(object sender, RoutedEventArgs e)
        {
            if (!_programmingState.IsReadyToProgram())
            {
                MessageBox.Show("Device or programmer not ready", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ReadCurrentValues();
        }

        private void WriteFuses_Click(object sender, RoutedEventArgs e)
        {
            if (!_programmingState.IsReadyToProgram())
            {
                MessageBox.Show("Device or programmer not ready", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var parameters = _programmingState.GetProgrammingParameters();
                string lfuse = LowFuseValue.Text;
                string hfuse = HighFuseValue.Text;
                string efuse = ExtendedFuseValue.Text;

                var result = _avrDudeWrapper.WriteFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port,
                    $"0x{lfuse}",
                    $"0x{hfuse}",
                    $"0x{efuse}");

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

        #region Individual Fuse Operations
        private void ReadLowFuse_Click(object sender, RoutedEventArgs e)
        {
            ReadSingleFuse("low");
        }

        private void ReadHighFuse_Click(object sender, RoutedEventArgs e)
        {
            ReadSingleFuse("high");
        }

        private void ReadExtFuse_Click(object sender, RoutedEventArgs e)
        {
            ReadSingleFuse("extended");
        }

        private void WriteLowFuse_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LowFuseValue.Text))
                return;
            WriteSingleFuse("low", LowFuseValue.Text);
        }

        private void WriteHighFuse_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(HighFuseValue.Text))
                return;
            WriteSingleFuse("high", HighFuseValue.Text);
        }

        private void WriteExtFuse_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ExtendedFuseValue.Text))
                return;
            WriteSingleFuse("extended", ExtendedFuseValue.Text);
        }

        private void ReadSingleFuse(string fuseType)
        {
            if (!_programmingState.IsReadyToProgram())
            {
                MessageBox.Show("Device or programmer not ready", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var parameters = _programmingState.GetProgrammingParameters();
                string fuseFlag;

                switch (fuseType)
                {
                    case "low":
                        fuseFlag = "lfuse";
                        break;
                    case "high":
                        fuseFlag = "hfuse";
                        break;
                    case "extended":
                        fuseFlag = "efuse";
                        break;
                    default:
                        throw new ArgumentException("Invalid fuse type");
                }

                string command = $"-p {parameters.DeviceName} -c {parameters.ProgrammerName} -P {parameters.Port} -U {fuseFlag}:r:-:h";
                var result = _avrDudeWrapper.ExecuteCommand(command);

                if (result.Success)
                {
                    string[] lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string line in lines)
                    {
                        if (line.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            string hexValue = line.Trim();
                            byte value;
                            if (byte.TryParse(hexValue.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value))
                            {
                                UpdateFuseDisplay(fuseType, value);
                                Logger.Log($"Read {fuseType} fuse value: 0x{value:X2}");
                                OutputPaneHandler.PrintTextToOutputPane($"{fuseType} fuse read successfully: 0x{value:X2}");
                                return;
                            }
                        }
                    }

                    //// Updated pattern to match single fuse read output
                    //string pattern = $"{fuseFlag} reads as (\\w+)";
                    //var match = Regex.Match(result.Output + "\n" + result.Error, pattern);

                    //if (match.Success)
                    //{
                    //    byte value = Convert.ToByte(match.Groups[1].Value, 16);
                    //    UpdateFuseDisplay(fuseType, value);
                    //    Logger.Log($"Read {fuseType} fuse value: 0x{value:X2}");
                    //    OutputPaneHandler.PrintTextToOutputPane($"{fuseType} fuse read successfully: 0x{value:X2}");
                    //}
                    //else
                    //{
                    //    Logger.Log($"Failed to parse {fuseType} fuse value from output: {result.Output}");
                    //    OutputPaneHandler.PrintTextToOutputPane($"Failed to parse {fuseType} fuse value");
                    //}
                }
                else
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to read {fuseType} fuse: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading {fuseType} fuse: {ex.Message}");
                MessageBox.Show($"Error reading {fuseType} fuse: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WriteSingleFuse(string fuseType, string value)
        {
            if (!_programmingState.IsReadyToProgram())
            {
                MessageBox.Show("Device or programmer not ready", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var parameters = _programmingState.GetProgrammingParameters();
                string command = string.Empty;

                // Build command based on fuse type
                switch (fuseType)
                {
                    case "low":
                        command = $"-p {parameters.DeviceName} -c {parameters.ProgrammerName} -P {parameters.Port} -U lfuse:w:0x{value}:m";
                        break;
                    case "high":
                        command = $"-p {parameters.DeviceName} -c {parameters.ProgrammerName} -P {parameters.Port} -U hfuse:w:0x{value}:m";
                        break;
                    case "extended":
                        command = $"-p {parameters.DeviceName} -c {parameters.ProgrammerName} -P {parameters.Port} -U efuse:w:0x{value}:m";
                        break;
                }

                var result = _avrDudeWrapper.ExecuteCommand(command);

                if (result.Success)
                {
                    OutputPaneHandler.PrintTextToOutputPane($"{fuseType} fuse written successfully: 0x{value}");
                }
                else
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to write {fuseType} fuse: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error writing {fuseType} fuse: {ex.Message}");
                MessageBox.Show($"Error writing {fuseType} fuse: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #endregion

        private void LowFuseValue_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void HighFuseValue_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void ExtendedFuseValue_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}