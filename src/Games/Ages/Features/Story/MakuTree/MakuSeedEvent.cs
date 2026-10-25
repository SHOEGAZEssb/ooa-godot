using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// makuTree.s:@state0e -> subid $06; CUTSCENE_TWINROVA_REVEAL.
internal sealed class MakuSeedEvent : RoomCutsceneCommandHost, IRoomEntryEvent
{
    private readonly MakuSeedDatabase _data = new();
    private readonly CutsceneCommandRunner _runner;
    private readonly HashSet<IRoomEntity> _ownedEntities = [];
    private NpcCharacter? _tree;
    private GroundTreasurePickup? _reward;
    private bool _active, _pressed, _treeEnded, _pendingReveal;
    private int _nativeState = -1, _counter, _flashFrame, _loadedText;
    private int _returnMusic;
    internal bool HasTree => _tree is { Active: true };
    internal int NativeState => _nativeState;
    internal int Counter => _runner.Counter;
    internal int CurrentCommandIndex => _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    public override RoomEventContext Context { get; }
    public bool HasState => _active;
    public bool BlocksGameplay => _active && (EventResources.InputLocked || OwnsGameLogic);
    public bool OwnsGameLogic => _nativeState >= 0;
    public bool MenusDisabled => BlocksGameplay;
    public bool AllScreenTransitionsDisabled => BlocksGameplay;

    internal MakuSeedEvent(RoomEventContext context)
    { Context = context; _runner = new(this); }

    public bool Matches(int group, OracleRoomData room) => group == 0 && room.Id == 0x38 && Context.Rooms.SaveData.MakuTreeState == 0x0e;

    public void Start(OracleRoomData room)
    {
        OracleSaveData save = Context.Rooms.SaveData;
        _active = true;
        NpcCharacter tree = Context.RequireNpc(0, room.Id, 0x87, 0, "INTERAC_MAKU_TREE");
        InitializeTree(tree);
        var treeEntity = new MakuSeedTreeRoomEntity(tree, this);
        Context.Entities.ReplaceEventOwnedNpc(tree, treeEntity);
        _ownedEntities.Add(treeEntity);
        if (save.HasGlobalFlag(AgesGlobalFlag.SawTwinrovaBeforeEndgame))
        {
            _runner.Start(_data.Commands("tree-repeat"));
        }
        else
        {
            Context.Player.Face(Vector2I.Up);
            Context.Player.RequestState08Control(this);
            tree.SetScriptAnimation(_data.Animation(0x87, save.HasGlobalFlag(AgesGlobalFlag.GotMakuSeed) ? 4 : 0));
            _runner.Start(_data.Commands(save.HasGlobalFlag(AgesGlobalFlag.GotMakuSeed) ? "tree-return" : "tree-seed"));
        }
        CreateFlower();
    }

    private void InitializeTree(NpcCharacter tree)
    {
        _tree = tree; _treeEnded = false; _pressed = false;
        if (tree.Record.SubId == 0) tree.AppendScriptGraphics(_data.Maku.Graphics.ExtraSprite);
        tree.SetScriptAnimation(_data.Animation(0x87, 0));
        tree.SetAnimationRate(0);
        tree.SetFixedDrawPriority(ObjectDrawPriority.FixedLowPriorityZIndex);
    }
    private void CreateFlower()
    {
        if (Context.Entities.InteractionSlotAvailable)
            AddOwnedEntity(new MakuSeedFlowerRoomEntity(_data.Maku, this));
    }
    private void AddOwnedEntity(IRoomEntity entity)
    { Context.Entities.AddEntity(entity); _ownedEntities.Add(entity); }
    internal bool TryInteractTree(Player player)
    {
        if (_tree is not { ScriptButtonSensitive: true } || !_tree.CanTalkTo(player)) return false;
        _pressed = true; return true;
    }
    internal void AdvanceTree()
    {
        if (!_active || _tree is not { Active: true }) return;
        if (_runner.Active && !_treeEnded) _runner.AdvanceFrame();
        _tree.AdvanceAnimationUpdates(1);
    }
    public void UpdateFrame() { } // The native INTERACTION slot dispatches the runner.

    internal void AdvanceBeforeObjects()
    {
        if (_pendingReveal)
        {
            _pendingReveal = false; _nativeState = 0;
            EventResources.CaptureFullScreenFade();
        }
        if (_nativeState < 0) return;
        Context.Transitions.AdvanceNativeWhiteFade();
        switch (_nativeState)
        {
            case 0:
                for (int address = 0xcfc0; address < 0xcfe0; address++) Write(address, 0);
                _returnMusic = Context.Sound.ActiveMusic;
                Context.Sound.PlaySound(SoundId.SndCtrlStopMusic);
                _counter = 60; Write(0xcfc0, 2);
                int text = IsLinkedGame ? 0x2815 : 0x2810;
                Context.ShowDialogue(_data.Text(text), sourceTextId: text);
                _nativeState = 1; return;
            case 1:
                if (Context.DialogueOpen || --_counter != 0) return;
                Context.Sound.PlaySound(SoundId.SndCtrlStopMusic);
                _flashFrame = 0; _nativeState = 2; return;
            case 2:
                if (!Flash("reveal-flashes")) return;
                // func_6fb0 clears BG palettes, then the handler replaces only
                // dynamic interactions. objectData77a5 keeps PART $27 first.
                Context.Fade.Color = new Color(Colors.White, 0);
                Context.RoomView.SetBackgroundFade(Colors.Black, 1);
                Context.Hud.HideStatusBar();
                // func_6fb0 sets LYC/SCY=$00: the black room BG covers the status strip.
                Context.Hud.SetHiddenStatusBarFade(Colors.Black, 1);
                Context.Transitions.NativeRoomUsesFullScreen = true;
                Context.Hud.Visible = false;
                Write(AgesWramAddress.wScrollMode, 0);
                Write(AgesWramAddress.wDisabledObjects, 1);
                _tree = null; _runner.Clear(); _treeEnded = true;
                Context.Entities.ClearDynamicInteractions();
                if (IsLinkedGame)
                {
                    Context.Player.Visible = false;
                    if (Context.Entities.InteractionSlotAvailable)
                        AddOwnedEntity(new TwinrovaRevealRoomEntity(Context, _data, 0x93, 4, AddOwnedEntity));
                    _nativeState = 3;
                }
                else
                {
                    if (Context.Entities.PartSlotAvailable)
                        AddOwnedEntity(new LightningRoomEntity(Context, new(0x50, 0x24), signalCutscene: false));
                    if (Context.Entities.InteractionSlotAvailable)
                        AddOwnedEntity(new TwinrovaRevealRoomEntity(Context, _data, 0x8d, 2, AddOwnedEntity));
                    _nativeState = 7;
                }
                return;
            case 3:
                if (Read(0xcfc0) != 3) return;
                Context.Sound.PlaySound(SoundId.SndCtrlStopMusic);
                Context.Sound.PlaySound(SoundId.SndLightning);
                _flashFrame = 0; _nativeState = 4; return;
            case 4:
                if (!Flash("linked-flashes")) return;
                Write(0xcfde, 0x12); LoadRevealRoom(0x12);
                Context.Transitions.NativeRoomUsesFullScreen = false;
                Context.Hud.ShowStatusBar();
                Context.Sound.PlaySound(SoundId.MusZeldaSaved);
                _nativeState = 5; return;
            case 5:
                if (Read(0xcfdf) != 0xff) return;
                Context.Sound.PlaySound(SoundId.SndLightning);
                Context.Sound.PlaySound(SoundId.SndCtrlStopMusic);
                _flashFrame = 0; _nativeState = 6; return;
            case 6:
                if (!Flash("reveal-flashes")) return;
                Write(0xcfde, Read(0xcfde) + 1); LoadRevealRoom(Read(0xcfde));
                Context.Player.SetNativeCutsceneCoordinates(0x68, 0x50);
                Context.Player.Face(Vector2I.Up); Context.Player.Visible = true;
                Context.Transitions.CancelNativeWhiteFade();
                Write(AgesWramAddress.wDisabledObjects, 1); Write(AgesWramAddress.wMenuDisabled, 1);
                Context.Hud.HideStatusBar(); Context.RoomView.SetBackgroundFade(Colors.Black, 1);
                Context.Hud.SetHiddenStatusBarFade(Colors.Black, 1);
                Context.Transitions.NativeRoomUsesFullScreen = true;
                Context.Hud.Visible = false;
                Write(AgesWramAddress.wScrollMode, 0);
                _nativeState = 7; return;
            case 7:
                if (Read(0xcfd0) != 9) return;
                Context.Sound.PlaySound(SoundId.SndCtrlFastFadeOut);
                Context.Transitions.BeginNativeBlackFade(true);
                _nativeState = 8; return;
            case 8:
                if (Context.Transitions.NativePaletteFadeActive) return;
                Context.Transitions.NativeRoomUsesFullScreen = false;
                Context.Hud.ShowStatusBar();
                Context.RoomView.ClearBackgroundFade();
                Write(AgesWramAddress.wScrollMode, 1);
                Context.Rooms.SaveData.SetGlobalFlag(AgesGlobalFlag.GotMakuSeed);
                Context.Transitions.BeginNativeBlackFade(false);
                NpcCharacter returned = _data.CreateActor(0x87, 6, new(0x50, 0x40));
                InitializeTree(returned);
                returned.SetScriptAnimation(_data.Animation(0x87, 4));
                if (!Context.Entities.InteractionSlotAvailable)
                    throw new NotSupportedException("CUTSCENE_TWINROVA_REVEAL state $08: unchecked $87:$06 allocation has no free INTERACTION slot.");
                AddOwnedEntity(new MakuSeedTreeRoomEntity(returned, this));
                CreateFlower();
                _runner.Start(_data.Commands("tree-return"));
                Context.Sound.PlaySound(_returnMusic);
                _nativeState = 9; return;
            case 9:
                if (Read(0xcfc0) != 4) return;
                _nativeState = -1; EventResources.ReleaseFullScreenFade(); return;
        }
    }
    private bool Flash(string profile)
    {
        int[] flashes = _data.Table(profile);
        _flashFrame++;
        int index = 0;
        while (flashes[index] < _flashFrame) index++;
        if (flashes[index] == 0xff) return true;
        Context.Fade.Color = new Color(Colors.White, (index & 1) == 0 ? 1 : 0);
        return false;
    }
    private void LoadRevealRoom(int index)
    {
        var row = _data.Room(index);
        _ownedEntities.Clear();
        var room = Context.Rooms.LoadCutsceneRoom(row.Group, row.Room);
        Context.Entities.LoadCutsceneRoom(row.Group, room, includeTimePortals: false);
        Context.RoomView.SetRoom(room.Texture); Context.RoomView.ClearBackgroundFade();
        Context.Transitions.ResetCamera();
        _reward = null;
        // disableLcdAndLoadRoom clears the object/menu mask before parsing the set.
        Write(AgesWramAddress.wDisabledObjects, 0); Write(AgesWramAddress.wMenuDisabled, 0);
        foreach (var actor in _data.Placements(row.Scene))
        {
            if (!Context.Entities.InteractionSlotAvailable) continue;
            AddOwnedEntity(actor.Id == 0x93
                ? new TwinrovaRevealRoomEntity(Context, _data, actor.Id, actor.SubId, AddOwnedEntity)
                : new MakuRevealSceneActor(Context, _data, actor.Id, actor.SubId, actor.Position));
        }
        Write(0xcfd1, 0);
        Context.Transitions.BeginNativeWhiteFade(false);
    }
    private byte Read(int address) => Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => Context.Entities.RuntimeState.SetWramByte(address, (byte)value);
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Actor" && HasTree;
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation)
    { _tree!.SetScriptAnimation(encodedAnimation); }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    { bool pressed = _pressed; _pressed = false; return pressed; }
    public override void SetActorButtonSensitive(string actor) => _tree!.SetScriptButtonSensitive(true);
    public override void InitializeActorCollisionRadii(string actor) => _tree!.InitializeCollisionRadii();
    public override void SetActorCollisionRadii(string actor, int y, int x) => _tree!.SetCollisionRadii(y, x);
    public override bool MemoryEquals(string binding, int value) => binding == "SeedSignal" ? Read(0xcfc0) == value : throw UnsupportedCommand(binding);
    public override bool GateOpen(string gate) => gate == "PaletteDone" ? !Context.Transitions.NativePaletteFadeActive : throw UnsupportedCommand(gate);
    public override void WriteMemory(string binding, int value)
    {
        switch (binding)
        {
            case "CutsceneTrigger":
                if (value != 0x0e) throw UnsupportedCommand($"cutscene ${value:x2}");
                _pendingReveal = true; break;
            case "SeedSignal": Write(0xcfc0, value); break;
            case "MakuMapText": Context.Rooms.SaveData.SetMakuMapTextPresent(value); break;
            default: throw UnsupportedCommand(binding);
        }
    }
    public override void ShowText(int id, string message) => Context.ShowDialogue(message, 2, sourceTextId: id);
    public override void ShowText(int id, string message, int? position) => Context.ShowDialogue(message, position, sourceTextId: id);
    public override void ShowLoadedText() => Context.ShowDialogue(_data.Text(_loadedText), 2, sourceTextId: _loadedText);
    public override void GiveItem(int treasureId, int parameter)
    {
        if (treasureId != TreasureId.MakuSeed || parameter != 0) throw UnsupportedCommand($"treasure ${treasureId:x2}:${parameter:x2}");
        if (!Context.Entities.InteractionSlotAvailable) return;
        var treasure = Context.GrantScriptTreasure(0, 0x38, treasureId, parameter, "TREASURE_OBJECT_MAKU_SEED_00",
            "scriptHelper.s:makuTree_subid06Script_part1_body", nativeDispatch: true);
        _reward = treasure;
        treasure.SetSpritePaletteOverrides(new Dictionary<int, Color[]> { [7] = _data.SeedPalette });
        _ownedEntities.Add(Context.Entities.EntityAdapters<IRoomEntity>().Single(entity => ReferenceEquals(entity.PresentationNode, treasure)));
        _runner.WaitForNativeCounter(3);
    }
    public override void SetInputEnabled(bool enabled)
    {
        base.SetInputEnabled(enabled);
        Write(AgesWramAddress.wDisabledObjects, enabled ? 0 : 0x81);
        Write(AgesWramAddress.wMenuDisabled, enabled ? 0 : 0x80);
    }
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "SpawnSeed":
                if (Context.Entities.InteractionSlotAvailable)
                    AddOwnedEntity(new MakuSeedFallingRoomEntity(Context, _data, AddOwnedEntity));
                break;
            case "ChooseText": _loadedText = IsLinkedGame ? 0x055f : 0x055b; break;
            case "LoadRepeatText": _loadedText = _data.Table("repeat-text")[IsLinkedGame ? 1 : 0]; break;
            case "ShowLoadedText": ShowLoadedText(); break;
            case "NextText": _loadedText++; break;
            case "IncMakuState": Context.Rooms.SaveData.SetMakuTreeState(Math.Min(0x10, Context.Rooms.SaveData.MakuTreeState + 1)); break;
            default: throw UnsupportedCommand(handler);
        }
    }
    public override void ScriptEnded() => _treeEnded = true;
    public void ReleaseOutgoingActors(int group, OracleRoomData room)
    { if (_active && (group != 0 || room.Id != 0x38)) Cancel(); }
    public void Cancel()
    {
        if (!_active) return;
        bool native = OwnsGameLogic;
        _active = false; _pendingReveal = false; _nativeState = -1;
        _runner.Clear();
        if (_reward is not null && GodotObject.IsInstanceValid(_reward)) _reward.Finish(Context.Player);
        _reward = null;
        Context.Player.CancelGetItemState();
        if (native) Context.Player.Visible = true;
        if (_tree is not null && GodotObject.IsInstanceValid(_tree)) _tree.SetActive(false);
        _tree = null;
        foreach (IRoomEntity entity in _ownedEntities) Context.Entities.RetireEntity(entity);
        _ownedEntities.Clear();
        if (EventResources.InputLocked) SetInputEnabled(true);
        Context.Transitions.CancelNativeWhiteFade();
        Context.Transitions.NativeRoomUsesFullScreen = false;
        Context.Hud.ShowStatusBar(); Context.RoomView.ClearBackgroundFade();
        EventResources.ReleaseFullScreenFade();
        if (native) Context.Sound.PlayRoomMusic(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, Context.Rooms.SaveData);
    }
}
