using Godot;
using System;
using System.IO;
using System.Security.Cryptography;

namespace oracleofages;

/// <summary>
/// Clone-side debug snapshot. This is deliberately separate from the original
/// SRAM image and includes the reconstructible live state needed to rebuild a
/// stable gameplay frame.
/// </summary>
internal sealed class DebugSavestateData
{
    private const int FormatVersion = 1;
    private const int HashSize = 32;
    private static readonly byte[] Magic = "OOASTATE"u8.ToArray();

    private readonly byte[] _saveImage;
    private readonly OracleRuntimeStateSnapshot _runtimeState;
    private readonly OracleRandomState _randomState;
    private readonly RoomEntityManagerState _entityManagerState;

    internal int Group { get; }
    internal int Room { get; }
    internal Vector2 PlayerPosition { get; }
    internal Vector2I PlayerFacing { get; }
    internal double AnimationTicks { get; }

    private DebugSavestateData(
        int group,
        int room,
        Vector2 playerPosition,
        Vector2I playerFacing,
        double animationTicks,
        byte[] saveImage,
        OracleRuntimeStateSnapshot runtimeState,
        OracleRandomState randomState,
        RoomEntityManagerState entityManagerState)
    {
        if (group is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(group));
        if (room is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(room));
        if (!float.IsFinite(playerPosition.X) || !float.IsFinite(playerPosition.Y))
            throw new ArgumentOutOfRangeException(nameof(playerPosition));
        _ = FacingCode(playerFacing);
        if (!double.IsFinite(animationTicks) || animationTicks < 0.0)
            throw new ArgumentOutOfRangeException(nameof(animationTicks));
        if (!OracleSaveData.TryDeserialize(saveImage, out _))
            throw new ArgumentException(
                "The debug state does not contain a valid Ages save image.",
                nameof(saveImage));
        ArgumentNullException.ThrowIfNull(runtimeState.Wram);
        ArgumentNullException.ThrowIfNull(runtimeState.SeedTreeRefillRooms);
        if (runtimeState.Wram.Length !=
                OracleRuntimeState.WramEnd - OracleRuntimeState.WramStart + 1 ||
            runtimeState.SeedTreeRefillRooms.Length !=
                OracleRuntimeState.SeedTreeRefillLocationCount *
                OracleRuntimeState.SeedTreeRefillRoomsPerLocation)
        {
            throw new ArgumentException(
                "The debug state does not contain the complete live WRAM state.",
                nameof(runtimeState));
        }
        ArgumentNullException.ThrowIfNull(randomState.PlacementBuffer);
        if (randomState.PlacementBuffer.Length != 256 || randomState.Calls < 0)
        {
            throw new ArgumentException(
                "The debug state does not contain a valid shared RNG state.",
                nameof(randomState));
        }
        ArgumentNullException.ThrowIfNull(
            entityManagerState.RecentEnemyDefeats.Rooms);
        ArgumentNullException.ThrowIfNull(
            entityManagerState.RecentEnemyDefeats.KilledEnemies);
        if (!double.IsFinite(entityManagerState.FrameAccumulator) ||
            entityManagerState.FrameAccumulator is < 0.0 or >= 1.0 ||
            entityManagerState.FrameCounter is < 0 or > 0xff ||
            entityManagerState.RecentEnemyDefeats.Rooms.Length != 8 ||
            entityManagerState.RecentEnemyDefeats.KilledEnemies.Length != 8 ||
            entityManagerState.RecentEnemyDefeats.Tail is < 0 or >= 8 ||
            entityManagerState.RecentEnemyDefeats.ActiveRoom is < -1 or > 0xff)
        {
            throw new ArgumentException(
                "The debug state does not contain valid room runtime state.",
                nameof(entityManagerState));
        }

        Group = group;
        Room = room;
        PlayerPosition = playerPosition;
        PlayerFacing = playerFacing;
        AnimationTicks = animationTicks;
        _saveImage = (byte[])saveImage.Clone();
        _runtimeState = new OracleRuntimeStateSnapshot(
            (byte[])runtimeState.Wram.Clone(),
            (byte[])runtimeState.SeedTreeRefillRooms.Clone());
        _randomState = randomState with
        {
            PlacementBuffer = (byte[])randomState.PlacementBuffer.Clone()
        };
        _entityManagerState = entityManagerState with
        {
            RecentEnemyDefeats = entityManagerState.RecentEnemyDefeats with
            {
                Rooms = (byte[])entityManagerState.RecentEnemyDefeats.Rooms.Clone(),
                KilledEnemies =
                    (byte[])entityManagerState.RecentEnemyDefeats.KilledEnemies.Clone()
            }
        };
    }

    internal static DebugSavestateData Capture(
        RoomSession rooms,
        Player player,
        OracleSaveData saveData,
        OracleRuntimeState runtimeState,
        OracleRandom random,
        RoomEntityManager entities,
        double animationTicks)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(saveData);
        ArgumentNullException.ThrowIfNull(runtimeState);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(entities);
        return new DebugSavestateData(
            rooms.ActiveGroup,
            rooms.CurrentRoom.Id,
            player.PrecisePosition,
            player.FacingVector,
            animationTicks,
            saveData.Serialize(),
            runtimeState.CaptureState(),
            random.CaptureState(),
            entities.CaptureDebugState());
    }

    internal OracleSaveData CreateSaveData()
    {
        if (!OracleSaveData.TryDeserialize(_saveImage, out OracleSaveData? save))
            throw new InvalidOperationException(
                "A validated debug savestate lost its Ages save image.");
        return save!;
    }

    internal void RestoreRoomParseState(RoomEntityManager entities) =>
        entities.RestoreDebugStateBeforeRoomParse(_entityManagerState);

    internal void RestoreLiveState(
        OracleSaveData saveData,
        OracleRuntimeState runtimeState,
        OracleRandom random,
        RoomEntityManager entities)
    {
        saveData.RestoreFrom(CreateSaveData());
        runtimeState.RestoreState(_runtimeState);
        random.RestoreState(_randomState);
        entities.RestoreDebugStateAfterRoomParse(_entityManagerState);
    }

    internal byte[] Serialize()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(Group);
            writer.Write(Room);
            writer.Write(PlayerPosition.X);
            writer.Write(PlayerPosition.Y);
            writer.Write(FacingCode(PlayerFacing));
            writer.Write(AnimationTicks);
            writer.Write(_entityManagerState.ActiveTriggers);
            writer.Write(_entityManagerState.FrameAccumulator);
            writer.Write(_entityManagerState.FrameCounter);
            WriteBytes(
                writer,
                _entityManagerState.RecentEnemyDefeats.Rooms);
            WriteBytes(
                writer,
                _entityManagerState.RecentEnemyDefeats.KilledEnemies);
            writer.Write(_entityManagerState.RecentEnemyDefeats.Tail);
            writer.Write(_entityManagerState.RecentEnemyDefeats.ActiveRoom);
            WriteBytes(writer, _saveImage);
            WriteBytes(writer, _runtimeState.Wram);
            WriteBytes(writer, _runtimeState.SeedTreeRefillRooms);
            writer.Write(_randomState.Rng1);
            writer.Write(_randomState.Rng2);
            WriteBytes(writer, _randomState.PlacementBuffer);
            writer.Write(_randomState.PlacementIndex);
            writer.Write(_randomState.PlacementBufferReady ? (byte)1 : (byte)0);
            writer.Write(_randomState.Calls);
            writer.Write(_randomState.LastResult.Value);
            writer.Write(_randomState.LastResult.High);
            writer.Write(_randomState.LastResult.Low);
        }

        byte[] payload = stream.ToArray();
        byte[] output = new byte[payload.Length + HashSize];
        payload.CopyTo(output, 0);
        SHA256.HashData(payload).CopyTo(output, payload.Length);
        return output;
    }

    internal static bool TryDeserialize(
        ReadOnlySpan<byte> source,
        out DebugSavestateData? state)
    {
        state = null;
        if (source.Length <= Magic.Length + sizeof(int) + HashSize)
            return false;

        ReadOnlySpan<byte> payload = source[..^HashSize];
        ReadOnlySpan<byte> storedHash = source[^HashSize..];
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(payload), storedHash))
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(payload.ToArray(), writable: false);
            using var reader = new BinaryReader(stream);
            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic) ||
                reader.ReadInt32() != FormatVersion)
            {
                return false;
            }

            int group = reader.ReadInt32();
            int room = reader.ReadInt32();
            var position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Vector2I facing = FacingVector(reader.ReadInt32());
            double animationTicks = reader.ReadDouble();
            byte activeTriggers = reader.ReadByte();
            double entityFrameAccumulator = reader.ReadDouble();
            int entityFrameCounter = reader.ReadInt32();
            byte[] recentRooms = ReadBytes(reader, 8);
            byte[] killedEnemies = ReadBytes(reader, 8);
            int recentTail = reader.ReadInt32();
            int recentActiveRoom = reader.ReadInt32();
            byte[] saveImage = ReadBytes(reader, OracleSaveData.FileSize);
            byte[] wram = ReadBytes(
                reader,
                OracleRuntimeState.WramEnd - OracleRuntimeState.WramStart + 1);
            byte[] seedTreeRooms = ReadBytes(
                reader,
                OracleRuntimeState.SeedTreeRefillLocationCount *
                OracleRuntimeState.SeedTreeRefillRoomsPerLocation);
            byte rng1 = reader.ReadByte();
            byte rng2 = reader.ReadByte();
            byte[] placementBuffer = ReadBytes(reader, 256);
            byte placementIndex = reader.ReadByte();
            byte placementReady = reader.ReadByte();
            if (placementReady > 1)
                return false;
            int calls = reader.ReadInt32();
            var lastResult = new OracleRandomResult(
                reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            if (stream.Position != stream.Length)
                return false;

            state = new DebugSavestateData(
                group,
                room,
                position,
                facing,
                animationTicks,
                saveImage,
                new OracleRuntimeStateSnapshot(wram, seedTreeRooms),
                new OracleRandomState(
                    rng1,
                    rng2,
                    placementBuffer,
                    placementIndex,
                    placementReady != 0,
                    calls,
                    lastResult),
                new RoomEntityManagerState(
                    activeTriggers,
                    entityFrameAccumulator,
                    entityFrameCounter,
                    new RecentEnemyDefeatsState(
                        recentRooms,
                        killedEnemies,
                        recentTail,
                        recentActiveRoom)));
            return true;
        }
        catch (Exception exception) when (exception is
            EndOfStreamException or
            IOException or
            ArgumentException or
            OverflowException)
        {
            return false;
        }
    }

    private static void WriteBytes(BinaryWriter writer, byte[] bytes)
    {
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static byte[] ReadBytes(BinaryReader reader, int expectedLength)
    {
        if (reader.ReadInt32() != expectedLength)
            throw new InvalidDataException(
                $"Expected a {expectedLength}-byte debug-state field.");
        byte[] bytes = reader.ReadBytes(expectedLength);
        if (bytes.Length != expectedLength)
            throw new EndOfStreamException();
        return bytes;
    }

    private static int FacingCode(Vector2I facing) => facing switch
    {
        { X: 0, Y: -1 } => 0,
        { X: 1, Y: 0 } => 1,
        { X: 0, Y: 1 } => 2,
        { X: -1, Y: 0 } => 3,
        _ => throw new ArgumentOutOfRangeException(
            nameof(facing), "Link must face one of the four cardinal directions.")
    };

    private static Vector2I FacingVector(int facing) => facing switch
    {
        0 => Vector2I.Up,
        1 => Vector2I.Right,
        2 => Vector2I.Down,
        3 => Vector2I.Left,
        _ => throw new InvalidDataException(
            "The debug state contains an invalid Link facing direction.")
    };
}
