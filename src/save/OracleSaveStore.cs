using Godot;
using System;
using System.IO;

namespace oracleofages;

public static class OracleSaveStore
{
    public const int SlotCount = 3;
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

    public static OracleSaveData? LoadSlot(int slot)
    {
        string absolutePath = ProjectSettings.GlobalizePath(PathForSlot(slot));
        if (TryLoad(absolutePath, out OracleSaveData? save))
            return save;
        if (TryLoad(absolutePath + ".bak", out save))
            return save;
        return null;
    }

    public static void SaveSlot(int slot, OracleSaveData save) =>
        Save(save, PathForSlot(slot));

    public static void EraseSlot(int slot)
    {
        string absolutePath = ProjectSettings.GlobalizePath(PathForSlot(slot));
        DeleteIfPresent(absolutePath);
        DeleteIfPresent(absolutePath + ".bak");
        DeleteIfPresent(absolutePath + ".tmp");
    }

    public static string PathForSlot(int slot)
    {
        ValidateSlot(slot);
        return slot == 0 ? DefaultPath : $"user://oracle_of_ages_{slot + 1}.sav";
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

    private static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A locked file remains visible in the menu and can be retried.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
