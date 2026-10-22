using System;
using System.IO;
using UnityEngine;

public class PledgeStorageManager : MonoBehaviour
{
    public static PledgeStorageManager Instance { get; private set; }

    public static string SavePath => Path.Combine(Application.persistentDataPath, "pledges.json");

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

    public void SavePledge(string visitorName, string employeeId, byte[] imageBytes)
    {
        PledgeDatabase database = LoadPledges();
        var entry = new PledgeEntry
        {
            visitorName = visitorName ?? string.Empty,
            employeeId = employeeId ?? string.Empty,
            pledgeText = string.Empty,
            imageBase64 = imageBytes != null && imageBytes.Length > 0
                ? Convert.ToBase64String(imageBytes)
                : string.Empty,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        database.pledges.Add(entry);
        WriteDatabase(database);
        Debug.Log($"Saved pledge to {SavePath} (count: {database.pledges.Count}).");
    }

    public PledgeDatabase LoadPledges()
    {
        try
        {
            if (!File.Exists(SavePath))
                return new PledgeDatabase();

            string json = File.ReadAllText(SavePath);
            if (string.IsNullOrWhiteSpace(json))
                return new PledgeDatabase();

            var database = JsonUtility.FromJson<PledgeDatabase>(json);
            if (database == null)
                return new PledgeDatabase();
            if (database.pledges == null)
                database.pledges = new System.Collections.Generic.List<PledgeEntry>();
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
}
