using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC_CLOAKED_TWINROVA $8d:$02 and INTERAC_TWINROVA $93:$02/$03.
internal sealed class TwinrovaRevealRoomEntity : CutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEventContext _context;
    private readonly MakuSeedDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private readonly TwinrovaRevealRoomEntity? _parent;
    private readonly Action<IRoomEntity> _spawn;
    private readonly int _id, _subid;
    private Vector2 _precise;
    private int _state, _substate, _counter, _presetIndex, _angle, _animation = -1, _z;
    private bool _scriptEnded;
    public Node2D Node => _actor;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => _state == 0 || _id == 0x93;
    public bool UpdatesDuringRoomEntityFreeze => _state == 0 || _id == 0x93;
    public override bool DialogueOpen => _context.DialogueOpen;
    public override bool ScriptExecutionBlocked => _context.ScriptDialogueOpen || _context.Player.IsDying;
    internal TwinrovaRevealRoomEntity(RoomEventContext context, MakuSeedDatabase data, int id, int subid,
        Action<IRoomEntity> spawn,
        TwinrovaRevealRoomEntity? parent = null)
    {
        _context = context; _data = data; _id = id; _subid = subid; _parent = parent; _spawn = spawn;
        _actor = data.CreateActor(id, subid, new(0x50, 0x28));
        _runner = new(this);
    }
    private void Animation(int animation)
    {
        if (_animation == animation) return;
        _animation = animation; _actor.SetScriptAnimation(_data.Animation(_id, animation));
    }
    private void Finish() { Finished = true; _actor.SetActive(false); }
    private void Publish() => _context.Entities.RuntimeState.SetWramByte(0xcfd5, (byte)_actor.Position.Y);
    private void CopyParent()
    {
        _precise = new(0xa0 - (int)_parent!._precise.X, (int)_parent._precise.Y);
        _actor.SetStatePosition(_precise); _z = _parent._z & ~0xff;
        _actor.SetScriptDrawOffset(new(0, _z >> 8)); _actor.SetScriptVisible(_parent._actor.Visible);
        Animation(_parent._animation switch { 1 => 3, 3 => 1, var other => other });
    }
    private bool LoadPreset()
    {
        int[] table = _data.Table(_substate == 3 ? "twinrova-departure" : "twinrova-arrival");
        _angle = table[_presetIndex * 2];
        int counter = table[_presetIndex * 2 + 1];
        if (counter == 0) return false;
        _counter = counter; _presetIndex++; return true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_state == 0)
        {
            _state = 1;
            _actor.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            if (_id == 0x8d)
            { _actor.SetScriptVisible(false); _runner.Start(_data.Commands("cloaked")); return; }
            if (_subid == 3) { _actor.SetBasePalette(1); CopyParent(); return; }
            _actor.SetBasePalette(2); _precise = new(0x50, 0x38); _actor.SetStatePosition(_precise); _z = -0x800;
            _actor.SetScriptVisible(false); _actor.SetScriptDrawOffset(new(0, -8));
            if (_context.Entities.InteractionSlotAvailable)
                _spawn(new TwinrovaRevealRoomEntity(_context, _data, 0x93, 3, _spawn, this));
            LoadPreset(); Animation(((_angle + 4) & 0x18) >> 3);
            _context.Sound.PlaySound(SoundId.SndBeam2);
            Publish(); _context.Entities.RuntimeState.SetWramByte(0xcfd6, (byte)_actor.Position.X);
            return;
        }
        if (_id == 0x8d)
        {
            _runner.AdvanceFrame();
            if (_scriptEnded)
            {
                _context.Entities.TryCreateVirePuff(_precise == Vector2.Zero ? _actor.Position : _precise, 0, 0);
                if (_context.Entities.InteractionSlotAvailable)
                    _spawn(new TwinrovaRevealRoomEntity(_context, _data, 0x93, 2, _spawn));
                Finish();
            }
            else _actor.AdvanceAnimationUpdates(1);
            return;
        }
        if (_subid == 3)
        {
            _actor.AdvanceAnimationUpdates(1);
            if (_parent!.Finished) Finish(); else CopyParent();
            return;
        }
        if (_substate == 0)
        {
            if (--_revealDelay == 0) { _actor.SetScriptVisible(true); _substate = 1; }
            return;
        }
        if (_substate == 2)
        {
            _actor.AdvanceAnimationUpdates(1);
            if ((frame.Counter & 7) == 0)
                _z = unchecked((short)(_z + (unchecked((sbyte)_data.Table("z-oscillation")[(frame.Counter >> 3) & 7]) << 8)));
            _runner.AdvanceFrame();
            if (_scriptEnded)
            {
                _context.Sound.PlaySound(SoundId.SndBeam2);
                Publish(); _context.Entities.RuntimeState.SetWramByte(0xcfd6, (byte)_actor.Position.X);
                _substate = 3; _presetIndex = 0; LoadPreset();
            }
        }
        else
        {
            Publish(); _context.Entities.RuntimeState.SetWramByte(0xcfd6, (byte)_actor.Position.X);
            _actor.AdvanceAnimationUpdates(1);
            _actor.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(ref _precise, 0x50, _angle));
            if (--_counter != 0 || LoadPreset()) Animation(((_angle + 4) & 0x18) >> 3);
            else if (_substate == 1) { _substate = 2; _runner.Start(_data.Commands("twinrova")); }
            else { _context.Entities.RuntimeState.SetWramByte(0xcfd0, 9); Finish(); }
        }
        _actor.SetScriptDrawOffset(new(0, _z >> 8));
    }
    private int _revealDelay = 2;
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Actor";
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => Animation(animation);
    public override void WriteObjectByte(string actor, int address, int value)
    { if (address != 0x48) throw UnsupportedCommand($"object byte ${address:x2}"); }
    public override void ShowText(int id, string message) => _context.ShowDialogue(message, sourceTextId: id);
    public override void ShowText(int id, string message, int? position) => _context.ShowDialogue(message, position, sourceTextId: id);
    public override void PlaySound(int sound) => _context.Sound.PlaySound(sound);
    public override void RunNativeHandler(string handler)
    {
        if (handler != "Reveal") throw UnsupportedCommand(handler);
        _actor.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
        _actor.SetScriptVisible(true);
    }
    public override void ScriptEnded() => _scriptEnded = true;
}
