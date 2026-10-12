using Godot;
using System;

namespace oracleofages;

/// <summary>CUTSCENE_BLACK_TOWER_COMPLETE, explanation stage $02, and TURN_TO_STONE.</summary>
internal sealed class PostD7Event(RoomEventContext context, RemoteMakuSeventhEssenceEvent remoteMaku)
    : IRoomEntryEvent, IRoomEventDialogueContext
{
    private readonly PostD7EventDatabase _data = new();
    private readonly BlackTowerEntranceEventDatabase _towerData = new();
    private readonly RoomEventResources _resources = new(context, new object());
    private BlackTowerExplanationScreen? _tower;
    private bool _pendingTower, _pendingStone;
    internal PostD7Stage Stage { get; private set; }
    internal int State { get; private set; }
    internal PostD7EventDatabase Database => _data;
    internal bool NativeActive => Stage is PostD7Stage.Complete or PostD7Stage.Tower or PostD7Stage.Stone;
    public bool HasState => Stage != PostD7Stage.Inactive;
    public bool BlocksGameplay => HasState;
    public bool OwnsGameLogic => NativeActive;
    public bool SkipsObjectUpdate { get; private set; }
    public bool DisablesLink => HasState;
    public bool MenusDisabled => HasState;
    public bool AllScreenTransitionsDisabled => HasState;
    public bool OwnsUpdatesOf(IRoomEvent other) => other is JabuExteriorEvent || ReferenceEquals(other, remoteMaku);
    public DialogueScreenContext? DialogueScreen => Stage switch
    {
        PostD7Stage.Tower => context.DialogueScreens.ClearedLink(0x09, 0x70),
        PostD7Stage.Complete when context.Rooms.SaveData.IsLinkedGame && State == 5 =>
            context.DialogueScreens.ClearedLink(0x02),
        // blackTowerComplete state2 writes SCY=$00 and screenOffsetY=$10
        // directly; it does not install a gfxRegisterStates row.
        PostD7Stage.Complete when State == 5 => new(0, 0, 0x10, 0),
        PostD7Stage.Ascent or PostD7Stage.Stone => context.DialogueScreens.ClearedLink(0x02),
        _ => null
    };
    private byte Read(int address) => context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    private bool Decrement(int address) { Write(address, Read(address) - 1); return Read(address) == 0; }

    public bool Matches(int group, OracleRoomData room) =>
        group == 2 && room.Id == 0x90 &&
            (context.Rooms.SaveData.ReadWramByte(WramAddress.wEssencesObtained) & 0x40) != 0 &&
            !context.Rooms.SaveData.HasRoomFlag(2, 0x90, 0x40) ||
        group == 4 && room.Id == 0xf6 && !context.Rooms.SaveData.HasRoomFlag(4, 0xf6, 0x40);

    public void Start(OracleRoomData room)
    {
        Cancel();
        Stage = room.Id == 0x90 ? PostD7Stage.Pending : PostD7Stage.Ascent;
        State = 0;
        if (Stage == PostD7Stage.Ascent)
        {
            context.Player.Visible = false;
            Write(WramAddress.wDisabledObjects, 1); Write(WramAddress.wMenuDisabled, 1);
            _resources.CaptureFullScreenFade(context.Hud.ZIndex + 2);
        }
    }

    public void UpdateFrame()
    {
        if (Stage == PostD7Stage.Stone && State == 6) { remoteMaku.UpdateFrame(); return; }
        if (Stage != PostD7Stage.Pending) return;
        Write(WramAddress.wDisabledObjects, 1);
        Write(WramAddress.wMenuDisabled, 1);
        _resources.CaptureFullScreenFade(context.Hud.ZIndex + 2);
        Stage = PostD7Stage.Complete;
    }

    internal void RequestTower() => _pendingTower = true;
    internal void RequestStone() => _pendingStone = true;

    internal void AdvanceBeforeObjects()
    {
        SkipsObjectUpdate = false;
        if (!HasState) return;
        context.Transitions.AdvanceNativeWhiteFade();
        if (_pendingTower)
        {
            _pendingTower = false;
            Stage = PostD7Stage.Tower;
            State = 0;
            // cutscene21 selects its trigger and returns without entering
            // updateAllObjects. The new handler runs on the next update.
            SkipsObjectUpdate = true;
            return;
        }
        if (_pendingStone)
        {
            _pendingStone = false;
            Stage = PostD7Stage.Stone;
            State = 0;
            _resources.CaptureFullScreenFade(context.Hud.ZIndex + 2);
        }
        if (!NativeActive) return;
        bool faded = context.Transitions.NativePaletteFadeActive;
        switch (Stage)
        {
            case PostD7Stage.Complete: AdvanceComplete(faded); break;
            case PostD7Stage.Tower: AdvanceTower(faded); break;
            case PostD7Stage.Stone: AdvanceStone(faded); break;
        }
    }

    private void AdvanceComplete(bool faded)
    {
        switch (State)
        {
            case 0: Write(0xcbb3, _data.Table("entry")[6]); State = 1; return;
            case 1:
                if (!Decrement(0xcbb3)) return;
                State = 2;
                context.Sound.PlaySound(SoundId.SndCtrlFastFadeOut);
                context.Transitions.BeginNativeBlackFade(true, speed: 3);
                return;
            case 2:
                if (faded) return;
                context.Hud.HideStatusBar();
                context.RoomView.SuspendUnderwaterWave(true);
                context.Transitions.ResetCamera();
                if (context.Rooms.SaveData.IsLinkedGame)
                { Write(0xcbb3, _data.Table("linked")[4]); State = 3; }
                else
                {
                    context.Entities.LoadCutsceneRoom(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom, false);
                    context.Player.Visible = false;
                    context.Fade.Color = Colors.Black;
                    context.RoomView.SetBackgroundFade(Colors.Black, 1);
                    Actor(0x8d, 1, Vector2.Zero);
                    State = 5;
                }
                return;
            case 3:
                if (!Decrement(0xcbb3)) return;
                BeginFlash(); State = 4; return;
            case 4:
                if (!Flash(2)) return;
                Load(5, 0xf1, "linked");
                context.Hud.ShowStatusBar();
                context.Sound.PlaySound(SoundId.MusDisaster);
                context.RoomCamera.Position += new Vector2(_data.Table("linked")[3], 0);
                context.Transitions.BeginNativeWhiteFade(false);
                State = 5;
                return;
            case 5: return;
            default: throw new InvalidOperationException($"CUTSCENE_BLACK_TOWER_COMPLETE state ${State:x2}.");
        }
    }

    private void AdvanceTower(bool faded)
    {
        if (State == 0)
        {
            if (faded) return;
            context.Entities.LoadCutsceneRoom(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom, false);
            context.Player.Visible = false;
            _tower = new BlackTowerExplanationScreen(_towerData, 2);
            context.InterfaceLayer.AddChild(_tower);
            context.Hud.HideStatusBar();
            Write(0xcbb3, _data.Table("tower")[1]);
            Write(0xcbb9, 0);
            context.Transitions.BeginNativeWhiteFade(false);
            context.Fade.Color = Colors.White;
            State = 1;
            return;
        }
        UpdateTowerFlash();
        if (State == 1)
        {
            if (faded || !Decrement(0xcbb3)) return;
            Write(0xcbbb, _data.Table("tower")[2]);
            Write(0xcbb6, Read(0xcbbb));
            Write(0xcbb3, _data.Table("tower")[3]);
            State = 2;
        }
        else if (State == 2)
        {
            if (!Decrement(0xcbb6)) return;
            if (Decrement(0xcbb3)) { Write(0xcbb3, _data.Table("tower")[4]); State = 3; return; }
            Write(0xcbb6, Read(0xcbbb));
            _scrollY = (_scrollY - 1) & 0xff;
            if (_scrollY == 0) _topLoaded = true;
            _tower!.SetCompletedTowerScroll(_scrollY, _topLoaded);
        }
        else if (State == 3 && Decrement(0xcbb3))
        {
            var table = _data.Table("tower");
            RemoveTower();
            context.Transitions.ApplyWarp(context.Player, new Warp(
                context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id, -1, 0, WarpSourceTransition.Instant,
                table[5], table[6], table[7], table[9], table[8]));
        }
    }
    private int _scrollY = 0x70;
    private bool _topLoaded;
    private void UpdateTowerFlash()
    {
        if (Read(0xcbb9) != 0)
        {
            if (Flash(1, 0xcbb7, tower: true)) Write(0xcbb9, 0);
            return;
        }
        if ((context.Entities.FrameCounter & 0x1f) != 0 || (context.Entities.NextRandomValue() & 7) != 0) return;
        Write(0xcbb7, 0); Write(0xcbb9, 0xff); Write(0xcbba, 0xff);
        context.Sound.PlaySound(SoundId.SndLightning);
    }
    internal void BeginFlash(bool sound = true)
    {
        Write(0xcbb3, 0); Write(0xcbba, 0xff);
        if (sound) context.Sound.PlaySound(SoundId.SndLightning);
    }
    internal bool Flash(int kind, int address = 0xcbb3, bool tower = false)
    {
        Write(address, Read(address) + 1);
        var data = _data.Table($"flash-{kind}");
        int index = 0;
        while (data[index] < Read(address)) index++;
        if (data[index] == 0xff) return true;
        int palette = index & 1;
        if (Read(0xcbba) == palette) return false;
        Write(0xcbba, palette);
        if (tower) _tower!.SetFlashWhite(palette == 0);
        else context.Fade.Color = new Color(Colors.White, palette == 0 ? 1 : 0);
        return false;
    }

    private void AdvanceStone(bool faded)
    {
        switch (State)
        {
            case 0:
                for (int address = 0xcfc0; address < 0xcfe0; address++) Write(address, 0);
                Write(0xcfde, 0x0d); context.Hud.ShowStatusBar();
                context.Transitions.BeginNativeWhiteFade(true); State = 1; return;
            case 1:
                if (faded) return;
                LoadStoneRoom(); context.Transitions.BeginNativeWhiteFade(false); State = 2; return;
            case 2:
                if (faded || Read(0xcfdf) != 0xff) return;
                Write(0xcfdf, 0); Write(0xcfde, Read(0xcfde) + 1);
                State = Read(0xcfde) == 0x0f ? 3 : 1;
                context.Transitions.BeginNativeWhiteFade(true); return;
            case 3:
                if (faded) return;
                LoadStoneRoom(); context.Transitions.BeginNativeWhiteFade(false);
                context.RoomCamera.Position += new Vector2(40, 8); State = 4; return;
            case 4:
                if (Read(0xcfc0) != 3) return;
                context.Transitions.BeginNativeWhiteFade(true); State = 5; return;
            case 5:
                if (faded) return;
                var destination = _data.Table("return");
                Load(destination[0], destination[1], "return");
                context.Player.WarpTo(new(destination[3], destination[2]), recordSafe: false);
                context.Player.Face(Vector2I.Down); context.Player.Visible = true;
                context.Sound.PlaySound(SoundId.SndCtrlStopMusic);
                remoteMaku.StartAfterStoneScene();
                context.Hud.ShowStatusBar(); context.Transitions.BeginNativeWhiteFade(false);
                context.Fade.Color = Colors.White;
                State = 6; return;
            case 6:
                if (faded) return;
                Write(WramAddress.wDisabledObjects, 1); Write(WramAddress.wMenuDisabled, 1);
                Stage = PostD7Stage.Inactive;
                _resources.ReleaseFullScreenFade(restoreColor: false);
                return;
            default: throw new InvalidOperationException($"CUTSCENE_TURN_TO_STONE state ${State:x2}.");
        }
    }
    private void LoadStoneRoom()
    {
        switch (Read(0xcfde))
        {
            case 0x0d: Load(1, 0x49, "family"); break;
            case 0x0e: Load(1, 0x84, "rabbit"); break;
            case 0x0f: Load(4, 0xf6, "sparkle"); break;
            default: throw new InvalidOperationException($"Post-D7 objectTable2 index ${Read(0xcfde):x2}.");
        }
        Write(0xcfd1, 0);
    }
    private void Load(int group, int room, string? scene)
    {
        var loaded = context.Rooms.LoadCutsceneRoom(group, room);
        context.Entities.LoadCutsceneRoom(group, loaded, false);
        context.RoomView.SetRoom(loaded.Texture); context.Transitions.ResetCamera();
        context.Player.WarpTo(Vector2.Zero, recordSafe: false); context.Player.Visible = false;
        if (scene is null) return;
        foreach (var placement in _data.Placements(scene))
        {
            if (!context.Entities.InteractionSlotAvailable) return;
            if (placement.Id == 0x8a) continue; // The shared remote owner starts after the global palette gate.
            if (placement.Id == 0xdc) context.Entities.AddEntity(new JabuExteriorActorRoomEntity(
                (byte)placement.SubId, context.Entities.JabuExterior, context.Entities, context.Rooms));
            else if (placement.Id == 0x95) context.Entities.AddEntity(new PostD7BallRoomEntity(context));
            else Actor(placement.Id, placement.SubId, new(placement.X, placement.Y));
        }
    }
    private void Actor(int id, int subId, Vector2 position) =>
        context.Entities.AddEntity(new PostD7ActorRoomEntity(context, _data, this, id, subId, position));
    internal IRoomEntity CreatePlacedAmbi(NpcRecord record) =>
        record is { Group: 4, Room: 0xf6, Id: 0x4d, SubId: 3 }
            ? new PostD7ActorRoomEntity(context, _data, this, record.Id, record.SubId, new(record.X, record.Y))
            : throw new InvalidOperationException($"Post-D7 Ambi placement ${record.Group:x1}:${record.Room:x2}/${record.Id:x2}:${record.SubId:x2}.");
    private void RemoveTower()
    {
        if (_tower is null) return;
        _tower.GetParent()?.RemoveChild(_tower); _tower.QueueFree(); _tower = null;
    }
    public void Cancel()
    {
        bool owned = HasState;
        RemoveTower(); Stage = PostD7Stage.Inactive; State = 0;
        _pendingTower = _pendingStone = false; _scrollY = 0x70; _topLoaded = false;
        SkipsObjectUpdate = false;
        if (!owned) return;
        context.Transitions.CancelNativeWhiteFade();
        _resources.ReleaseFullScreenFade();
        context.Player.Visible = true; context.Hud.ShowStatusBar();
        context.RoomView.SuspendUnderwaterWave(false);
        context.RoomView.ClearBackgroundFade();
        Write(WramAddress.wDisabledObjects, 0); Write(WramAddress.wMenuDisabled, 0);
    }
}

internal enum PostD7Stage { Inactive, Pending, Complete, Tower, Ascent, Stone }
