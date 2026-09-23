using Godot;
using System;

namespace oracleofages;

// INTERAC$33 state6/stateA. The encounter owner supplies eligible updates;
// this sequence owns their counter1, source cursor and interaction position.
internal sealed class SmogTileSequence
{
    private SmogTileWrite[] _tiles = [];
    private int _index, _scanPosition, _rows, _columns;
    internal int Counter { get; private set; }
    internal Vector2I Position { get; private set; }
    internal bool Complete { get; private set; } = true;
    internal bool Clearing { get; private set; }

    internal void BeginGeneration(SmogPhaseRecord phase, Vector2I position)
    {
        _tiles = phase.Tiles; _index = 0; Position = position;
        Counter = 5; Clearing = false; Complete = false;
    }

    internal void BeginClearing(Vector2I position)
    {
        Position = position; _scanPosition = 0x11; _rows = 9; _columns = 13;
        Counter = 5; Clearing = true; Complete = false;
    }

    internal void Update(Func<int,byte> collision, Func<int,int,bool> setTile, Action<Vector2I> puff)
    {
        if (Complete || --Counter != 0) return;
        Counter = 5;
        if (!Clearing)
        {
            // The terminator is read on its own fifth update. State7 then
            // inherits counter1=5; no spawn occurs in this state6 update.
            if (_index == _tiles.Length) { Complete = true; return; }
            var tile = _tiles[_index];
            Position = Unpack(tile.Position);
            // setTile's Z result means the changed-tile queue is full. A
            // same-value tile write still succeeds and advances the pointer.
            if (!setTile(tile.Position,tile.Tile)) return;
            _index++;
            puff(Position);
            return;
        }

        while (_rows != 0)
        {
            if (collision(_scanPosition) != 0)
            {
                Position = Unpack(_scanPosition);
                // stateA ignores setTile failure and always attempts its puff.
                // Leave the cursor here; only a later scan of zero advances it.
                setTile(_scanPosition,0xa3);
                puff(Position);
                return;
            }
            _scanPosition++;
            if (--_columns != 0) continue;
            _columns = 13;
            _scanPosition += 3;
            _rows--;
        }
        Complete = true;
    }

    private static Vector2I Unpack(int packed) => new((packed & 15) * 16 + 8,(packed & 0xf0) + 8);
}
