using Godot;
using System.Collections.Generic;

namespace oracleofages;

// Preserve the original object slots. Only the podium sets enabled bit 7.
internal sealed class LibraryActorRoomEntity
    : NpcCharacterRoomEntityAdapter,
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
      IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly LibraryDatabase _data;
    private readonly OracleSaveData? _save;
    private readonly OracleRuntimeState _runtime;
    internal bool NativeInitialized { get; private set; }
    internal int InitialState { get; private set; } = 1;
    internal LibraryActorRoomEntity(NpcCharacter npc, LibraryDatabase data, OracleSaveData? save, OracleRuntimeState runtime)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data; _save = save; _runtime = runtime;
        npc.SetScriptVisible(false);
        npc.SetScriptButtonSensitive(false);
        npc.SetDialogue(0, "", canFace: false);
        npc.SetBlocksLink(npc.Record.Id == InteractionId.OldMan);
        npc.TimeWarpSolidMarked = false;
    }
    internal void InitializeNative()
    {
        if (NativeInitialized) return;
        NativeInitialized = true;
        var r = Npc.Record;
        bool RoomFlag(int mask) => _save?.HasRoomFlag(r.Group, r.Room, (byte)mask) == true;
        if (r.Id != InteractionId.BookOfSealsPodium) Npc.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        if (r.Id == InteractionId.Decoration)
        {
            Npc.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            Npc.SetScriptAnimation(_data.Animation(InteractionId.Decoration, 0, r.SubId));
            Npc.SetActive(!RoomFlag(0x20));
        }
        else if (r.Id == InteractionId.BookOfSealsPodium)
        {
            Npc.SetCollisionRadii(6, 6);
            Npc.SetScriptAnimation(_data.Animation(InteractionId.BookOfSealsPodium, 0));
            Npc.SetScriptButtonSensitive(true);
            if (r.SubId == 0)
            {
                for (int i = 0; i < 16; i++) _runtime.SetWramByte(0xcfd0 + i, 0);
                if (!RoomFlag(0x40)) InitialState = 2;
            }
        }
        else if (r.SubId >= 3)
        {
            var d = _data.Npc(r.SubId);
            bool clean = _save?.HasGlobalFlag(GlobalFlag.WaterPollutionFixed) == true;
            Npc.SetBasePalette(d.Palette);
            Npc.SetCollisionRadii(d.RadiusY, 6);
            Npc.SetDialogue(clean ? d.CleanText : d.PollutedText, clean ? d.CleanMessage : d.PollutedMessage, canFace: false);
            Npc.SetScriptAnimation(_data.Animation(InteractionId.OldMan, d.Animation));
        }
        else
        {
            bool finished = _save?.HasGlobalFlag(GlobalFlag.FinishedGame) == true;
            Npc.SetActive(r.SubId == 1 || (r.SubId == 0 ? finished : !finished));
            Npc.SetDialogue(0, "", canFace: r.SubId == 0);
            Npc.SetScriptAnimation(_data.Animation(InteractionId.OldMan, 2));
        }
        // Graphics initialization does not publish visibility. Podiums first
        // become visible at their native relative-priority call ($00 only
        // after the book-placement script ends).
        if (r.Id != InteractionId.BookOfSealsPodium)
        {
            Npc.SetScriptVisible(true);
            Npc.ShowInitializedPresentation();
        }
    }
    internal LibraryScriptHost? Host { get; set; }
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => false;
    public bool RefreshVisibility => false;
    public bool UpdatesDuringDialogue => !NativeInitialized || Entity.Record.Id == InteractionId.BookOfSealsPodium;
    public bool UpdatesDuringRoomEntityFreeze => UpdatesDuringDialogue;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Host?.AdvanceFrame();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (Npc.Record.Id != InteractionId.BookOfSealsPodium) return;
        if (Host is not null) { Host.AdvanceFrame(scrolling: true); return; }
        // Incoming scripts remain at their preloaded state-zero boundary.
        // State one still publishes its pose through the native always bit;
        // spawnAllPodiums cannot allocate until scroll mode $01 resumes.
        if (InitialState == 1)
        {
            Npc.SetScriptVisible(true);
            Npc.ClearFixedDrawPriority();
            Npc.UpdateDrawPriority(frame.Player.Position, withTerrainEffects: true);
        }
    }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
    ScreenTransitionPresentation IScreenTransitionPreloadRoomEntity.PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        InitializeNative();
        Host?.InitializeAfterPreload();
        return Npc.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        bool fresh = !NativeInitialized;
        InitializeNative();
        if (fresh && Npc.Active && Npc.Record.Id == InteractionId.OldMan && Npc.Record.SubId <= 2 && player is not null)
        {
            Npc.SetScriptButtonSensitive(true);
            Npc.ClearFixedDrawPriority();
            if (Npc.Record.SubId == 0) Npc.FaceLinkAndAnimateOneUpdate(player);
            else Npc.AnimateAsNpcOneUpdate(player);
        }
        Host?.InitializeAfterPreload();
        return Npc.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
}

internal sealed record LibraryPodiumSpawn(LibraryEvent Owner, int SubId) : RoomEntitySpawn(UpdateThisFrame: true);
