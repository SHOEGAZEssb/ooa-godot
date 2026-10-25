using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// Native Link statue $6b:$0e and its finished-game $15 placement.
/// </summary>
internal sealed class MakuLinkStatueRoomEntity
    : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, IRoomBlocker, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized;
    private Action? _initializeNative;
    internal bool Initialized=>_initializeNative is null;
    internal MakuLinkStatueRoomEntity(NpcCharacter npc, MakuSproutRoomDatabase database, OracleRoomData room,
        OracleSaveData save, Func<long> animationTick, Func<Color[]> paletteLoader) : base(npc, npc.SetTransitionDrawOffset)
    {
        if (npc.Record is not { Id: AgesInteractionId.Miscellaneous1, SubId: 0x15 })
            throw new InvalidOperationException("Finished-game Link statue requires INTERAC $6b:$15.");
        npc.Visible = false;
        _initializeNative = () => {
            if (!save.HasGlobalFlag(AgesGlobalFlag.FinishedGame)) { Finished = true; return; }
            byte tile = room.GetMetatile(npc.Position);
            room.SetPositionTileAndCollision(npc.Position, tile, (byte)database.Record.StatueCollision, animationTick());
            npc.SetScriptPaletteOverride(paletteLoader());
            npc.SetSourceGrayscaleInverted(database.Record.StatueSourceInverted);
            npc.SetCollisionRadii(database.Record.StatueRadiusY, database.Record.StatueRadiusX);
            npc.SetScriptAnimation(tile == database.Record.StatueAppearanceTile
                ? database.Record.StatueAlternateAnimation : database.Record.StatueNormalAnimation);
            npc.Visible = true;
        };
    }
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
            if(npc.Record is not {Id:AgesInteractionId.Miscellaneous1,SubId:0x0e})
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
                Id: AgesInteractionId.Miscellaneous1,
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
        if (Finished) return;
        Entity.AnimateAsNpcOneUpdate(frame.Player);
        Entity.SetFixedDrawPriority(Entity.ZIndex);Entity.ClearFixedDrawPriority();
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Initialized && !Finished && Entity.BlocksLinkCenter(linkCenter);

    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("Link statue preload requires Link's live animation sample.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException("Link statue preload requires Link's live animation sample.");
        UpdateFrame(new RoomEntityFrame(player, 0, false), spawns);
        return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
}
