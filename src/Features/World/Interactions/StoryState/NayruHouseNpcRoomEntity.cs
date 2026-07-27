using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class NayruHouseNpcRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IFixedRoomEntity, IRoomBlocker,
        ITalkTarget, IOrdinaryNpcEntity, INpcTalkLifecycle
{
    private readonly NayruHouseDatabase _database;
    private readonly OracleRoomData _room;
    private readonly Func<long> _animationTick;
    private bool _initialized;

    internal NayruHouseNpcRoomEntity(
        NpcCharacter npc,
        NayruHouseDatabase database,
        OracleRoomData room,
        Func<long> animationTick)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _database = database;
        _room = room;
        _animationTick = animationTick;
        if (!database.Matches(npc.Record))
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} is not part of " +
                "the imported Nayru-house contract.");
        }
    }

    public NpcCharacter Npc => Entity;
    public NpcCharacter TalkNpc => Entity;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            if (Entity.Record is
                {
                    Id: 0x4f,
                    Var03: 0x01 or 0x0a
                })
            {
                Entity.SetFacingDirection(Vector2I.Up);
            }
            if (Entity.Record is
                {
                    Id: 0x4f,
                    SubId: 0x00,
                    Var03: 0x00
                })
            {
                NayruHouseRecord record = _database.Record;
                _room.SetPositionTileAndCollision(
                    PackedCenter(record.StairPosition),
                    (byte)record.StairTile,
                    collision: null,
                    _animationTick(),
                    preserveRenderedTile: record.PreserveRendered);
            }
            return;
        }

        if (!Entity.Active)
            return;
        if (Entity.Record.Id == 0x4f)
        {
            if (Entity.Record.Var03 is 0x01 or 0x0a)
                Entity.AnimateAsNpcOneUpdate(frame.Player);
            else
                Entity.FaceLinkImmediatelyAndAnimateAsNpcOneUpdate(frame.Player);
            return;
        }

        Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    public void OnNpcTalkStarted()
    {
    }

    public void OnNpcTalkEnded()
    {
        if (Entity.Record is { Id: 0x4f, Var03: 0x01 or 0x0a })
            Entity.SetFacingDirection(Vector2I.Up);
    }

    private static Vector2 PackedCenter(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize +
            OracleRoomData.MetatileSize / 2,
        (packed >> 4) * OracleRoomData.MetatileSize +
            OracleRoomData.MetatileSize / 2);
}
