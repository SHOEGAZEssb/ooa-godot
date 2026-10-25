using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Native INTERAC $6d introduction and $6e rescue actors execute at their
// own live INTERACTION slots. cfd0 belongs to the shared WRAM owner.
internal sealed class PalacePossessionRoomEntity : CutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly PalacePossessionDatabase _data;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _runner;
    private readonly PalacePossessionRoomEntity? _parent;
    private readonly int _guardIndex, _entryDirection;
    private Player _player = null!;
    private Vector2 _precise;
    private int _speed, _angle, _speedZ, _z, _turnAnimation;
    private bool _scriptEnded;
    internal int Id { get; }
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int GhostSignal { get; private set; }
    internal int ZFixed => _z;
    internal Vector2 PrecisePosition => _precise;
    public Node2D Node => _actor;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public override bool DialogueOpen => _owner.IsScriptTextActive();
    public override bool ScriptExecutionBlocked => DialogueOpen || _player.IsDying;
    internal PalacePossessionRoomEntity(RoomEntityManager owner, PalacePossessionDatabase data,
        int id, int subid, Vector2 position, PalacePossessionRoomEntity? parent = null, int guardIndex = 0, int entryDirection = 0)
    {
        _owner = owner; _data = data; Id=id; SubId=subid; _parent=parent; _guardIndex=guardIndex; _entryDirection=entryDirection;
        _actor = NpcCharacter.CreateFromRecord(data.Actor(subid,position,id==0x6d));
        _actor.SetAnimationRate(0); _actor.SetBlocksLink(false); _actor.SetDialogue(0,"",false);
        _precise=position; _runner=new(this);
        _actor.TreeExiting += () => { if (Id==0x6d && SubId==0 && !Finished) SetControl(false); };
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    private void Start(string script)
    {
        _scriptEnded=false; _runner.Start(_data.Commands,_data.Entry(script));
        _runner.SetInitialMotionRegisters(new("Actor"),_speed,_angle);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) => ScreenTransitionPresentation.Hidden;
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if (player is not null && State==0) Initialize(player);
        return _actor.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    private void Initialize(Player player)
    {
        _player=player;
        if (Id==0x6d)
        {
            if (SubId==0)
            {
                if (_owner.SaveData!.HasGlobalFlag(0x19)) { Finish(); return; }
                _actor.SetScriptPaletteOverride(_data.Palettes[6]);
                if (_owner.SaveData.HasGlobalFlag(0x18)) { SpawnBoss(); return; }
                if (!_owner.InteractionSlotAvailable) return;
                _owner.AddEntity(new PalacePossessionRoomEntity(_owner,_data,0x6d,2,Node.Position,this));
                _player.RequestNativeForcedMovement(0x0e,_entryDirection*8,_entryDirection);
                SetControl(true); State=1; _actor.ShowInitializedPresentation(); Start("possessedNayru_beginFightScript");
            }
            else { State=1; _z=-0x400; }
            return;
        }
        State=1; _actor.ShowInitializedPresentation();
        switch (SubId)
        {
            case 0:
                SetPosition(new(0x78,0x58)); _speed=0x0a; _actor.SetBasePalette(1);
                _player.SetScriptedPosition(new(0x78+_player.Position.X%1,0x64+_player.Position.Y%1)); _player.Face(Vector2I.Up);
                for (int address=0xcfd0; address<0xcfe0; address++) _owner.RuntimeState.SetWramByte(address,0);
                _owner.BeginPalacePossessionFade(false,1); SetAnimation(0x0a); Start("interaction6e_subid00Script"); break;
            case 1:
                _speed=0x28; _actor.SetBasePalette(1); Start("interaction6e_subid01Script_part1"); break;
            case 2:
                _speed=0x50; _angle=0x0e; Counter=0x48; Show(0x560c); _owner.OnSoundRequested(SoundId.SndCtrlStopMusic); break;
            case 3: _speed=0x50; Start("interaction6e_subid03Script"); break;
            case 4: _speed=0x3c; SetPosition(new(0x78,0xb0)); Start($"interaction6e_guard{_guardIndex}Script"); break;
            default: throw new NotSupportedException($"nayruSavedCutscene.s: INTERAC$6e:${SubId:x2}.");
        }
        DrawZ();
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        _player=frame.Player;
        if (State==0) { Initialize(frame.Player); DrawZ(); return; }
        if (Id==0x6d)
        {
            if (SubId==0)
            {
                if (State==1) { _runner.AdvanceFrame(); if (_scriptEnded) { State=2; SpawnBoss(); } }
                else if (State==2) SpawnBoss();
                else { _owner.SaveData!.SetGlobalFlag(0x18); SetControl(false); Finish(); }
            }
            else if (State==1)
            {
                if (_parent!.GhostSignal!=0) { _parent.GhostSignal++; State=2; _speed=0x14; _actor.ShowInitializedPresentation(); Start("possessedNayru_veranGhostScript"); }
            }
            else
            {
                _runner.AdvanceFrame();
                if (_scriptEnded) { _parent!.GhostSignal=0; Finish(); }
                else _actor.AdvanceAnimationUpdates(1);
            }
            DrawZ(); return;
        }
        switch (SubId)
        {
            case 0:
                if (State==1)
                {
                    _runner.AdvanceFrame();
                    if (_scriptEnded) { State=2; _owner.BeginPalacePossessionFade(true,4); }
                    else _actor.AdvanceAnimationUpdates(1);
                }
                else if (!_owner.PalacePossessionPaletteActive())
                { _owner.SaveData!.SetGlobalFlag(0x19); _owner.RequestNayruRescueWarp(); Finish(); }
                break;
            case 1: UpdateAmbi(frame.Counter); break;
            case 2: UpdateGhost(frame); break;
            default: _runner.AdvanceFrame(); _actor.AdvanceAnimationUpdates(1); break;
        }
        DrawZ();
    }
    private void SpawnBoss()
    {
        if (_owner.TryCreatePalacePossessionEnemy(Node.Position,0) is not null) State=3;
    }
    private void UpdateAmbi(int frame)
    {
        switch (State)
        {
            case 1:
                if (!Gravity()) return;
                _runner.AdvanceFrame();
                if (_scriptEnded) { State=2; Counter=244; _turnAnimation=5; }
                _actor.AdvanceAnimationUpdates(1); break;
            case 2:
                Counter=(Counter-1)&255;
                if (Counter==0) { State=3; SetAnimation(6); }
                else if (Counter>=0xc1) _actor.AdvanceAnimationUpdates(1);
                else if ((Counter&31)==0) { _turnAnimation^=2; SetAnimation(_turnAnimation); }
                break;
            case 3:
                if (ReadMemory("RescueSignal")==7)
                {
                    State=4; SetPosition(new(0x78+Node.Position.X%1,Node.Position.Y));
                    _actor.SetBasePalette(6); _actor.SetScriptPaletteOverride(_data.Palettes[6]);
                    Start("interaction6e_subid01Script_part2"); _owner.OnSoundRequested(SoundId.SndLightning);
                    _owner.OnSoundRequested(SoundId.MusDisaster); _owner.BeginPalacePossessionFade(false,4);
                }
                else if ((frame&1)==0) SetPosition(new((((int)Node.Position.X+1)&1)+0x78+Node.Position.X%1,Node.Position.Y));
                break;
            case 4:
                if (!_owner.PalacePossessionPaletteActive()) { if (!Gravity()) return; _runner.AdvanceFrame(); }
                _actor.AdvanceAnimationUpdates(1); break;
        }
    }
    private void UpdateGhost(RoomEntityFrame frame)
    {
        if (State==1)
        {
            if (Counter==0x48) _owner.OnSoundRequested(SoundId.SndBeam);
            if (--Counter!=0) { Move(); _actor.AdvanceAnimationUpdates(1); return; }
            Counter=0xac; State=2; _angle=0x16;
            _player.RequestNativeForcedMovement(4,0,2); _owner.OnSoundRequested(SoundId.SndCircling);
        }
        if (State==2)
        {
            if (--Counter!=0)
            {
                if (Counter==0x56) _owner.OnSoundRequested(SoundId.SndCircling);
                if ((Counter&1)!=0) _angle=(_angle-1)&31;
                SetPosition(new Vector2(0x78+_precise.X%1,0x7e+_precise.Y%1)+(Vector2)NativeObjectMovement.CircleArcOffset(_owner.RuntimeState,0x10,_angle));
                _actor.AdvanceAnimationUpdates(1); return;
            }
            Counter=0x50; State=3; _speed=5; _angle=0x10; _owner.OnSoundRequested(SoundId.SndBossDamage);
        }
        if (--Counter==0) { WriteMemory("RescueSignal",7); Finish(); return; }
        _actor.Visible=!_actor.Visible; Move(); _actor.AdvanceAnimationUpdates(1);
    }
    private bool Gravity() => OracleObjectMath.UpdateSpeedZ(ref _z,ref _speedZ,0x30);
    private void DrawZ() => _actor.SetScriptDrawOffset(Vector2.Down*(_z>>8));
    private void Move() => _actor.SetStatePosition(NativeObjectMovement.ApplySpeed(_owner.RuntimeState,ref _precise,_speed,_angle));
    private void SetPosition(Vector2 position) { _precise=position; _actor.SetStatePosition(position); }
    private void SetAnimation(int index) => _actor.SetScriptAnimation(_data.Animation(index));
    private void Finish() { Finished=true; _actor.SetActive(false); }
    private void SetControl(bool disabled)
    {
        _owner.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects,disabled?(byte)1:(byte)0);
        _owner.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled,disabled?(byte)1:(byte)0);
    }
    private void Show(int id) => _owner.OnRoomEntityDialogueRequested(id,_data.Text(id),Node.Position);
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value=="Actor";
    public override int ReadMemory(string binding) => binding switch { "GhostSignal" => GhostSignal,
        "RescueSignal" => _owner.RuntimeState.ReadWramByte(0xcfd0), _ => throw UnsupportedCommand(binding) };
    public override bool MemoryEquals(string binding,int value) => ReadMemory(binding)==value;
    public override void WriteMemory(string binding,int value)
    {
        if (binding=="RescueSignal") _owner.RuntimeState.SetWramByte(0xcfd0,(byte)value);
        else if (binding=="LinkDirection") _player.Face(value switch {0=>Vector2I.Up,1=>Vector2I.Right,2=>Vector2I.Down,3=>Vector2I.Left,_=>throw UnsupportedCommand(binding)});
        else throw UnsupportedCommand(binding);
    }
    public override void WriteObjectByte(string actor,int address,int value)
    {
        if (address==0x77) GhostSignal=value;
        else if (address==0x49) _angle=value;
        else throw UnsupportedCommand($"INTERAC$6d/$6e.${address:x2}");
    }
    public override void SetActorAnimation(string actor,int animation,string encodedAnimation) => _actor.SetScriptAnimation(encodedAnimation);
    public override void MoveActorAtSpeed(string actor,int speed,int angle) => Move();
    public override void ShowText(int id,string message,int? position) => _owner.OnRoomEntityDialogueRequested(id,message,Node.Position);
    public override void ScriptEnded() => _scriptEnded=true;
    public override void RunNativeHandler(string handler)
    {
        string[] parts=handler.Split(':');
        switch (parts[0])
        {
            case "Exclaim": _owner.OnSoundRequested(SoundId.SndCtrlStopMusic);
                if (_owner.InteractionSlotAvailable) _owner.CreatePalaceExclamation(Node.Position+new Vector2(8,-12),24); break;
            case "MoveLink": _player.RequestNativeForcedMovement(8,0,0); break;
            case "Sound": _owner.OnSoundRequested(int.Parse(parts[1])); break;
            case "Swirl": _owner.CreateEnergySwirl(Node.Position,0xff); break;
            case "Jump": _speedZ=int.Parse(parts[1])==0?-0x180:-0x100; break;
            case "SpawnGuard": Spawn(4,Vector2.Zero,int.Parse(parts[1])); break;
            case "Spawn": Spawn(Convert.ToInt32(parts[1],16),new(Convert.ToInt32(parts[3],16),Convert.ToInt32(parts[2],16))); break;
            case "Preset": var row=_data.Table("presets"); int index=int.Parse(parts[1])*2; _angle=row[index]; SetAnimation(row[index+1]); break;
            case "GuardAnimation": SetAnimation(_data.Table("guard-animations")[_guardIndex]); break;
            case "GuardCenter": var motion=_data.Table("guard-motion"); _speed=0x0a; _angle=motion[_guardIndex*2]; SetAnimation(motion[_guardIndex*2+1]); break;
            default: throw UnsupportedCommand(handler);
        }
    }
    private void Spawn(int subid,Vector2 position,int guardIndex=0)
    { if (_owner.InteractionSlotAvailable) _owner.AddEntity(new PalacePossessionRoomEntity(_owner,_data,0x6e,subid,position,guardIndex:guardIndex)); }
}
