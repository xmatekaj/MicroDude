using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using MicroDude.Properties;
using MicroDude.Core;

namespace MicroDude.Commands
{
    /// <summary>
    /// Class handles state of Flash Auto icon and command
    /// </summary>
    public class FlashAutoCommand
    {
        private readonly Package _package;
        private readonly OleMenuCommand _command;

        public static FlashAutoCommand Instance { get; private set; }

        public static void Initialize(Package package)
        {
            Instance = new FlashAutoCommand(package);
        }

        private FlashAutoCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _package = package;
            var commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            if (commandService != null)
            {
                var commandId = new CommandID(GuidsList.guidMicroDudeCmdSet, PkgCmdIDList.cmdidFlashAutoCommandId);
                _command = new OleMenuCommand(Execute, commandId);
                _command.BeforeQueryStatus += OnBeforeQueryStatus;
                commandService.AddCommand(_command);
            }
        }

        private IServiceProvider ServiceProvider
        {
            get { return _package; }
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ToggleAutoFlash();
        }

        private bool IsEnabled()
        {
            return MicroDudeSettings.Default.AutoFlash;
        }

        private void ToggleAutoFlash()
        {
            MicroDudeSettings.Default.AutoFlash = !MicroDudeSettings.Default.AutoFlash;
            MicroDudeSettings.Default.Save();
            if (IsEnabled())
            {
                OutputPaneHandler.PrintTextToOutputPane("Auto-Flash is now Enabled");
            }
            else
            {
                OutputPaneHandler.PrintTextToOutputPane("Auto-Flash is now Disabled");
            }
        }

        private void OnBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var command = sender as OleMenuCommand;
            if (command == null)
            {
                return;
            }

            command.Enabled = true;
            command.Visible = true;
            command.Checked = IsEnabled();
            command.Text = string.Format("Auto-Flash [{0}]", IsEnabled() ? "Enabled" : "Disabled");
        }
    }
}