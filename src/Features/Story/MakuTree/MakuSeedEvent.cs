using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// makuTree.s:@state0e -> subid $06; unlinked CUTSCENE_TWINROVA_REVEAL.
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
        if (save.IsLinkedGame && !save.HasGlobalFlag(GlobalFlag.GotMakuSeed) && !save.HasGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame))
            throw new NotSupportedException("makuTree.s:@state0e / CUTSCENE_TWINROVA_REVEAL: the linked Zelda kidnapping scene is not implemented.");
        _active = true;
        NpcCharacter tree = Context.RequireNpc(0, room.Id, 0x87, 0, "INTERAC_MAKU_TREE");
        InitializeTree(tree);
        var treeEntity = new MakuSeedTreeRoomEntity(tree, this);
        Context.Entities.ReplaceEventOwnedNpc(tree, treeEntity);
        _ownedEntities.Add(treeEntity);
        if (save.HasGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame))
        {
            _runner.Start(_data.Commands("tree-repeat"));
        }
        else
        {
            Context.Player.Face(Vector2I.Up);
            Context.Player.RequestState08Control(this);
            tree.SetScriptAnimation(_data.Animation(0x87, save.HasGlobalFlag(GlobalFlag.GotMakuSeed) ? 4 : 0));
            _runner.Start(_data.Commands(save.HasGlobalFlag(GlobalFlag.GotMakuSeed) ? "tree-return" : "tree-seed"));
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
                Context.ShowDialogue(_data.Text(0x2810), sourceTextId: 0x2810);
                _nativeState = 1; return;
            case 1:
                if (Context.DialogueOpen || --_counter != 0) return;
                Context.Sound.PlaySound(SoundId.SndCtrlStopMusic);
                _flashFrame = 0; _nativeState = 2; return;
            case 2:
                int[] flashes = _data.Table("reveal-flashes");
                _flashFrame++;
                int index = 0;
                while (flashes[index] < _flashFrame) index++;
                if (flashes[index] != 0xff)
                {
                    Context.Fade.Color = new Color(Colors.White, (index & 1) == 0 ? 1 : 0);
                    return;
                }
                // func_6fb0 clears BG palettes, then the handler replaces only
                // dynamic interactions. objectData77a5 keeps PART $27 first.
                Context.Fade.Color = new Color(Colors.White, 0);
                Context.RoomView.SetBackgroundFade(Colors.Black, 1);
                Context.Hud.HideStatusBar();
                Write(WramAddress.wDisabledObjects, 1);
                _tree = null; _runner.Clear(); _treeEnded = true;
                Context.Entities.ClearDynamicInteractions();
                if (Context.Entities.PartSlotAvailable)
                    AddOwnedEntity(new LightningRoomEntity(Context, new(0x50, 0x24), signalCutscene: false));
                if (Context.Entities.InteractionSlotAvailable)
                    AddOwnedEntity(new TwinrovaRevealRoomEntity(Context, _data, 0x8d, 2, AddOwnedEntity));
                _nativeState = 7; return;
            case 7:
                if (Read(0xcfd0) != 9) return;
                Context.Sound.PlaySound(SoundId.SndCtrlFastFadeOut);
                Context.Transitions.BeginNativeBlackFade(true);
                _nativeState = 8; return;
            case 8:
                if (Context.Transitions.NativePaletteFadeActive) return;
                Context.Hud.ShowStatusBar();
                Context.RoomView.ClearBackgroundFade();
                Context.Rooms.SaveData.SetGlobalFlag(GlobalFlag.GotMakuSeed);
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
        Write(WramAddress.wDisabledObjects, enabled ? 0 : 0x81);
        Write(WramAddress.wMenuDisabled, enabled ? 0 : 0x80);
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
        if (_tree is not null && GodotObject.IsInstanceValid(_tree)) _tree.SetActive(false);
        _tree = null;
        foreach (IRoomEntity entity in _ownedEntities) Context.Entities.RetireEntity(entity);
        _ownedEntities.Clear();
        if (EventResources.InputLocked) SetInputEnabled(true);
        Context.Transitions.CancelNativeWhiteFade();
        Context.Hud.ShowStatusBar(); Context.RoomView.ClearBackgroundFade();
        EventResources.ReleaseFullScreenFade();
        if (native) Context.Sound.PlayRoomMusic(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, Context.Rooms.SaveData);
    }
}
