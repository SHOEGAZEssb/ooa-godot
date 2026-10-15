using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Native interaction dispatches used only by the temporary post-D7 streams.</summary>
internal sealed class PostD7ActorRoomEntity : CutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEventContext _context;
    private readonly PostD7EventDatabase _data;
    private readonly PostD7Event _owner;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private Vector2 _precise;
    private int _counter, _counter2, _catchCount = 2, _spawnIndex, _baseX, _z, _speedZ;
    private bool _scriptEnded, _boyAnimation;
    internal int Id { get; }
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal Vector2 PrecisePosition => _precise;
    internal int ZFixed => _z;
    public Node2D Node => _actor;
    public bool Finished { get; private set; }
    // bank0._updateInteractionsIfStateIsZero admits initialization and
    // enabled bit 7 only. sparkle.s and twinrovaFlame.s set that bit.
    public bool UpdatesDuringDialogue => State == 0 || Id is 0x84 or 0xa9;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public override bool DialogueOpen => _context.DialogueOpen;
    public override bool ScriptExecutionBlocked => _context.ScriptDialogueOpen || _context.Player.IsDying;
    public override int FrameCounter => _context.Entities.FrameCounter;

    internal PostD7ActorRoomEntity(RoomEventContext context, PostD7EventDatabase data, PostD7Event owner,
        int id, int subId, Vector2 position, int stoneCounter = 0)
    {
        _context = context; _data = data; _owner = owner; Id = id; SubId = subId; _precise = position;
        _counter = stoneCounter;
        _actor = NpcCharacter.CreateFromRecord(data.Actor(id, subId, position, context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id));
        _actor.SetGraphicsSourceOffset(data.SourceOffset(id, subId));
        // Native interactionInitGraphics selects one imported animation. Use
        // its complete OAM bounds even before a script changes animation;
        // the ordinary facing compositor clips large effects to 32x32.
        _actor.SetScriptAnimation(_actor.Record.DownAnimation);
        _actor.SetAnimationRate(0); _actor.SetBlocksLink(false); _runner = new(this);
    }
    private byte Read(int address) => _context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => _context.Entities.RuntimeState.SetWramByte(address, unchecked((byte)value));
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    private void Animation(int index) => _actor.SetScriptAnimation(_data.Animation(Id, index));
    private void Start(string script) => _runner.Start(_data.Commands(script));
    private void Position(Vector2 position) { _precise = position; _actor.SetStatePosition(position.Floor()); }
    private void StonePalette() { _actor.SetBasePalette(6); _actor.SetScriptPaletteOverride(_data.StonePalette); }
    private void Initialize()
    {
        State = 1; _actor.ShowInitializedPresentation();
        switch (Id)
        {
            case 0x8d:
                Position(new(0x50, 0x40)); _counter = _data.Table("unlinked")[4]; _actor.Visible = false; break;
            case 0xb0:
                _actor.SetBasePalette(SubId == 0 ? 1 : 2); Animation(SubId);
                if (SubId == 0) Start("linked"); break;
            case 0xa9:
                Position(new(0x48, 0x50));
                if (_context.Rooms.SaveData.HasRoomFlag(_context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id, 0x80))
                    Finish();
                // Subid $04 adds zero. getThisRoomFlags redirected HL to
                // room flags, so this leaves the actor's animation phase alone.
                break;
            case 0x4d:
                _actor.SetBasePalette(6); _actor.SetScriptPaletteOverride(_data.PossessedPalette);
                Start(SubId == 3 ? "ascent" : "ambi"); break;
            case 0x3c: Animation(3); Start("boy"); break;
            case 0x3a: Animation(1); Start("father"); break;
            case 0x43: Start("observer"); break;
            case 0x4b:
                if (SubId == 2) { _counter = 180; RandomSpawnDelay(); }
                else { Animation(5); _speedZ = -0x180; }
                break;
            case 0x84: break;
            default: throw new InvalidOperationException($"Post-D7 INTERAC ${Id:x2}:${SubId:x2} state $00.");
        }
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { Initialize(); return; }
        switch (Id)
        {
            case 0x8d: UpdateCloaked(); break;
            case 0xb0:
                if (SubId != 0) { _actor.AdvanceAnimationUpdates(1); break; }
                if (Substate == 0)
                {
                    _actor.AdvanceAnimationUpdates(1);
                    _runner.AdvanceFrame();
                    if (_scriptEnded) { _owner.BeginFlash(); Substate = 1; }
                }
                else if (_owner.Flash(2)) _owner.RequestTower();
                break;
            case 0xa9: _actor.AdvanceAnimationUpdates(1); break;
            case 0x4d: UpdateAmbi(); break;
            case 0x3c:
                if (_boyAnimation) AnimateMotion();
                _runner.AdvanceFrame(); break;
            case 0x3a:
                if (Substate == 0) { _runner.AdvanceFrame(); if (Read(0xcfd1) == 1) Substate = 1; }
                else if (Substate == 1 && Read(0xcfd1) == 2) { Substate = 2; StonePalette(); }
                break;
            case 0x43:
                _runner.AdvanceFrame(); if (Read(0xcfd1) < 2) _actor.AdvanceAnimationUpdates(1); break;
            case 0x4b: if (SubId == 2) UpdateRabbitController(); else UpdateRabbit(); break;
            case 0x84: _actor.AdvanceAnimationUpdates(1); _actor.Visible = (FrameCounter & 1) == 0; break;
        }
        _actor.SetScriptDrawOffset(new(0, _z >> 8));
    }
    private void UpdateCloaked()
    {
        _actor.AdvanceAnimationUpdates(1);
        switch (Substate)
        {
            case 0:
                if (--_counter != 0) return;
                _counter = _data.Table("unlinked")[5];
                // cloakedTwinrova.s calls playSound without writing wActiveMusic.
                _context.Sound.PlayNativeSound(SoundId.MusDisaster); _actor.Visible = true;
                _context.Transitions.BeginNativeBlackFade(false); Substate = 1; return;
            case 1:
                if (_context.Transitions.NativePaletteFadeActive || --_counter != 0) return;
                _counter = _data.Table("unlinked")[6];
                _context.ShowDialogue(_data.Text(0x2808), sourceTextId: 0x2808); Substate = 2; return;
            case 2:
                if (DialogueOpen || --_counter != 0) return;
                _owner.BeginFlash(); Substate = 3; return;
            case 3:
                if (!_owner.Flash(2)) return;
                _owner.RequestTower(); Finish(); return;
        }
    }
    private void UpdateAmbi()
    {
        if (SubId == 4)
        {
            if (Substate == 0)
            {
                AnimateMotion(); _runner.AdvanceFrame();
                if (_scriptEnded) { _owner.BeginFlash(sound: false); Substate = 1; }
            }
            else if (Substate == 1 && _owner.Flash(2)) { Write(0xcfc0, 3); Substate = 2; }
            else if (Substate == 2) _actor.AdvanceAnimationUpdates(1);
            return;
        }
        switch (Substate)
        {
            case 0:
                if (Read(0xcfc0) == 1)
                {
                    Substate = 1; _counter = 0x14; // SPEED_80 is the raw byte $14.
                    Lightning(_precise.Floor()); return;
                }
                AnimateMotion(); _runner.AdvanceFrame(); return;
            case 1:
                if (--_counter != 0) return;
                _owner.BeginFlash(sound: false); Substate = 2; return;
            case 2:
                if (!_owner.Flash(2)) return;
                Substate = 3;
                if (_context.Entities.InteractionSlotAvailable)
                    _context.Entities.AddEntity(new PostD7ActorRoomEntity(_context, _data, _owner, 0x84, 8, _precise.Floor()));
                _context.Transitions.BeginNativeWhiteFade(false, delay: 2); return;
            case 3:
                if (_context.Transitions.NativePaletteFadeActive) return;
                Write(0xcfc0, 2); Substate = 4; return;
            case 4: AnimateMotion(); _runner.AdvanceFrame(); return;
        }
    }
    private void AnimateMotion()
    {
        int speed = _runner.ActorSpeed(new("Actor"));
        _actor.AdvanceAnimationUpdates(_runner.MovementCounter(new("Actor")) == 0 || speed < 0x28 ? 1 : speed < 0x50 ? 2 : speed < 0x78 ? 3 : 4);
    }
    private void RandomSpawnDelay() => _counter2 = _data.RabbitTable("spawn-delays")[_context.Entities.NextRandomValue() & 3];
    private void SpawnRabbit(Vector2 position, int stoneCounter)
    {
        if (!_context.Entities.InteractionSlotAvailable) return;
        _context.Entities.AddEntity(new PostD7ActorRoomEntity(_context, _data, _owner, 0x4b, 1, position, stoneCounter));
    }
    private void UpdateRabbitController()
    {
        if (_counter != 0 && --_counter == 0)
        {
            var data = _data.RabbitTable("stone-spawns"); int index = _spawnIndex++ * 3;
            _counter = data[index]; SpawnRabbit(new(data[index + 2], data[index + 1]), 95);
        }
        if (--_counter2 != 0) return;
        int y = _data.RabbitTable("y-positions")[_context.Entities.NextRandomValue() & 7];
        int x = 0xb0 - (_context.Entities.NextRandomValue() & 15);
        SpawnRabbit(new(x, y), 0); RandomSpawnDelay();
    }
    private void UpdateRabbit()
    {
        if (_counter != 0 && --_counter == 0)
        {
            _counter2 = 30; Substate = 2;
            Exclaim(_precise.Floor() + new Vector2(0, -16), 30);
        }
        switch (Substate)
        {
            case 0:
                _actor.AdvanceAnimationUpdates(1);
                if (_actor.CurrentAnimationParameter != 0) { _context.Sound.PlaySound(SoundId.SndJump); Substate = 1; }
                return;
            case 1:
                if (Mathf.FloorToInt(_precise.X) >= 0xd0) { Finish(); return; }
                Move(0x3c, 0x18);
                if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20)) { Substate = 0; Animation(5); _speedZ = -0x180; }
                return;
            case 2:
                if (--_counter2 == 0) { _counter2 = 60; _baseX = Mathf.FloorToInt(_precise.X); Substate = 3; }
                return;
            case 3:
                Position(new(_baseX + (_context.Entities.NextRandomValue() & 1) - 1 + (_precise.X % 1), _precise.Y));
                if (--_counter2 == 0) { _counter2 = 20; StonePalette(); Substate = 4; }
                return;
            case 4: if (--_counter2 == 0) { _speedZ = 0; Substate = 5; } return;
            case 5:
                if (!OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20)) return;
                Substate = 6; _counter2 = 240; _context.Entities.BeginScreenShake(4); return;
            case 6: if (--_counter2 == 0) Write(0xcfdf, 0xff); return;
        }
    }
    private void Move(int speed, int angle)
    {
        OracleObjectMovement.Shared.ApplySpeed(ref _precise, speed, angle);
        _precise = new((_precise.X + 256) % 256, (_precise.Y + 256) % 256);
        _actor.SetStatePosition(_precise.Floor());
    }
    private void Finish() { Finished = true; _actor.SetActive(false); }
    private void Lightning(Vector2 position)
    {
        if (_context.Entities.PartSlotAvailable) _context.Entities.AddEntity(new LightningRoomEntity(_context, position));
    }
    private void Exclaim(Vector2 position, int duration)
    {
        if (_context.Entities.InteractionSlotAvailable) _context.Entities.Spawn<NpcCharacter>(new ExclamationMarkSpawn(position, Duration: duration));
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Actor";
    public override void SetActorAnimation(string actor, int index, string encodedAnimation) => Animation(index);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) => Animation((angle & 0x18) >> 3);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) => Move(speed, angle);
    public override void ShowText(int id, string message) => ShowText(id, message, null);
    public override void ShowText(int id, string message, int? position) => _context.ShowDialogue(message, position, sourceTextId: id);
    public override void ScriptEnded() => _scriptEnded = true;
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding) => binding switch
    {
        "SharedState" => Read(0xcfc0), "StoneSignal" => Read(0xcfd1), "SceneDone" => Read(0xcfdf), "BoyCatchCount" => _catchCount,
        _ => throw UnsupportedCommand(binding)
    };
    public override void WriteMemory(string binding, int value)
    {
        int address = binding switch { "SharedState" => 0xcfc0, "StoneSignal" => 0xcfd1, "SceneDone" => 0xcfdf, _ => throw UnsupportedCommand(binding) };
        Write(address, value);
    }
    public override bool GateOpen(string gate) => gate == "PaletteDone" ? !_context.Transitions.NativePaletteFadeActive : throw UnsupportedCommand(gate);
    public override void OrRoomFlag(int flag) => _context.Rooms.SaveData.SetRoomFlag(_context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id, (byte)flag);
    public override void WriteObjectByte(string actor, int address, int value)
    {
        if (address != 0x5b || value != 6) throw UnsupportedCommand($"object byte ${address:x2}=${value:x2}");
        StonePalette();
    }
    public override void RunNativeHandler(string handler)
    {
        var parts = handler.Split(':');
        switch (parts[0])
        {
            case "TurnToStone": _owner.RequestStone(); break;
            case "Throw":
                int signal = Convert.ToInt32(parts[1], 16);
                _catchCount = (_catchCount - 1) & 0xff;
                _actor.ForceNextAnimationFrame(); Write(0xcfd3, signal); break;
            case "Lightning": Lightning(Convert.ToInt32(parts[1], 16) == 0 ? new(0x38, 0x48) : new(0x78, 0x28)); break;
            case "FadeOutWhite": _context.Transitions.BeginNativeWhiteFade(true); break;
            case "FadeInWhite": _context.Transitions.BeginNativeWhiteFade(false); break;
            case "EnableBoyAnimation": _boyAnimation = true; Substate++; break;
            case "Exclaim": Exclaim(_precise.Floor() + new Vector2(0, -12), 0x28); break;
            default: throw UnsupportedCommand(handler);
        }
    }
}
