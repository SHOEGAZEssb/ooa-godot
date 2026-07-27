using Godot;
using System;
using System.IO;
using System.Security;

namespace oracleofages;

internal static class DebugSavestateStore
{
    internal const int SlotCount = 10;

    internal static SaveResult SaveSlot(int slot, DebugSavestateData state) =>
        Save(state, PathForSlot(slot));

    internal static DebugSavestateLoadResult LoadSlot(int slot) =>
        Load(PathForSlot(slot));

    internal static SaveResult Save(DebugSavestateData state, string path)
    {
        ArgumentNullException.ThrowIfNull(state);
        string absolutePath = ProjectSettings.GlobalizePath(path);
        string temporaryPath = absolutePath + ".tmp";
        try
        {
            string? directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            byte[] serialized = state.Serialize();
            if (!DebugSavestateData.TryDeserialize(serialized, out _))
            {
                return SaveResult.Failed(
                    "The serialized debug state failed validation.");
            }

            using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                System.IO.FileAccess.Write,
                FileShare.None))
            {
                stream.Write(serialized);
                stream.Flush(flushToDisk: true);
            }

            byte[] staged = File.ReadAllBytes(temporaryPath);
            if (!DebugSavestateData.TryDeserialize(staged, out _))
            {
                return SaveResult.Failed(
                    "The temporary debug state could not be read back.");
            }

            File.Move(temporaryPath, absolutePath, overwrite: true);
            return SaveResult.Succeeded;
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            return SaveResult.Failed(exception.Message);
        }
        finally
        {
            DeleteTemporaryFile(temporaryPath);
        }
    }

    internal static DebugSavestateLoadResult Load(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
            return DebugSavestateLoadResult.NotFound;

        try
        {
            return DebugSavestateData.TryDeserialize(
                File.ReadAllBytes(absolutePath),
                out DebugSavestateData? state)
                ? DebugSavestateLoadResult.Loaded(state!)
                : DebugSavestateLoadResult.Failed(
                    "The debug state is corrupt or uses an unsupported format.");
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
            return DebugSavestateLoadResult.Failed(exception.Message);
        }
    }

    internal static string PathForSlot(int slot)
    {
        if (slot is < 0 or >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return $"user://oracle_of_ages_debug_state_{slot}.state";
    }

    private static bool IsStorageException(Exception exception) => exception is
        IOException or
        UnauthorizedAccessException or
        NotSupportedException or
        SecurityException;

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (IsStorageException(exception))
        {
        }
    }
}

internal readonly record struct DebugSavestateLoadResult(
    bool Found,
    DebugSavestateData? State,
    string ErrorMessage)
{
    internal bool Success => Found && State is not null;
    internal static readonly DebugSavestateLoadResult NotFound =
        new(false, null, string.Empty);
    internal static DebugSavestateLoadResult Loaded(DebugSavestateData state) =>
        new(true, state, string.Empty);
    internal static DebugSavestateLoadResult Failed(string message) =>
        new(true, null, message);
}
