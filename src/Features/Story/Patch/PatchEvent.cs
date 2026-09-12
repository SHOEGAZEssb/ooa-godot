using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>INTERAC_PATCH $94: each native slot runs after the preceding slot's writes.</summary>
internal sealed class PatchEvent : InteractiveCutsceneCommandHost, IRoomEntryEvent,
    ICutsceneCommandHost, IUpdatesDuringDialogueRoomEvent
{
    internal PatchDatabase Database { get; } = new();
    public override RoomEventContext Context { get; }
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _patch;
    private NpcCharacter? _cart;
    private NpcCharacter? _broken;
    private NpcCharacter? _fixed;
    private Vector2 _patchPosition;
    private Vector2 _cartPosition;
    private int _z, _speedZ, _var38;
    private bool _buttonSensitive, _buttonPressed, _upstairs;
    private int _cartState, _cartAngle, _switchState, _brokenState;
    private int _managerState, _managerCounter, _extraPending, _extraSpawned, _wonSignal;
    private int _fixedCounter;
    private int _pendingHoleEvents;
    private int _fadeDirection, _fadeOffset, _fadeCounter, _fadeDelay;
    private readonly List<HardhatBeetleCharacter> _beetles = new();
    private readonly List<GroundTreasurePickup> _rewards = new();
    private OracleRoomData? _room;
    private bool _stairsClosed;
    public bool HasState => _patch is not null || _cart is not null;
    public bool BlocksGameplay => InputControlHeld;
    public bool MenusDisabled => InputControlHeld;
    public bool ScreenTransitionsDisabled => InputControlHeld;
    internal bool FreezesObjects => State == 3;
    internal int State { get; private set; }
    internal int CartState => _cartState;
    internal int CartAngle => _cartAngle;
    internal int BeetlesRemaining => _managerCounter;
    internal int ManagerState => _managerState;
    internal int ScriptIndex => _runner.Instruction;
    internal int ScriptCounter => _runner.Counter;
    internal bool Fading => _fadeDirection != 0;
    private byte Read(int address) => Context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => Context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    internal PatchEvent(RoomEventContext context) { Context = context; _runner = new(this); }
    public bool Matches(int group, OracleRoomData room) => Context.Entities.EntityAdapters<PatchRoomEntity>().Any();
    public void Start(OracleRoomData room)
    {
        Cancel();
        _room = room;
        foreach (var entity in Context.Entities.EntityAdapters<PatchRoomEntity>())
        {
            entity.Bind(this);
            entity.Npc.SetAnimationRate(0);
            entity.Npc.SetScriptButtonSensitive(false);
            if (entity.Npc.Record.SubId == 2) _cart = entity.Npc;
            else _patch = entity.Npc;
        }
        _upstairs = _patch!.Record.SubId == 0;
        _patchPosition = _patch.Position;
        State = 1;
        if (_upstairs)
        {
            // State 0 repairs the in-progress inventory bytes even when Patch deletes himself.
            if (Context.Inventory.TuniNutState == 1) Context.Inventory.SetRestorationItemState(TreasureVariable.TuniNutState, 0);
            if (Context.Inventory.TradeItem == 0x0c) Context.Inventory.SetRestorationItemState(TreasureVariable.TradeItem, 0x0b);
            if (Read(0xcfd2) == 1) { _patch.SetActive(false); _patch = null; return; }
            if (Context.Rooms.SaveData.HasGlobalFlag(Database.Constant("repaired-flag")))
                StartScript("upstairsRepairedEverythingScript");
            else if (Context.Inventory.HasTreasure(TreasureDatabase.TreasureTradeItem) && Context.Inventory.TradeItem == 0x0b)
            {
                Write(0xcfd7, 0x13); Write(0xcfd0, 1); Write(0xcfd1, Context.Inventory.SwordLevel & 1);
                StartScript("upstairsRepairSwordScript_body");
            }
            else
            {
                Write(0xcfd7, 0x12); Write(0xcfd0, 0);
                _var38 = Context.Inventory.HasTreasure(TreasureDatabase.TreasureTuniNut) && Context.Inventory.TuniNutState == 0 ? 0 : 1;
                StartScript("upstairsRepairTuniNutScript");
            }
        }
        else
        {
            Context.Entities.ObjectFellInHole += OnObjectFellInHole;
            _cartPosition = _cart!.Position; _cartAngle = 8; _cartState = 1;
            _cart.SetFixedDrawPriority(NpcCharacter.BehindLinkZIndex);
            Write(OracleRuntimeState.SwitchStateAddress, Context.Entities.ActiveTriggers);
            _switchState = Context.Entities.ActiveTriggers;
            if (Read(0xcfd2) == 0) { _patch.SetActive(false); _patch = null; return; }
            InitializeDownstairs();
        }
    }
    private void InitializeDownstairs()
    {
        State = Read(0xcfd3) != 0 ? 4 : 1;
        if (State == 1)
        {
            Write(0xcfd4, 0); Write(0xcfd5, 0); Write(0xcfd6, 0);
            Write(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress, 1);
        }
        StartScript(State == 4 ? "downstairsAfterBeatingMinigameScript" : "downstairsScript_body");
    }
    private void StartScript(string name)
    {
        _runner.Start(Database.Commands, Database.Entry(name));
        _runner.SetInitialMotionRegisters("Patch", Database.Constant("speed"), 0);
    }
    public void UpdateFrame()
    {
        UpdateFade();
        // updateInteractions selects its dispatcher once before the slot pass.
        // A textbox opened by Patch does not retroactively freeze later slots.
        bool ordinary = !Context.DialogueOpen && !Context.Player.IsUsingHarp;
        if (_patch is not null)
        {
            if (_upstairs) UpdateUpstairs(); else UpdateDownstairs();
        }
        // The cart is an ordinary interaction; Patch sets the always-update bit.
        if (ordinary)
        {
            UpdateCart();
            UpdateSwitch();
            UpdateManager();
            UpdateBrokenItem();
            UpdateFixedItem();
        }
        // Enemy-created $0f slots follow the manager. Their state-0 writes
        // become visible to the manager on its next update, including text.
        while (_pendingHoleEvents > 0)
        {
            _pendingHoleEvents--;
            for (int address = 0xcfd8; address < 0xcfe0; address += 2)
            {
                if (Read(address) != 0xff) continue;
                Write(address, 0x80); Write(address + 1, 0x5f); break;
            }
        }
    }
    private void OnObjectFellInHole(ObjectFellInHoleKind kind)
    {
        if (kind == ObjectFellInHoleKind.HarmlessHardhatBeetle) _pendingHoleEvents++;
    }
    public void UpdateDuringDialogueFrame() => UpdateFrame();
    private void UpdateUpstairs()
    {
        if (State == 1)
        {
            bool landed = OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20);
            _patch!.SetScriptDrawOffset(new Vector2(0, _z >> 8));
            if (!landed) return;
            _runner.AdvanceFrame();
            if (_runner.Active) _patch.FaceLinkAndAnimateOneUpdate(Context.Player);
            else { State = 2; StartScript("upstairsMoveToStaircaseScript"); }
        }
        else
        {
            _runner.AdvanceFrame();
            if (_runner.Active) _patch!.AdvanceAnimationUpdates(1);
            else
            {
                ReleaseInputControl(); Write(0xcfd2, 1);
                _patch!.SetActive(false); _patch = null;
            }
        }
    }
    private void UpdateDownstairs()
    {
        switch (State)
        {
            case 1:
                if (Fading) return;
                _runner.AdvanceFrame();
                if (_runner.Active) { _patch!.FaceLinkAndAnimateOneUpdate(Context.Player); return; }
                Write(0xcfd4, 1);
                Context.Sound.PlaySound(Database.Constant("whistle"));
                Context.Sound.PlaySound(0x2d); // MUS_MINIBOSS
                _managerState = 1; _managerCounter = Database.Constant("beetle-delay");
                _extraPending = _extraSpawned = 0;
                ClearHoleBuffer();
                Context.Inventory.SetRestorationItemState(Read(0xcfd0) == 0 ? TreasureVariable.TuniNutState : TreasureVariable.TradeItem,
                    Read(0xcfd0) == 0 ? 1 : 0x0c);
                SetAnimation(6); State = 2; _wonSignal = 0;
                StartScript("duringMinigameScript");
                goto case 2;
            case 2:
                if (Read(0xcfd5) != 0)
                {
                    if (!Context.Player.PatchCollisionsEnabled || MenusDisabled) return;
                    SetInputEnabled(false); State = 5; BeginFade(1, 4); return;
                }
                if (_wonSignal != 0)
                {
                    Write(0xcfd4, 0);
                    Context.Player.ClearInteractionKnockback();
                    if (!Context.Player.PatchCollisionsEnabled || MenusDisabled || Context.Player.InvincibilityFrames != 0) return;
                    SetInputEnabled(false);
                    _fixed = SpawnVisual(Read(0xcfd0) + 6, Vector2.Zero);
                    _fixed.Visible = false; _fixedCounter = -1;
                    State = 3; StartScript("linkWonMinigameScript");
                    Context.Sound.PlaySound(Database.Constant("solve-sound")); RestoreMusic(); return;
                }
                _runner.AdvanceFrame(); _patch!.AnimateAsNpcOneUpdate(Context.Player); return;
            case 3:
                if (Fading || Context.DialogueOpen) { _patch!.AdvanceAnimationUpdates(1); return; }
                _runner.AdvanceFrame();
                if (!_runner.Active)
                {
                    if (Read(0xcfd0) != 0) Context.Rooms.SaveData.SetGlobalFlag(Database.Constant("repaired-flag"));
                    State = 4; StartScript("downstairsAfterBeatingMinigameScript");
                }
                else _patch!.FaceLinkAndAnimateOneUpdate(Context.Player);
                return;
            case 4:
                _runner.AdvanceFrame(); _patch!.FaceLinkAndAnimateOneUpdate(Context.Player); return;
            case 5:
                if (Fading) return;
                foreach (var beetle in _beetles) if (GodotObject.IsInstanceValid(beetle)) beetle.DeleteForRoomEvent();
                _beetles.Clear();
                Context.Inventory.SetRestorationItemState(Read(0xcfd0) == 0 ? TreasureVariable.TuniNutState : TreasureVariable.TradeItem,
                    Read(0xcfd0) == 0 ? 0 : 0x0b);
                State = 6; RestoreMusic(); StartScript("linkFailedMinigameScript"); return;
            case 6:
                _runner.AdvanceFrame();
                if (!_runner.Active) State = 0;
                _patch!.FaceLinkAndAnimateOneUpdate(Context.Player); return;
            case 0: InitializeDownstairs(); return;
        }
    }
    private void UpdateCart()
    {
        if (_cart is null) return;
        Write(OracleRuntimeState.SwitchStateAddress, Context.Entities.ActiveTriggers);
        switch (_cartState)
        {
            case 1:
                if (Read(0xcfd4) == 0) return;
                _broken = SpawnVisual(Read(0xcfd0) + 4, new Vector2(0x78, 0x18));
                _brokenState = 0; _cartState = 2; return;
            case 2:
                if (Read(0xcfd4) == 0) { _cartState = 3; return; }
                if (Read(0xcfd5) != 0)
                {
                    if (Read(0xcfd6) == 0) return;
                    _cart.Position = _cartPosition = new Vector2(0x68, 8); _cartState = 3; return;
                }
                _cart.Position = OracleObjectMovement.Shared.ApplySpeed(ref _cartPosition, Database.Constant("speed"), _cartAngle);
                _cart.AdvanceAnimationUpdates(1);
                int x = (int)_cart.Position.X, y = (int)_cart.Position.Y;
                if ((x & 15) != 8 || (y & 15) != 8) return;
                int packed = ((y >> 4) << 4) | (x >> 4);
                int tile = Context.Rooms.CurrentRoom.GetMetatile(_cart.Position);
                int angle = packed == 0x15 ? 8 : tile switch { 0x5c => 16, 0x5a => 24, 0x5b => 0, 0x59 => 8, _ => -1 };
                if (angle < 0) return;
                _cartAngle = angle; _cart.SetScriptAnimation(Database.Animation((angle & 8) == 0 ? 7 : 8)); return;
            case 3: if (Read(0xcfd4) == 0) _cartState = 1; return;
        }
    }
    private void UpdateSwitch()
    {
        if (_cart is null) return;
        int value = Read(OracleRuntimeState.SwitchStateAddress);
        if (value == _switchState) return;
        _switchState = value; SetTile(0x05, (value & 1) != 0 ? 0x5d : 0x5c);
    }
    private void UpdateManager()
    {
        if (_managerState == 0) return;
        if (_managerState == 1)
        {
            // The newly created manager's state 0 does not decrement its delay.
            _managerState = 2; return;
        }
        if (_managerState == 2)
        {
            if (--_managerCounter != 0) return;
            _managerCounter = 4 + Read(0xcfd0) * 4; _managerState = 3;
            foreach (int packed in new[] { 0x44, 0x4a, 0x75, 0x78 }) SpawnBeetle(packed);
            return;
        }
        if (Read(0xcfd5) != 0) { _managerState = 0; return; }
        for (int slot = 0; slot < 4; slot++)
        {
            if (Read(0xcfd9 + slot * 2) != 0x5f) continue;
            if (--_managerCounter == 0) { _wonSignal++; _managerState = 0; return; }
            if (_managerCounter >= 4) _extraPending++;
        }
        if (_extraPending != 0 && SpawnBeetle(new[] { 0x4a, 0x57, 0x75, 0x78 }[_extraSpawned]))
        { _extraPending--; _extraSpawned++; }
        ClearHoleBuffer();
    }
    private bool SpawnBeetle(int packed)
    {
        var position = new Vector2((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        SpawnPuff(position);
        if (!Context.Entities.TrySpawnEnemy(0x5f, 0, position, "patch.s:@spawnBeetle", out string error))
        {
            if (error.Contains("slots are occupied", StringComparison.Ordinal)) return false;
            throw new InvalidOperationException($"patch.s:@spawnBeetle: {error}");
        }
        _beetles.Add(Context.Entities.Entities<HardhatBeetleCharacter>().Last());
        return true;
    }
    private void ClearHoleBuffer() { for (int i = 0; i < 8; i++) Write(0xcfd8 + i, 0xff); }
    private void UpdateBrokenItem()
    {
        if (_broken is null) return;
        if (_brokenState == 0) { _brokenState = 1; return; }
        if (_brokenState == 1)
        {
            if (Fading) { _brokenState = 2; return; }
            if (!Player.EnemyCollisionOverlaps(_cart!.Position, new Rect2(_broken.Position - Vector2.One * 6, Vector2.One * 12))) return;
            Write(0xcfd5, 1); _brokenState = 2;
            Context.Entities.Spawn(new InteractionExplosionSpawn(_broken.Position - Vector2.Right * 8, 0, Database.ExplosionVisual));
        }
        else if (Read(0xcfd6) != 0) { _broken.SetActive(false); _broken = null; }
    }
    private void UpdateFixedItem()
    {
        if (_fixed is null) return;
        if (_fixedCounter < 0)
        {
            if (Read(0xcfd3) == 0) return;
            _fixedCounter = Database.Constant("fixed-item-life");
            _fixed.Position = _patch!.Position + new Vector2(-8, -14); _fixed.Visible = true;
            if (Read(0xcfd0) != 0 && Read(0xcfd1) == 0)
            { _fixed.SetBasePalette(4); _fixed.SetScriptAnimation(Database.Animation(12)); }
            return;
        }
        if (--_fixedCounter == 0) { _fixed.SetActive(false); _fixed = null; }
    }
    private NpcCharacter SpawnVisual(int subid, Vector2 position)
    {
        var visual = Database.Visual(subid);
        var record = new NpcRecord(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, 0x94, subid,
            (int)position.Y, (int)position.X, 0, 0, visual.Sprite, visual.TileBase, visual.Palette, 0, false,
            visual.Animation, visual.Animation, visual.Animation, visual.Animation, string.Empty, NpcImplementationClassification.EventOwned);
        var actor = Context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(record, $"PatchItem{subid:x2}"));
        actor.SetAnimationRate(0);
        // patch_subid04/05 uses objectSetVisible83: the broken item is behind
        // Patch and the cart. Only the repaired item (06/07) uses visible81.
        actor.SetFixedDrawPriority(subid is 4 or 5
            ? NpcCharacter.FixedLowPriorityZIndex : NpcCharacter.InFrontOfLinkZIndex);
        return actor;
    }
    private void SpawnPuff(Vector2 position) => Context.Entities.Spawn(new PuzzlePuffSpawn(position, OracleSoundEngine.SndPoof));
    private void SetTile(int packed, int tile) => Context.Rooms.CurrentRoom.SetPositionTileAndCollision(
        new Vector2((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8), (byte)tile, null, Context.AnimationTick());
    private void SetAnimation(int animation) => _patch!.SetScriptAnimation(Database.Animation(animation));
    private void RestoreMusic() => Context.Sound.PlayRoomMusic(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id);
    private void BeginFade(int direction, int delay)
    {
        EventResources.CaptureFullScreenFade(); _fadeDirection = direction; _fadeDelay = delay;
        _fadeCounter = 1; _fadeOffset = direction > 0 ? 0 : 32;
    }
    private void UpdateFade()
    {
        if (!Fading || --_fadeCounter != 0) return;
        _fadeCounter = _fadeDelay;
        _fadeOffset += _fadeDirection;
        if (_fadeOffset >= 32 || _fadeOffset < 0) { _fadeDirection = 0; return; }
        Context.Fade.Color = new Color(1, 1, 1, Math.Min(_fadeOffset, 31) / 31f);
    }
    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (!ReferenceEquals(_patch, npc) || !_buttonSensitive || BlocksGameplay || Context.DialogueOpen) return false;
        _buttonPressed = true; return true;
    }
    public void Cancel()
    {
        Context.Entities.ObjectFellInHole -= OnObjectFellInHole;
        _pendingHoleEvents = 0;
        ReleaseInputControl(); EventResources.ReleaseFullScreenFade();
        if (_stairsClosed && ReferenceEquals(_room, Context.Rooms.CurrentRoom)) SetTile(0x49, 0x44);
        _stairsClosed = false; _room = null;
        foreach (var beetle in _beetles)
            if (GodotObject.IsInstanceValid(beetle)) beetle.DeleteForRoomEvent();
        foreach (var reward in _rewards)
            if (GodotObject.IsInstanceValid(reward)) reward.Finish(Context.Player);
        _rewards.Clear();
        _patch?.SetScriptButtonSensitive(false); _patch = _cart = null;
        if (_broken is not null && GodotObject.IsInstanceValid(_broken)) _broken.SetActive(false);
        if (_fixed is not null && GodotObject.IsInstanceValid(_fixed)) _fixed.SetActive(false);
        _broken = _fixed = null; _beetles.Clear(); _runner.Clear();
        _buttonSensitive = _buttonPressed = false; _z = _speedZ = _var38 = 0;
        _managerState = _extraPending = _extraSpawned = _wonSignal = _fadeDirection = 0;
        State = 0;
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Patch";
    public override void ScriptEnded() { } // Carry is consumed by the native state handler above.
    public override void InitializeActorCollisionRadii(string actor) => _patch!.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string actor) { _buttonSensitive = true; _patch!.SetScriptButtonSensitive(true); }
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool value = _buttonPressed; _buttonPressed = false;
        if (value) Context.Player.ApplyObjectInteractionGrace();
        return value;
    }
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => _patch!.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) => _patch!.SetScriptAnimation(encodedAnimation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) => _patch!.Position = OracleObjectMovement.Shared.ApplySpeed(ref _patchPosition, speed, angle);
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding) => binding switch
    {
        "MetPatch" => (Context.Rooms.SaveData.GetRoomFlags(1, 0xbe) & 6) != 0 ? 1 : 0,
        "Interaction.var38" => _var38,
        "wTmpcfc0.patchMinigame.fixingSword" => Read(0xcfd0),
        "wTmpcfc0.patchMinigame.swordLevel" => Read(0xcfd1),
        _ => throw UnsupportedCommand($"read Patch memory {binding}")
    };
    public override void WriteMemory(string binding, int value)
    {
        if (binding != "wTmpcfc0.patchMinigame.wonMinigame") throw UnsupportedCommand($"write Patch memory {binding}");
        Write(0xcfd3, value);
    }
    public override bool TextOptionEquals(int value) => EventResources.RequireDialogueChoice("patch.s: missing dialogue choice.") == value;
    public override void ShowText(int textId, string message, int? textboxPosition)
    {
        int flags = Database.TextFlags(textId);
        if (message.Contains(@"\call(0xff)", StringComparison.Ordinal))
            message = message.Replace(@"\call(0xff)", Database.ItemName(0x5800 | Read(0xcbaf)), StringComparison.Ordinal);
        if (message.Contains(@"\opt(", StringComparison.Ordinal)) Context.ShowChoiceDialogue(message, textboxPosition: textboxPosition, textboxFlags: flags);
        else Context.ShowDialogue(message, textboxPosition, flags);
    }
    public override void GiveItem(int treasureId, int parameter)
    {
        string name = treasureId == TreasureDatabase.TreasureTuniNut ? "TREASURE_OBJECT_TUNI_NUT_01" : $"TREASURE_OBJECT_SWORD_{parameter:x2}";
        var item = Context.Treasures.GetObject(name);
        var policy = Database.Reward(name);
        _rewards.Add(Context.Entities.GrantGroundTreasure(new GroundTreasureGrantRequest(
            Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, 0,
            (int)Context.Player.Position.Y, (int)Context.Player.Position.X, name, "scripts.s:patch_giveRepairedItem")
        {
            GrabMode = policy.Mode,
            RoomFlagTiming = policy.RoomFlag ? GroundTreasureRoomFlagTiming.OnActivation : GroundTreasureRoomFlagTiming.Never,
            CompletionOwner = policy.Mode == 3 ? GroundTreasureCompletionOwner.Caller : GroundTreasureCompletionOwner.SharedInteraction,
            ExpectedTreasureId = treasureId, ExpectedSubId = parameter, ExpectedObjectParameter = item.Parameter
        }, Context.Player));
    }
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "MetPatch": Context.Rooms.SaveData.SetRoomFlag(1, 0xbe, 6); break;
            case "patch_jump": _speedZ = -0x180; SetInputEnabled(false); Context.Sound.PlaySound(Database.Constant("jump-sound")); break;
            case "patch_updateTextSubstitution": Write(0xcbaf, Read(0xcfd7)); break;
            case "patch_turnToFaceLink":
                SetAnimation(((OracleObjectMovement.Shared.RelativeAngle(_patch!.Position, Context.Player.Position) + 4) & 0x18) >> 3); break;
            case "patch_setStairTile:160": _stairsClosed = true; SetTile(0x49, 0xa0); SpawnPuff(new Vector2(0x98, 0x48)); break;
            case "patch_restoreControlAndStairs": _stairsClosed = false; ReleaseInputControl(); SetTile(0x49, 0x44); SpawnPuff(new Vector2(0x98, 0x48)); break;
            case "patch_moveLinkPositionAtMinigameEnd":
                Context.Entities.ClearPhysicalPlayerItems(); Context.Player.PutOnGroundForScript();
                Context.Player.ClearInteractionKnockback(clearInvincibility: true);
                Context.Player.Position = new Vector2(0x78, 0x48); Context.Player.Face(Vector2I.Up); Write(0xcfd6, 1); break;
            case "fadeoutToWhiteWithDelay:2": BeginFade(1, 2); break;
            case "fadeinFromWhiteWithDelay:2": BeginFade(-1, 2); break;
            case "fadeinFromWhiteWithDelay:4": BeginFade(-1, 4); break;
            case "loseTreasure:65": Context.Inventory.LoseTreasure(TreasureDatabase.TreasureTradeItem); break;
            default: throw UnsupportedCommand($"run Patch helper {handler}");
        }
    }
}
