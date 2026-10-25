using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC$20: native tile scripts retain each command's carry/yield result.
internal sealed partial class DungeonTileScriptRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonTileScriptProfile _profile;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<int> _triggers, _torches, _enemies;
    private readonly Func<bool> _completed, _suspended;
    private readonly Action _complete, _wall;
    private readonly Action<byte> _tile;
    private readonly Action<int> _sound;
    private readonly Func<Vector2, bool> _puff;
    private bool _stopped;
    internal bool Initialized { get; private set; }
    internal int Step { get; private set; }
    internal int Counter { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Initialized;

    internal DungeonTileScriptRoomEntity(DungeonObjectRecord record, DungeonTileScriptProfile profile,
        OracleRuntimeState runtime, Func<int> triggers, Func<int> torches, Func<int> enemies,
        Func<bool> completed, Action complete, Func<bool> suspended, Action<byte> tile,
        Action<int> sound, Func<Vector2, bool> puff, Action wall)
    {
        _profile = profile; _runtime = runtime; _triggers = triggers; _torches = torches; _enemies = enemies;
        _completed = completed; _complete = complete; _suspended = suspended;
        _tile = tile; _sound = sound; _puff = puff; _wall = wall;
        Position = record.Position; Visible = false; Name = $"DungeonTileScript_{record.SubId:x2}";
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance(frame.Player);
    private void Advance(Player player)
    {
        if (Finished) return;
        bool initializing = !Initialized;
        if (initializing)
        {
            Initialized = true;
            _runtime.SetWramByte(0xcfc1, 0); _runtime.SetWramByte(0xcfc2, 0);
        }
        if (player.IsDying || _suspended()) return;
        if (_stopped) { Finished = true; return; }
        if (Counter != 0 && --Counter != 0) return;
        if (Step == 0)
        {
            // stopifroomflag* changes the script pointer with carry set.
            if (_profile.Flag != 0 && _completed())
            { _stopped = true; Finished = !initializing; return; }
            Step = 1;
            if (_profile.Kind == DungeonTileScriptKind.EnemyStairs)
            { Counter = _profile.Wait; return; }
        }
        switch (_profile.Kind)
        {
            case DungeonTileScriptKind.NorthEnemyStairs:
                if (Step == 1) { if (_enemies() == 0) Step = 2; return; }
                // checknoenemies yields. settilehere returns carry set,
                // so playsound runs with the tile; scriptend follows later.
                if (Step == 2) { _tile(_profile.Tile); _sound(SoundId.SndSolvePuzzle); Step = 3; return; }
                break;
            case DungeonTileScriptKind.TriggerStairs:
                if (Step == 1) { if (_triggers() == _profile.GateValue) Step = 2; return; }
                if (Step == 2) { _tile(_profile.Tile); _complete(); Step = 3; return; }
                if (Step == 3) { _puff(Position); _sound(SoundId.SndSolvePuzzle); Step = 4; return; }
                break;
            case DungeonTileScriptKind.WallRetraction:
                if (Step == 1) { if (_triggers() == _profile.GateValue) Step = 2; return; }
                if (Step == 2)
                {
                    _runtime.SetWramByte(AgesWramAddress.wDisabledObjects, 0x81);
                    _runtime.SetWramByte(AgesWramAddress.wMenuDisabled, 0x80);
                    player.ClearNativeItemParents();
                    player.ApplyInteractionInvincibility(0x80);
                    Counter = _profile.Wait; Step = 3; return;
                }
                if (Step == 3) { player.ResetPortalDamageState(); _wall(); break; }
                throw new InvalidOperationException($"{_profile.Source}: invalid wall script step {Step}.");
            case DungeonTileScriptKind.EnemyStairs:
                if (Step == 1) { if (_enemies() == _profile.GateValue) Step = 2; return; }
                if (Step == 2) { _sound(SoundId.SndSolvePuzzle); Step = 3; return; }
                if (Step == 3) { _puff(Position); _tile(_profile.Tile); _complete(); Step = 4; return; }
                break;
            case DungeonTileScriptKind.TorchBridge:
                if (Step == 1) { if (_torches() == _profile.GateValue) Step = 2; return; }
                if (Step == 2) { _tile(_profile.Tile); _sound(SoundId.SndSolvePuzzle); Step = 3; return; }
                if (Step == 3) { _puff(Position); Step = 4; return; }
                break;
            default: throw new NotSupportedException($"{_profile.Source}: unsupported tile script {_profile.Kind}.");
        }
        _stopped = true; Finished = !initializing;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException($"{_profile.Source}: script initialization requires Link's death gate.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException($"{_profile.Source}: missing preload Link.");
        if (!Initialized) Advance(player);
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (!Initialized) Finished = true; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}

internal enum DungeonTileScriptKind { TriggerStairs, WallRetraction, EnemyStairs, NorthEnemyStairs, TorchBridge }
internal readonly record struct DungeonTileScriptProfile(DungeonTileScriptKind Kind, int GateValue,
    byte Tile, byte Flag, int Wait, string Source);
