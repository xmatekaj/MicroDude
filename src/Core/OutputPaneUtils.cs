using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace MicroDude.Core
{
    public static class OutputPaneUtils
    {
        private static IVsOutputWindowPane _microDudePane;
        private static DTE2 _dte;

        public static OutputWindowPane GetActiveOrMicroDudePane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                // First try DTE2 approach
                try
                {
                    _dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
                    if (_dte != null && _dte.ToolWindows != null)
                    {
                        try
                        {
                            OutputWindow outputWindow = _dte.ToolWindows.OutputWindow;
                            if (outputWindow?.OutputWindowPanes != null)
                            {
                                // Return active pane if there is one
                                if (outputWindow.ActivePane != null)
                                {
                                    return outputWindow.ActivePane;
                                }

                                // Try to find existing MicroDude pane
                                foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
                                {
                                    if (pane?.Name == "MicroDude")
                                    {
                                        return pane;
                                    }
                                }

                                // Create new MicroDude pane if needed
                                try
                                {
                                    return outputWindow.OutputWindowPanes.Add("MicroDude");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log($"Failed to create MicroDude pane via DTE: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log($"Error accessing OutputWindow via DTE: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"DTE approach failed: {ex.Message}");
                }

                // If DTE approach failed, try IVsOutputWindowPane approach
                if (_microDudePane == null)
                {
                    var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
                    if (outputWindow != null)
                    {
                        Guid microDudePaneGuid = OutputPaneHandler.MicroDudePaneGuid;

                        // Create the pane if it doesn't exist
                        outputWindow.CreatePane(ref microDudePaneGuid, "MicroDude", 1, 1);
                        if (outputWindow.GetPane(ref microDudePaneGuid, out _microDudePane) == VSConstants.S_OK)
                        {
                            return new OutputWindowPaneWrapper(_microDudePane);
                        }
                    }
                }
                else
                {
                    return new OutputWindowPaneWrapper(_microDudePane);
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting output pane: {ex.Message}");
                return null;
            }
        }

        public static OutputWindowPane CreateMicroDudePane()
        {
            var outputWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow != null)
            {
                Guid microDudePaneGuid = OutputPaneHandler.MicroDudePaneGuid;

                // Create the pane if it doesn't exist
                outputWindow.CreatePane(ref microDudePaneGuid, "MicroDude", 1, 1);
                if (outputWindow.GetPane(ref microDudePaneGuid, out _microDudePane) == VSConstants.S_OK)
                {
                    return new OutputWindowPaneWrapper(_microDudePane);
                }
            }
            return null;
        }

        private class OutputWindowPaneWrapper : OutputWindowPane
        {
            private readonly IVsOutputWindowPane _vsPane;

            public OutputWindowPaneWrapper(IVsOutputWindowPane vsPane)
            {
                _vsPane = vsPane;
            }

            void OutputWindowPane.Activate()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _vsPane?.Activate();
            }

            void OutputWindowPane.Clear()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _vsPane?.Clear();
            }

            void OutputWindowPane.OutputString(string text)
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _vsPane?.OutputString(text);
            }

            string OutputWindowPane.Name => "MicroDude";
            OutputWindowPanes OutputWindowPane.Collection => null;
            DTE OutputWindowPane.DTE => null;
            string OutputWindowPane.Guid => OutputPaneHandler.MicroDudePaneGuid.ToString();
            TextDocument OutputWindowPane.TextDocument => null;

            void OutputWindowPane.OutputTaskItemString(
                string text,
                vsTaskPriority priority,
                string category,
                vsTaskIcon icon,
                string file,
                int line,
                string description,
                bool shouldLog)
            {
            }

            void OutputWindowPane.ForceItemsToTaskList()
            {
            }
        }

        public static void Clear()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var pane = GetActiveOrMicroDudePane();
                pane?.Clear();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error clearing output pane: {ex.Message}");
            }
        }
    }
}