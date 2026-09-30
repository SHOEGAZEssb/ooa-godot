using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBraceletImpactRom(BraceletRom rom, string context)
    {
        int[] nativeEffects = Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0x40)
            .Where(slot => rom[slot] != 0).ToArray();
        FailIf(nativeEffects.Length != 1,
            $"{context}: expected one native impact interaction, got {nativeEffects.Length}.");
        int slot = nativeEffects[0];
        int id = rom[slot + 1];
        Vector2 nativePosition = new(rom[slot + 0xd], rom[slot + 0xb]);
        var debris = _entities.Entities<RockDebrisEffect>();
        var falling = _entities.Entities<FallingDownHoleEffect>();
        if (id is 0x06 or 0x0c)
            FailIf(debris.Count != 1 || debris[0].Position != nativePosition ||
                falling.Count != 0 || _terrain.ActiveSplashCount != 0,
                $"{context}: debris interaction ${id:x2} must start at native high-byte position {nativePosition}, got [{string.Join(',', debris.Select(d => d.Position))}].");
        else if (id is 0x03 or 0x04)
            FailIf(_terrain.ActiveSplashCount != 1 || _terrain.ActiveSplash!.Position != nativePosition ||
                _terrain.ActiveSplash.IsLava != (id == 0x04) || debris.Count != 0 || falling.Count != 0,
                $"{context}: splash interaction ${id:x2} must start at native position {nativePosition} without debris.");
        else if (id == 0x0f)
            // Runtime interactions have already advanced in this gameplay
            // update. This bounded ROM fixture stops at their allocation.
            FailIf(falling.Count != 1 || debris.Count != 0 || _terrain.ActiveSplashCount != 0,
                $"{context}: falling interaction $0f must replace the child without debris or splash.");
        else
            throw new InvalidOperationException($"{context}: unsupported native impact interaction ${id:x2}.");
    }

    private void CompareBraceletHazardsRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (byte tile in new byte[] { 0xf3, 0xfa, 0x61 })
        foreach (bool airborneWall in new[] { false, true })
        {
            BraceletRom rom = PrepareBraceletRom();
            StepBraceletRom(rom, 1, batched, held: true, pressed: true);
            StepBraceletRom(rom, 11, batched, 16, held: true);
            StepBraceletRom(rom, 20, batched);
            StepBraceletRom(rom, 3, batched, 16, moveLink: true);
            _player.Face(airborneWall ? Vector2I.Down : Vector2I.Up);
            rom[0xd008] = (byte)(airborneWall ? 2 : 0);
            StepBraceletRom(rom, 1, batched);
            // The upward throw lands with its center in floor row $03.
            // Only the native YH+$05 foot probe reaches hazard row $04.
            // The bottom-right quarter of a hazard tile stops a downward
            // throw after its center enters the tile, while Z is negative.
            // objectCheckIsOnHazard must reject that airborne contact.
            _currentRoom.SetPositionTileAndCollision(new(120, airborneWall ? 120 : 72),
                tile, (byte)(airborneWall ? 0x01 : 0x10), 0);
            rom.CopyRoom(_currentRoom);
            StepBraceletRom(rom, 1, batched, airborneWall ? 16 : 0, held: true, pressed: true);
            StepBraceletRom(rom, 40, batched);
            int expectedId = tile switch { 0xf3 => 0x0f, 0xfa => 0x03, 0x61 => 0x04, _ => 0 };
            int slot = Enumerable.Range(0xd0, 16).Select(page => page * 256 + 0x40)
                .Single(address => rom[address] != 0);
            int y = rom[slot + 0xb];
            bool wrongId = airborneWall ? rom[slot + 1] is not (0x06 or 0x0c) : rom[slot + 1] != expectedId;
            bool wrongPosition = airborneWall ? y < 112 || y + 5 >= 128 : y >= 64 || y + 5 < 64;
            FailIf(wrongId || wrongPosition ||
                _bracelet.LiftedObject is not null || rom.ChildActive,
                $"Bracelet hazard ${tile:x2}, airborneWall={airborneWall}: impact must use the native hazard position/height gate, got ${rom[slot + 1]:x2} at Y={y}.");
            StepBraceletRom(rom, 3, batched);
            FailIf(_bracelet.LiftedObject is not null || _bracelet.State != BraceletState.Idle,
                "Hazard-deleted Bracelet child must release ownership and remain retired.");
        }
        GD.Print("Validated executed-ROM Bracelet hole/water/lava landing at the Y+$05 terrain boundary, airborne impact height gate, effect type and child retirement through individual/batched gameplay updates.");
    }
}
