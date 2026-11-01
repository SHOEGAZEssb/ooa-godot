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

    public static SaveResult Save(OracleSaveData save, string path) => Save(save, path, writeBackup: true);

    internal static SaveResult ReadImport(string path, OracleGame game, out OracleSaveData? save)
    {
        save = null;
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length != OracleSaveData.FileSize)
                return SaveResult.Failed("Choose a single-slot port save (.sav, 0x550 bytes).");
            byte[] bytes = new byte[OracleSaveData.FileSize];
            stream.ReadExactly(bytes);
            return OracleSaveData.TryDeserialize(bytes, game, out save)
                ? SaveResult.Succeeded
                : SaveResult.Failed($"Invalid {game} save: game signature or checksum does not match.");
        }
        catch (Exception exception) when (IsSaveException(exception) || exception is ArgumentException)
        {
            return SaveResult.Failed(exception.Message);
        }
    }

    internal static SaveResult Export(OracleSaveData save, string path)
    {
        try
        {
            string absolutePath = Path.GetFullPath(ProjectSettings.GlobalizePath(path));
            foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
            for (int slot = 0; slot < SlotCount; slot++)
            {
                string slotPath = Path.GetFullPath(ProjectSettings.GlobalizePath(PathForSlot(slot, game)));
                if (Array.Exists(new[] { slotPath, slotPath + ".bak", slotPath + ".tmp", slotPath + ".bak.tmp" },
                    managed => string.Equals(absolutePath, managed, StringComparison.OrdinalIgnoreCase)))
                    return SaveResult.Failed("Export to a separate file outside the game's save slots.");
            }
            return Save(save, absolutePath, writeBackup: false);
        }
        catch (Exception exception) when (IsSaveException(exception) || exception is ArgumentException)
        {
            return SaveResult.Failed(exception.Message);
        }
    }

    private static SaveResult Save(OracleSaveData save, string path, bool writeBackup)
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
            if (!writeBackup) return SaveResult.Succeeded;
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
            if (writeBackup) DeleteIfPresent(backupTemporaryPath);
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
