using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// INTERAC_DEKU_SCRUB $d6:$00 keeps var3e's near/far animation state while
/// linkedGameNpcScript owns its choice loop and persistent secret flag.
/// </summary>
internal sealed class DekuScrubRoomEntity
    : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, IRoomBlocker,
        ITalkTarget, IOrdinaryNpcEntity, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private const int ProximityDistance = 0x20;
    private readonly DekuScrubDatabase _database;
    private bool _shown;
    private bool _storyInitialized;
    private bool _nativeInitialized;

    public DekuScrubRoomEntity(NpcCharacter npc, DekuScrubDatabase database)
        : base(RequireDekuScrub(npc), npc.SetTransitionDrawOffset)
    {
        _database = database;
        Entity.SetScriptAnimation(database.Animation(0));
    }

    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => !_storyInitialized;
    public void CompleteStoryInitialization() => _storyInitialized = true;
    public bool UpdatesDuringDialogue => _nativeInitialized;
    public bool UpdatesDuringRoomEntityFreeze => _nativeInitialized;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;
        // interactionSetAlwaysUpdateBit is written during state zero, after
        // the ordinary destination freeze has released this interaction.
        _nativeInitialized = true;
        Advance(frame.Player);
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (_nativeInitialized)
            Advance(frame.Player);
    }

    private void Advance(Player player)
    {
        if (!Entity.Active)
            return;

        // dekuScrub.s runs interactionAnimateAsNpc before comparing Link's
        // high-byte Manhattan distance. Exactly $20 is outside the range.
        Entity.AnimateAsNpcOneUpdate(player);
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 scrub = OracleObjectMath.ToPixelPosition(Entity.Position);
        bool close = Math.Abs(link.X - scrub.X) +
            Math.Abs(link.Y - scrub.Y) < ProximityDistance;
        if (close == _shown)
            return;
        _shown = close;
        Entity.SetScriptAnimation(_database.Animation(close ? 1 : 3));
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    private static NpcCharacter RequireDekuScrub(NpcCharacter npc)
    {
        if (npc.Record is not
            {
                Group: 1, Room: 0x90, Id: 0xd6, SubId: 0,
                Implementation: NpcImplementationClassification.SpecializedNative
            })
        {
            throw new InvalidOperationException(
                $"Room 1:90 Deku Scrub $d6:$00 received " +
                $"{npc.Record.Group:x1}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} " +
                $"({npc.Record.Implementation}).");
        }
        return npc;
    }
}
