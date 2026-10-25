using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Appends each visitor's name and email on this device.
/// Android path: Android/data/&lt;package&gt;/files/Visitors/visitors.csv
/// </summary>
public static class VisitorLog
{
    public static string FolderPath => Path.Combine(Application.persistentDataPath, "Visitors");
    public static string FilePath => Path.Combine(FolderPath, "visitors.csv");

    public static void Append(string visitorName, string emailId)
    {
        try
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            bool writeHeader = !File.Exists(FilePath);
            var line = new StringBuilder();
            if (writeHeader)
                line.AppendLine("timestamp,name,email");

            line.Append(Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            line.Append(',');
            line.Append(Csv(visitorName));
            line.Append(',');
            line.Append(Csv(emailId));
            line.AppendLine();

            File.AppendAllText(FilePath, line.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Could not save visitor CSV: " + ex.Message);
        }
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        string cleaned = value.Trim().Replace("\"", "\"\"");
        return "\"" + cleaned + "\"";
    }
}
