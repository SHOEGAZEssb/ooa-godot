using Godot;
using System;
using System.IO;

namespace oracleofages;

public static class OracleSaveStore
{
    public const string DefaultPath = "user://oracle_of_ages.sav";

    public static OracleSaveData LoadOrCreate(string path = DefaultPath)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (TryLoad(absolutePath, out OracleSaveData? save))
            return save!;
        if (TryLoad(absolutePath + ".bak", out save))
            return save!;
        return OracleSaveData.CreateStandardGame();
    }

    public static void Save(OracleSaveData save, string path = DefaultPath)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        string? directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = absolutePath + ".tmp";
        File.WriteAllBytes(temporaryPath, save.Serialize());
        File.Move(temporaryPath, absolutePath, overwrite: true);
        File.Copy(absolutePath, absolutePath + ".bak", overwrite: true);
    }

    private static bool TryLoad(string path, out OracleSaveData? save)
    {
        save = null;
        try
        {
            return File.Exists(path) && OracleSaveData.TryDeserialize(File.ReadAllBytes(path), out save);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
