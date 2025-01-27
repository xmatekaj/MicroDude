using System;
using System.Windows;
using System.Windows.Controls;
using MicroDude.Core;
using MicroDude.Services;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;
using MicroDude.Properties;
using System.Windows.Threading;
using System.Windows.Media;

namespace MicroDude.UI
{
    public partial class OscillatorWindow : Window
    {
        private readonly FuseBitProgrammer _programmer;
        private readonly AvrDudeWrapper _avrDudeWrapper;
        private readonly ProgrammingStateService _programmingState;
        private readonly Style _originalWriteStyle;
        private readonly Style _originalReadStyle;
        private readonly Brush _originalWriteBackground;
        private readonly Brush _originalWriteForeground;
        private readonly Brush _originalReadBackground;
        private readonly Brush _originalReadForeground;
        private readonly string _originalWriteButtonContent;
        private readonly string _originalReadButtonContent;
        private readonly DispatcherTimer _resetButtonTimer;
        private bool _isUpdatingUI;
        private Button _activeButton;


        private class OscillatorConfig
        {
            public byte CKSEL { get; set; }
            public byte CKOPT { get; set; }
            public byte SUT { get; set; }
            public string Description { get; set; }
        }

        private Dictionary<string, OscillatorConfig> _configMap;

        public OscillatorWindow(FuseBitProgrammer programmer)
        {
            InitializeComponent();
            _programmer = programmer;
            _programmingState = ProgrammingStateService.Instance;
            // Store original button states
            _originalWriteStyle = Write.Style;
            _originalWriteBackground = Write.Background;
            _originalWriteForeground = Write.Foreground;
            _originalWriteButtonContent = Write.Content as string;

            _originalReadStyle = Read.Style;
            _originalReadBackground = Read.Background;
            _originalReadForeground = Read.Foreground;
            _originalReadButtonContent = Read.Content as string;

            // Initialize timer for resetting button appearance
            _resetButtonTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _resetButtonTimer.Tick += (s, e) => ResetButtonAppearance();

            string extensionDirectory = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);
            string avrDudeExePath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.exe");
            string avrDudeConfigPath = System.IO.Path.Combine(extensionDirectory, "AvrDude", "avrdude.conf");
            _avrDudeWrapper = new AvrDudeWrapper(avrDudeExePath, avrDudeConfigPath);

            InitializeConfigMap();
            InitializeUI();

            // Default selections
            StartupTimeCombo.SelectedIndex = 1; // Select recommended startup time by default
            ExternalFrequencyCombo.SelectedIndex = 0;
            UpdateCurrentConfigText();
        }

        private void InitializeUI()
        {
            // Get current microcontroller
            var mcu = _programmingState.CurrentMicrocontroller;

            // Clear existing options
            InternalOscillatorPanel.Children.Clear();

            // Add available internal oscillator options based on MCU
            if (mcu != null)
            {
                // Create radio button group for oscillator source
                var sourceGroup = new GroupBox();

                // Add radio buttons to the same group by naming it
                const string OSCILLATOR_GROUP = "OscillatorSource";

                // This would need to be configured based on MCU characteristics
                var internalOptions = new Dictionary<string, string>
        {
            { "8 MHz", "Internal8MHz" },
            { "4 MHz", "Internal4MHz" },
            { "2 MHz", "Internal2MHz" },
            { "1 MHz", "Internal1MHz" },
            { "128 KHz", "Internal128KHz" }
        };

                foreach (var option in internalOptions)
                {
                    var radioButton = new RadioButton
                    {
                        Content = option.Key,
                        Tag = option.Value,
                        Margin = new Thickness(0, 5, 0, 0),
                        GroupName = OSCILLATOR_GROUP // Add to the same group
                    };

                    radioButton.Checked += OscillatorOption_Changed;
                    InternalOscillatorPanel.Children.Add(radioButton);
                }

                // Add external options to same group
                ExternalCrystal.GroupName = OSCILLATOR_GROUP;
                ExternalClock.GroupName = OSCILLATOR_GROUP;
            }

            // Initialize startup time options
            StartupTimeCombo.Items.Clear();
            StartupTimeCombo.Items.Add(new ComboBoxItem { Content = "Minimum (No Delay)" });
            StartupTimeCombo.Items.Add(new ComboBoxItem { Content = "Recommended (+ 4.1ms)" });
            StartupTimeCombo.Items.Add(new ComboBoxItem { Content = "Maximum (+ 65ms)" });

            // Initialize external frequency options
            ExternalFrequencyCombo.Items.Clear();
            ExternalFrequencyCombo.Items.Add(new ComboBoxItem { Content = "0.4 - 0.9 MHz (Low Frequency)" });
            ExternalFrequencyCombo.Items.Add(new ComboBoxItem { Content = "0.9 - 3.0 MHz" });
            ExternalFrequencyCombo.Items.Add(new ComboBoxItem { Content = "3.0 - 8.0 MHz" });
            ExternalFrequencyCombo.Items.Add(new ComboBoxItem { Content = "8.0+ MHz (Full Swing)" });

            // Hook up event handlers
            ExternalCrystal.Checked += OscillatorOption_Changed;
            ExternalClock.Checked += OscillatorOption_Changed;
            ExternalFrequencyCombo.SelectionChanged += OscillatorOption_Changed;
            StartupTimeCombo.SelectionChanged += OscillatorOption_Changed;
        }

        private OscillatorConfig GetCurrentConfiguration()
        {
            OscillatorConfig config = null;

            // Check internal oscillator options
            var selectedInternalRadio = InternalOscillatorPanel.Children
                .OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked.HasValue && rb.IsChecked.Value);

            if (selectedInternalRadio != null && selectedInternalRadio.Tag != null)
            {
                string configKey = selectedInternalRadio.Tag as string;
                if (configKey != null)
                {
                    config = _configMap[configKey];
                }
            }
            else if (ExternalCrystal.IsChecked == true)
            {
                switch (ExternalFrequencyCombo.SelectedIndex)
                {
                    case 0: config = _configMap["ExternalCrystalLow"]; break;
                    case 1: config = _configMap["ExternalCrystalMed"]; break;
                    case 2: config = _configMap["ExternalCrystalHigh"]; break;
                    case 3: config = _configMap["ExternalCrystalFullSwing"]; break;
                }
            }
            else if (ExternalClock.IsChecked == true)
            {
                config = _configMap["ExternalClock"];
            }

            if (config != null && StartupTimeCombo.SelectedIndex >= 0)
            {
                // Clone the config so we don't modify the original
                config = new OscillatorConfig
                {
                    CKSEL = config.CKSEL,
                    CKOPT = config.CKOPT,
                    SUT = config.SUT,
                    Description = config.Description
                };

                // Adjust SUT based on startup time selection
                switch (StartupTimeCombo.SelectedIndex)
                {
                    case 0: config.SUT = 0x00; break; // No delay
                    case 1: config.SUT = 0x01; break; // +4.1ms
                    case 2: config.SUT = 0x02; break; // +65ms
                }
            }

            return config;
        }


        private void InitializeConfigMap()
        {
            _configMap = new Dictionary<string, OscillatorConfig>
            {
                // Internal RC Oscillators
                {"Internal8MHz", new OscillatorConfig
                    {
                        CKSEL = 0x04,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "Internal 8 MHz RC Oscillator"
                    }
                },
                {"Internal4MHz", new OscillatorConfig
                    {
                        CKSEL = 0x03,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "Internal 4 MHz RC Oscillator"
                    }
                },
                {"Internal2MHz", new OscillatorConfig
                    {
                        CKSEL = 0x02,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "Internal 2 MHz RC Oscillator"
                    }
                },
                {"Internal1MHz", new OscillatorConfig
                    {
                        CKSEL = 0x01,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "Internal 1 MHz RC Oscillator"
                    }
                },
                {"Internal128KHz", new OscillatorConfig
                    {
                        CKSEL = 0x00,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "Internal 128 KHz RC Oscillator"
                    }
                },
                
                // External Crystal configurations remain the same...
                {"ExternalCrystalLow", new OscillatorConfig
                    {
                        CKSEL = 0x09,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "External Crystal 0.4-0.9 MHz"
                    }
                },
                        
                // External Crystal - Medium Freq (0.9-3.0MHz)
                {"ExternalCrystalMed", new OscillatorConfig
                    {
                        CKSEL = 0x0A,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "External Crystal 0.9-3.0 MHz"
                    }
                },
                
                // External Crystal - High Freq (3.0-8.0MHz)
                {"ExternalCrystalHigh", new OscillatorConfig
                    {
                        CKSEL = 0x0B,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "External Crystal 3.0-8.0 MHz"
                    }
                },
                
                // External Crystal - Full Swing (>8.0MHz)
                {"ExternalCrystalFullSwing", new OscillatorConfig
                    {
                        CKSEL = 0x0C,
                        CKOPT = 0x01,
                        SUT = 0x03,
                        Description = "External Crystal >8.0 MHz (Full Swing)"
                    }
                },
                
                // External Clock
                {"ExternalClock", new OscillatorConfig
                    {
                        CKSEL = 0x00,
                        CKOPT = 0x00,
                        SUT = 0x03,
                        Description = "External Clock Source"
                    }
                }
            };
        }

        private void ReadSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetButtonAppearance();
                Read.IsEnabled = false;
                _activeButton = Read;

                if (!_programmingState.IsReadyToProgram())
                {
                    ShowButtonFeedback(false, "Not Ready");
                    return;
                }

                var parameters = _programmingState.GetProgrammingParameters();
                var result = _avrDudeWrapper.ReadFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port);

                if (!result.Success)
                {
                    ShowButtonFeedback(false, "Failed!");
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to read fuses: {result.Error}");
                    return;
                }

                // Log the raw output for debugging
                Logger.Log($"Raw AvrDude output: {result.Output}");
                Logger.Log($"Raw AvrDude error: {result.Error}");

                if (MicroDudeSettings.Default.Verbose)
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Raw AvrDude output:\n{result.Output}");
                    OutputPaneHandler.PrintTextToOutputPane($"Raw AvrDude error:\n{result.Error}");
                }


                // Parse the fuses from AvrDude output
                var fuseValues = ParseFuseValues(result.Output + "\n" + result.Error);
                if (fuseValues == null || !fuseValues.Any())
                {
                    ShowButtonFeedback(false, "Failed!");
                    MessageBox.Show("Could not parse fuse values from AvrDude output. Raw output:\n\n" + result.Output,
                        "Parse Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Logger.Log($"Read fuse values: {string.Join(", ", fuseValues.Select(kv => $"{kv.Key}=0x{kv.Value:X2}"))}");
                UpdateUIFromFuseValues(fuseValues);
                ShowButtonFeedback(true, "Success!");

                OutputPaneHandler.PrintTextToOutputPane("Oscillator settings read successfully.");
                if (MicroDudeSettings.Default.Verbose)
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Read fuse values:\n{string.Join(", ", fuseValues.Select(kv => $"{kv.Key}=0x{kv.Value:X2}"))}");
                }
            }
            catch (Exception ex)
            {
                ShowButtonFeedback(false, "Error!");
                MessageBox.Show($"Error reading oscillator settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string, byte> ParseFuseValues(string output)
        {
            var fuseValues = new Dictionary<string, byte>();

            Logger.Log("Starting fuse value parsing...");
            Logger.Log($"Raw output to parse: {output}");

            try
            {
                // Split the output into lines
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                // First three lines should contain the hex values
                if (lines.Length >= 3)
                {
                    var hexPattern = @"0x([0-9a-fA-F]{2})";

                    // Get the first three values
                    var match1 = Regex.Match(lines[0], hexPattern, RegexOptions.IgnoreCase);
                    var match2 = Regex.Match(lines[1], hexPattern, RegexOptions.IgnoreCase);
                    var match3 = Regex.Match(lines[2], hexPattern, RegexOptions.IgnoreCase);

                    if (match1.Success && match2.Success && match3.Success)
                    {
                        fuseValues["lfuse"] = Convert.ToByte(match1.Groups[1].Value, 16);
                        fuseValues["hfuse"] = Convert.ToByte(match2.Groups[1].Value, 16);
                        fuseValues["efuse"] = Convert.ToByte(match3.Groups[1].Value, 16);

                        Logger.Log("Successfully parsed fuse values:");
                        Logger.Log($"lfuse = 0x{fuseValues["lfuse"]:X2}");
                        Logger.Log($"hfuse = 0x{fuseValues["hfuse"]:X2}");
                        Logger.Log($"efuse = 0x{fuseValues["efuse"]:X2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing fuse values: {ex.Message}");
            }

            if (!fuseValues.Any())
            {
                Logger.Log("No fuse values could be parsed from the output");
            }

            return fuseValues;
        }

        private void Write_click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetButtonAppearance();
                Write.IsEnabled = false;
                _activeButton = Write;

                if (!_programmingState.IsReadyToProgram())
                {
                    ShowButtonFeedback(false, "Not Ready");
                    return;
                }

                var config = GetCurrentConfiguration();
                if (config == null) return;

                var parameters = _programmingState.GetProgrammingParameters();

                byte lfuse = (byte)(~(config.CKSEL | (config.SUT << 4)));
                Logger.Log($"Writing low fuse value: 0x{lfuse:X2} (CKSEL=0x{config.CKSEL:X2}, SUT=0x{config.SUT:X2})");

                var result = _avrDudeWrapper.WriteFuses(
                    parameters.DeviceName,
                    parameters.ProgrammerName,
                    parameters.Port,
                    $"0x{lfuse:X2}",  // Only write low fuse
                    null,             // Don't write high fuse
                    null);            // Don't write extended fuse

                if (result.Success)
                {
                    // Update UI
                    ClearProgrammedMarkers();
                    var configuredValues = new Dictionary<string, byte> { { "lfuse", lfuse } };
                    UpdateUIFromFuseValues(configuredValues);

                    OutputPaneHandler.PrintTextToOutputPane($"Applied oscillator setting: {config.Description}");
                    ShowButtonFeedback(true, "Success!");
                }
                else
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Failed to write fuses: {result.Error}");
                    ShowButtonFeedback(false, "Failed!");
                }
            }
            catch (Exception ex)
            {
                OutputPaneHandler.PrintTextToOutputPane($"Error applying oscillator settings: {ex.Message}");
                ShowButtonFeedback(false, "Error!");
            }
        }

        private void UpdateUISelections(byte ckselValue, byte sutValue)
        {
            // Find and select appropriate oscillator configuration
            bool found = false;
            foreach (var config in _configMap)
            {
                if (config.Value.CKSEL == ckselValue)
                {
                    found = true;
                    // Internal RC options
                    if (config.Key.StartsWith("Internal"))
                    {
                        var internalButton = InternalOscillatorPanel.Children
                            .OfType<RadioButton>()
                            .FirstOrDefault(rb => rb.Tag as string == config.Key);

                        if (internalButton != null)
                            internalButton.IsChecked = true;
                    }
                    // External options
                    else if (config.Key.StartsWith("ExternalCrystal"))
                    {
                        ExternalCrystal.IsChecked = true;
                        switch (config.Key)
                        {
                            case "ExternalCrystalLow": ExternalFrequencyCombo.SelectedIndex = 0; break;
                            case "ExternalCrystalMed": ExternalFrequencyCombo.SelectedIndex = 1; break;
                            case "ExternalCrystalHigh": ExternalFrequencyCombo.SelectedIndex = 2; break;
                            case "ExternalCrystalFullSwing": ExternalFrequencyCombo.SelectedIndex = 3; break;
                        }
                    }
                    else if (config.Key == "ExternalClock")
                    {
                        ExternalClock.IsChecked = true;
                    }

                    // Set startup time
                    if (StartupTimeCombo.Items.Count > sutValue)
                    {
                        StartupTimeCombo.SelectedIndex = sutValue;
                    }
                    break;
                }
            }

            if (!found)
            {
                Logger.Log($"Unknown clock configuration: CKSEL=0x{ckselValue:X2}");
            }

            // Update the current configuration text
            UpdateCurrentConfigText();
        }

        private void SwitchToFuseBits_Click(object sender, RoutedEventArgs e)
        {
            var fuseBitsWindow = new FuseBitsWindow();
            fuseBitsWindow.Show();
            this.Close();
        }

        private void UpdateCurrentConfigText()
        {
            var config = GetCurrentConfiguration();
            var configText = new System.Text.StringBuilder();
            if (config == null)
            {
                //CurrentConfigText.Text = "Please select an oscillator configuration.";
                //return;
                //configText.AppendLine($"Unknown clock source");
                //configText.AppendLine($"Startup Time: -");

                configText.AppendLine($"Fuse Values:");
                configText.AppendLine($"CKSEL: 0x--");
                configText.AppendLine($"CKOPT: 0x--");
                configText.AppendLine($"SUT: 0x--");
            }
            else
            {
                // configText.AppendLine(config.Description);

                //var selectedTimeItem = StartupTimeCombo.SelectedItem as ComboBoxItem;
                //if (selectedTimeItem != null)
                //{
                //    string startupTime = selectedTimeItem.Content.ToString();
                //    startupTime = startupTime.Remove(startupTime.IndexOf('[') - 1);
                //    configText.AppendLine($"Startup Time: {startupTime}");
                //}

                configText.AppendLine($"Fuse Values:");
                configText.AppendLine($"CKSEL: 0x{config.CKSEL:X2}");
                configText.AppendLine($"CKOPT: 0x{config.CKOPT:X2}");
                configText.AppendLine($"SUT: 0x{config.SUT:X2}");
            }

            CurrentConfigText.Text = configText.ToString();
        }

        private string InterpretClockSource(byte cksel)
        {
            switch (cksel)
            {
                case 0x00: return "Internal 128 KHz RC Oscillator";
                case 0x01: return "Internal 1 MHz RC Oscillator";
                case 0x02: return "Internal 2 MHz RC Oscillator";
                case 0x03: return "Internal 4 MHz RC Oscillator";
                case 0x04: return "Internal 8 MHz RC Oscillator";
                case 0x09: return "External Crystal/Resonator (0.4-0.9 MHz)";
                case 0x0A: return "External Crystal/Resonator (0.9-3.0 MHz)";
                case 0x0B: return "External Crystal/Resonator (3.0-8.0 MHz)";
                case 0x0C: return "External Crystal/Resonator (>8.0 MHz)";
                default: return $"Unknown Clock Source Configuration (0x{cksel:X2})";
            }
        }

        private string InterpretStartupTime(byte sutValue)
        {
            // For active-low fuses, we need to invert the bits
            byte invertedSut = (byte)(~sutValue & 0x03);

            switch (invertedSut)
            {
                case 0x00: return "Fast Rising Power";
                case 0x01: return "Slowly Rising Power";
                case 0x02: return "BOD Enabled";
                case 0x03: return "BOD Disabled";
                default: return $"Unknown Start-up Time (0x{invertedSut:X2})";
            }
        }

        private void ClearProgrammedMarkers()
        {
            // Clear all selections and markers
            foreach (RadioButton rb in InternalOscillatorPanel.Children.OfType<RadioButton>())
            {
                rb.IsChecked = false;
                rb.Content = rb.Content.ToString().Split('[')[0].Trim();
            }
            ExternalCrystal.IsChecked = false;
            ExternalClock.IsChecked = false;
            ExternalCrystal.Content = ExternalCrystal.Content.ToString().Split('[')[0].Trim();
            ExternalClock.Content = ExternalClock.Content.ToString().Split('[')[0].Trim();

            // Clear external frequency combo markers
            for (int i = 0; i < ExternalFrequencyCombo.Items.Count; i++)
            {
                var item = ExternalFrequencyCombo.Items[i] as ComboBoxItem;
                if (item != null)
                {
                    item.Content = item.Content.ToString().Split('[')[0].Trim();
                }
            }

            // Clear startup time markers
            for (int i = 0; i < StartupTimeCombo.Items.Count; i++)
            {
                var item = StartupTimeCombo.Items[i] as ComboBoxItem;
                if (item != null)
                {
                    item.Content = item.Content.ToString().Split('[')[0].Trim();
                }
            }
        }

        private void OscillatorOption_Changed(object sender, EventArgs e)
        {
            if (!_isUpdatingUI)
            {
                // Enable/disable frequency combo based on crystal selection
                ExternalFrequencyCombo.IsEnabled = ExternalCrystal.IsChecked ?? false;

                UpdateCurrentConfigText();
            }
        }

        private void ShowButtonFeedback(bool success, string message)
        {
            // Cancel any pending reset
            _resetButtonTimer.Stop();

            if (_activeButton != null)
            {
                // Update button appearance
                _activeButton.Content = message;
                _activeButton.Background = success ?
                    new SolidColorBrush(Colors.LightGreen) :
                    new SolidColorBrush(Colors.LightPink);
                _activeButton.Foreground = new SolidColorBrush(Colors.Black);
                _activeButton.IsEnabled = true;

                // Start timer to reset appearance
                _resetButtonTimer.Start();
            }
        }

        private void ResetButtonAppearance()
        {
            _resetButtonTimer.Stop();

            if (_activeButton == Write)
            {
                Write.Content = _originalWriteButtonContent;
                Write.Style = _originalWriteStyle;
                Write.Background = _originalWriteBackground;
                Write.Foreground = _originalWriteForeground;
            }
            else if (_activeButton == Read)
            {
                Read.Content = _originalReadButtonContent;
                Read.Style = _originalReadStyle;
                Read.Background = _originalReadBackground;
                Read.Foreground = _originalReadForeground;
            }

            _activeButton = null;
        }

        private void UpdateUIFromFuseValues(Dictionary<string, byte> fuseValues)
        {
            _isUpdatingUI = true;
            try
            {
                ClearProgrammedMarkers();

                byte lfuseByte;
                if (fuseValues.TryGetValue("lfuse", out lfuseByte))
                {
                    // Remember fuses are active low, so we need to invert the bits
                    byte ckselValue = (byte)(~lfuseByte & 0x0F);        // Get inverted lower 4 bits
                    byte sutValue = (byte)((~lfuseByte >> 4) & 0x03);   // Get inverted bits 5:4

                    // Find and mark programmed configuration
                    bool found = false;
                    foreach (var config in _configMap)
                    {
                        if (config.Value.CKSEL == ckselValue)
                        {
                            found = true;
                            // Internal RC options
                            if (config.Key.StartsWith("Internal"))
                            {
                                var internalButton = InternalOscillatorPanel.Children
                                    .OfType<RadioButton>()
                                    .FirstOrDefault(rb => rb.Tag as string == config.Key);

                                if (internalButton != null)
                                {
                                    internalButton.IsChecked = true;
                                    internalButton.Content = internalButton.Content.ToString().Split('[')[0].Trim() + " [programmed]";
                                }
                            }
                            // External options
                            else if (config.Key.StartsWith("ExternalCrystal"))
                            {
                                ExternalCrystal.IsChecked = true;
                                ExternalCrystal.Content = ExternalCrystal.Content.ToString().Split('[')[0].Trim() + " [programmed]";
                                switch (config.Key)
                                {
                                    case "ExternalCrystalLow": ExternalFrequencyCombo.SelectedIndex = 0; break;
                                    case "ExternalCrystalMed": ExternalFrequencyCombo.SelectedIndex = 1; break;
                                    case "ExternalCrystalHigh": ExternalFrequencyCombo.SelectedIndex = 2; break;
                                    case "ExternalCrystalFullSwing": ExternalFrequencyCombo.SelectedIndex = 3; break;
                                }

                                // Also mark the selected frequency option
                                var selectedItem = ExternalFrequencyCombo.SelectedItem as ComboBoxItem;
                                if (selectedItem != null)
                                {
                                    selectedItem.Content = selectedItem.Content.ToString().Split('[')[0].Trim() + " [programmed]";
                                }
                            }
                            else if (config.Key == "ExternalClock")
                            {
                                ExternalClock.IsChecked = true;
                                ExternalClock.Content = ExternalClock.Content.ToString().Split('[')[0].Trim() + " [programmed]";
                            }

                            // Set and mark startup time
                            if (StartupTimeCombo.Items.Count > sutValue)
                            {
                                StartupTimeCombo.SelectedIndex = sutValue;
                                var selectedItem = StartupTimeCombo.SelectedItem as ComboBoxItem;
                                if (selectedItem != null)
                                {
                                    selectedItem.Content = selectedItem.Content.ToString().Split('[')[0].Trim() + " [programmed]";
                                }
                            }
                            break;
                        }
                    }

                    if (!found)
                    {
                        Logger.Log($"Unknown clock configuration: CKSEL=0x{ckselValue:X2}");
                    }

                    UpdateUISelections(ckselValue, sutValue);
                }
                else
                {
                    Logger.Log("Low fuse value not found in parsed values");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating UI from fuse values: {ex.Message}");
                MessageBox.Show($"Error updating UI from fuse values: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

    }
}
