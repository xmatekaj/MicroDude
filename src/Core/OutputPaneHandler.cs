using MicroDude.Models;
using MicroDude.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using EnvDTE;
using Microsoft.VisualStudio;

namespace MicroDude.Core
{
    class OutputPaneHandler
    {
        public static readonly Guid MicroDudePaneGuid = new Guid("4E33953E-ED90-4A9B-8488-A0EEFBDF660D");

        private static string PrepareOutputString(string text)
        {
            string outputText = DateTime.Now.ToString(@"hh\:mm\:ss\.fff") + "  # MicroDude #  " + text + Environment.NewLine;
            return outputText;
        }

        public static void PrintTextToOutputPane(string text)
        {
            try
            {
                OutputDestination destination = (OutputDestination)MicroDudeSettings.Default.OutputDestination;
                if (destination == OutputDestination.None)
                    return;

                string outputText = PrepareOutputString(text);
                Logger.Log(text);

                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    try
                    {
                        OutputWindowPane pane = null;

                        switch (destination)
                        {
                            case OutputDestination.MicroDude:
                                OutputToMicroDudePane(outputText);
                                break;
                            case OutputDestination.MicroDudeOrActivePane:
                                pane = OutputPaneUtils.GetActiveOrMicroDudePane();
                                pane?.OutputString(outputText);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error writing to output pane: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in PrintTextToOutputPane: {ex.Message}");
            }
        }

        private static void OutputToMicroDudePane(string text)
        {
            var pane = OutputPaneUtils.CreateMicroDudePane();
            if (pane != null)
            {
                pane.OutputString(text);
            }
            else
            {
                Logger.Log($"Error in creating MicroDude Pane");
            }
        }

        private static void OutputToBuildPane(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                if (outputWindow != null)
                {
                    Guid buildPaneGuid = VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid;
                    IVsOutputWindowPane buildPane;

                    outputWindow.CreatePane(ref buildPaneGuid, "Build", 1, 0);
                    if (outputWindow.GetPane(ref buildPaneGuid, out buildPane) == VSConstants.S_OK)
                    {
                        buildPane.OutputString(text);
                        buildPane.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error writing to Build pane: {ex.Message}");
            }
        }
    }
}