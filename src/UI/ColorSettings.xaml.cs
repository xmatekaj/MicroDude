using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Forms;
using MicroDude.Properties;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;

namespace MicroDude.UI
{

    public static class ClassificationFormatMapService
    {
        public const string OutputFormatMap = "output";
        public const string TextFormatMap = "text";
    }

    public partial class ColorSettings : Window
    {
        private readonly IClassificationFormatMapService _formatMapService;
        private readonly IClassificationTypeRegistryService _classificationRegistry;

        public ColorSettings()
        {
            InitializeComponent();

            try
            {
                var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
                if (componentModel != null)
                {
                    _formatMapService = componentModel.GetService<IClassificationFormatMapService>();
                    _classificationRegistry = componentModel.GetService<IClassificationTypeRegistryService>();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing color services: {ex}");
            }

            LoadCurrentColors();
        }

        private void LoadCurrentColors()
        {
            try
            {
                if (MicroDudeButton != null)
                    MicroDudeButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.MicroDudeColor));
                if (ErrorButton != null)
                    ErrorButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.ErrorColor));
                if (WarningButton != null)
                    WarningButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.WarningColor));
                if (SuccessButton != null)
                    SuccessButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.SuccessColor));
                if (InfoButton != null)
                    InfoButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(MicroDudeSettings.Default.InfoColor));
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading colors: {ex}");
                ResetDefaults_Click(null, null);
            }
        }

        private void RefreshOutputWindowColors()
        {
            try
            {
                var formatMap = _formatMapService.GetClassificationFormatMap("text");
                formatMap.BeginBatchUpdate();

                UpdateClassificationColor(formatMap, Output.ClassificationTypes.MicroDude, MicroDudeSettings.Default.MicroDudeColor);
                UpdateClassificationColor(formatMap, Output.ClassificationTypes.BuildError, MicroDudeSettings.Default.ErrorColor);
                UpdateClassificationColor(formatMap, Output.ClassificationTypes.BuildWarning, MicroDudeSettings.Default.WarningColor);
                UpdateClassificationColor(formatMap, Output.ClassificationTypes.BuildSuccess, MicroDudeSettings.Default.SuccessColor);
                UpdateClassificationColor(formatMap, Output.ClassificationTypes.BuildText, MicroDudeSettings.Default.InfoColor);
                UpdateClassificationColor(formatMap, Output.ClassificationTypes.BuildProgress, MicroDudeSettings.Default.InfoColor);
                UpdateClassificationColor(formatMap, Output.ClassificationTypes.Normal, MicroDudeSettings.Default.InfoColor);

                formatMap.EndBatchUpdate();

                // Try to refresh with output format map as well
                try
                {
                    var outputFormatMap = _formatMapService.GetClassificationFormatMap("output");
                    if (outputFormatMap != formatMap) // Only if it's a different map
                    {
                        outputFormatMap.BeginBatchUpdate();

                        UpdateClassificationColor(outputFormatMap, Output.ClassificationTypes.MicroDude, MicroDudeSettings.Default.MicroDudeColor);
                        UpdateClassificationColor(outputFormatMap, Output.ClassificationTypes.BuildError, MicroDudeSettings.Default.ErrorColor);
                        UpdateClassificationColor(outputFormatMap, Output.ClassificationTypes.BuildWarning, MicroDudeSettings.Default.WarningColor);
                        UpdateClassificationColor(outputFormatMap, Output.ClassificationTypes.BuildSuccess, MicroDudeSettings.Default.SuccessColor);
                        UpdateClassificationColor(outputFormatMap, Output.ClassificationTypes.BuildText, MicroDudeSettings.Default.InfoColor);
                        UpdateClassificationColor(outputFormatMap, Output.ClassificationTypes.BuildProgress, MicroDudeSettings.Default.InfoColor);
                        UpdateClassificationColor(outputFormatMap, Output.ClassificationTypes.Normal, MicroDudeSettings.Default.InfoColor);

                        outputFormatMap.EndBatchUpdate();
                    }
                }
                catch
                {
                    // Ignore errors with output format map - we already updated the text format map
                }

                // Force Visual Studio to refresh the output window
                RefreshOutputWindow();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error refreshing output window colors: {ex}");
            }
        }

        private void UpdateClassificationColor(IClassificationFormatMap formatMap, string classificationType, string colorString)
        {
            try
            {
                var type = _classificationRegistry.GetClassificationType(classificationType);
                if (type != null)
                {
                    var properties = formatMap.GetTextProperties(type);
                    var newColor = (Color)ColorConverter.ConvertFromString(colorString);
                    var newProperties = properties.SetForeground(newColor);
                    formatMap.SetTextProperties(type, newProperties);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating classification color for {classificationType}: {ex}");
            }
        }

        private void RefreshOutputWindow()
        {
            try
            {
                var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null)
                {
                    // Get the MicroDude pane
                    Guid microDudePaneGuid = new Guid("4E33953E-ED90-4A9B-8488-A0EEFBDF660D");
                    IVsOutputWindowPane microDudePane;
                    outputWindow.GetPane(ref microDudePaneGuid, out microDudePane);

                    // Get the Build pane
                    Guid buildPaneGuid = VSConstants.GUID_BuildOutputWindowPane;
                    IVsOutputWindowPane buildPane;
                    outputWindow.GetPane(ref buildPaneGuid, out buildPane);

                    // Clear and re-show both panes to force a refresh
                    if (microDudePane != null)
                    {
                        microDudePane.Clear();
                        microDudePane.Activate();
                    }

                    if (buildPane != null)
                    {
                        buildPane.Clear();
                        buildPane.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error refreshing output window: {ex}");
            }
        }

        private void UpdateColor(string currentColor, Action<string> updateAction)
        {
            using (var colorDialog = new ColorDialog())
            {
                var color = (Color)ColorConverter.ConvertFromString(currentColor);
                colorDialog.Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B);

                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromRgb(
                        colorDialog.Color.R,
                        colorDialog.Color.G,
                        colorDialog.Color.B);

                    updateAction(newColor.ToString());
                    MicroDudeSettings.Default.Save();
                    LoadCurrentColors();
                    RefreshOutputWindowColors();
                }
            }
        }

        private void MicroDudeColor_Click(object sender, RoutedEventArgs e)
        {
            UpdateColor(
                MicroDudeSettings.Default.MicroDudeColor,
                newColor => MicroDudeSettings.Default.MicroDudeColor = newColor);
        }

        private void ErrorColor_Click(object sender, RoutedEventArgs e)
        {
            UpdateColor(
                MicroDudeSettings.Default.ErrorColor,
                newColor => MicroDudeSettings.Default.ErrorColor = newColor);
        }

        private void WarningColor_Click(object sender, RoutedEventArgs e)
        {
            UpdateColor(
                MicroDudeSettings.Default.WarningColor,
                newColor => MicroDudeSettings.Default.WarningColor = newColor);
        }

        private void SuccessColor_Click(object sender, RoutedEventArgs e)
        {
            UpdateColor(
                MicroDudeSettings.Default.SuccessColor,
                newColor => MicroDudeSettings.Default.SuccessColor = newColor);
        }

        private void InfoColor_Click(object sender, RoutedEventArgs e)
        {
            UpdateColor(
                MicroDudeSettings.Default.InfoColor,
                newColor => MicroDudeSettings.Default.InfoColor = newColor);
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            MicroDudeSettings.Default.MicroDudeColor = "#FF4682B4";  // SteelBlue
            MicroDudeSettings.Default.ErrorColor = "#FFDC143C";      // Crimson
            MicroDudeSettings.Default.WarningColor = "#FFD4AF37";    // DarkGoldenrod
            MicroDudeSettings.Default.SuccessColor = "#FF008000";    // Green
            MicroDudeSettings.Default.InfoColor = "#FF808080";       // Gray
            MicroDudeSettings.Default.Save();
            LoadCurrentColors();
            RefreshOutputWindowColors();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}