using System;
using System.IO;
using UnityEngine;

[Serializable]
public class WallConfig
{
    public ushort port = NetworkSession.DefaultPort;
    public bool showHostStatus = false;
    // Informational only: rewritten on every run. Copy this into drawing_config.json.
    public string detectedHostAddress = "";
}

[Serializable]
public class DrawingConfig
{
    public string hostAddress = "127.0.0.1";
    public ushort port = NetworkSession.DefaultPort;
    public bool autoConnect = true;
    public float retryIntervalSeconds = 5f;
}

/// <summary>
/// JSON config files stored next to the built .exe (or in the project root when run in the Editor).
/// A missing file is created with defaults the first time it is requested.
/// </summary>
public static class AppConfig
{
    public const string WallFileName = "wall_config.json";
    public const string DrawingFileName = "drawing_config.json";

    public static string Directory => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    public static WallConfig LoadWall() => LoadOrCreate<WallConfig>(WallFileName);
    public static DrawingConfig LoadDrawing() => LoadOrCreate<DrawingConfig>(DrawingFileName);
    public static void SaveWall(WallConfig config) => Save(WallFileName, config);

    private static T LoadOrCreate<T>(string fileName) where T : new()
    {
        string path = Path.Combine(Directory, fileName);
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (loaded != null)
                    return loaded;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not read {path}, using defaults: {e.Message}");
        }

        var config = new T();
        Save(fileName, config);
        return config;
    }

    private static void Save(string fileName, object config)
    {
        string path = Path.Combine(Directory, fileName);
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(config, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not write {path}: {e.Message}");
        }
    }
}
