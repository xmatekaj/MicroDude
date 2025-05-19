using System;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace MicroDude.Core
{
    public class StatusBarHandler : IDisposable
    {
        private static readonly object _lock = new object();
        private static volatile StatusBarHandler _instance;
        private readonly IVsStatusbar _statusBar;
        private bool _isInitialized;
        private object _statusBarAnimation;
        private DispatcherTimer _refreshTimer;
        private const int REFRESH_INTERVAL_MS = 500;
        private string _lastMicroDudeStatus = string.Empty;
        private string _currentFullStatus = string.Empty;
        private string _currentMicroDudeStatus = string.Empty;

        public static StatusBarHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new StatusBarHandler();
                        }
                    }
                }
                return _instance;
            }
        }

        private StatusBarHandler()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _statusBar = Package.GetGlobalService(typeof(SVsStatusbar)) as IVsStatusbar;
            RegisterToServiceEvents();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(REFRESH_INTERVAL_MS)
            };
            _refreshTimer.Tick += (s, e) =>
            {
                try
                {
                    ThreadHelper.ThrowIfNotOnUIThread();

                    string currentStatus = GetCurrentStatusText();
                    // Only update if VS status changed
                    if (!currentStatus.Contains(_currentMicroDudeStatus) ||
                        !_currentFullStatus.Contains(currentStatus))
                    {
                        UpdateStatusBar();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error in status refresh: {ex.Message}");
                }
            };
        }

        private void RegisterToServiceEvents()
        {
            var service = ProgrammingStateService.Instance;
            service.PropertyChanged += OnProgrammingStateChanged;
            service.DeviceChanged += OnDeviceChanged;
            service.ProgrammerChanged += OnProgrammerChanged;
            service.PortChanged += OnPortChanged;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateStatusBar();
            _refreshTimer.Start();
            _isInitialized = true;
        }

        private string GetCurrentStatusText()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_statusBar == null) return string.Empty;

            string currentText = string.Empty;
            int frozen = 0;
            _statusBar.IsFrozen(out frozen);

            if (frozen == 0)
            {
                _statusBar.GetText(out currentText);
            }

            // Keep build messages but remove our previous status
            if (!string.IsNullOrEmpty(_lastMicroDudeStatus) &&
                !string.IsNullOrEmpty(currentText) &&
                currentText.Contains(_lastMicroDudeStatus))
            {
                currentText = currentText.Replace(_lastMicroDudeStatus, "").Trim();
            }

            return currentText;
        }

        //private void UpdateStatusBar()
        //{
        //    ThreadHelper.ThrowIfNotOnUIThread();

        //    if (_statusBar == null) return;

        //    try
        //    {
        //        string currentStatus = GetCurrentStatusText();
        //        string[] statusParts = SplitStatusText(currentStatus);

        //        var parameters = ProgrammingStateService.Instance.GetProgrammingParameters();
        //        string microDudeStatus = GetFormattedStatusText(parameters);

        //        _lastMicroDudeStatus = microDudeStatus;

        //        // Combine all parts with our status in the middle
        //        string combinedStatus = CombineStatusParts(statusParts[0], microDudeStatus, statusParts[1]);

        //        _statusBar.SetText(combinedStatus);

        //        Logger.Log($"Status bar updated: {combinedStatus}");
        //    }
        //    catch (Exception ex)
        //    {
        //        Logger.Log($"Error updating status bar: {ex.Message}");
        //    }
        //}

        private string[] SplitStatusText(string currentStatus)
        {
            string leftPart = string.Empty;
            string rightPart = string.Empty;

            if (!string.IsNullOrWhiteSpace(currentStatus))
            {
                // Find position for editor indicators
                string[] rightMarkers = { "Ln ", "Col ", "Ch ", "INS" };
                int rightPosition = -1;

                foreach (var marker in rightMarkers)
                {
                    rightPosition = currentStatus.LastIndexOf(marker);
                    if (rightPosition >= 0) break;
                }

                if (rightPosition >= 0)
                {
                    rightPart = currentStatus.Substring(rightPosition);
                    leftPart = currentStatus.Substring(0, rightPosition).Trim();
                }
                else
                {
                    // If no right markers found, check for build status
                    string[] buildMarkers = { "Build succeeded", "Build failed", "Debug", "Deploy" };
                    int buildPosition = -1;

                    foreach (var marker in buildMarkers)
                    {
                        buildPosition = currentStatus.IndexOf(marker);
                        if (buildPosition >= 0)
                        {
                            int endPosition = currentStatus.IndexOf("    ", buildPosition);
                            if (endPosition > buildPosition)
                            {
                                leftPart = currentStatus.Substring(0, endPosition).Trim();
                            }
                            else
                            {
                                leftPart = currentStatus.Trim();
                            }
                            break;
                        }
                    }
                }
            }

            return new[] { leftPart, rightPart };
        }

        private string CombineStatusParts(string leftPart, string middlePart, string rightPart)
        {
            var combined = new System.Text.StringBuilder();

            if (!string.IsNullOrWhiteSpace(leftPart))
            {
                combined.Append(leftPart).Append("     ");
            }

            combined.Append(middlePart);

            if (!string.IsNullOrWhiteSpace(rightPart))
            {
                combined.Append("     ").Append(rightPart);
            }

            return combined.ToString();
        }

        private string GetFormattedStatusText(ProgrammingStateService.ProgrammingParameters parameters)
        {
            var parts = new System.Text.StringBuilder();
            parts.Append("                      ");
            parts.Append("[ MicroDude ]");

            // Programmer info
            if (!string.IsNullOrEmpty(parameters.ProgrammerName))
            {
                parts.Append($" | 📟 {parameters.ProgrammerName}");
                if (parameters.IsAutoDetectedProgrammer)
                {
                    parts.Append(" 🅰");
                }
            }

            // Port info
            if (!string.IsNullOrEmpty(parameters.Port))
            {
                parts.Append($" | ⚡ {parameters.Port}");
            }

            // Device info
            if (!string.IsNullOrEmpty(parameters.DeviceName))
            {
                string detectedDevice = ProgrammingStateService.Instance.DetectedDevice;
                if (!string.IsNullOrEmpty(detectedDevice) &&
                    !parameters.DeviceName.Equals(detectedDevice, StringComparison.OrdinalIgnoreCase))
                {
                    parts.Append($" | 🚫 {parameters.DeviceName}≠{detectedDevice}");
                }
                else
                {
                    parts.Append($" | 🔲 {parameters.DeviceName}");
                }

                // Add memory info if device is set
                var memoryUsage = ProgrammingStateService.Instance.MemoryUsage;
                if (memoryUsage != null)
                {
                    if (memoryUsage.FlashTotal > 0)
                    {
                        parts.Append($" | 📝 {memoryUsage.GetFlashUsageString()}");
                    }
                    if (memoryUsage.EepromTotal > 0)
                    {
                        parts.Append($" | 💾 {memoryUsage.GetEepromUsageString()}");
                    }
                }
            }

            if (Properties.MicroDudeSettings.Default.AutoFlash)
            {
                parts.Append(" | 🔄");
            }

            parts.Append("     ");
            return parts.ToString();
        }

        private void UpdateStatusBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_statusBar == null) return;

            try
            {
                string currentStatus = GetCurrentStatusText();
                string[] statusParts = SplitStatusText(currentStatus);

                var parameters = ProgrammingStateService.Instance.GetProgrammingParameters();
                string microDudeStatus = GetFormattedStatusText(parameters);

                // TODO, need rework
                if (microDudeStatus == _currentMicroDudeStatus || microDudeStatus != currentStatus)
                {
                    // Check if VS status changed
                    string newFullStatus = CombineStatusParts(statusParts[0], microDudeStatus, statusParts[1]);
                    if (newFullStatus == _currentFullStatus && microDudeStatus == currentStatus)
                    {
                        // Nothing changed, skip update
                        return;
                    }
                }

                // Status changed, update it
                _currentMicroDudeStatus = microDudeStatus;
                _lastMicroDudeStatus = microDudeStatus;

                string combinedStatus = CombineStatusParts(statusParts[0], microDudeStatus, statusParts[1]);
                _currentFullStatus = combinedStatus;

                if (!currentStatus.Contains("Microdude"))
                {
                    combinedStatus = currentStatus + combinedStatus;
                }
                combinedStatus.TrimStart();

                Logger.Log($"Status bar updated (changed): {combinedStatus}");
                _statusBar.SetText(combinedStatus);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error updating status bar: {ex.Message}");
            }
        }

        public void ShowProgress(string operation)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_statusBar == null) return;

            // Start the animation
            object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_Build;
            _statusBar.Animation(1, ref icon);
            _statusBarAnimation = icon;

            string currentStatus = GetCurrentStatusText();
            string progressText = $"⚡ {operation}...";
            _lastMicroDudeStatus = progressText;

            _statusBar.SetText($"{currentStatus}  {progressText}");
        }

        public void ClearProgress()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_statusBar == null) return;

            // Stop the animation
            if (_statusBarAnimation != null)
            {
                object icon = _statusBarAnimation;
                _statusBar.Animation(0, ref icon);
                _statusBarAnimation = null;
            }

            UpdateStatusBar();
        }

        private void OnProgrammingStateChanged(object sender, PropertyChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateStatusBar();
        }

        private void OnDeviceChanged(object sender, string newDevice)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateStatusBar();
        }

        private void OnProgrammerChanged(object sender, Models.Programmer newProgrammer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateStatusBar();
        }

        private void OnPortChanged(object sender, string newPort)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateStatusBar();
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

             _refreshTimer?.Stop();
        _currentFullStatus = string.Empty;
        _currentMicroDudeStatus = string.Empty;

            if (_statusBar != null)
            {
                if (_statusBarAnimation != null)
                {
                    object icon = _statusBarAnimation;
                    _statusBar.Animation(0, ref icon);
                    _statusBarAnimation = null;
                }
            }

            var service = ProgrammingStateService.Instance;
            service.PropertyChanged -= OnProgrammingStateChanged;
            service.DeviceChanged -= OnDeviceChanged;
            service.ProgrammerChanged -= OnProgrammerChanged;
            service.PortChanged -= OnPortChanged;
        }
    }
}