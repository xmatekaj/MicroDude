using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MicroDude.Core
{
    public class SolutionHandler
    {
        private const string DEFAULT_OUTPUT_FOLDER = "Debug";
        private static readonly string[] POSSIBLE_CONFIG_FOLDERS = { "Debug", "Release" };
        private static readonly string[] TARGET_EXTENSIONS = { ".hex", ".eep" };

        public class BuildOutputFiles
        {
            public string HexFile { get; set; }
            public string EepFile { get; set; }
            public string OutputPath { get; set; }
        }

        public static BuildOutputFiles GetBuildOutputFiles(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (dte?.Solution?.Projects == null)
                {
                    ActivityLog.LogError(nameof(MicroDudePackage), "DTE, Solution, or Projects is null");
                    return null;
                }

                Project project = dte.Solution.Projects.Item(1); // Assumes first project in solution
                if (project == null)
                {
                    ActivityLog.LogError(nameof(MicroDudePackage), "Unable to get the first project in the solution");
                    return null;
                }

                string outputPath = GetOutputPath(project);
                if (string.IsNullOrEmpty(outputPath))
                {
                    ActivityLog.LogError(nameof(MicroDudePackage), "Unable to determine output path");
                    return null;
                }

                var buildFiles = new BuildOutputFiles
                {
                    OutputPath = outputPath,
                    HexFile = FindOutputFile(outputPath, ".hex"),
                    EepFile = FindOutputFile(outputPath, ".eep")
                };

                LogFoundFiles(buildFiles);
                return buildFiles;
            }
            catch (Exception ex)
            {
                ActivityLog.LogError(nameof(MicroDudePackage), $"Error in GetBuildOutputFiles: {ex.Message}");
                return null;
            }
        }

        internal static string GetOutputPath(Project project)
        {
            try
            {
                string outputPath = TryGetOutputPathFromAvr(project)
                    ?? TryGetOutputPathFromProperties(project)
                    ?? TryGetOutputPathFromOutputGroups(project)
                    ?? TryGetDefaultOutputPath(project);

                if (!string.IsNullOrEmpty(outputPath))
                {
                    Logger.Log($"Found output path: {outputPath}");
                    return outputPath;
                }

                return null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting output path: {ex.Message}");
                return null;
            }
        }

        private static string TryGetOutputPathFromAvr(Project project)
        {
            try
            {
                // For AVR projects, check the project directory first
                string projectDir = Path.GetDirectoryName(project.FullName);

                // Try to get active configuration
                string configName = "Debug"; // Default to Debug
                try
                {
                    configName = project.ConfigurationManager.ActiveConfiguration.ConfigurationName;
                }
                catch
                {
                    Logger.Log("Could not get active configuration, using Debug");
                }

                // First try the active configuration folder
                string outputPath = Path.Combine(projectDir, configName);
                if (Directory.Exists(outputPath) && HasTargetFiles(outputPath))
                {
                    return outputPath;
                }

                // Try all possible configuration folders
                foreach (var config in POSSIBLE_CONFIG_FOLDERS)
                {
                    outputPath = Path.Combine(projectDir, config);
                    if (Directory.Exists(outputPath) && HasTargetFiles(outputPath))
                    {
                        return outputPath;
                    }
                }

                // Try project directory itself
                if (HasTargetFiles(projectDir))
                {
                    return projectDir;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in TryGetOutputPathFromAvr: {ex.Message}");
            }
            return null;
        }

        private static bool HasTargetFiles(string path)
        {
            return TARGET_EXTENSIONS.Any(ext => Directory.GetFiles(path, $"*{ext}").Any());
        }

        private static string FindOutputFile(string outputPath, string extension)
        {
            try
            {
                // Look for any file with the specified extension
                var files = Directory.GetFiles(outputPath, $"*{extension}");
                if (files.Length > 0)
                {
                    // If multiple files exist, get the most recently modified one
                    return files.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error finding {extension} file: {ex.Message}");
            }
            return null;
        }

        private static void LogFoundFiles(BuildOutputFiles files)
        {
            Logger.Log($"Output path: {files.OutputPath}");
            Logger.Log($"HEX file: {files.HexFile ?? "not found"}");
            Logger.Log($"EEP file: {files.EepFile ?? "not found"}");
        }

        private static string TryGetOutputPathFromProperties(Project project)
        {
            try
            {
                var properties = project.Properties;
                if (properties == null) return null;

                // Look for AVR-specific output directory property
                var outputPathProperty = properties.Item("OutputDirectory");
                if (outputPathProperty?.Value != null)
                {
                    string outputPath = outputPathProperty.Value.ToString();
                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        string fullPath = Path.Combine(Path.GetDirectoryName(project.FullName), outputPath);
                        if (HasTargetFiles(fullPath)) return fullPath;
                    }
                }

                // Try standard OutputPath property as fallback
                outputPathProperty = properties.Item("OutputPath");
                if (outputPathProperty?.Value != null)
                {
                    string outputPath = outputPathProperty.Value.ToString();
                    if (!string.IsNullOrEmpty(outputPath))
                    {
                        string fullPath = Path.Combine(Path.GetDirectoryName(project.FullName), outputPath);
                        if (HasTargetFiles(fullPath)) return fullPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting output path from properties: {ex.Message}");
            }
            return null;
        }

        private static string TryGetOutputPathFromOutputGroups(Project project)
        {
            try
            {
                var outputGroups = project.ConfigurationManager.ActiveConfiguration.OutputGroups;
                foreach (OutputGroup group in outputGroups)
                {
                    if (group.CanonicalName == "Built")
                    {
                        var outputs = group.FileURLs as Array;
                        if (outputs?.Length > 0)
                        {
                            string path = Path.GetDirectoryName(new Uri(outputs.GetValue(0).ToString()).LocalPath);
                            if (HasTargetFiles(path)) return path;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting output path from output groups: {ex.Message}");
            }
            return null;
        }

        private static string TryGetDefaultOutputPath(Project project)
        {
            try
            {
                string projectDir = Path.GetDirectoryName(project.FullName);
                string configName = project.ConfigurationManager.ActiveConfiguration?.ConfigurationName ?? DEFAULT_OUTPUT_FOLDER;
                string path = Path.Combine(projectDir, configName);

                if (Directory.Exists(path) && HasTargetFiles(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error getting default output path: {ex.Message}");
            }
            return null;
        }
    }
}