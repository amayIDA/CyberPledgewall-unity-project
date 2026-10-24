using System;
using System.IO;
using UnityEngine;

public class PledgeStorageManager : MonoBehaviour
{
    public static PledgeStorageManager Instance { get; private set; }

    public static string SavePath => Path.Combine(AppConfig.Directory, "pledges.json");
    public static string ImagesDirectory => Path.Combine(AppConfig.Directory, "Pledges");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SavePledge(string visitorName, string emailId, byte[] imageBytes)
    {
        PledgeDatabase database = LoadPledges();
        var entry = new PledgeEntry
        {
            visitorName = visitorName ?? string.Empty,
            emailId = emailId ?? string.Empty,
            pledgeText = string.Empty,
            imageBase64 = imageBytes != null && imageBytes.Length > 0
                ? Convert.ToBase64String(imageBytes)
                : string.Empty,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        database.pledges.Add(entry);
        WriteDatabase(database);
        WriteImageFile(entry.timestamp, visitorName, imageBytes);
        Debug.Log($"Saved pledge to {SavePath} (count: {database.pledges.Count}).");
    }

    public PledgeDatabase LoadPledges()
    {
        try
        {
            string path = SavePath;
            if (!File.Exists(path))
            {
                string legacy = Path.Combine(Application.persistentDataPath, "pledges.json");
                if (File.Exists(legacy))
                    path = legacy;
                else
                    return new PledgeDatabase();
            }

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new PledgeDatabase();

            var database = JsonUtility.FromJson<PledgeDatabase>(json);
            if (database == null)
                return new PledgeDatabase();
            if (database.pledges == null)
                database.pledges = new System.Collections.Generic.List<PledgeEntry>();
            foreach (var entry in database.pledges)
            {
                if (string.IsNullOrEmpty(entry.emailId) && !string.IsNullOrEmpty(entry.employeeId))
                    entry.emailId = entry.employeeId;
            }
            return database;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load pledges from {SavePath}: {ex.Message}");
            return new PledgeDatabase();
        }
    }

    public void ClearAllPledges()
    {
        try
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to delete {SavePath}: {ex.Message}");
        }

        if (WallManager.Instance != null)
            WallManager.Instance.ClearDisplayedPledges();

        Debug.Log("Cleared all saved pledges.");
    }

    public static byte[] DecodeImageBytes(string imageBase64)
    {
        if (string.IsNullOrEmpty(imageBase64))
            return Array.Empty<byte>();

        try
        {
            return Convert.FromBase64String(imageBase64);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Invalid pledge image Base64: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private static void WriteDatabase(PledgeDatabase database)
    {
        if (database.pledges == null)
            database.pledges = new System.Collections.Generic.List<PledgeEntry>();

        string directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string json = JsonUtility.ToJson(database, true);
        File.WriteAllText(SavePath, json);
    }

    private static void WriteImageFile(string timestamp, string visitorName, byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            return;

        try
        {
            if (!Directory.Exists(ImagesDirectory))
                Directory.CreateDirectory(ImagesDirectory);

            string safeName = string.IsNullOrWhiteSpace(visitorName) ? "pledge" : visitorName;
            foreach (char c in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(c, '_');

            string stamp = string.IsNullOrEmpty(timestamp) ? DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") : timestamp.Replace(':', '-');
            File.WriteAllBytes(Path.Combine(ImagesDirectory, stamp + "_" + safeName + ".png"), imageBytes);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Could not write pledge PNG: {ex.Message}");
        }
    }
}
