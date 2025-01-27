using System;
using System.IO;

public static class Logger
{
    private static string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MicroDudeLog.txt");

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(logFilePath, $"{DateTime.Now}: {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }

    public static void ClearLog()
    {
        try
        {
            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }
        }
        catch
        {
            // Silently fail if we can't delete the log file
        }
    }
}