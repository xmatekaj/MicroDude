using System;
using System.ComponentModel.Design;
using System.IO;
using Microsoft.VisualStudio.Shell;
using MicroDude.Services;
using MicroDude.Models;
using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using MicroDude.Properties;
using MicroDude.UI;
using System.Runtime.InteropServices;
using System.Linq;
using MicroDude.Core;
using MicroDude.Commands;
using EnvDTE;
using EnvDTE80;
using MicroDude.Parsers;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.ComponentModelHost;
using MicroDude.Output;


namespace MicroDude
{
    [ProvideService(typeof(IClassificationFormatMapService))]
    [ProvideService(typeof(IClassificationTypeRegistryService))]
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidsList.guidMicroDudePkg_string)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    public sealed class MicroDudePackage : Package, IVsUpdateSolutionEvents
    {
        private string _avrDudeExePath;
        private string _avrDudeConfigPath;
        private UsbDeviceService _usbDeviceService;
        //private FuseBitProgrammer _fuseBitProgrammer;
        private AvrDudeWrapper _avrDudeWrapper;
        private XmlParser _xmlParser;
        private ProjectMonitor _projectMonitor;
        private ProgrammingStateService _programmingStateService;
        private const string AvrDudeDir = "AvrDude";
        private const string AvrDudeExeName = "avrdude.exe";
        private const string AvrDudeConfName = "avrdude.conf";
        private DTE2 _dte;
        private uint _updateSolutionEventsCookie;
        private IClassificationTypeRegistryService _classificationRegistry;
        private IClassificationFormatMapService _formatMapService;

        protected override void Initialize()
        {
            base.Initialize();

            try
            {
                Logger.ClearLog();  // Start with a fresh log
                Logger.Log("MicroDude package initialization starting");

                InitializeAvrDude();
                InitializeAvrdudeConfigService();
                InitializeProgrammingStateService();
                InitializeUsbDeviceService();
                InitializeMenuCommand();
                InitializeProjectMonitor();
                RegisterCommands();
                _avrDudeWrapper = new AvrDudeWrapper(_avrDudeExePath, _avrDudeConfigPath);
                InitializeSolutionBuildManager();
                _xmlParser = new XmlParser();
                StatusBarHandler.Instance.Initialize();
                InitializeClassifier();

                OutputPaneHandler.PrintTextToOutputPane("MicroDude is up and ready!");

                PerformProgrammersCheck();
                
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(nameof(MicroDudePackage), $"Initialization failed: {ex}");
                throw;
            }
        }

        private void PerformProgrammersCheck()
        {
            // Final step after initialization
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                CheckConnectedProgrammers();
            });
        }

        private void InitializeSolutionBuildManager()
        {
            _dte = GetService(typeof(DTE)) as DTE2;
            if (_dte == null)
            {
                throw new InvalidOperationException("Failed to get DTE2 service.");
            }

            var solutionBuildManager = GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
            if (solutionBuildManager != null)
            {
                solutionBuildManager.AdviseUpdateSolutionEvents(this, out _updateSolutionEventsCookie);
            }
            else
            {
                throw new InvalidOperationException("Failed to get IVsSolutionBuildManager2 service.");
            }
        }

        private void RegisterCommands()
        {
            OleMenuCommandService commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                RegisterCommands(commandService);
            }
        }

        private void InitializeProjectMonitor()
        {
            _projectMonitor = new ProjectMonitor();
            if (_projectMonitor != null)
            {
                _projectMonitor.MicrocontrollerChanged += OnMicrocontrollerChanged;
            }
            else
            {
                throw new InvalidOperationException("ProjectMonitor failed to initialize.");
            }
        }

        private void InitializeProgrammingStateService()
        {
            _programmingStateService = ProgrammingStateService.Instance;
            _programmingStateService.PropertyChanged += OnProgrammingStateChanged;
        }

        private void InitializeClassifier()
        {
            var componentModel = GetService(typeof(SComponentModel)) as IComponentModel;
            if (componentModel != null)
            {
                _classificationRegistry = componentModel.GetService<IClassificationTypeRegistryService>();
                _formatMapService = componentModel.GetService<IClassificationFormatMapService>();

                // Get the output window
                var outputWindow = GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null)
                {
                    // Initialize for Build pane
                    Guid buildPaneGuid = VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid;
                    IVsOutputWindowPane vsPane;
                    if (outputWindow.GetPane(ref buildPaneGuid, out vsPane) == VSConstants.S_OK)
                    {
                        var userData = vsPane as IVsUserData;
                        if (userData != null)
                        {
                            // Get the text buffer
                            object textViewHost;
                            Guid guidIWpfTextViewHost = Microsoft.VisualStudio.Editor.DefGuidList.guidIWpfTextViewHost;
                            if (ErrorHandler.Succeeded(userData.GetData(ref guidIWpfTextViewHost, out textViewHost)))
                            {
                                var host = textViewHost as IWpfTextViewHost;
                                if (host?.TextView?.TextBuffer != null)
                                {
                                    // Apply the classifier
                                    OutputClassifierProvider classifierProvider = new OutputClassifierProvider();
                                    classifierProvider.ClassificationRegistry = _classificationRegistry;
                                    classifierProvider.GetClassifier(host.TextView.TextBuffer);
                                    Logger.Log("Output classifier applied to build pane");
                                }
                            }
                        }
                    }

                    // Initialize for MicroDude pane
                    Guid microDudePaneGuid = new Guid("4E33953E-ED90-4A9B-8488-A0EEFBDF660D");
                    outputWindow.CreatePane(ref microDudePaneGuid, "MicroDude", 1, 1);
                    IVsOutputWindowPane microDudeVsPane;
                    if (outputWindow.GetPane(ref microDudePaneGuid, out microDudeVsPane) == VSConstants.S_OK)
                    {
                        var userData = microDudeVsPane as IVsUserData;
                        if (userData != null)
                        {
                            object textViewHost;
                            Guid guidIWpfTextViewHost = Microsoft.VisualStudio.Editor.DefGuidList.guidIWpfTextViewHost;
                            if (ErrorHandler.Succeeded(userData.GetData(ref guidIWpfTextViewHost, out textViewHost)))
                            {
                                var host = textViewHost as IWpfTextViewHost;
                                if (host?.TextView?.TextBuffer != null)
                                {
                                    OutputClassifierProvider classifierProvider = new OutputClassifierProvider();
                                    classifierProvider.ClassificationRegistry = _classificationRegistry;
                                    classifierProvider.GetClassifier(host.TextView.TextBuffer);
                                    Logger.Log("Output classifier applied to MicroDude pane");
                                }
                            }
                        }
                    }
                }

                Logger.Log("Classification services initialized");
            }
            else
            {
                Logger.Log("Failed to get component model");
            }
        }

        private void OnMicrocontrollerChanged(object sender, string microcontroller)
        {
            if (!string.IsNullOrEmpty(microcontroller))
            {
                _programmingStateService.CurrentDevice = microcontroller;
                OutputPaneHandler.PrintTextToOutputPane($"Microcontroller in the solution: {microcontroller}");
            }
            else
            {
                _programmingStateService.CurrentDevice = null;
                OutputPaneHandler.PrintTextToOutputPane($"Warning. Microcontroller in the solution is unknown");
            }
        }

        private void OnProgrammingStateChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            UpdateCommandAvailability();
        }

        private void UpdateCommandAvailability()
        {
            // Update command availability based on programming state
            bool canProgram = _programmingStateService.IsReadyToProgram();
            // Update UI commands here if needed
        }

        private void InitializeAvrDude()
        {
            string extensionDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
            string avrDudeDirectory = Path.Combine(extensionDirectory, AvrDudeDir);
            if (!Directory.Exists(avrDudeDirectory))
            {
                Directory.CreateDirectory(avrDudeDirectory);
            }

            _avrDudeExePath = Path.Combine(avrDudeDirectory, AvrDudeExeName);
            _avrDudeConfigPath = Path.Combine(avrDudeDirectory, AvrDudeConfName);

            if (!File.Exists(_avrDudeExePath))
            {
                File.Copy(Path.Combine(extensionDirectory, AvrDudeExeName), _avrDudeExePath);
            }

            if (!File.Exists(_avrDudeConfigPath))
            {
                File.Copy(Path.Combine(extensionDirectory, AvrDudeConfName), _avrDudeConfigPath);
            }

            // Always update the stored path
            MicroDudeSettings.Default.AvrDudePath = _avrDudeExePath;
            MicroDudeSettings.Default.Save();

            if (_avrDudeExePath == null)
            {
                throw new InvalidOperationException("AvrDude executable path is not set.");
            }

            Logger.Log($"AvrDude initialized. Executable path: {_avrDudeExePath}");
            Logger.Log($"AvrDude config path: {_avrDudeConfigPath}");
        }

        private void InitializeAvrdudeConfigService()
        {
            try
            {
                if (string.IsNullOrEmpty(_avrDudeExePath))
                {
                    throw new InvalidOperationException("AvrDude executable path is not set.");
                }
                AvrdudeConfigService.Instance.InitializeSync(_avrDudeExePath);
                Logger.Log($"AvrdudeConfigService initialized. Programmer count: {AvrdudeConfigService.Instance.Programmers.Count}");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error initializing AvrdudeConfigService: {ex.Message}");
                throw;
            }
            if (AvrdudeConfigService.Instance == null)
            {
                throw new InvalidOperationException("AvrdudeConfigService failed to initialize.");
            }
        }

        public int OnActiveProjectCfgChange(IVsHierarchy pIVsHierarchy)
        {
            return VSConstants.S_OK;
        }

        private void InitializeUsbDeviceService()
        {
            var supportedProgrammers = AvrdudeConfigService.Instance?.Programmers
                .Where(p => !string.IsNullOrEmpty(p.UsbVid) && p.UsbPids.Any())
                .ToList() ?? new List<Programmer>();

            _usbDeviceService = new UsbDeviceService(supportedProgrammers);

            if (_usbDeviceService == null)
            {
                throw new InvalidOperationException("UsbDeviceService failed to initialize.");
            }

            _usbDeviceService.UsbDeviceChanged += UsbDeviceService_UsbDeviceChanged;
        }

        private void UsbDeviceService_UsbDeviceChanged(object sender, UsbDeviceChangeEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    Logger.Log("### UsbDeviceService_UsbDeviceChanged");
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (e.ConnectedProgrammers.Any())
                    {
                        foreach (var programmer in e.ConnectedProgrammers)
                        {
                            _programmingStateService.DetectedProgrammer = programmer;
                            OutputPaneHandler.PrintTextToOutputPane($"Programmer connected: {programmer.Id}");
                            Logger.Log($"Programmer connected: {programmer.Id}");
                        }
                    }

                    if (e.DisconnectedProgrammers.Any())
                    {
                        foreach (var programmer in e.DisconnectedProgrammers)
                        {
                            _programmingStateService.DetectedProgrammer = null;
                            OutputPaneHandler.PrintTextToOutputPane($"Programmer disconnected: {programmer.Id}");
                            Logger.Log($"Programmer disconnected: {programmer.Id}");
                        }
                    }
                    
                    if (e.CurrentlyConnectedProgrammerIds.Count == 0)
                    {
                        OutputPaneHandler.PrintTextToOutputPane("No programmers are currently connected.");
                    }
                    // TODO:
                    // support multiple programmers
                    //else
                    //{
                    //    string programmers = "";
                    //    foreach (var programmerId in e.CurrentlyConnectedProgrammerIds)
                    //    {
                    //        programmers += programmerId + " ";
                    //    }
                    //    OutputPaneHandler.PrintTextToOutputPane("Currently connected programmers: " + programmers);
                    //}
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error in UsbDeviceService_UsbDeviceChanged: {ex.Message}");
                }
            });
        }

        private void InitializeMenuCommand()
        {
            OleMenuCommandService commandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null)
            {
                throw new InvalidOperationException("Failed to get IMenuCommandService");
            }

            var commandId = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidSettingsCommandId);
            var menuItem = new MenuCommand(SettingsCommandCallback, commandId);
            commandService.AddCommand(menuItem);
        }

        private void RegisterCommands(OleMenuCommandService commandService)
        {
            // Register Oscillator command
            CommandID oscillatorCommandId = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidOscillatorCommandId);
            MenuCommand oscillatorCommand = new MenuCommand(OscillatorCommandCallback, oscillatorCommandId);
            commandService.AddCommand(oscillatorCommand);

            // Register Fuse Bit command
            CommandID fuseBitsCommandId = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidFuseBitsCommandId);
            MenuCommand fuseBitsCommand = new MenuCommand(FuseBitsCommandCallback, fuseBitsCommandId);
            commandService.AddCommand(fuseBitsCommand);

            // Register Lock Bit command
            CommandID lockBitsCommandId = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidLockBitsCommandId);
            MenuCommand lockBitsCommand = new MenuCommand(LockBitsCommandCallback, lockBitsCommandId);
            commandService.AddCommand(lockBitsCommand);

            // Register Detect command
            CommandID detectCommandId = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidDetectCommandId);
            MenuCommand detectCommand = new MenuCommand(DetectCommandCallback, detectCommandId);
            commandService.AddCommand(detectCommand);

            // Register Verify command
            CommandID verifyCommandId = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidVerifyCommandId);
            MenuCommand verifyCommand = new MenuCommand(VerifyCommandCallback, verifyCommandId);
            commandService.AddCommand(verifyCommand);

            FlashAutoCommand.Initialize(this);
        }

        private void DetectCommandCallback(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            AvrDudeResult result = _avrDudeWrapper.GetSignature(MicroDudeSettings.Default.Programmer, "usb");
            AvrDudeOutputParser.ParseSignatureOutput(result);
        }

        private void VerifyCommandCallback(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var buildFiles = SolutionHandler.GetBuildOutputFiles(_dte);
            if (buildFiles != null && !string.IsNullOrEmpty(buildFiles.HexFile))
            {
                if (_programmingStateService.IsReadyToProgram())
                {
                    var parameters = _programmingStateService.GetProgrammingParameters();
                    var result = _avrDudeWrapper.FlashFirmware(
                        parameters.DeviceName,
                        parameters.ProgrammerName,
                        parameters.Port,
                        buildFiles.HexFile);

                    if (result.Success)
                    {
                        OutputPaneHandler.PrintTextToOutputPane("Verification succeeded!");
                    }
                    else
                    {
                        OutputPaneHandler.PrintTextToOutputPane($"Verification failed!\n{result.Error}");
                    }
                }
            }
        }

        private void LockBitsCommandCallback(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var lockBitsWindow = new LockBitsWindow();
            lockBitsWindow.Show();
        }

        private void FuseBitsCommandCallback(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var fuseBitsWindow = new FuseBitsWindow();
            fuseBitsWindow.Show();
        }

        private void OscillatorCommandCallback(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var oscillatorWindow = new OscillatorWindow();
            oscillatorWindow.Show();
        }

        private void CheckConnectedProgrammers()
        {
            try
            {
                _usbDeviceService.CheckForConnectedProgrammers();
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(nameof(MicroDudePackage), $"Error checking for connected programmers: {ex}");
                OutputPaneHandler.PrintTextToOutputPane($"Error checking for connected programmers: {ex.Message}");
            }
        }

        private void SettingsCommandCallback(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                Logger.Log("Attempting to open Settings window");
                var settingsWindow = new Settings();
                Logger.Log("Settings window instance created");
                settingsWindow.ShowDialog();
                Logger.Log("Settings window shown");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in SettingsCommandCallback: {ex}");
                ActivityLog.LogError(nameof(MicroDudePackage), $"Error in SettingsCommandCallback: {ex}");
            }
        }

        // IVsUpdateSolutionEvents implementation
        public int UpdateSolution_Begin(ref int pfCancelUpdate) { return VSConstants.S_OK; }

        public int UpdateSolution_Done(int fSucceeded, int fModified, int fCancelCommand)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (fSucceeded == 1)
            {
                var buildFiles = SolutionHandler.GetBuildOutputFiles(_dte);
                bool needsFlashing = false;

                if (buildFiles != null)
                {
                    // Check if hex file has changed
                    if (!string.IsNullOrEmpty(buildFiles.HexFile))
                    {
                        if (_programmingStateService.HasFileChanged(buildFiles.HexFile))
                        {
                            needsFlashing = true;
                            _programmingStateService.UpdateFileHash(buildFiles.HexFile);
                            Logger.Log($"HEX file changed: {buildFiles.HexFile}");
                        }
                    }

                    // Check if eep file has changed
                    if (!string.IsNullOrEmpty(buildFiles.EepFile))
                    {
                        if (_programmingStateService.HasFileChanged(buildFiles.EepFile))
                        {
                            needsFlashing = true;
                            _programmingStateService.UpdateFileHash(buildFiles.EepFile);
                            Logger.Log($"EEP file changed: {buildFiles.EepFile}");
                        }
                    }
                }

                UpdateMemoryUsageFromBuildOutput();

                // Handle auto-flash if enabled and files have changed
                if (MicroDudeSettings.Default.AutoFlash && needsFlashing)
                {
                    if (buildFiles != null && !string.IsNullOrEmpty(buildFiles.HexFile))
                    {
                        if (_programmingStateService.IsReadyToProgram())
                        {
                            var parameters = _programmingStateService.GetProgrammingParameters();
                            var result = _avrDudeWrapper.FlashFirmware(
                                parameters.DeviceName,
                                parameters.ProgrammerName,
                                parameters.Port,
                                buildFiles.HexFile);

                            if (result.Success)
                            {
                                OutputPaneHandler.PrintTextToOutputPane("Programming successful!");
                                OutputPaneHandler.PrintTextToOutputPane("Memory Usage:");
                                OutputPaneHandler.PrintTextToOutputPane($"Flash: {_programmingStateService.MemoryUsage.GetFlashUsageString()}");
                                OutputPaneHandler.PrintTextToOutputPane($"EEPROM: {_programmingStateService.MemoryUsage.GetEepromUsageString()}");
                            }
                            else
                            {
                                OutputPaneHandler.PrintTextToOutputPane($"Programming failed: {result.Error}");
                            }
                        }
                        else
                        {
                            OutputPaneHandler.PrintTextToOutputPane("Cannot program: missing device, programmer, or port configuration.");
                        }
                    }
                }
                else if (MicroDudeSettings.Default.AutoFlash && !needsFlashing)
                {
                    OutputPaneHandler.PrintTextToOutputPane($"Files did not change. Auto-flash will not be performed.");
                }
                else if (_programmingStateService.MemoryUsage != null)
                {
                    // Even if not auto-flashing, still show memory usage
                    OutputPaneHandler.PrintTextToOutputPane("Build successful. Memory Usage:");
                    OutputPaneHandler.PrintTextToOutputPane($"Flash: {_programmingStateService.MemoryUsage.GetFlashUsageString()}");
                    OutputPaneHandler.PrintTextToOutputPane($"EEPROM: {_programmingStateService.MemoryUsage.GetEepromUsageString()}");
                }
            }

            return VSConstants.S_OK;
        }

        private void UpdateMemoryUsageFromBuildOutput()
        {
            // Get build output for memory usage parsing
            var outputWindow = GetService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow != null)
            {
                IVsOutputWindowPane buildPane;
                Guid buildPaneGuid = VSConstants.GUID_BuildOutputWindowPane;
                outputWindow.GetPane(ref buildPaneGuid, out buildPane);

                if (buildPane != null)
                {
                    try
                    {
                        var userData = buildPane as IVsUserData;
                        if (userData != null)
                        {
                            object textViewHost;
                            Guid guidIWpfTextViewHost = Microsoft.VisualStudio.Editor.DefGuidList.guidIWpfTextViewHost;
                            if (ErrorHandler.Succeeded(userData.GetData(ref guidIWpfTextViewHost, out textViewHost)))
                            {
                                var host = textViewHost as IWpfTextViewHost;
                                if (host?.TextView?.TextBuffer != null)
                                {
                                    string buildOutput = host.TextView.TextBuffer.CurrentSnapshot.GetText();

                                    // Parse memory usage and update ProgrammingStateService
                                    var programMatch = Regex.Match(buildOutput, @"Program Memory Usage\s*:\s*(\d+)\s*bytes");
                                    var dataMatch = Regex.Match(buildOutput, @"Data Memory Usage\s*:\s*(\d+)\s*bytes");

                                    if (programMatch.Success && dataMatch.Success)
                                    {
                                        int flashUsed = int.Parse(programMatch.Groups[1].Value);
                                        int eepromUsed = int.Parse(dataMatch.Groups[1].Value);

                                        // Update memory usage in ProgrammingStateService
                                        _programmingStateService.UpdateMemoryUsage(flashUsed, eepromUsed);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ActivityLog.LogError(nameof(MicroDudePackage), $"Error reading build output: {ex.Message}");
                    }
                }
            }
        }

        public int UpdateSolution_StartUpdate(ref int pfCancelUpdate) { return VSConstants.S_OK; }
        public int UpdateSolution_Cancel() { return VSConstants.S_OK; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _usbDeviceService?.Dispose();

                if (_updateSolutionEventsCookie != 0)
                {
                    var solutionBuildManager = GetService(typeof(SVsSolutionBuildManager)) as IVsSolutionBuildManager2;
                    if (solutionBuildManager != null)
                    {
                        solutionBuildManager.UnadviseUpdateSolutionEvents(_updateSolutionEventsCookie);
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}