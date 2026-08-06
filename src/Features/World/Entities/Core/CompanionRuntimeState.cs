using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Owns the live w1Companion slot and the remembered-companion room fields.
/// The slot is shared by animal companions and the minecart, exactly as in
/// the original object dispatcher; only one owner may be active at a time.
/// </summary>
internal static class CompanionRuntimeState
{
    internal const int MinecartId = 0x0a;
    internal const int RickyId = 0x0b;
    internal const int MooshId = 0x0d;
    internal const int RaftId = 0x13;

    private const int Active = 0xd100;
    private const int Id = 0xd101;
    private const int Room = 0xd102;
    private const int Direction = 0xd103;
    private const int Y = 0xd104;
    private const int X = 0xd105;

    private const int RememberedId = 0xcc24;
    private const int RememberedGroup = 0xcc25;
    private const int RememberedRoom = 0xcc26;
    private const int RememberedY = 0xcc27;
    private const int RememberedX = 0xcc28;

    // wLastAnimalMountPointY/X are live WRAM state, shared by every animal
    // companion. companionFinalizeMounting and finishScrollingTransition
    // overwrite them; companionRespawn reads them without a room-id guard.
    private const int LastAnimalMountY = 0xc638;
    private const int LastAnimalMountX = 0xc639;

    internal static bool IsActive(OracleRuntimeState state, int id) =>
        state.ReadWramByte(Active) != 0 &&
        state.ReadWramByte(Id) == id;

    internal static bool AnyActive(OracleRuntimeState state) =>
        state.ReadWramByte(Active) != 0;

    internal static ActiveCompanion Read(OracleRuntimeState state) => new(
        state.ReadWramByte(Id),
        state.ReadWramByte(Room),
        state.ReadWramByte(Direction),
        state.ReadWramByte(Y),
        state.ReadWramByte(X));

    internal static void Begin(
        OracleRuntimeState state,
        int id,
        int room,
        Vector2 position,
        int direction)
    {
        state.SetWramByte(Active, 1);
        state.SetWramByte(Id, checked((byte)id));
        Update(state, id, room, position, direction);
        if (id is RickyId or MooshId)
            SetLastAnimalMountPosition(state, position);
    }

    internal static void Update(
        OracleRuntimeState state,
        int id,
        int room,
        Vector2 position,
        int direction)
    {
        if (!IsActive(state, id))
            throw new InvalidOperationException(
                $"SPECIALOBJECT ${id:x2} does not own w1Companion.");
        state.SetWramByte(Room, checked((byte)room));
        state.SetWramByte(Direction, checked((byte)direction));
        state.SetWramByte(Y, (byte)Mathf.FloorToInt(position.Y));
        state.SetWramByte(X, (byte)Mathf.FloorToInt(position.X));
    }

    internal static void Clear(OracleRuntimeState state, int id)
    {
        if (!IsActive(state, id))
            return;
        for (int address = Active; address <= X; address++)
            state.SetWramByte(address, 0);
    }

    internal static Vector2 ReadLastAnimalMountPosition(
        OracleRuntimeState state) => new(
            state.ReadWramByte(LastAnimalMountX),
            state.ReadWramByte(LastAnimalMountY));

    internal static void SetLastAnimalMountPosition(
        OracleRuntimeState state,
        Vector2 position)
    {
        state.SetWramByte(
            LastAnimalMountY,
            (byte)Mathf.FloorToInt(position.Y));
        state.SetWramByte(
            LastAnimalMountX,
            (byte)Mathf.FloorToInt(position.X));
    }

    internal static void Remember(
        OracleRuntimeState state,
        int id,
        int group,
        int room,
        Vector2 position)
    {
        state.SetWramByte(RememberedId, checked((byte)id));
        state.SetWramByte(RememberedGroup, checked((byte)group));
        state.SetWramByte(RememberedRoom, checked((byte)room));
        state.SetWramByte(RememberedY, (byte)Mathf.FloorToInt(position.Y));
        state.SetWramByte(RememberedX, (byte)Mathf.FloorToInt(position.X));
    }

    /// <summary>
    /// Clears wRememberedCompanionId without disturbing the remembered room
    /// and position bytes, matching scripts which write only $cc24.
    /// </summary>
    internal static void ForgetRemembered(OracleRuntimeState state) =>
        state.SetWramByte(RememberedId, 0);

    internal static bool TryGetRemembered(
        OracleRuntimeState state,
        int id,
        int group,
        int room,
        out Vector2 position)
    {
        if (state.ReadWramByte(RememberedId) == id &&
            state.ReadWramByte(RememberedGroup) == group &&
            state.ReadWramByte(RememberedRoom) == room)
        {
            position = new Vector2(
                state.ReadWramByte(RememberedX),
                state.ReadWramByte(RememberedY));
            return true;
        }
        position = default;
        return false;
    }

    internal static RememberedCompanion ReadRemembered(
        OracleRuntimeState state) => new(
            state.ReadWramByte(RememberedId),
            state.ReadWramByte(RememberedGroup),
            state.ReadWramByte(RememberedRoom),
            state.ReadWramByte(RememberedY),
            state.ReadWramByte(RememberedX));

    internal static void RestoreRememberedFromDeathRespawn(
        OracleRuntimeState state,
        OracleSaveData saveData)
    {
        state.SetWramByte(
            RememberedId,
            saveData.ReadWramByte(OracleSaveData.RespawnRememberedCompanionIdAddress));
        state.SetWramByte(
            RememberedGroup,
            saveData.ReadWramByte(OracleSaveData.RespawnRememberedCompanionGroupAddress));
        state.SetWramByte(
            RememberedRoom,
            saveData.ReadWramByte(OracleSaveData.RespawnRememberedCompanionRoomAddress));
        state.SetWramByte(
            RememberedY,
            saveData.ReadWramByte(OracleSaveData.RespawnRememberedCompanionYAddress));
        state.SetWramByte(
            RememberedX,
            saveData.ReadWramByte(OracleSaveData.RespawnRememberedCompanionXAddress));
    }
}

internal readonly record struct ActiveCompanion(
    int Id,
    int Room,
    int Direction,
    int Y,
    int X)
{
    internal Vector2 Position => new(X, Y);
}

internal readonly record struct RememberedCompanion(
    int Id,
    int Group,
    int Room,
    int Y,
    int X);
