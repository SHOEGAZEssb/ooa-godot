using Godot;
using System;
using System.IO;
using System.Security;

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
        if (File.Exists(absolutePath) || File.Exists(absolutePath + ".bak"))
            throw new InvalidDataException($"Neither save copy is valid: {absolutePath}");
        return OracleSaveData.CreateStandardGame();
    }

    public static SaveResult Save(OracleSaveData save, string path = DefaultPath)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        string temporaryPath = absolutePath + ".tmp";
        string backupPath = absolutePath + ".bak";
        string backupTemporaryPath = backupPath + ".tmp";
        try
        {
            string? directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            byte[] serialized = save.Serialize();
            if (!OracleSaveData.TryDeserialize(serialized, out _))
                return SaveResult.Failed("The serialized save failed its signature or checksum validation.");

            WriteDurably(temporaryPath, serialized);
            if (!TryLoad(temporaryPath, out _))
                return SaveResult.Failed("The temporary save could not be read back and validated.");

            // fileManagement.saveFile copies the same image to both slots,
            // including the first save. There is no previous-generation history.
            File.Move(temporaryPath, absolutePath, overwrite: true);
            WriteDurably(backupTemporaryPath, serialized);
            if (!TryLoad(backupTemporaryPath, out _))
                return SaveResult.Failed("The temporary backup save could not be read back and validated.");
            File.Move(backupTemporaryPath, backupPath, overwrite: true);
            return SaveResult.Succeeded;
        }
        catch (Exception exception) when (IsSaveException(exception))
        {
            return SaveResult.Failed(exception.Message);
        }
        finally
        {
            DeleteIfPresent(temporaryPath);
            DeleteIfPresent(backupTemporaryPath);
        }
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

    public static SaveResult SaveSlot(int slot, OracleSaveData save) =>
        Save(save, PathForSlot(slot));

    public static void EraseSlot(int slot)
    {
        string absolutePath = ProjectSettings.GlobalizePath(PathForSlot(slot));
        DeleteIfPresent(absolutePath);
        DeleteIfPresent(absolutePath + ".bak");
        DeleteIfPresent(absolutePath + ".tmp");
        DeleteIfPresent(absolutePath + ".bak.tmp");
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

    private static void WriteDurably(string path, ReadOnlySpan<byte> data)
    {
        using var stream = new FileStream(
            path, FileMode.Create, System.IO.FileAccess.Write, FileShare.None);
        stream.Write(data);
        stream.Flush(flushToDisk: true);
    }

    private static bool IsSaveException(Exception exception) => exception is
        IOException or UnauthorizedAccessException or NotSupportedException or SecurityException;

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
        catch (SecurityException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    private static void ValidateSlot(int slot)
    {
        if (slot is < 0 or >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}

public readonly record struct SaveResult(bool Success, string ErrorMessage)
{
    public static readonly SaveResult Succeeded = new(true, string.Empty);
    public static SaveResult Failed(string message) => new(false, string.IsNullOrWhiteSpace(message) ? "Unknown save error." : message);
}
