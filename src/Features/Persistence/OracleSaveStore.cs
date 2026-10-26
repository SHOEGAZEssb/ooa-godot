using Godot;
using System;
using System.IO;
using System.Security;

namespace Oracles;

public static class OracleSaveStore
{
    public const int SlotCount = 3;
    internal static OracleSaveData LoadOrCreate(string path, OracleGame game)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (TryLoad(absolutePath, out OracleSaveData? save, game))
            return save!;
        if (TryLoad(absolutePath + ".bak", out save, game))
            return save!;
        if (File.Exists(absolutePath) || File.Exists(absolutePath + ".bak"))
            throw new InvalidDataException($"Neither save copy is valid: {absolutePath}");
        return OracleSaveData.CreateStandardGame(game);
    }

    public static SaveResult Save(OracleSaveData save, string path)
    {
        if (save.Game == OracleGame.Seasons && Array.Exists(new[] { 0, 1, 2 }, slot =>
            string.Equals(ProjectSettings.GlobalizePath(path), ProjectSettings.GlobalizePath(AgesGameContent.SaveSlotPath(slot)), StringComparison.OrdinalIgnoreCase)))
            return SaveResult.Failed("Seasons requires its own save path.");
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
            if (!OracleSaveData.TryDeserialize(serialized, save.Game, out _))
                return SaveResult.Failed("The serialized save failed its signature or checksum validation.");

            WriteDurably(temporaryPath, serialized);
            if (!TryLoad(temporaryPath, out _, save.Game))
                return SaveResult.Failed("The temporary save could not be read back and validated.");

            // fileManagement.saveFile copies the same image to both slots,
            // including the first save. There is no previous-generation history.
            File.Move(temporaryPath, absolutePath, overwrite: true);
            WriteDurably(backupTemporaryPath, serialized);
            if (!TryLoad(backupTemporaryPath, out _, save.Game))
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

    internal static OracleSaveData? LoadSlot(int slot, OracleGame game)
    {
        string absolutePath = ProjectSettings.GlobalizePath(PathForSlot(slot, game));
        if (TryLoad(absolutePath, out OracleSaveData? save, game))
            return save;
        if (TryLoad(absolutePath + ".bak", out save, game))
            return save;
        return null;
    }

    public static SaveResult SaveSlot(int slot, OracleSaveData save) =>
        Save(save, PathForSlot(slot, save.Game));

    internal static void EraseSlot(int slot, OracleGame game)
    {
        string absolutePath = ProjectSettings.GlobalizePath(PathForSlot(slot, game));
        DeleteIfPresent(absolutePath);
        DeleteIfPresent(absolutePath + ".bak");
        DeleteIfPresent(absolutePath + ".tmp");
        DeleteIfPresent(absolutePath + ".bak.tmp");
    }

    internal static string PathForSlot(int slot, OracleGame game)
    {
        ValidateSlot(slot);
        return game switch
        {
            OracleGame.Ages => AgesGameContent.SaveSlotPath(slot),
            OracleGame.Seasons => SeasonsGameContent.SaveSlotPath(slot),
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };
    }

    private static bool TryLoad(string path, out OracleSaveData? save, OracleGame game)
    {
        save = null;
        try
        {
            return File.Exists(path) && OracleSaveData.TryDeserialize(File.ReadAllBytes(path), game, out save);
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
