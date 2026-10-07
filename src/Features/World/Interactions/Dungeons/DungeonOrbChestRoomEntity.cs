using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class DungeonOrbChestRoomEntity : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly OracleRuntimeState _runtime;
    private readonly Action<int> _sound;
    private readonly Action _writeChest;
    private readonly Func<bool> _textActive;
    private readonly Func<bool> _itemFlag;
    private readonly int _mask;
    private readonly ChestAfterPuffScript _script;
    private bool _initialized, _itemChecked, _scriptStopped;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int Counter => _script.Counter;
    internal DungeonOrbChestRoomEntity(DungeonObjectRecord record,OracleRuntimeState runtime,
        Action<int> sound,Func<bool> textActive,Func<bool> itemFlag,Action writeChest)
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/orb_chest_scripts.tsv",
            new GeneratedTableSchema("orb chest scripts", GeneratedTableKeySemantics.Unique,
                ["subid", "mask", "wait", "source"], ["subid"], headerRequired: true));
        if (table.Rows.Count != 2 || record.SubId is not (2 or 3)) throw new InvalidOperationException($"Unknown orb script at {record.Source}.");
        var row = table.Rows[record.SubId - 2];
        if (row.HexByte(0) != record.SubId) throw row.Invalid(0, "ordered dungeon04 script subids02/03");
        _mask = row.HexByte(1); _script = new(row.UnsignedDecimal(2));
        _runtime = runtime; _sound = sound; _textActive = textActive; _itemFlag = itemFlag; _writeChest = writeChest; Position = record.Position;
        Visible = false;
    }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance(spawns,frame.Player.IsDying);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
        => throw new InvalidOperationException("INTERAC$20 orb chest preload requires Link's live death-trigger state.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException("INTERAC$20 orb chest preload requires Link's live death-trigger state.");
        if (!_initialized) Advance(spawns,player.IsDying);
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        // Pending outgoing state0 reaches interactionDeleteAndRetIfEnabled02;
        // initialized ordinary interactions are retained until bulk clearing.
        if (!_initialized) Finished = true;
    }
    private void Advance(ICollection<RoomEntitySpawn> spawns,bool deathTriggered)
    {
        if (Finished) return;
        bool initializing = !_initialized;
        if (!_initialized)
        {
            _initialized = true;
            _runtime.SetWramByte(0xcfc1, 0); _runtime.SetWramByte(0xcfc2, 0);
        }
        if (deathTriggered || _textActive()) return;
        if (_scriptStopped) { Finished = true; return; }
        if (!_itemChecked)
        {
            _itemChecked = true;
            if (_itemFlag())
            {
                // state0 tail-jumps to interactionRunScript without checking
                // carry. stopifitemflagset installs stubScript; state1 deletes
                // it on the next eligible update, even if the flag changes.
                _scriptStopped = initializing; Finished = !initializing; return;
            }
        }
        Finished = _script.Advance((_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress)&_mask) != 0,
            Position,spawns,_sound,_writeChest);
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
