using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Win32;
using MicroDude.Properties;
using System.IO;
using MicroDude.Parsers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MicroDude.Models;

namespace MicroDude.UI
{
    public partial class Settings : Window, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<Programmer> _programmers;
        public ObservableCollection<Programmer> Programmers
        {
            get { return _programmers; }
            set
            {
                _programmers = value;
                OnPropertyChanged();
            }
        }
        private void LoadProgrammers()
        {
            Logger.Log("LoadProgrammers started");
            Programmers = new ObservableCollection<Programmer>();
            string avrdudeConfPath = Path.Combine(Path.GetDirectoryName(AvrDudePath), "avrdude.conf");
            Logger.Log($"Attempting to load avrdude.conf from: {avrdudeConfPath}");

            if (File.Exists(avrdudeConfPath))
            {
                Logger.Log("avrdude.conf file found");
                var parser = new AvrdudeConfParser();
                parser.ParseFile(avrdudeConfPath);
                Logger.Log($"Number of programmers loaded: {parser.Programmers.Count}");
                foreach (var programmer in parser.Programmers)
                {
                    Programmers.Add(programmer);
                }
            }
            else
            {
                Logger.Log("avrdude.conf file not found");
            }

            ProgrammerComboBox.ItemsSource = Programmers;
            ProgrammerComboBox.DisplayMemberPath = "Id";
            ProgrammerComboBox.SelectedValuePath = "Id";
            Logger.Log($"ProgrammerComboBox ItemsSource set with {Programmers.Count} items");
        }

        private string _avrDudePath;
        public string AvrDudePath
        {
            get { return _avrDudePath; }
            set
            {
                if (_avrDudePath != value)
                {
                    _avrDudePath = value;
                    MicroDudeSettings.Default.AvrDudePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoFlash
        {
            get { return MicroDudeSettings.Default.AutoFlash; }
            set { MicroDudeSettings.Default.AutoFlash = value; }
        }

        public string Programmer
        {
            get { return MicroDudeSettings.Default.Programmer; }
            set { MicroDudeSettings.Default.Programmer = value; }
        }

        public Settings()
        {
            Logger.Log("Settings constructor called");
            InitializeComponent();
            AvrDudePath = MicroDudeSettings.Default.AvrDudePath;
            DataContext = this;
            LoadSettings();
        }

        private void LoadSettings()
        {
            Logger.Log($"LoadSettings called. AvrDudePath: {AvrDudePath}");

            AutoFlashCheckBox.IsChecked = AutoFlash;
            LoadProgrammers();
            ProgrammerComboBox.SelectedValue = Programmer;
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
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle language change
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(AvrDudePath))
            {
                MicroDudeSettings.Default.AvrDudePath = AvrDudePath;
            }
            AutoFlash = AutoFlashCheckBox.IsChecked ?? false;
            Programmer = ProgrammerComboBox.SelectedValue as string;
            MicroDudeSettings.Default.Save();
            Close();
        }

        private void AvrdudePathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Logger.Log($"AvrdudePathTextBox_TextChanged: {AvrDudePath}");

            if (File.Exists(AvrDudePath))
            {
                LoadProgrammers();
            }
        }

        private void ProgrammerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}