using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>interactionCode9d, its ROM scripts, and the copied Tokkey dance helper.</summary>
internal sealed class TokkeyEvent : InteractiveCutsceneCommandHost, IRoomEntryEvent,
    ICutsceneCommandHost, IUpdatesDuringDialogueRoomEvent
{
    private readonly CutsceneCommandRunner _runner;
    private readonly EffectRecord _note = new SharedEffectDatabase().Effect("MusicNote");
    private readonly HarpOfAgesEventRecord _song = new HarpOfAgesEventDatabase().Record;
    private readonly List<TokkeyNote> _notes = new();
    private NpcCharacter? _actor;
    private NpcCharacter? _exclamation;
    private bool _exclamationFresh;
    private int _exclamationCounter;
    private bool _buttonSensitive;
    private bool _buttonPressed;
    private Vector2 _precisePosition;
    private int _zFixed;
    private int _speedZ;
    private int _songUpdate = -1;
    private int _effectSerial;

    internal TokkeyEvent(RoomEventContext context)
    {
        Context = context;
        _runner = new(this);
    }

    public RoomEventContext Context { get; }
    protected override RoomEventContext InputContext => Context;
    internal TokkeyDatabase Database { get; } = new();
    public bool HasState => _runner.Active;
    public bool BlocksGameplay => InputLeaseHeld;
    internal int State { get; private set; }
    internal int Counter => _runner.Counter;
    internal int CommandIndex => _runner.Instruction;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    private byte Signal
    {
        get => Context.Entities.RuntimeState.ReadWramByte(0xcfc0);
        set => Context.Entities.RuntimeState.SetWramByte(0xcfc0, value);
    }

    public bool Matches(int group, OracleRoomData room) =>
        group == Database.Group && room.Id == Database.Room;

    public void Start(OracleRoomData room)
    {
        Cancel();
        _actor = Context.RequireNpc(Database.Group, room.Id, Database.Id, Database.SubId, "INTERAC_TOKKEY");
        _actor.SetAnimationRate(0);
        _precisePosition = _actor.Position;
        Signal = 0; // parseObjectData clears the room's temporary interaction bytes.
        State = 1; // state 0 installs the script without running it.
        _runner.Start(Database.Commands);
    }

    public void UpdateFrame()
    {
        if (_actor is null) return;
        // ITEM_HARP freezes ordinary interactions and clears Link's collision
        // bit until its final animation update. Only $9f/$a0 keep updating.
        if (Context.Player.IsUsingHarp)
        {
            UpdateDuringDialogueFrame();
            return;
        }
        int state = State; // Dispatch is selected before script writes Interaction.state.
        if (state == 1)
        {
            if ((Signal & 1) != 0 && Context.Entities.PlayingInstrumentSource() == 1)
            {
                // On the completion update, the parent has restored Link's
                // collisions while wLinkPlayingInstrument still contains $01.
                Player link = Context.Player;
                if (InputLeaseHeld || link.IsDying || link.GaleActive ||
                    link.ElectricShockActive || !link.OverlapsTimePortalHeight)
                    return;
                int tile = ((int)link.Position.Y & 0xf0) | (((int)link.Position.X >> 4) & 0x0f);
                if (tile != Database.HarpTile)
                {
                    Context.ShowDialogue(Database.WrongPositionText, Database.WrongPositionTextboxPosition);
                    return;
                }
                Vector2 position = _actor.Position + new Vector2(16, -8);
                _exclamation = Context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
                    Database.Exclamation((int)position.X, (int)position.Y), "TokkeyExclamation"));
                _exclamation.SetAnimationRate(0);
                _exclamation.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex);
                _exclamationCounter = Database.ExclamationFrames;
                _exclamationFresh = true;
                Context.Sound.PlaySound(OracleSoundEngine.SndClink);
                _runner.Start(Database.Commands, Database.HeardEntry);
                State = 2;
            }
            else
            {
                UpdateZ(Database.Gravity);
                _runner.AdvanceFrame();
                _actor.FaceLinkAndAnimateOneUpdate(Context.Player);
            }
        }
        else
        {
            if (state is 2 or 3 && (Signal & 2) != 0 &&
                (Context.Entities.FrameCounter & Database.NoteMask) == 0)
            {
                bool right = (Context.Entities.NextRandomValue() & 1) != 0;
                SpawnNote(_actor.Position + new Vector2(8, -8), right, _zFixed >> 8);
            }
            if (state == 2) _actor.AdvanceAnimationUpdates(1);
            _runner.AdvanceFrame();
            if (state == 3) _actor.AdvanceAnimationUpdates(2);
            bool landed = UpdateZ(state == 3 ? Database.BounceGravity : Database.Gravity);
            if (state == 3 && landed) _speedZ = Database.BounceSpeed;
        }
        // INTERAC_PLAY_HARP_SONG and effects occupy later interaction slots.
        UpdateSong();
        UpdateExclamation(ref _exclamation, ref _exclamationFresh, ref _exclamationCounter);
        UpdateNotes();
    }

    public void UpdateDuringDialogueFrame()
    {
        // $9f/$a0 set Interaction.enabled bit 7; Tokkey and $c5 do not.
        UpdateExclamation(ref _exclamation, ref _exclamationFresh, ref _exclamationCounter);
        UpdateNotes();
    }

    private bool UpdateZ(int gravity)
    {
        bool landed = OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, gravity);
        _actor!.SetScriptDrawOffset(new Vector2(0, _zFixed >> 8));
        return landed;
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!HasState || !_buttonSensitive || InputLeaseHeld || !ReferenceEquals(npc, _actor)) return false;
        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        ReleaseInputControl();
        if (_songUpdate >= 0) Context.Player.EndHarpPose();
        _songUpdate = -1;
        RetireExclamation(ref _exclamation, ref _exclamationFresh, ref _exclamationCounter);
        foreach (var note in _notes) note.Actor.SetActive(false);
        _notes.Clear();
        _actor?.SetScriptButtonSensitive(false);
        _actor = null;
        _buttonSensitive = _buttonPressed = false;
        _zFixed = _speedZ = 0;
        State = 0;
        _runner.Clear();
    }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Tokkey";
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = _buttonPressed;
        _buttonPressed = false;
        return pressed;
    }
    public override void SetActorButtonSensitive(string actor)
    {
        _buttonSensitive = true;
        _actor!.SetScriptButtonSensitive(true);
    }
    public override void InitializeActorCollisionRadii(string actor) =>
        _actor!.InitializeCollisionRadii();

    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX) =>
        _actor!.SetCollisionRadii(radiusY, radiusX);
    public override bool RoomFlagSet(int flag) => Context.Rooms.SaveData.HasRoomFlag(Database.Group, Database.Room, (byte)flag);
    public override void OrRoomFlag(int flag) => Context.Rooms.SaveData.SetRoomFlag(Database.Group, Database.Room, (byte)flag);
    public override void SetDisabledObjects(int value)
    {
        if (value != 0x91) throw UnsupportedCommand($"set disabled objects ${value:x2}");
        SetInputEnabled(false);
    }
    public override void ShowText(int textId, string message, int? textboxPosition) =>
        Context.ShowDialogue(message, textboxPosition);
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => _actor!.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) => _actor!.SetScriptAnimation(encodedAnimation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) =>
        _actor!.Position = OracleObjectMovement.Shared.ApplySpeed(ref _precisePosition, speed, angle);
    public override void SetMusic(int music)
    {
        if (music == 0xff) Context.Sound.PlayRoomMusic(Database.Group, Database.Room);
        else Context.Sound.PlaySound(music);
    }
    public override bool GateOpen(string gate) => gate == "SongFinished"
        ? (Signal & 0x80) != 0 : throw UnsupportedCommand($"read gate {gate}");
    public override void GiveItem(int treasureId, int parameter) =>
        Context.GrantScriptTreasure(Database.Group, Database.Room, treasureId, parameter,
            "TREASURE_OBJECT_TUNE_OF_CURRENTS_00", "scriptHelper.s:tokkayScript_justHeardTune_body");

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "LoadText:2c01": case "LoadText:2c04": break; // Payload is bound in the expanded genericNpcScript.
            case "Xor:0": Signal ^= 1; break;
            case "Xor:1": Signal ^= 2; break;
            case "State:01": State = 1; break;
            case "State:02": State = 2; break;
            case "State:03": State = 3; break;
            case "State:04": State = 4; break;
            case "tokkey_jump": _speedZ = Database.JumpSpeed; break;
            case "tokkey_centerLinkOnTile":
                Context.Player.Position = new Vector2(((int)Context.Player.Position.X & 0xf0) | 8,
                    ((int)Context.Player.Position.Y & 0xf0) | 8);
                Context.Player.Face(Vector2I.Right);
                break;
            case "tokkey_makeLinkPlayTuneOfCurrents": _songUpdate = 0; break;
            default: throw UnsupportedCommand($"run native {handler}");
        }
    }

    private void UpdateSong()
    {
        if (_songUpdate < 0) return;
        int update = _songUpdate++;
        if (update < _song.SongInitialDelay) return;
        if (update == _song.SongInitialDelay)
        {
            Context.Player.BeginHarpPose();
            Context.Sound.PlaySound(OracleSoundEngine.SndTuneOfCurrents);
            return;
        }
        int phraseUpdate = update - _song.SongInitialDelay - 1;
        if (phraseUpdate < _song.SongPhaseFrames * _song.SongPhases)
        {
            bool right = ((phraseUpdate / _song.SongPhaseFrames) & 1) != 0;
            if ((Context.Entities.FrameCounter & 0x1f) == 0)
                SpawnNote(Context.Player.Position + new Vector2(right ? 8 : -8, -8), right);
            Context.Player.AdvanceHarpPose();
            return;
        }
        Signal |= 0x80;
        Context.Player.EndHarpPose();
        _songUpdate = -1;
    }

    private void SpawnNote(Vector2 position, bool right, int z = 0)
    {
        var actor = Context.Entities.Spawn<NpcCharacter>(new CutsceneNpcSpawn(
            _note.ToNpcRecord(Database.Group, Database.Room, (int)position.Y, (int)position.X),
            $"TokkeyMusicNote{_effectSerial++}"));
        actor.Position = position;
        actor.SetScriptDrawOffset(new Vector2(0, z));
        actor.SetFixedDrawPriority(NpcCharacter.InFrontOfLinkZIndex);
        _notes.Add(new(actor, _note.Duration,
            new Vector2((right ? 1 : -1) * _note.VelocityXFixed, _note.VelocityYFixed) / 256f));
    }

    private void UpdateNotes()
    {
        for (int i = _notes.Count - 1; i >= 0; i--)
        {
            TokkeyNote note = _notes[i];
            if (note.Fresh) { note.Fresh = false; continue; }
            if (--note.Remaining == 0)
            {
                note.Actor.SetActive(false);
                _notes.RemoveAt(i);
                continue;
            }
            note.Actor.Position += note.Velocity;
            if (_note.Sway && (Context.Entities.FrameCounter & 7) == 0)
            {
                ReadOnlySpan<int> sway = [-1, -2, -1, 0, 1, 2, 1, 0];
                note.Actor.Position += Vector2.Right * sway[(Context.Entities.FrameCounter >> 3) & 7];
            }
        }
    }
}

internal sealed record TokkeyNote(NpcCharacter Actor, int Duration, Vector2 Velocity)
{
    internal int Remaining { get; set; } = Duration;
    internal bool Fresh { get; set; } = true;
}
