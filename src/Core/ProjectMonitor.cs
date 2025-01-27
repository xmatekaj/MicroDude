using System;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System.Xml.Linq;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio;

namespace MicroDude.Core
{
    public class ProjectMonitor
    {
        private DTE2 _dte;
        private Events2 _events;
        private SolutionEvents _solutionEvents;
        private string _currentMicrocontroller;
        private readonly IServiceProvider _serviceProvider;

        private static readonly string[] PROJECT_FILE_EXTENSIONS = { ".cppproj", ".cproj", ".avrproj" };

        public event EventHandler<string> MicrocontrollerChanged;
        public string CurrentMicrocontroller => _currentMicrocontroller;

        public ProjectMonitor()
        {
            _serviceProvider = ServiceProvider.GlobalProvider;

            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                InitializeServices();
            });
        }

        private void InitializeServices()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
                if (_dte == null)
                {
                    Logger.Log("Failed to get DTE2 service");
                    return;
                }

                _events = _dte.Events as Events2;
                if (_events == null)
                {
                    Logger.Log("Failed to get Events2 object");
                    return;
                }

                _solutionEvents = _events.SolutionEvents;
                if (_solutionEvents == null)
                {
                    Logger.Log("Failed to get SolutionEvents");
                    return;
                }

                _solutionEvents.Opened += HandleSolutionOpened;
                _solutionEvents.AfterClosing += HandleSolutionClosed;

                if (_dte.Solution != null && !string.IsNullOrEmpty(_dte.Solution.FullName))
                {
                    HandleSolutionOpened();
                }

                Logger.Log("ProjectMonitor initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing ProjectMonitor: {ex.Message}");
            }
        }

        private void HandleSolutionOpened()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (_dte?.Solution == null)
                {
                    Logger.Log("Solution or DTE is null in HandleSolutionOpened");
                    return;
                }

                string solutionPath = _dte.Solution.FullName;
                if (string.IsNullOrEmpty(solutionPath))
                {
                    Logger.Log("Solution path is empty");
                    return;
                }

                string projectPath = GetProjectPathFromSolution(solutionPath);
                if (string.IsNullOrEmpty(projectPath))
                {
                    Logger.Log("Could not find project path in solution file");
                    return;
                }

                string deviceName = null;

                // First try to get info from the project file
                if (File.Exists(projectPath))
                {
                    deviceName = GetDeviceFromProjectFile(projectPath);
                }

                // If not found, try componentinfo.xml
                if (string.IsNullOrEmpty(deviceName))
                {
                    string componentInfoPath = Path.Combine(
                        Path.GetDirectoryName(projectPath),
                        Path.GetFileNameWithoutExtension(projectPath) + ".componentinfo.xml"
                    );

                    if (File.Exists(componentInfoPath))
                    {
                        deviceName = GetDeviceFromComponentInfo(componentInfoPath);
                    }
                }

                if (!string.IsNullOrEmpty(deviceName))
                {
                    UpdateMicrocontroller(deviceName);
                }
                else
                {
                    Logger.Log("Could not find device information in any project files");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in HandleSolutionOpened: {ex.Message}");
            }
        }

        private string GetProjectPathFromSolution(string solutionPath)
        {
            try
            {
                string solutionDir = Path.GetDirectoryName(solutionPath);
                string solutionContent = File.ReadAllText(solutionPath);

                foreach (string extension in PROJECT_FILE_EXTENSIONS)
                {
                    string pattern = $@"""([^""]*?{extension})""";
                    var match = Regex.Match(solutionContent, pattern);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        string relativePath = match.Groups[1].Value;
                        string fullPath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));
                        Logger.Log($"Found project file: {fullPath}");
                        return fullPath;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error parsing solution file: {ex.Message}");
                return null;
            }
        }

        private string GetDeviceFromProjectFile(string projectPath)
        {
            try
            {
                var doc = XDocument.Load(projectPath);
                var deviceElement = doc.Descendants("avrdevice").FirstOrDefault();

                if (deviceElement != null)
                {
                    string deviceName = deviceElement.Value.ToUpperInvariant();
                    Logger.Log($"Found device in project file: {deviceName}");
                    return deviceName;
                }

                // Also check common.Device element which might contain device info
                var commonDevice = doc.Descendants()
                    .Where(e => e.Name.LocalName == "avrgcc.common.Device" ||
                               e.Name.LocalName == "avrgcccpp.common.Device")
                    .FirstOrDefault();

                if (commonDevice != null)
                {
                    var match = Regex.Match(commonDevice.Value, @"-mmcu=(\w+)");
                    if (match.Success)
                    {
                        string deviceName = match.Groups[1].Value.ToUpperInvariant();
                        Logger.Log($"Found device in compiler options: {deviceName}");
                        return deviceName;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading project file: {ex.Message}");
                return null;
            }
        }

        private string GetDeviceFromComponentInfo(string componentInfoPath)
        {
            try
            {
                var doc = XDocument.Load(componentInfoPath);
                XNamespace ns = "AtmelPackComponentManagement";

                var referenceConditionId = doc.Descendants(ns + "ReferenceConditionId").FirstOrDefault();
                if (referenceConditionId != null)
                {
                    string deviceName = referenceConditionId.Value.ToUpperInvariant();
                    Logger.Log($"Found device in componentinfo.xml: {deviceName}");
                    return deviceName;
                }

                // Fallback: try without namespace
                referenceConditionId = doc.Descendants("ReferenceConditionId").FirstOrDefault();
                if (referenceConditionId != null)
                {
                    string deviceName = referenceConditionId.Value.ToUpperInvariant();
                    Logger.Log($"Found device in componentinfo.xml (no namespace): {deviceName}");
                    return deviceName;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading componentinfo.xml: {ex.Message}");
                return null;
            }
        }

        private void HandleSolutionClosed()
        {
            try
            {
                UpdateMicrocontroller(null);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in HandleSolutionClosed: {ex.Message}");
            }
        }

        private void UpdateMicrocontroller(string deviceName)
        {
            try
            {
                if (_currentMicrocontroller != deviceName)
                {
                    _currentMicrocontroller = deviceName;

                    ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        //if (!string.IsNullOrEmpty(deviceName))
                        //{
                        //    OutputPaneHandler.PrintTextToOutputPane($"Microcontroller detected: {deviceName}");
                        //}
                        MicrocontrollerChanged?.Invoke(this, deviceName);
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in UpdateMicrocontroller: {ex.Message}");
            }
        }

        public void RefreshMicrocontrollerInfo()
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    HandleSolutionOpened();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error in RefreshMicrocontrollerInfo: {ex.Message}");
                }
            });
        }
    }
}
