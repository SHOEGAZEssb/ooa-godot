using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC$90:$00: first pull always fails; subsequent pulls have a 1/4 chance.
internal sealed partial class MermaidBossKeyRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly OracleRuntimeState _runtime;
    private readonly OracleRandom _random;
    private readonly Func<bool> _itemFlagSet;
    private readonly Func<int> _enemyCount;
    private readonly Action<int,bool> _setTrigger;
    private readonly Action _spawnEnemies;
    private readonly Action<int> _playSound;
    private readonly Action _spawnChest;
    private bool _firstFailure;
    internal int State { get; private set; }
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;

    internal MermaidBossKeyRoomEntity(DungeonObjectRecord record,OracleRuntimeState runtime,OracleRandom random,
        Func<bool> itemFlagSet,Func<int> enemyCount,Action<int,bool> setTrigger,Action spawnEnemies,
        Action<int> playSound,Action spawnChest)
    {
        Position = record.Position; Name = "MermaidBossKey"; Visible = false;
        _runtime = runtime; _random = random; _itemFlagSet = itemFlagSet; _enemyCount = enemyCount;
        _setTrigger = setTrigger; _spawnEnemies = spawnEnemies; _playSound = playSound; _spawnChest = spawnChest;
    }
    private void Advance()
    {
        if (Finished) return;
        if (State == 0) State = 1; // Native initialization falls through.
        if (State == 2) { if (_enemyCount() == 0) State = 1; return; }
        if (State == 3) { PublishTrigger(); _spawnChest(); Finished = true; return; }
        if ((_runtime.ReadWramByte(WramAddress.wLever1PullDistance)&0x80) == 0 &&
            (_runtime.ReadWramByte(WramAddress.wLever2PullDistance)&0x80) == 0) return;
        if (_itemFlagSet()) { PublishTrigger(); Finished = true; return; }
        State = 2;
        if (_firstFailure && (_random.Next().Value&3) == 0) { State = 3; return; }
        _firstFailure = true;
        _playSound(SoundId.SndError);
        _runtime.SetWramByte(WramAddress.wWarpDestPos,_runtime.ReadWramByte(WramAddress.wActiveTilePos));
        // Unlike parseObjectData, this does not also clear wTmpcfc0.
        for (int address = 0xcec0; address < 0xcee0; address++) _runtime.SetWramByte(address,0);
        _random.GeneratePermutation();
        _spawnEnemies();
    }
    private void PublishTrigger()
    { for (int bit = 0; bit < 8; bit++) _setTrigger(bit,bit == 0); }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (State == 0) Advance(); return ScreenTransitionPresentation.Hidden; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
