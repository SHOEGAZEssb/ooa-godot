using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Companion forest introduction, rescue, reward and guide controllers $71:$08-$0b.</summary>
internal sealed class CompanionForestEvent : InteractiveCutsceneCommandHost,
    IRoomEntryEvent, IUpdatesDuringDialogueRoomEvent, ICutsceneCommandHost
{
    private readonly RoomEventContext _context;
    private readonly CompanionForestDatabase _data = new();
    private readonly FairiesWoodsDatabase _fairies = new();
    private readonly CutsceneCommandRunner _runner;
    private readonly List<ForestFairyFlight> _flights = new();
    private FairiesWoodsSparkleLayer? _sparkles;
    private IForestCompanion? _companion;
    private int _companionId;
    private NpcCharacter? _flute;
    private bool _waitingForTrigger;
    private bool _menusDisabled;
    private int _role;
    private int _choice;
    private int _companionDescription = 0x1124;
    private bool _rewardVisible;
    private bool _giveFlutePending;
    private NpcCharacter? _exclamation;
    private int _exclamationCounter;

    internal CompanionForestEvent(RoomEventContext context) { _context = context; _runner = new(this); }
    public override RoomEventContext Context => _context;
    public bool HasState => _waitingForTrigger || _runner.Active || _flights.Count != 0 ||
        (_sparkles?.Count ?? 0) != 0 || _exclamationCounter != 0 || _giveFlutePending;
    public bool BlocksGameplay => InputControlHeld;
    public bool MenusDisabled => _menusDisabled;
    internal int Instruction => _runner.Instruction;
    internal int Signal => _context.Entities.RuntimeState.ReadWramByte(0xcfd2);
    internal IReadOnlyList<ForestFairyFlight> Flights => _flights;
    private OracleSaveData Save => _context.Rooms.SaveData;
    private bool Flag(int flag) => Save.HasGlobalFlag(flag);

    public bool Matches(int group, OracleRoomData room)
    {
        int role = _data.Role(group, room.Id);
        return role switch
        {
            8 => Flag(0x22) && !Save.HasRoomFlag(group, room.Id, 0x40) &&
                _context.Transitions.ScrollActive && _context.Transitions.ScrollDirection == Vector2I.Left,
            9 => Flag(0x22) && !Flag(0x23),
            10 => Flag(0x24) && !Flag(0x23),
            11 => Flag(0x42) && !Flag(0x23) && _context.Transitions.ScrollActive &&
                _context.Transitions.ScrollDirection == Vector2I.Down,
            _ => false
        };
    }

    internal void OnRoomLoaded(int group, OracleRoomData room)
    {
        int role = _data.Role(group, room.Id);
        if (role == 12 && (Save.ReadWramByte(0xc647) & 0x20) != 0)
            Save.WriteWramByte(0xc647, (byte)(Save.ReadWramByte(0xc647) | 0x40));
        if (role != 8) return;
        for (int address = 0xcfd0; address < 0xcfe0; address++) _context.Entities.RuntimeState.SetWramByte(address, 0);
        Save.SetGlobalFlag(0x1d, false);
        // companionScripts.s:subid08@state0 assigns Moosh without granting a
        // flute, before checking entry direction, carpenter progress or room flags.
        if (_context.Inventory.AnimalCompanion == 0)
            _context.Inventory.AssignAnimalCompanion(0x0d);
        _companionDescription = 0x1123 + _context.Inventory.AnimalCompanion - 0x0b;
    }

    public void Start(OracleRoomData room)
    {
        Cancel();
        _role = _data.Role(_context.Rooms.ActiveGroup, room.Id);
        _companionId = _context.Inventory.AnimalCompanion;
        _context.Entities.RuntimeState.SetWramByte(0xcfd2, (byte)(_role == 10 ? 1 : 0));
        if (_role == 8) { _waitingForTrigger = true; return; }
        if (_role == 9 && Flag(0x42) || _role == 10)
        {
            Vector2 position = _role == 10 ? new(0x68, 0x48) : new(0x50, 0x58);
            if (CompanionRuntimeState.AnyActive(_context.Entities.RuntimeState))
                throw new InvalidOperationException($"companionSpawner.s:${(_role == 10 ? 5 : 4):x2} found an occupied companion slot in 0:{room.Id:x2}.");
            _companion = _companionId switch
            {
                0x0b => _context.Entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(position, 2, 0, room.Id)),
                0x0c => _context.Entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(position, 2, 0, room.Id)),
                0x0d => _context.Entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(position, 2, 0, room.Id)),
                _ => throw UnsupportedCommand($"spawn forest companion ${_companionId:x2}")
            };
            _companion.UseForestInteraction(_role == 10);
            CompanionRuntimeState.ForgetRemembered(_context.Entities.RuntimeState);
            CompanionRuntimeState.SetLastAnimalMountPosition(_context.Entities.RuntimeState, position);
        }
        if (_role == 10)
        {
            _context.Player.Face(Vector2I.Up);
            SetInputEnabled(false);
            for (int preset = 0x11; preset <= 0x13; preset++) SpawnFairy(4, preset);
        }
        if (_role == 11) { SetInputEnabled(false); SpawnFairy(3, 0x14); }
        _runner.Start(_data.Commands(_role));
    }

    public void UpdateFrame()
    {
        UpdateExclamation();
        if (_giveFlutePending) { _giveFlutePending = false; RunNativeHandler("GiveFluteNow"); return; }
        if (_waitingForTrigger)
        {
            if (_context.Player.Position.X >= 0x50) return;
            _waitingForTrigger = false;
            SetInputEnabled(false);
            _context.Player.PutOnGroundForScript();
            SpawnFairy(3, 0x0f);
            _runner.Start(_data.Commands(8));
            return;
        }
        if (_rewardVisible && !_context.DialogueOpen)
        {
            _rewardVisible = false;
            _flute?.SetActive(false);
            _context.Player.EndGetItemTwoHandPose();
        }
        // The controller's placed slot precedes its dynamically allocated fairies.
        _runner.AdvanceFrame();
        foreach (var flight in _flights) flight.UpdateFrame(_context.Entities.FrameCounter, _context.DialogueOpen);
        _flights.RemoveAll(flight => !flight.Active);
        _sparkles?.UpdateFrame();
    }

    public void UpdateDuringDialogueFrame() { UpdateExclamation(); _sparkles?.UpdateFrame(); }
    private void UpdateExclamation()
    {
        if (_exclamationCounter == 0 || _exclamation is null) return;
        if (--_exclamationCounter == 0) _exclamation.SetActive(false);
        else _exclamation.AdvanceAnimationUpdates(1);
    }
    public override void SetInputEnabled(bool enabled)
    {
        base.SetInputEnabled(enabled);
        _menusDisabled = !enabled;
    }
    public override bool MemoryEquals(string binding, int value) => binding switch
    {
        "wTmpcfc0.fairyHideAndSeek.cfd2" => Signal == value,
        "w1Companion.var3d" => (_companion?.ForestButtonPressed == true ? 1 : 0) == value,
        "wLinkObjectIndex" => (_companion?.LinkRiding == true ? 1 : 0) == value,
        _ => throw UnsupportedCommand($"compare forest binding '{binding}'")
    };
    public override void WriteMemory(string binding, int value)
    {
        if (binding != "wTmpcfc0.fairyHideAndSeek.cfd2") throw UnsupportedCommand($"write forest binding '{binding}'");
        _context.Entities.RuntimeState.SetWramByte(0xcfd2, checked((byte)value));
    }
    public override bool TextOptionEquals(int value)
    {
        if (_context.TryTakeDialogueChoice(out int choice)) _choice = choice;
        return _choice == value;
    }
    public override void ShowText(int textId, string message)
    {
        if (textId is 0x1131 or 0x1132)
        {
            var branch = _data.Companion(_companionId);
            textId = textId == 0x1131
                ? _role == 9 ? branch.RescueFirst : IsLinkedGame ? branch.RewardLinked : branch.RewardUnlinked
                : _role == 9 ? IsLinkedGame ? branch.RescueLinked : branch.RescueUnlinked : branch.RewardAfter;
        }
        message = _data.Text(textId).Replace("\\call(0xff)", _data.Text(_role == 8 ? _companionDescription : 0x1124));
        if (textId == 0x1121) _context.ShowChoiceDialogue(message);
        else _context.ShowDialogue(message);
    }
    public override void OrRoomFlag(int flag) => Save.SetRoomFlag(_context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id, (byte)flag);
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "NoticeLink":
                _companion?.NoticeForestLink(_data.Companion(_companionId).NoticeAnimation);
                _exclamation = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
                    _data.ExclamationRecord(_context.Rooms.CurrentRoom.Id, new Vector2(0x50, 0x48)),
                    "ForestCompanionExclamation", Talkable: false, Solid: false));
                _exclamation.SetAnimationRate(0);
                _exclamationCounter = 30;
                _context.Sound.PlaySound(OracleSoundEngine.SndClink);
                break;
            case "SpawnRescueFairy": SpawnFairy(3, 0x0f); break;
            case "ScrambleForest": Save.SetGlobalFlag(0x2b, false); break;
            case "GiveFlute":
                _giveFlutePending = true;
                break;
            case "GiveFluteNow":
                ShowText((_context.Inventory.HasTreasure(0x0e) ? 0x0069 : 0x0038) + _companionId - 0x0b, string.Empty);
                Save.WriteWramByte(0xc6b5, (byte)(_companionId - 0x0a));
                int stateAddress = 0xc646 + _companionId - 0x0b;
                Save.WriteWramByte(stateAddress, (byte)(Save.ReadWramByte(stateAddress) | 0x80));
                _context.Inventory.GiveTreasure(0x0e, 1);
                _context.Sound.PlaySound(OracleSoundEngine.SndGetItem);
                _flute = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
                    _data.FluteRecord(_context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id,
                        _context.Player.Position + new Vector2(0, -14), _companionId), "CompanionFluteReward", Talkable: false, Solid: false));
                _flute.SetAnimationRate(0);
                _context.Player.BeginGetItemTwoHandPose();
                _rewardVisible = true;
                break;
            case "ForceMount":
                _companion?.ForceForestMount();
                // The native controller enables special objects for mounting
                // while retaining its menu lock until the script completes.
                base.SetInputEnabled(true);
                _menusDisabled = true;
                break;
            case "WarpOut":
                _context.Transitions.ApplyWarpWithFadeOut(_context.Player,
                    new Warp(0, _context.Rooms.CurrentRoom.Id, -1, 0, 0, 0, 0x63, 0x56, 0, 0));
                break;
            default: throw UnsupportedCommand($"run forest helper '{handler}'");
        }
    }
    public override void ScriptEnded() { ReleaseInputControl(); _menusDisabled = false; }
    public void Cancel()
    {
        _runner.Clear(); _waitingForTrigger = false;
        foreach (var flight in _flights) flight.Deactivate();
        _flights.Clear();
        _sparkles?.Clear();
        if (_flute is not null && GodotObject.IsInstanceValid(_flute)) _flute.SetActive(false);
        _flute = null; _companion = null;
        if (_rewardVisible) _context.Player.EndGetItemTwoHandPose();
        _rewardVisible = false;
        _giveFlutePending = false;
        if (_exclamation is not null && GodotObject.IsInstanceValid(_exclamation)) _exclamation.SetActive(false);
        _exclamation = null; _exclamationCounter = 0;
        ReleaseInputControl(); _menusDisabled = false;
    }
    private void SpawnFairy(int subId, int preset)
    {
        var movement = _fairies.Movements[preset]; var visual = _fairies.Event;
        var record = new NpcRecord(0, _context.Rooms.CurrentRoom.Id, 0x49, subId,
            movement.InitialY, movement.InitialX, preset, 0, visual.FairySprite, visual.FairyTileBase,
            0, 0, false, visual.Animation0, visual.Animation0, visual.Animation0, visual.Animation0,
            string.Empty, NpcImplementationClassification.EventOwned);
        var actor = _context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(record, $"CompanionFairy{subId:x2}_{preset:x2}", Talkable: false, Solid: false));
        actor.SetAnimationRate(0); actor.SetCollisionRadii(4, 4);
        if (_sparkles is null || !GodotObject.IsInstanceValid(_sparkles))
        {
            _sparkles = new FairiesWoodsSparkleLayer(); _sparkles.Initialize(visual);
            _context.RoomView.GetParent().AddChild(_sparkles);
        }
        _flights.Add(new ForestFairyFlight(_fairies, _context.Entities.RuntimeState, _context.Sound,
            _sparkles, actor, preset, position => _context.Entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(position, visual.PuffSound)), subId));
    }
}
