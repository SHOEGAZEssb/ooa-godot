using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native INTERAC_RABBIT $4b:$06 state. These actors have no script or
/// A-button behavior; they only push Link away and update draw priority.
/// </summary>
internal sealed class StoneRabbitRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IFixedRoomEntity, IRoomBlocker,
        IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public StoneRabbitRoomEntity(
        NpcCharacter npc,
        StoneRabbitDatabase database)
        : base(RequireStoneRabbit(npc, database), npc.SetTransitionDrawOffset)
    {
        StoneRabbitRecord state = database.Record;
        Entity.SetBasePalette(state.Palette);
        Entity.SetScriptPaletteOverride(database.StonePalette);
        Entity.SetScriptAnimation(state.Animation);
        Entity.SetCollisionRadii(
            state.CollisionRadius,
            state.CollisionRadius);
        Entity.SetDialogue(0, string.Empty, canFace: false);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player);

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    private static NpcCharacter RequireStoneRabbit(
        NpcCharacter npc,
        StoneRabbitDatabase database)
    {
        if (npc.Record.Implementation !=
                NpcImplementationClassification.SpecializedNative ||
            !database.Matches(npc.Record))
        {
            throw new InvalidOperationException(
                $"NPC {npc.Record.Group}:{npc.Record.Room:x2} " +
                $"${npc.Record.Id:x2}:${npc.Record.SubId:x2} cannot use " +
                "the room 1:84 stone-rabbit adapter.");
        }
        return npc;
    }
}
