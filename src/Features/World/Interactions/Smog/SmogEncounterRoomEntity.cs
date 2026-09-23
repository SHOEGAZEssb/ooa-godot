using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SmogEncounterRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime, ISmogEncounterWorld,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly SmogEncounterServices _world;
    private Player? _player;
    internal SmogEncounterController Controller { get; }
    // smog.s uses Interaction.counter2 ($47) with the ENEMY page still in d.
    // Preserve that aliased byte. interactionCode33 never reads counter2;
    // its tile/phase delays use counter1, so this must not alter their timing.
    internal byte Counter2Alias { get; private set; }
    internal void WriteCounter2Alias(int value) => Counter2Alias = unchecked((byte)value);
    public bool Finished => Controller.Finished;
    public bool UpdatesDuringDialogue => Controller.State == 0;

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // State0 only reads room globals and requests its intro allocation;
        // it never touches Link's coordinates. Enemy preload must precede it.
        if (Controller.State == 0) Controller.Update(this);
        return ScreenTransitionPresentation.Hidden;
    }

    internal SmogEncounterRoomEntity(SmogControllerDatabase data, Vector2I position, SmogEncounterServices world)
        : base(new Node2D { Name = "SmogController_33", Position = position, Visible = false },_ => {})
    {
        _world = world; Controller = new(data,position);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _player = frame.Player;
        try { Controller.Update(this); Entity.Position = Controller.Position; }
        finally { _player = null; }
    }

    private Player Link => _player ?? throw new InvalidOperationException("INTERAC$33 player access requires its active interaction update.");
    int ISmogEncounterWorld.RoomFlags => _world.RoomFlags();
    bool ISmogEncounterWorld.EntryBusy => _world.EntryBusy();
    int ISmogEncounterWorld.EnemyCount => _world.EnemyCount();
    Vector2I ISmogEncounterWorld.LinkHighPosition
    {
        get => new(OracleObjectPosition.HighByte(Link.Position.X),OracleObjectPosition.HighByte(Link.Position.Y));
        set
        {
            Link.SetScriptedCoordinateHigh(false,value.Y);
            Link.SetScriptedCoordinateHigh(true,value.X);
        }
    }
    byte ISmogEncounterWorld.LinkZHigh { get => Link.ScriptedZHigh; set => Link.SetScriptedZHigh(value); }
    bool ISmogEncounterWorld.LinkNormal => Link.NativeNormalStateForInteraction;
    bool ISmogEncounterWorld.LinkInAir => Link.NativeInAirForInteraction;
    void ISmogEncounterWorld.ApplyResetPenalty() => Link.Inventory.ApplySmogResetPenalty();
    void ISmogEncounterWorld.LockLinkAndMenu() => _world.LockLinkAndMenu();
    void ISmogEncounterWorld.EnableLinkCollisionsAndMenu() => _world.EnableLinkCollisionsAndMenu();
    void ISmogEncounterWorld.SetResetFlag(bool enabled) => _world.SetResetFlag(enabled);
    void ISmogEncounterWorld.Spawn(SmogEnemySpawn spawn) => _world.Spawn(spawn);
    void ISmogEncounterWorld.Puff(Vector2I position) => _world.Puff(position);
    byte ISmogEncounterWorld.Collision(int position) => _world.Collision(position);
    byte ISmogEncounterWorld.Tile(Vector2I position) => _world.Tile(position);
    bool ISmogEncounterWorld.SetTile(int position,int tile) => _world.SetTile(position,tile);
    void ISmogEncounterWorld.MergeClouds(int phase) => _world.MergeClouds(phase);
    void ISmogEncounterWorld.PlayResetSound() => _world.PlayResetSound();
    void ISmogEncounterWorld.DecrementEnemyCount() => _world.DecrementEnemyCount();
}
