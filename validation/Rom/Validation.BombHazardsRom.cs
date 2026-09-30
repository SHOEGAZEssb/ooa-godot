using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombHazardsRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (byte tile in new byte[] { 0xf3, 0xfa, 0xfc })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Bombs, 0x10);
            _inventory.EquipA(TreasureId.Bombs);
            _inventory.EquipB(0);
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0x3a, 0, 0);
            _player.WarpTo(new(80, 112));
            StepGameplayUpdates(33, Vector2.Up);
            FailIf(_player.Position != new Vector2(80, 79) || _collision.Collides(_player.Position),
                "Bomb hazard fixture must walk to the planting point on floor geometry.");
            var seed = _random.CaptureState();
            var rom = new BombRom(0, 0xff, _inventory.Bombs, seed.Rng1 | seed.Rng2 << 8);
            rom[0xd00d] = 80;
            rom[0xd00b] = 79;
            rom[0xcc33] = (byte)_currentRoom.ActiveCollisions;
            CopyBombRoomToRom(rom);
            int update = 0;
            void Step(int count, bool press = false)
            {
                int first = update;
                StepGameplayUpdates(count, Vector2.Zero, press ? ["attack"] : [], press ? ["attack"] : [], batched,
                    afterUpdate: () =>
                    {
                        rom.Update(press, press && update == first);
                        CompareBombRom(rom, $"Bomb hazard ${tile:x2}, batch={batched}, update={update++}");
                    });
            }
            Step(1, true);
            Step(19);
            Step(1, true);
            Step(9);
            int walk = 0;
            StepGameplayUpdates(20, Vector2.Up, batched: batched, afterUpdate: () =>
            {
                rom[0xd00b] = (byte)(79 - ++walk);
                rom.Update();
                CompareBombRom(rom, $"Bomb hazard approach update={update++}");
                FailIf(_player.Position != new Vector2(80, 79 - walk), "Link must walk away before the bomb's hazard probe.");
            });
            Step(10);
            FailIf(_entities.Entities<BombEffect>().Single().State != BombState.Grounded,
                "Bomb hazard boundary probe must start with a grounded bomb.");
            // objectCheckIsOverHazard samples YH+$05. The object's center is
            // still on floor $3a; only that foot probe crosses into row $05.
            Vector2 point = new(80, 88);
            _currentRoom.SetPositionTileAndCollision(point, tile, 0x10, 0);
            CopyBombRoomToRom(rom);
            Step(1);
            FailIf(rom.BombSlots.Length != 0 || _entities.ActiveBombCount != 0 || _inventory.Bombs != 0x09,
                $"Bomb hazard ${tile:x2} must retire its ITEM slot without refunding ammo.");
            int nativeEffects = Enumerable.Range(0xd0, 16).Count(page => rom[page * 256 + 0x40] != 0);
            FailIf(nativeEffects != 1, $"Bomb hazard ${tile:x2} must allocate exactly one native falling/splash interaction, got {nativeEffects}.");
            if (tile == 0xf3)
                FailIf(_entities.Entities<FallingDownHoleEffect>().Count != 1,
                    "A bomb falling into a hole must publish exactly one falling interaction to its owner.");
            Step(3);
            FailIf(_entities.ActiveBombCount != 0, "A hazard-deleted bomb must not resume its fuse on later updates.");
        }
        GD.Print("Validated executed-ROM bomb hole/water/lava deletion at the Y+$05 terrain boundary, effect allocation, ammo retention and retired slot lifetime through individual/batched gameplay updates.");
    }
}
