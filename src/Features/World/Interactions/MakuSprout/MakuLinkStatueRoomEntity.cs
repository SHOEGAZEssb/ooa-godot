using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Finished-game INTERAC_MISCELLANEOUS_1 $6b:$15 in room $1:$38.
/// </summary>
internal sealed class MakuLinkStatueRoomEntity
    : RoomEntityAdapter<NpcCharacter>, IFixedRoomEntity, IRoomBlocker
{
    public MakuLinkStatueRoomEntity(
        NpcCharacter npc,
        MakuSproutRoomDatabase database,
        OracleRoomData room,
        Func<long> animationTick)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        MakuSproutRoomRecord record = database.Record;
        if (npc.Record is not
            {
                Group: 1,
                Room: 0x38,
                Id: 0x6b,
                SubId: 0x15,
                Y: 0x40,
                X: 0x84
            })
        {
            throw new InvalidOperationException(
                "The room 1:38 postgame Link statue was created from an " +
                "unexpected interaction record.");
        }

        byte tile = room.GetMetatile(npc.Position);
        Entity.SetSourceGrayscaleInverted(record.StatueSourceInverted);
        Entity.SetSpritePalette(database.StatuePalette);
        Entity.SetCollisionRadii(
            record.StatueRadiusY, record.StatueRadiusX);
        Entity.SetScriptAnimation(
            tile == record.StatueAppearanceTile
                ? record.StatueAlternateAnimation
                : record.StatueNormalAnimation);
        room.SetPositionTileAndCollision(
            npc.Position,
            tile,
            (byte)record.StatueCollision,
            animationTick());
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.AnimateAsNpcOneUpdate(frame.Player);

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
}
