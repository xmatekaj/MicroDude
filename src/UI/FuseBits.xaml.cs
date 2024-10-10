using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MicroDude.Core;
using System.Windows.Data;
using MicroDude.Models;

namespace MicroDude.UI
{
    public partial class FuseBitsWindow : Window
    {
        private FuseBitProgrammer _programmer;
        public List<FuseBitOption> SelectedFuseOptions { get; set; }

        public FuseBitsWindow()
        {
            InitializeComponent();
            _programmer = new FuseBitProgrammer("path_to_your_xml_file.xml");
            DataContext = _programmer;

            FuseSelector.SelectedIndex = 0;
            CreateManualControls();
        }

        private void CreateManualControls()
        {
            foreach (var fuse in _programmer.AvailableFuses)
            {
                var fuseGroup = new GroupBox { Header = fuse };
                var stackPanel = new StackPanel();

                for (int i = 0; i < 8; i++)
                {
                    var checkBox = new CheckBox { Content = $"Bit {i}", Tag = new { Fuse = fuse, Bit = i } };
                    checkBox.SetBinding(CheckBox.IsCheckedProperty,
                        new Binding($"GetFuseBitStatus[{fuse},{i}]")
                        {
                            Source = _programmer,
                            Mode = BindingMode.TwoWay
                        });
                    stackPanel.Children.Add(checkBox);
                }

                fuseGroup.Content = stackPanel;
                ManualFusePanel.Children.Add(fuseGroup);
            }
        }

        private void FuseSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedFuse = FuseSelector.SelectedItem as string;
            if (selectedFuse != null)
            {
                SelectedFuseOptions = _programmer.FuseBitOptions[selectedFuse];
                OptionSelector.ItemsSource = SelectedFuseOptions;
                OptionSelector.SelectedIndex = 0;
            }
        }

        private void OptionSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FuseBitOption selectedOption = OptionSelector.SelectedItem as FuseBitOption;
            string selectedFuse = FuseSelector.SelectedItem as string;
            if (selectedOption != null && selectedFuse != null)
            {
                byte value = byte.Parse(selectedOption.Value, System.Globalization.NumberStyles.HexNumber);
                _programmer.SetFuseValue(selectedFuse, value);
            }
        }

        private void ReadFuses_Click(object sender, RoutedEventArgs e)
        {
            _programmer.ReadFusesFromMicrocontroller();
        }

        private void WriteFuses_Click(object sender, RoutedEventArgs e)
        {
            _programmer.WriteFusesToMicrocontroller();
        }
    }
}