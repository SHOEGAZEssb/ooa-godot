using Godot;
using System;

namespace oracleofages;

// oldMan.s / bookOfSealsPodium.s / decoration.s. Scripts and native tails
// advance once per original interaction slot, including dynamically allocated books.
internal sealed class LibraryScriptHost : RoomCutsceneCommandHost
{
    private readonly LibraryEvent _owner;
    private readonly LibraryActorRoomEntity _slot;
    private readonly CutsceneCommandRunner _runner;
    private bool _initialized, _childrenSpawned, _ended, _secretPending, _fading;
    private int _pathIndex, _pathTile, _counter, _secretResult, _generation;
    private string _secret = "";
    internal NpcCharacter Npc { get; }
    internal int State { get; private set; }
    internal int Counter => Npc.Record.Id == InteractionId.BookOfSealsPodium ? _counter : _runner.Counter;
    internal bool ButtonSensitive { get; private set; }
    internal bool ButtonPressed { get; set; }
    public override RoomEventContext Context => _owner.Context;
    public override bool DialogueOpen => Context.DialogueOpen || _secretPending;
    public override bool ScriptExecutionBlocked => base.ScriptExecutionBlocked || _secretPending;
    private LibraryDatabase Data => _owner.Database;
    private int SubId => Npc.Record.SubId;
    internal LibraryScriptHost(LibraryEvent owner, LibraryActorRoomEntity slot)
    {
        _owner = owner; _slot = slot; Npc = slot.Npc; _runner = new(this);
        if (slot.NativeInitialized) Initialize(runInitialUpdate: false);
    }
    internal void InitializeAfterPreload()
    {
        if (!_initialized) Initialize(runInitialUpdate: false);
    }
    internal void AdvanceFrame(bool scrolling = false)
    {
        if (!Npc.Active) return;
        if (!_initialized) { Initialize(runInitialUpdate: true); return; }
        if (_fading) Context.Transitions.AdvanceNativeWhiteFade();
        if (Npc.Record.Id == InteractionId.Decoration)
        {
            if (RoomFlagSet(0x20)) Npc.SetActive(false);
            return; // decoration subids $07/$08 deliberately do not animate.
        }
        Npc.ClearFixedDrawPriority();
        if (Npc.Record.Id == InteractionId.BookOfSealsPodium) { AdvancePodium(scrolling); return; }
        // $52:$02 animates before the script; the generic library actors
        // animate afterwards. $00 uses npcFaceLinkAndAnimate afterwards.
        if (SubId == 2) Npc.AnimateAsNpcOneUpdate(Context.Player);
        _runner.AdvanceFrame();
        if (SubId == 0)
        {
            if (_ended) Npc.SetActive(false);
            else Npc.FaceLinkAndAnimateOneUpdate(Context.Player);
        }
        else if (SubId == 1 && _ended) Npc.SetActive(false);
        else if (SubId != 2) Npc.AnimateAsNpcOneUpdate(Context.Player);
        if (SubId == 2 && _ended) WarpToLibrary();
    }
    private void Initialize(bool runInitialUpdate)
    {
        _slot.InitializeNative();
        _initialized = true; State = _slot.InitialState;
        if (!Npc.Active || Npc.Record.Id == InteractionId.Decoration) return;
        if (Npc.Record.Id == InteractionId.BookOfSealsPodium)
        {
            SetActorButtonSensitive("Library");
            if (State == 2) _runner.Start(Data.Podium);
            return;
        }
        if (SubId >= 3)
        {
            _runner.Start(Data.Generic);
            return; // State zero installs the script without running it.
        }
        _runner.Start(SubId == 0 ? Data.Secret : SubId == 1 ? Data.Book : Data.Powder);
        if (!runInitialUpdate) { SetActorButtonSensitive("Library"); return; }
        Npc.ClearFixedDrawPriority();
        if (SubId == 2) Npc.AnimateAsNpcOneUpdate(Context.Player);
        _runner.AdvanceFrame();
        if (SubId == 0)
        {
            if (_ended) Npc.SetActive(false);
            else Npc.FaceLinkAndAnimateOneUpdate(Context.Player);
        }
        else if (SubId == 1)
        {
            if (_ended) Npc.SetActive(false);
            else Npc.AnimateAsNpcOneUpdate(Context.Player);
        }
    }
    private void AdvancePodium(bool scrolling)
    {
        if (State is 1 or 2 && !_childrenSpawned && !scrolling)
        {
            _childrenSpawned = true;
            if (SubId == 0)
                for (int i = 5; i > 0 && Context.Entities.InteractionSlotAvailable; i--)
                    Context.Entities.Spawn(new LibraryPodiumSpawn(_owner, i));
        }
        if (State == 1)
        {
            Npc.SetScriptVisible(true);
            Npc.UpdateDrawPriority(Context.Player.Position, withTerrainEffects: true);
            if (ButtonPressed) ActivateBook();
        }
        else if (State == 2)
        {
            _runner.AdvanceFrame();
            if (!_ended) return;
            Npc.SetScriptVisible(true);
            Npc.UpdateDrawPriority(Context.Player.Position, withTerrainEffects: true);
            Context.Sound.PlaySound(SoundId.SndSolvePuzzle);
            Context.Inventory.LoseTreasure(TreasureId.BookOfSeals);
            ActivateBook();
        }
        else if (State == 3)
        {
            if (Context.DialogueOpen) return;
            if (ReplacePath()) { _pathTile = 0xf4; _pathIndex = 0; }
        }
        else if (State == 4)
        {
            Npc.UpdateDrawPriority(Context.Player.Position, withTerrainEffects: true);
            if (ButtonPressed)
            {
                ButtonPressed = false;
                var p = Data.Path(SubId);
                Context.ShowDialogue(p.Message, sourceTextId: p.TextId);
                return;
            }
            if (Context.Entities.RuntimeState.ReadWramByte(0xcfd0) == SubId || Context.DialogueOpen) return;
            if (ReplacePath())
            {
                Npc.SetScriptAnimation(Data.Animation(InteractionId.BookOfSealsPodium, 0));
                State = 1; ButtonPressed = false;
            }
        }
    }
    private void ActivateBook()
    {
        Npc.SetScriptAnimation(Data.Animation(InteractionId.BookOfSealsPodium, 1));
        Context.Entities.RuntimeState.SetWramByte(0xcfd0, (byte)SubId);
        var p = Data.Path(SubId);
        Context.ShowDialogue(p.Message, sourceTextId: p.TextId);
        SetInputEnabled(false);
        Context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 0x81);
        _pathTile = 0xd3; _pathIndex = 0; _counter = 2; State = 3;
    }
    // The counter decrements as a byte. A queue-full setTile retains the
    // pointer, and the terminator takes its own two-update interval.
    private bool ReplacePath()
    {
        _owner.SetInputEnabled(false);
        Context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0x81);
        _counter = (_counter - 1) & 0xff;
        if (_counter != 0) return false;
        _counter = 2;
        byte position = Data.Path(SubId).Path[_pathIndex];
        if (position != 0)
        {
            if (Context.Rooms.TrySetTile(position, (byte)_pathTile)) _pathIndex++;
            return false;
        }
        SetInputEnabled(true);
        ButtonPressed = false;
        State++;
        return true;
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Library";
    public override void InitializeActorCollisionRadii(string actor) => Npc.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string actor) { ButtonSensitive = true; Npc.SetScriptButtonSensitive(true); }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = ButtonPressed; ButtonPressed = false;
        if (pressed) Context.Player.ApplyObjectInteractionGrace();
        return pressed;
    }
    public override void SetInputEnabled(bool enabled)
    {
        _owner.SetInputEnabled(enabled);
        Context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, (byte)(enabled ? 0 : 0x81));
        SetMenuEnabled(enabled);
    }
    public override void SetMenuEnabled(bool enabled) => Context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, (byte)(enabled ? 0 : 0x80));
    public override bool RoomFlagSet(int flag) => Context.Rooms.SaveData.HasRoomFlag(Npc.Record.Group, Npc.Record.Room, (byte)flag);
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(Npc.Record.Group, Npc.Record.Room, (byte)flag);
    public override bool MemoryEquals(string binding, int value) => binding switch
    {
        "Book" => (Context.Inventory.HasTreasure(TreasureId.BookOfSeals) ? 1 : 0) == value,
        "SecretResult" => _secretResult == value,
        "PaletteBusy" => (Context.Transitions.NativePaletteFadeActive ? 1 : 0) == value,
        _ when binding.StartsWith("Global:", StringComparison.Ordinal) => (Context.Rooms.SaveData.HasGlobalFlag(int.Parse(binding.AsSpan(7))) ? 1 : 0) == value,
        _ => throw UnsupportedCommand(binding)
    };
    public override bool TextOptionEquals(int value) => EventResources.RequireDialogueChoice("Library script has no completed choice.") == value;
    public override void ShowLoadedText() => Context.ShowDialogue(Npc.Message, Npc.TextPosition, sourceTextId: Npc.TextId);
    public override void ShowText(int id, string message, int? position)
    {
        message = message.Replace("\\secret1", _secret, StringComparison.Ordinal);
        if (id is 0x3310 or 0x1212) Context.ShowChoiceDialogue(message, textboxPosition: position);
        else Context.ShowDialogue(message, position, sourceTextId: id);
    }
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => Npc.SetScriptAnimation(encodedAnimation);
    public override void GiveItem(int treasure, int parameter)
    {
        if (parameter != 0 || (SubId == 1 ? treasure != TreasureId.BookOfSeals : treasure != TreasureId.FairyPowder))
            throw UnsupportedCommand($"giveitem ${treasure:x2}:${parameter:x2}");
        // scriptCmd_spawnItem consumes $de even when allocation fails. It
        // initializes counter1=$03 only after acquiring the treasure slot.
        if (!Context.Entities.InteractionSlotAvailable) return;
        bool book = treasure == TreasureId.BookOfSeals;
        Context.GrantScriptTreasure(Npc.Record.Group, Npc.Record.Room, treasure, parameter,
            book ? "TREASURE_OBJECT_BOOK_OF_SEALS_00" : "TREASURE_OBJECT_FAIRY_POWDER_00",
            book ? "scriptHelper.s:oldManScript_givesBookOfSeals" : "scriptHelper.s:oldManScript_givesFairyPowder", nativeDispatch: true);
        WaitForReward();
    }
    private void WaitForReward() => _runner.WaitForNativeCounter(3);
    public override void ScriptEnded() => _ended = true;
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "EnableMenu": SetMenuEnabled(true); break;
            case "FaceLink":
                int direction = (OracleObjectMovement.Shared.RelativeAngle(Npc.Position, Context.Player.Position) + 4) & 0x18;
                Npc.SetScriptAnimation(Data.Animation(InteractionId.OldMan, direction / 8));
                break;
            case "oldManSetAnimationToVar38": Npc.SetScriptAnimation(Data.Animation(InteractionId.OldMan, Data.Npc(SubId).Animation)); break;
            case "oldManWarpLinkToLibrary": WarpToLibrary(); break;
            case "AskSecret":
                _secretPending = true; int generation = _generation;
                if (_owner.OpenSecretMenu is null || !_owner.OpenSecretMenu(4, valid =>
                    { if (generation != _generation) return; _secretResult = valid ? 0 : 1; _secretPending = false; }))
                    throw UnsupportedCommand("acquire MENU_SECRET for LIBRARY_SECRET $04");
                break;
            case "GenerateSecret": _secret = new LinkedGameNpcDatabase().GenerateSecret(0x14, Context.Rooms.SaveData); break;
            case "oldManGiveShieldUpgradeToLink":
                if (!Context.Entities.InteractionSlotAvailable) break;
                int level = Context.Inventory.HasTreasure(TreasureId.Shield) ? Context.Inventory.ShieldLevel : 0;
                if (level >= 3) level = 2;
                Context.GrantScriptTreasure(Npc.Record.Group, Npc.Record.Room, TreasureId.Shield, level,
                    $"TREASURE_OBJECT_SHIELD_{level:x2}", "scriptHelper.s:oldManGiveShieldUpgradeToLink",
                    objectParameter: level + 1, nativeDispatch: true);
                break;
            case "createSparkle": Context.Entities.TryCreateOwlSparkle(new(Npc.Position, new OwlStatueDatabase().Record(0).Sparkle)); break;
            case "func_50e4": Context.Entities.CreateEnergySwirl(Context.Player.Position, 0x6e); break;
            case "fadeoutToWhite": BeginFade(true, 1); break;
            case "fadeinFromWhiteWithDelay, $04": BeginFade(false, 4); break;
            default: throw UnsupportedCommand(handler);
        }
    }
    private void BeginFade(bool fadeOut, int delay)
    {
        EventResources.CaptureFullScreenFade(); _fading = true;
        Context.Transitions.BeginNativeWhiteFade(fadeOut, delay);
    }
    private void WarpToLibrary()
    {
        _ended = false;
        Context.Sound.PlaySound(SoundId.SndTeleport);
        Context.Entities.OnRoomWarpRequested(new(Npc.Record.Group, Npc.Record.Room, -1, 0,
            WarpSourceTransition.FadeOut, 5, 0xec, 0x17, 0, 3, DirectFadeOut: true));
    }
    internal void Cancel()
    {
        _generation++; _secretPending = false; _runner.Clear();
        if (_fading) { Context.Transitions.CancelNativeWhiteFade(); EventResources.ReleaseFullScreenFade(); }
        Npc.SetScriptButtonSensitive(false);
    }
}
