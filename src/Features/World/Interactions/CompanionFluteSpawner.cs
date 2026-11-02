
namespace Oracles;

/// <summary>INTERAC_COMPANION_SPAWNER $67/$5f:$80 source-ordered entrance search.</summary>
internal sealed class CompanionFluteSpawner(RoomSession rooms, RoomEntityManager entities,
    FluteDatabase data, Action<string, Player> showText) :
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private int _address;
    private bool _retired;
    public Node2D Node { get; } = new();
    public bool Finished => _retired || _address!=0 && Read(0)==0;
    public bool NativeInitializationPending => !Finished && (_address==0 || Read(4)==0);
    public bool UpdatesDuringDialogue => _address!=0 && (Read(0)&0x80)!=0;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;

    internal static bool TryCreate(RoomSession rooms, RoomEntityManager entities, FluteDatabase data, Action<string,Player> showText)
    {
        if (rooms.AssetRoot!=data.AssetRoot || entities.AssetRoot!=data.AssetRoot || !ReferenceEquals(rooms.RuntimeState,entities.RuntimeState))
            throw new ArgumentException("Flute caller requires the selected room, data and interaction pool owners.");
        if (!entities.InteractionSlotAvailable) return false;
        entities.AddEntity(new CompanionFluteSpawner(rooms,entities,data,showText));
        return true;
    }
    public void BindNativeInteractionSlot(int slot)
    {
        if (_address!=0) throw new InvalidOperationException("companionSpawner.s: Flute caller already owns an interaction slot.");
        _address=((0xd0+slot)<<8)|0x40;
        Write(0,1); Write(1,data.SpawnerId); Write(2,0x80);
        // objectCreateInteraction copies the active D5 item's position highs only.
        foreach (int offset in new[] { 0x0b,0x0d,0x0f })
            Write(offset,entities.RuntimeState.ReadWramByte(0xd500+offset));
    }
    public void ReleaseNativeInteractionSlot()
    {
        if (_address!=0 && !_retired && Read(1)==data.SpawnerId && (Read(2)&0x80)!=0)
            for (int offset=0;offset<0x40;offset++) Write(offset,0);
        _retired=true;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (Read(1)!=data.SpawnerId || (Read(2)&0x80)==0)
            throw new NotSupportedException($"companionSpawner.s: slot${_address:x4} ID${Read(1):x2}:${Read(2):x2} has no bound Flute caller.");
        Execute(frame.Player);
        ReleaseNativeInteractionSlot();
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (NativeInitializationPending || UpdatesDuringDialogue) UpdateFrame(frame,Array.Empty<RoomEntitySpawn>());
    }
    private int Read(int offset)=>entities.RuntimeState.ReadWramByte(_address+offset);
    private void Write(int offset,int value)=>entities.RuntimeState.SetWramByte(_address+offset,unchecked((byte)value));
    private void ShowText(int id,Player player)
    {
        if (!entities.IsTextActive()) showText(data.Text(id),player);
    }
    private void Execute(Player player)
    {
        bool liveSlot = CompanionRuntimeState.AnyActive(entities.RuntimeState);
        int liveId = liveSlot ? CompanionRuntimeState.Read(entities.RuntimeState).Id : -1;
        if (liveSlot && liveId < 0x0e && liveId != 0x0a) return;
        if ((rooms.CurrentRoom.TilesetFlags & 0x81) != 1) return;
        if (player.Inventory.FluteIcon == 0) { ShowText(0x510f,player); return; }
        if (!data.Callable(rooms.CurrentRoom.Id)) { ShowText(0x510c,player); return; }
        if (liveSlot) return;

        int column = (Mathf.FloorToInt(player.PrecisePosition.X) >> 4) & 15;
        int row = Mathf.FloorToInt(player.PrecisePosition.Y) & 0xf0;
        Write(0x3e,row); Write(0x3f,column);
        (int Packed, int Stride, int Direction)[] direct =
            [(column, 16, 2), (0x60 + column, 16, 0), (row + 8, 1, 3), (row, 1, 1)];
        foreach (var probe in direct)
            if (TryPair(probe.Packed, probe.Stride, out Vector2 point))
            { Spawn(probe.Direction, point, player); return; }
        (int Packed, int Stride, int Step, int Direction)[] ranges =
            [(3, 16, 1, 2), (0x63, 16, 1, 0), (0x28, 1, 16, 3), (0x20, 1, 16, 1)];
        foreach (var range in ranges)
        for (int index = 0; index < 4; index++)
            if (TryPair(range.Packed + index * range.Step, range.Stride, out Vector2 point))
            { Spawn(range.Direction, point, player); return; }
        ShowText(0x510c,player);
    }

    private bool TryPair(int packed, int stride, out Vector2 point)
    {
        Vector2 first = new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        int second = (packed + stride) & 255;
        point = new((second & 15) * 16 + 8, (second >> 4) * 16 + 8);
        return rooms.CurrentRoom.GetTerrainInfo(first).Collision == 0 &&
            rooms.CurrentRoom.GetTerrainInfo(point).Collision == 0;
    }

    private void Spawn(int direction, Vector2 point, Player player)
    {
        Vector2 start = direction switch
        {
            0 => new(point.X, 136), 1 => new(-8, point.Y),
            2 => new(point.X, -8), 3 => new(168, point.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        Vector2 destination = direction switch
        {
            0 => new(point.X, 112), 1 => new(16, point.Y),
            2 => new(point.X, 16), _ => new(144, point.Y)
        };
        switch (player.Inventory.AnimalCompanion)
        {
            case 0x0b:
                entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(start, direction,
                    rooms.ActiveGroup, rooms.CurrentRoom.Id, FluteDestination: destination));
                break;
            case 0x0c:
                entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(start, direction,
                    rooms.ActiveGroup, rooms.CurrentRoom.Id, FluteDestination: destination));
                break;
            case 0x0d:
                entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(start, direction,
                    rooms.ActiveGroup, rooms.CurrentRoom.Id, FluteDestination: destination));
                break;
            default:
                throw new InvalidOperationException($"companionSpawner.s:@fluteCall invalid companion ${player.Inventory.AnimalCompanion:x2}.");
        }
    }
}
