using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class WallmasterRoomEntity
    : CombatEnemyRoomEntityAdapter<WallmasterCharacter>, IFixedRoomEntity
{
    private readonly Action<int> _soundRequested;
    private readonly Action<Warp> _warpRequested;
    private readonly int _group;
    private readonly int _room;
    private readonly int _destinationGroup;
    private readonly int _destinationRoom;

    public WallmasterRoomEntity(
        WallmasterCharacter wallmaster,
        Action<int> soundRequested,
        Action<Warp> warpRequested,
        int group,
        int room,
        int destinationGroup,
        int destinationRoom,
        int killableEnemyIndex)
        : base(
            wallmaster, wallmaster.SetTransitionDrawOffset,
            new EnemyCombatComponent(
                () => wallmaster.IsDead,
                () => wallmaster.CollisionBounds,
                wallmaster.TakeSwordHit,
                wallmaster.TakeBurnHit,
                player =>
                {
                    if (wallmaster.HandleLinkContact(player))
                        soundRequested(OracleSoundEngine.SndBossDead);
                },
                wallmaster.TakeDeathPuff),
            countsAsEnemy: true,
            killableEnemyIndex,
            collisionZ: () => wallmaster.ZFixed >> 8)
    {
        _soundRequested = soundRequested;
        _warpRequested = warpRequested;
        _group = group;
        _room = room;
        _destinationGroup = destinationGroup;
        _destinationRoom = destinationRoom;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.UpdateFrame(frame.Player, _soundRequested);
        if (Entity.TakeWarpedPlayer() is null)
            return;
        _warpRequested(new Warp(
            _group, _room, -1, 0, 0,
            _destinationGroup, _destinationRoom, 0x87, 0, 3));
    }
}
