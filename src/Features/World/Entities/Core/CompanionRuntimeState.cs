
namespace Oracles;

/// <summary>
/// Shared access to w1Companion motion and remembered-companion fields.
/// Native handlers own the slot's state bytes; room identity comes from the
/// room/entity owners rather than private metadata over the object union.
/// </summary>
internal static class CompanionRuntimeState
{
    internal const int MinecartId = 0x0a;
    internal const int RickyId = 0x0b;
    internal const int DimitriId = 0x0c;
    internal const int MooshId = 0x0d;
    internal const int RaftId = 0x13;

    private const int Active = 0xd100;
    private const int Id = 0xd101;
    // include/structs.s: SpecialObject motion; $02-$05 are subid/var03/state/substate.
    private const int Direction = 0xd108;
    private const int Y = 0xd10a;
    private const int X = 0xd10c;


    // Ages wDisallowMountingCompanion. The interaction owns its countdown.
    internal static bool MountingDisabled(OracleRuntimeState state) => state.ReadField("wDisallowMountingCompanion") != 0;
    internal static void SetMountingLock(OracleRuntimeState state, int frames) => state.SetField("wDisallowMountingCompanion", checked((byte)frames));

    internal static bool IsActive(OracleRuntimeState state, int id) =>
        state.ReadWramByte(Active) != 0 &&
        state.ReadWramByte(Id) == id;

    internal static bool AnyActive(OracleRuntimeState state) =>
        state.ReadWramByte(Active) != 0;
    internal static int ReadCutsceneId(OracleRuntimeState state)=>state.ReadWramByte(Id);
    internal static int ReadCutsceneSubid(OracleRuntimeState state)=>state.ReadWramByte(0xd102);
    internal static void BeginCutscene(OracleRuntimeState state,int id,int subid,Vector2 position)
    {
        if(id is <0x0f or >0x12)throw new ArgumentOutOfRangeException(nameof(id));
        state.SetWramByte(Active,1);state.SetWramByte(Id,(byte)id);state.SetWramByte(0xd102,(byte)subid);
        state.SetWramByte(0xd10b,(byte)position.Y);state.SetWramByte(0xd10d,(byte)position.X);
    }
    internal static void ReplaceCutscene(OracleRuntimeState state,int id,int subid,Vector2 position)
    {
        for(int address=Id;address<0xd140;address++)state.SetWramByte(address,0);
        // Native replacement retains enabled, rather than allocating again.
        state.SetWramByte(Id,(byte)id);state.SetWramByte(0xd102,(byte)subid);
        state.SetWramByte(0xd10b,(byte)position.Y);state.SetWramByte(0xd10d,(byte)position.X);
    }
    internal static int ReadScriptSignals(OracleRuntimeState state)=>state.ReadWramByte(0xd13e);
    internal static void OrScriptSignals(OracleRuntimeState state,int mask)=>state.SetWramByte(0xd13e,(byte)(ReadScriptSignals(state)|mask));
    internal static int ReadScriptButton(OracleRuntimeState state)=>state.ReadWramByte(0xd13d);
    internal static void WriteScriptButton(OracleRuntimeState state,int value)=>state.SetWramByte(0xd13d,checked((byte)value));
    internal static void ClearScriptScratch(OracleRuntimeState state)
    {state.SetWramByte(0xd13d,0);state.SetWramByte(0xd13e,0);}

    internal static ActiveCompanion Read(OracleRuntimeState state) => new(
        state.ReadWramByte(Id),
        state.ReadWramByte(Direction),
        ReadPositionWord(state, Y),
        ReadPositionWord(state, X));

    internal static void BeginIdle(OracleRuntimeState state, int id, Vector2 position)
    {
        // loadRememberedCompanion/companionSpawner write only the header and highs.
        state.SetWramByte(Active, 1);
        state.SetWramByte(Id, checked((byte)id));
        state.SetWramByte(Y + 1, OracleObjectPosition.HighByte(position.Y));
        state.SetWramByte(X + 1, OracleObjectPosition.HighByte(position.X));
    }

    internal static void Begin(
        OracleRuntimeState state,
        int id,
        Vector2 position,
        int direction)
    {
        // Retained cart/raft/carry/mount owners survive setObjectsEnabledTo2.
        state.SetWramByte(Active, 3);
        state.SetWramByte(Id, checked((byte)id));
        Update(state, id, position, direction);
    }

    internal static void BeginAnimalRide(
        OracleRuntimeState state,
        OracleSaveData saveData,
        int id,
        Vector2 position,
        int direction)
    {
        RequireSameGame(state, saveData);
        if (id is not (RickyId or DimitriId or MooshId))
            throw new ArgumentOutOfRangeException(nameof(id), id,
                $"SPECIALOBJECT ${id:x2} cannot finalize animal mounting.");
        Begin(state, id, position, direction);
        // commonCode.s:companionFinalizeMounting changes state, not just retention.
        state.SetWramByte(0xd104, 5);
        state.SetWramByte(0xd109, 0xff);
        state.SetWramByte(0xd13c, 0xff);
        state.SetWramByte(0xd11a, (byte)((state.ReadWramByte(0xd11a) & 0xc0) | 1));
        state.SetWramByte(0xd13d, 0);
        state.SetField("wDisableScreenTransitions", 0);
        SetLastAnimalMountPosition(saveData, position);
        state.SetField("wLinkObjectIndex", 0xd1);
    }

    internal static void Update(
        OracleRuntimeState state,
        int id,
        Vector2 position,
        int direction)
    {
        if (!IsActive(state, id))
            throw new InvalidOperationException(
                $"SPECIALOBJECT ${id:x2} does not own w1Companion.");
        state.SetWramByte(Direction, checked((byte)direction));
        WritePositionWord(state, Y, position.Y);
        WritePositionWord(state, X, position.X);
    }

    internal static void UpdatePosition(OracleRuntimeState state, int id, Vector2 position)
    {
        if (!IsActive(state, id))
            throw new InvalidOperationException($"SPECIALOBJECT ${id:x2} does not own w1Companion.");
        WritePositionWord(state, Y, position.Y);
        WritePositionWord(state, X, position.X);
    }

    internal static void DismountAnimal(OracleRuntimeState state, int id)
    {
        if (id is not (RickyId or DimitriId or MooshId) || !IsActive(state, id))
            throw new InvalidOperationException($"SPECIALOBJECT ${id:x2} cannot dismount without owning w1Companion.");
        // commonCode.s:companionDismount retains the animal; Link owns its jump.
        state.SetWramByte(0xd11c, state.ReadWramByte(0xd11b));
        state.SetWramByte(0xd125, 0);
        state.SetWramByte(0xd12b, 0);
        state.SetWramByte(0xd12c, 0);
        state.SetWramByte(0xd12d, 0);
        state.SetWramByte(0xd13c, 0);
        state.SetField("wLinkForceState",0);state.SetField("wcc50",0);
        state.SetWramByte(Active, 1);
        state.SetWramByte(0xd109, (byte)(state.ReadWramByte(Direction) * 8));
    }

    internal static void ReleaseCarried(OracleRuntimeState state, int id)
    {
        if (!IsActive(state, id))
            throw new InvalidOperationException($"SPECIALOBJECT ${id:x2} cannot release an unowned reserved slot.");
        // dimitriState2Substate1/2 clear retention, rather than deleting Dimitri.
        state.SetWramByte(Active, (byte)(state.ReadWramByte(Active) & ~2));
    }

    internal static void MarkForRoomDeletion(OracleRuntimeState state)
    {
        int enabled = state.ReadWramByte(Active);
        // bank1.setObjectsEnabledTo2 preserves the upper bits and retained $03.
        if ((enabled & 3) == 1)
            state.SetWramByte(Active, (byte)((enabled & ~3) | 2));
    }

    internal static void ClearMarkedForRoomDeletion(OracleRuntimeState state)
    {
        if ((state.ReadWramByte(Active) & 3) == 2)
            Clear(state, state.ReadWramByte(Id));
    }

    private static int ReadPositionWord(OracleRuntimeState state, int address) =>
        state.ReadWramByte(address) | state.ReadWramByte(address + 1) << 8;
    private static void WritePositionWord(OracleRuntimeState state, int address, float value)
    {
        int word = Mathf.FloorToInt(value * 256);
        state.SetWramByte(address, unchecked((byte)word));
        state.SetWramByte(address + 1, unchecked((byte)(word >> 8)));
    }

    internal static void Clear(OracleRuntimeState state, int id)
    {
        if (!IsActive(state, id))
            return;
        // bank0.itemDelete clears the entire $40-byte special object.
        for (int address = Active; address < Active + 0x40; address++)
            state.SetWramByte(address, 0);
    }

    // wLastAnimalMountPointY/X belong to the live save image. Mounting and
    // scrolling overwrite them; companionRespawn reads without a room guard.
    internal static Vector2 ReadLastAnimalMountPosition(
        OracleSaveData saveData) => new(saveData.LastAnimalMountX, saveData.LastAnimalMountY);

    internal static void SetLastAnimalMountPosition(
        OracleSaveData saveData,
        Vector2 position)
    {
        saveData.SetLastAnimalMountPoint(
            (byte)Mathf.FloorToInt(position.Y),
            (byte)Mathf.FloorToInt(position.X));
    }

    internal static void Remember(
        OracleRuntimeState state,
        int id,
        int group,
        int room,
        Vector2 position)
    {
        state.SetField("wRememberedCompanionId", checked((byte)id));
        state.SetField("wRememberedCompanionGroup", checked((byte)group));
        state.SetField("wRememberedCompanionRoom", checked((byte)room));
        state.SetField("wRememberedCompanionY", (byte)Mathf.FloorToInt(position.Y));
        state.SetField("wRememberedCompanionX", (byte)Mathf.FloorToInt(position.X));
    }

    internal static void RememberAfterDismount(OracleRuntimeState state,InventoryState inventory,
        int id,int group,int room,Vector2 position)
    {
        if(!ReferenceEquals(state,inventory.RuntimeState))
            throw new ArgumentException("companionDismountAndSavePosition requires the live inventory/runtime owner.",nameof(inventory));
        if(id is not (RickyId or DimitriId or MooshId))throw new ArgumentOutOfRangeException(nameof(id),$"Not a dismounted animal ${id:x2}.");
        Remember(state,id,group,room,position);
        // commonCode.s: Seasons forgets a temporary animal only after saving its position.
        if(state.Game==OracleGame.Seasons && inventory.AnimalCompanion!=id && (id switch
        {
            RickyId=>inventory.FluteIcon!=0,
            DimitriId=>inventory.HasTreasure(TreasureId.Flippers),
            MooshId=>(inventory.Essences&0x10)!=0,
            _=>throw new ArgumentOutOfRangeException(nameof(id),$"Not a dismounted animal ${id:x2}.")
        }))ForgetRemembered(state);
    }

    /// <summary>
    /// Clears wRememberedCompanionId without disturbing the remembered room
    /// and position bytes, matching the native field's single-byte write.
    /// </summary>
    internal static void ForgetRemembered(OracleRuntimeState state) =>
        state.SetField("wRememberedCompanionId", 0);

    // companionSpawner.s:@loadCompanionPreset overwrites only the remembered
    // ID and last-mount coordinates; the remembered room/position survive.
    internal static void InstallPreset(OracleRuntimeState state, OracleSaveData saveData, Vector2 position)
    {
        RequireSameGame(state, saveData);
        ForgetRemembered(state);
        SetLastAnimalMountPosition(saveData, position);
    }

    internal static bool TryGetRemembered(
        OracleRuntimeState state,
        int id,
        int group,
        int room,
        out Vector2 position)
    {
        if (state.ReadField("wRememberedCompanionId") == id &&
            state.ReadField("wRememberedCompanionGroup") == group &&
            state.ReadField("wRememberedCompanionRoom") == room)
        {
            position = new Vector2(
                state.ReadField("wRememberedCompanionX"),
                state.ReadField("wRememberedCompanionY"));
            return true;
        }
        position = default;
        return false;
    }

    internal static RememberedCompanion ReadRemembered(
        OracleRuntimeState state) => new(
            state.ReadField("wRememberedCompanionId"),
            state.ReadField("wRememberedCompanionGroup"),
            state.ReadField("wRememberedCompanionRoom"),
            state.ReadField("wRememberedCompanionY"),
            state.ReadField("wRememberedCompanionX"));

    internal static void RestoreRememberedFromDeathRespawn(
        OracleRuntimeState state,
        OracleSaveData saveData)
    {
        RequireSameGame(state, saveData);
        state.SetField(
            "wRememberedCompanionId",
            saveData.ReadField("wDeathRespawnBuffer.rememberedCompanionId"));
        state.SetField(
            "wRememberedCompanionGroup",
            saveData.ReadField("wDeathRespawnBuffer.rememberedCompanionGroup"));
        state.SetField(
            "wRememberedCompanionRoom",
            saveData.ReadField("wDeathRespawnBuffer.rememberedCompanionRoom"));
        state.SetField(
            "wRememberedCompanionY",
            saveData.ReadField("wDeathRespawnBuffer.rememberedCompanionY"));
        state.SetField(
            "wRememberedCompanionX",
            saveData.ReadField("wDeathRespawnBuffer.rememberedCompanionX"));
    }

    private static void RequireSameGame(OracleRuntimeState state, OracleSaveData saveData)
    {
        if (state.Game != saveData.Game)
            throw new ArgumentException("Companion runtime and saved checkpoint must belong to the same game.");
    }
}

internal readonly record struct ActiveCompanion(
    int Id,
    int Direction,
    int YFixed,
    int XFixed)
{
    internal int Y => YFixed >> 8;
    internal int X => XFixed >> 8;
    internal Vector2 Position => new(XFixed / 256f, YFixed / 256f);
}

internal readonly record struct RememberedCompanion(
    int Id,
    int Group,
    int Room,
    int Y,
    int X);
