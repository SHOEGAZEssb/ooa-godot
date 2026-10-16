using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native Link statue $6b:$0e and its finished-game $15 placement.
/// </summary>
internal sealed class MakuLinkStatueRoomEntity
    : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, IRoomBlocker
{
    private Action? _initializeNative;
    internal bool Initialized=>_initializeNative is null;
    public MakuLinkStatueRoomEntity(
        NpcCharacter npc,
        MakuSproutRoomDatabase database,
        OracleRoomData room,
        Func<long> animationTick,Func<Color[]>? nativePaletteLoader=null)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        MakuSproutRoomRecord record = database.Record;
        if(nativePaletteLoader is not null)
        {
            if(npc.Record is not {Id:InteractionId.Miscellaneous1,SubId:0x0e})
                throw new InvalidOperationException("Native Link statue requires INTERAC $6b:$0e.");
            _initializeNative=()=>
            {
                Entity.SetScriptPaletteOverride(nativePaletteLoader());
                Entity.SetSourceGrayscaleInverted(record.StatueSourceInverted);
                Entity.SetCollisionRadii(record.StatueRadiusY,record.StatueRadiusX);
                Entity.SetScriptAnimation(room.GetMetatile(npc.Position)==record.StatueAppearanceTile?record.StatueAlternateAnimation:record.StatueNormalAnimation);
                Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);Entity.ClearFixedDrawPriority();
            };
            return;
        }
        if (npc.Record is not
            {
                Group: 1,
                Room: 0x38,
                Id: InteractionId.Miscellaneous1,
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
        ICollection<RoomEntitySpawn> spawns)
    {
        if(_initializeNative is not null){_initializeNative();_initializeNative=null;}
        Entity.AnimateAsNpcOneUpdate(frame.Player);
        Entity.SetFixedDrawPriority(Entity.ZIndex);Entity.ClearFixedDrawPriority();
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
}
