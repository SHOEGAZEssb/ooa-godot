
namespace Oracles;

public sealed partial class RoomEntityManager
{
    private EnemyHandlerRegistry? _placementHandlers;
    private EnemySpawnTileDatabase? _enemySpawnTiles;

    internal IEnumerable<IRoomEntity> PlaceRoomObject(RoomObjectRecord source, OracleRoomData room,
        EnemyPlacementContext placement, EnemyPlacementReservations? reservations=null, Action? onAllocationFailure=null)
    {
        if (!RoomObjectConditionMet(source)) yield break;
        reservations ??= new(_runtimeState);
        switch (source.Kind)
        {
            case RoomObjectKind.RandomEnemy or RoomObjectKind.FixedEnemy or RoomObjectKind.ParameterEnemy:
                foreach (var entity in PlaceEnemies(source,room,placement,reservations,onAllocationFailure)) yield return entity;
                break;
            case RoomObjectKind.ReservingPart or RoomObjectKind.ParameterPart:
                if (!PartSlotAvailable) { onAllocationFailure?.Invoke(); yield break; }
                if (source.Kind==RoomObjectKind.ReservingPart) reservations.Add(source.PackedPosition);
                yield return _factory.CreatePlacedPart(source,room);
                break;
            case RoomObjectKind.ItemDrop:
                if (source.Id!=EnemyId.ItemDropProducer || source.SubId is <0 or >=16)
                    throw new NotSupportedException($"{source.Source}: expected ENEMY_ITEM_DROP_PRODUCER $59 with a native drop subid $00-$0f.");
                int index=NextKillableEnemyIndex(source.Flags);
                if (WasKilledDuringPlacement(index)) yield break;
                int slot=FindFreeEnemySlot();
                if (slot<0) { onAllocationFailure?.Invoke(); yield break; }
                reservations.Add(source.PackedPosition);
                var producer=new ItemDropProducer { Name=$"ItemDropProducer_{source.Order}_${source.SubId:x2}" };
                producer.Initialize(source.SubId,new((source.PackedPosition&15)*16+8,(source.PackedPosition>>4)*16+8),
                    room,_inventory,_saveData,_random);
                var wrapper=new ItemDropProducerRoomEntity(producer,index);
                RegisterEnemySlot(wrapper,slot);
                yield return wrapper;
                break;
            default:
                throw new NotSupportedException($"{source.Source}: unknown room placement {source.Kind}.");
        }
    }

    internal bool RoomObjectConditionMet(RoomObjectRecord source)
    {
        if (source.ConditionMask == 0xff) return true;
        int modifier = _runtimeState.ReadField("wRoomStateModifier");
        if (modifier >= 8)
            throw new NotSupportedException($"{source.Source}: objectDataOp0 uses unsupported wRoomStateModifier ${modifier:x2}; bitTable supports $00-$07.");
        return (source.ConditionMask & (1 << modifier)) != 0;
    }

    // Native opcodes $06/$07/$09 share allocation and placement state. The
    // Stream callers select pointer/event branches; conditions use live room state.
    internal IEnumerable<IRoomEntity> PlaceEnemies(RoomObjectRecord source, OracleRoomData room,
        EnemyPlacementContext placement, EnemyPlacementReservations? reservations = null, Action? onAllocationFailure = null)
    {
        if (source.Kind is not (RoomObjectKind.RandomEnemy or RoomObjectKind.FixedEnemy or RoomObjectKind.ParameterEnemy))
            throw new InvalidOperationException($"{source.Source}: expected an enemy placement opcode.");
        if (!RoomObjectConditionMet(source)) yield break;
        var handler = (_placementHandlers ??= new(AssetRoot)).ResolveHandler(source);
        reservations ??= new(_runtimeState);
        int count = source.Kind == RoomObjectKind.RandomEnemy ? source.Count : 1;
        for (int instance = 0; instance < count; instance++)
        {
            int index = source.Kind == RoomObjectKind.ParameterEnemy ? 0 : NextKillableEnemyIndex(source.Flags);
            if (WasKilledDuringPlacement(index)) continue;
            int slot = FindFreeEnemySlot();
            if (slot < 0) { onAllocationFailure?.Invoke(); yield break; }
            Vector2 position = new(source.X,source.Y);
            if (source.Kind == RoomObjectKind.RandomEnemy)
            {
                if (!TryChooseRandomEnemyPosition(room,source.Flags,reservations,placement,out position))
                {
                    // The clean US allocator clears enabled on failure but
                    // retains its counted allocation in wNumEnemies.
                    RetainFailedPlacementCount(source.Flags);
                    continue;
                }
            }
            else if (source.Kind == RoomObjectKind.FixedEnemy)
                reservations.Add(room.GetPackedPosition(position));
            // Op$09 never checks recent defeats or reserves a position.
            var entity = _factory.CreatePlacedEnemy(handler,source,room,position,instance,index,placement);
            // Op$09 calls getFreeEnemySlot_uncounted without changing raw
            // object flags; its later death can still decrement a live count.
            if (source.Kind == RoomObjectKind.ParameterEnemy && entity is IRoomEnemyCounterEntity { CountsAsEnemy:true })
                _enemyCountOffset--;
            RegisterEnemySlot(entity,slot);
            if (entity is not null) yield return entity;
        }
    }

    internal bool WasKilledDuringPlacement(int index) => index != 0 &&
        (_runtimeState.ReadWramByte(EnemyPlacementMemory.KilledEnemies) & (1 << index)) != 0;

    internal int NextKillableEnemyIndex(int flags)
    {
        // Flags bit $01 bypasses checkEnemyKilled. Only seven checked objects
        // receive an index in Enemy.enabled, even when later allocation fails.
        int count = _runtimeState.ReadWramByte(EnemyPlacementMemory.KillableCount);
        if ((flags & 1) != 0 || count >= 7) return 0;
        _runtimeState.SetWramByte(EnemyPlacementMemory.KillableCount,(byte)(count+1));
        return count+1;
    }

    internal bool TryChooseRandomEnemyPosition(OracleRoomData room, int flags,
        EnemyPlacementReservations reservations, EnemyPlacementContext placement, out Vector2 position)
    {
        _runtimeState.SetWramByte(EnemyPlacementMemory.AttemptsRemaining,0x40);
        while (true)
        {
            // Boundary/reservation retries consume cursor bytes inside one
            // attempt; entry and terrain rejection consume another attempt.
            byte attempts = unchecked((byte)(_runtimeState.ReadWramByte(EnemyPlacementMemory.AttemptsRemaining)-1));
            _runtimeState.SetWramByte(EnemyPlacementMemory.AttemptsRemaining,attempts);
            if (attempts == 0) break;
            int packed,tileY,tileX;
            while (true)
            {
                packed = _random.NextPlacementValue(); tileY=packed>>4; tileX=packed&15;
                bool boundary = room.Group < 4
                    ? tileY < OracleRoomData.ViewportHeight/OracleRoomData.MetatileSize &&
                        tileX < OracleRoomData.ViewportWidth/OracleRoomData.MetatileSize
                    : tileY > 0 && tileY < room.HeightInTiles-1 && tileX > 0 && tileX < room.WidthInTiles-1;
                if (boundary && !reservations.Contains(packed)) break;
            }
            _runtimeState.SetWramByte(EnemyPlacementMemory.Position,(byte)packed);
            if (!placement.Allows(room,packed)) continue;
            position = new(tileX*OracleRoomData.MetatileSize+8,tileY*OracleRoomData.MetatileSize+8);
            if ((flags & 4) == 0 && !(_enemySpawnTiles ??= new(AssetRoot)).IsValid(
                room.ActiveCollisions,room.GetTerrainInfo(position))) continue;
            reservations.Add(packed);
            return true;
        }
        position=Vector2.Zero;
        return false;
    }
}
