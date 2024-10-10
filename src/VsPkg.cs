using MicroDude.Properties;
using MicroDude.UI;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;

namespace MicroDude
{
    /// <summary>
    /// This is the class that implements the package. This is the class that Visual Studio will create
    /// when one of the commands will be selected by the user, and so it can be considered the main
    /// entry point for the integration with the IDE.
    /// Notice that this implementation derives from Microsoft.VisualStudio.Shell.Package that is the
    /// basic implementation of a package provided by the Managed Package Framework (MPF).
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]

    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidsList.guidMicroDudePkg_string)]
    [ComVisible(true)]
    public sealed class MicroDude : Package
    {
        /// <summary>
        /// Default constructor of the package. This is the constructor that will be used by VS
        /// to create an instance of your package. Inside the constructor you should do only the
        /// more basic initializazion like setting the initial value for some member variable. But
        /// you should never try to use any VS service because this object is not part of VS
        /// environment yet; you should wait and perform this kind of initialization inside the
        /// Initialize method.
        /// </summary>
        public MicroDude()
        {
        }

        private void InitializeAvrDude()
        {
            string extensionDirectory = Path.GetDirectoryName(GetType().Assembly.Location);
            string avrDudeDirectory = Path.Combine(extensionDirectory, "AvrDude");

            if (!Directory.Exists(avrDudeDirectory))
            {
                Directory.CreateDirectory(avrDudeDirectory);
            }

            string avrDudeExePath = Path.Combine(avrDudeDirectory, "avrdude.exe");
            string avrDudeConfPath = Path.Combine(avrDudeDirectory, "avrdude.conf");

            if (!File.Exists(avrDudeExePath))
            {
                File.Copy(Path.Combine(extensionDirectory, "avrdude.exe"), avrDudeExePath);
            }

            if (!File.Exists(avrDudeConfPath))
            {
                File.Copy(Path.Combine(extensionDirectory, "avrdude.conf"), avrDudeConfPath);
            }

            if (string.IsNullOrEmpty(MicroDudeSettings.Default.AvrDudePath))
            {
                MicroDudeSettings.Default.AvrDudePath = avrDudeExePath;
                MicroDudeSettings.Default.Save();
            }
        }

        private void SettingsCommandCallback(object sender, EventArgs e)
        {
            var settingsWindow = new Settings();
            settingsWindow.ShowDialog(); // This will show the window as a modal dialog
        }

        /// <summary>
        /// Initialization of the package; this is the place where you can put all the initialization
        /// code that relies on services provided by Visual Studio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            InitializeAvrDude();

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                CommandID settingsCommandID = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidSettingsCommandId);
                OleMenuCommand settingsCommand = new OleMenuCommand(new EventHandler(SettingsCommandCallback), settingsCommandID);
                mcs.AddCommand(settingsCommand);
            }
        }

        public static void OutputDebug(string text)
        {
            var package = (MicroDude)Package.GetGlobalService(typeof(MicroDude));
            if (package == null)
            {
                Debug.WriteLine("Failed to get package instance");
                return;
            }

            IVsOutputWindowPane windowPane = (IVsOutputWindowPane)package.GetService(typeof(SVsGeneralOutputWindowPane));
            if (null == windowPane)
            {
                Debug.WriteLine("Failed to get a reference to the Output window General pane");
                return;
            }
            if (Microsoft.VisualStudio.ErrorHandler.Failed(windowPane.OutputString(text + Environment.NewLine)))
            {
                Debug.WriteLine("Failed to write on the Output window");
            }
        }
    }
}
