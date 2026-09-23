using Godot;
using System;
using System.Linq;

namespace oracleofages;

// INTERAC$33. Each call represents one eligible interaction update. World
// ownership/phase scheduling belongs to its room adapter, not to this machine.
internal sealed class SmogEncounterController(SmogControllerDatabase data, Vector2I position)
{
    private readonly SmogTileSequence _tiles = new();
    private int _counter, _savedSpawnIndex;
    private Vector2I _linkDestination;
    internal int State { get; private set; }
    internal int Phase { get; private set; }
    internal int SpawnIndex { get; private set; } = 0xff;
    internal Vector2I Position { get; private set; } = position;
    internal bool Finished { get; private set; }
    internal int Counter => State is 6 or 10 ? _tiles.Counter : _counter;

    internal void Update(ISmogEncounterWorld world)
    {
        if (Finished) return;
        switch (State)
        {
            case 0:
                if ((world.RoomFlags & 0x80) != 0) { Finished = true; return; }
                world.LockLinkAndMenu();
                if (world.EntryBusy) return;
                State = 1; SpawnNext(world); world.Puff(Position); return;
            case 1:
                if (world.EnemyCount != 1) return;
                world.LockLinkAndMenu(); State = 2; return;
            case 2:
                world.LinkZHigh = unchecked((byte)(world.LinkZHigh - 1));
                if (world.LinkZHigh > 0xf9) return;
                _linkDestination = data.Phases[Phase].LinkPosition;
                State = 3; return;
            case 3:
                var link = world.LinkHighPosition;
                if (link.Y != _linkDestination.Y) link.Y += Math.Sign(_linkDestination.Y - link.Y);
                else if (link.X != _linkDestination.X) link.X += Math.Sign(_linkDestination.X - link.X);
                else { State = 4; return; }
                world.LinkHighPosition = link; return;
            case 4:
                world.LinkZHigh = unchecked((byte)(world.LinkZHigh + 1));
                if (world.LinkZHigh == 0) State = 5;
                return;
            case 5:
                if (world.EnemyCount != 1) return;
                _tiles.BeginGeneration(data.Phases[Phase],Position); State = 6; return;
            case 6:
                UpdateTiles(world);
                if (_tiles.Complete) { _counter = _tiles.Counter; State = 7; }
                return;
            case 7:
                _counter = (_counter - 1) & 255;
                if (_counter != 0) return;
                world.SetResetFlag(false);
                _savedSpawnIndex = SpawnIndex;
                for (int i = 0; i < data.Phases[Phase].Enemies.Length; i++) SpawnNext(world);
                world.EnableLinkCollisionsAndMenu();
                Position = new(0x18,0x14); State = 8; return;
            case 8:
                if (world.EnemyCount == 1) { State = 9; return; }
                if (world.EnemyCount != 2)
                {
                    bool reset = world.Tile(Position) != 0x0c;
                    if (!reset && world.LinkNormal && !world.LinkInAir &&
                        Math.Abs(world.LinkHighPosition.X - Position.X) + Math.Abs(world.LinkHighPosition.Y - Position.Y) < 4)
                    {
                        world.SetTile(0x11,0x11); reset = true;
                    }
                    if (reset)
                    {
                        world.ApplyResetPenalty();
                        world.PlayResetSound(); world.SetResetFlag(true);
                        SpawnIndex = _savedSpawnIndex;
                        BeginCleanup(world); return;
                    }
                }
                world.MergeClouds(Phase); return;
            case 9:
                Phase++;
                if (Phase == 4) { world.DecrementEnemyCount(); Finished = true; return; }
                BeginCleanup(world); return;
            case 10:
                UpdateTiles(world);
                if (_tiles.Complete) { _counter = _tiles.Counter; State = 1; }
                return;
            default: throw new NotSupportedException($"INTERAC$33 state${State:x2} is not represented.");
        }
    }

    private void SpawnNext(ISmogEncounterWorld world)
    {
        SpawnIndex = (SpawnIndex + 1) & 255;
        var stream = data.Phases.SelectMany(phase => phase.Enemies).Prepend(data.Intro).ToArray();
        if (SpawnIndex >= stream.Length) throw new NotSupportedException($"INTERAC$33 spawn index${SpawnIndex:x2} exceeds @smogEnemyData.");
        world.Spawn(stream[SpawnIndex] with { Phase = Phase });
    }

    private void BeginCleanup(ISmogEncounterWorld world)
    {
        world.LockLinkAndMenu();
        _tiles.BeginClearing(Position); State = 10;
    }

    private void UpdateTiles(ISmogEncounterWorld world)
    {
        _tiles.Update(world.Collision,world.SetTile,world.Puff);
        Position = _tiles.Position;
    }
}
