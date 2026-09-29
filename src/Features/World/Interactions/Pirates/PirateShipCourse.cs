using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>The offscreen global course in code/ages/pirateShip.s.</summary>
internal sealed class PirateShipCourse
{
    private readonly List<Turn>[] _turns = [[], []];
    private readonly (byte Y, byte X)[] _speed = new (byte, byte)[4];

    internal PirateShipCourse()
    {
        var course = GeneratedTable.Load("res://assets/oracle/world/pirate_ship_course.tsv",
            new GeneratedTableSchema("pirate ship course", GeneratedTableKeySemantics.Unique,
                ["linked", "order", "room", "tile", "direction", "source"], ["linked", "order"], headerRequired: true));
        foreach (var row in course.Rows)
        {
            int linked = row.Decimal(0), order = row.Decimal(1), direction = row.Decimal(4);
            if (linked is < 0 or > 1 || order != _turns[linked].Count || direction is < 0 or > 3)
                throw row.Invalid(0, "ordered linked/unlinked course with cardinal directions");
            _turns[linked].Add(new((byte)row.HexByte(2), (byte)row.HexByte(3), (byte)direction));
        }
        var speeds = GeneratedTable.Load("res://assets/oracle/world/pirate_ship_speed.tsv",
            new GeneratedTableSchema("pirate ship speed", GeneratedTableKeySemantics.Unique,
                ["direction", "dy", "dx", "source"], ["direction"], headerRequired: true));
        int index = 0;
        foreach (var row in speeds.Rows)
        {
            if (row.Decimal(0) != index || index >= 4) throw row.Invalid(0, "four ordered speed pairs");
            _speed[index++] = ((byte)row.HexByte(1), (byte)row.HexByte(2));
        }
        if (_turns[0].Count != 6 || _turns[1].Count != 12 || index != 4)
            throw new InvalidOperationException("code/ages/pirateShip.s: incomplete generated course.");
    }

    internal void Update(OracleSaveData save, OracleRuntimeState runtime, bool textActive, bool playingInstrument)
    {
        if (save.HasGlobalFlag(GlobalFlag.PiratesGone)) return;
        byte y = save.ReadWramByte(WramAddress.wPirateShipY);
        byte x = save.ReadWramByte(WramAddress.wPirateShipX);
        byte room = save.ReadWramByte(WramAddress.wPirateShipRoom);
        byte direction = save.ReadWramByte(WramAddress.wPirateShipAngle);
        // The tile signal is consumed before the text/instrument movement gate.
        if ((y & 15) == 8 && (x & 15) == 8)
            runtime.SetWramByte(WramAddress.wPirateShipChangedTile, (byte)((y & 0xf0) | (x >> 4)));
        byte tile = runtime.ReadWramByte(WramAddress.wPirateShipChangedTile);
        if (tile != 0)
        {
            runtime.SetWramByte(WramAddress.wPirateShipChangedTile, 0);
            foreach (Turn turn in _turns[save.IsLinkedGame ? 1 : 0])
            {
                if (turn.Room != room || turn.Tile != tile) continue;
                direction = turn.Direction;
                save.WriteWramByte(WramAddress.wPirateShipAngle, direction);
                break;
            }
        }
        // mainThread copies the incremented low playtime byte to wFrameCounter
        // before cutscene01 calls this handler. No private ship clock exists.
        if (!textActive && !playingInstrument && (save.ReadWramByte(WramAddress.wPlaytimeCounter) & 1) == 0)
        {
            var speed = _speed[direction & 3];
            y = unchecked((byte)(y + speed.Y));
            x = unchecked((byte)(x + speed.X));
        }
        // updatePirateShipRoom runs even when movement is paused.
        switch (direction & 3)
        {
            case 0 when y == 0xf8: y = 0x80; room = unchecked((byte)(room - 0x10)); break;
            case 1 when x == 0x98: x = 0; room = unchecked((byte)(room + 1)); break;
            case 2 when y == 0x88: y = 0; room = unchecked((byte)(room + 0x10)); break;
            case 3 when x == 0xf8: x = 0xa0; room = unchecked((byte)(room - 1)); break;
        }
        save.WriteWramByte(WramAddress.wPirateShipY, y);
        save.WriteWramByte(WramAddress.wPirateShipX, x);
        save.WriteWramByte(WramAddress.wPirateShipRoom, room);
    }

    private readonly record struct Turn(byte Room, byte Tile, byte Direction);
}
