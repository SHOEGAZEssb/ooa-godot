using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBombSelfDamageRom()
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(0, 0x33);
        _entities.Clear();
        for (int y = 8; y < 128; y += 16)
        for (int x = 8; x < 160; x += 16)
            _currentRoom.SetPositionTileAndCollision(new(x, y), 0x3a, 0, 0);
        var edges = new List<(int X, int Y, int Z)>();
        foreach (int edge in new[] { -13, -12, -11, 0, 11, 12, 13 })
        {
            edges.Add((edge, 0, 0));
            edges.Add((0, edge, 0));
        }
        foreach (int edge in new[] { -7, -6, -5, 0, 5, 6, 7 }) edges.Add((0, 0, edge));
        int cases = 0;
        foreach (int ring in new[] { 0xff, 0x0c, 0x30, 0x01, 0x04, 0x08, 0x0a, 0x10, 0x3f })
        foreach (var edge in edges)
        {
            var fixture = CreateSwordRomPlayer(0, 1, ring);
            var bomb = new BombEffect();
            var spawns = new List<RoomEntitySpawn>();
            var sounds = new List<int>();
            var rom = new BombRom(0, ring);
            rom[0xd004] = 1;
            rom[0xd029] = 1;
            try
            {
                Vector2I offset = new(0, edge.Z);
                bomb.Initialize(new BombDatabase().Data, _currentRoom, new BreakableTileDatabase(),
                    fixture.Player, 0, b => b.ReleaseExploding(fixture.Player, offset),
                    sounds.Add, (_, _, _) => { }, () => { }, () => 0, _ => null, null, null);
                bomb.SetHeldOffset(fixture.Player, offset);
                bomb.UpdateFrame(fixture.Player, spawns);
                rom.Update(true, true);
                // Isolate the child handler at a declared initial height,
                // rather than execute the unrelated parent lift animation.
                rom[0xd70f] = unchecked((byte)edge.Z);
                for (int update = 1; update < 116; update++)
                {
                    bomb.UpdateFrame(fixture.Player, spawns);
                    rom.Call(7, 0x4872);
                    FailIf(bomb.AnimationCounter != rom[0xd720],
                        $"Own-bomb ring=${ring:x2}, update={update}: fuse differs.");
                }
                FailIf(bomb.State != BombState.Exploding || rom[0xd704] != 0xff,
                    "The isolated ITEM $03 child must explode on update 116, including initialization.");
                fixture.Player.SetScriptedPosition(new(80 + edge.X, 64 + edge.Y));
                rom[0xd00d] = (byte)(80 + edge.X);
                rom[0xd00b] = (byte)(64 + edge.Y);
                rom[0xd024] = 0x80; // Isolated Link is not in a parent's lift.
                bomb.UpdateFrame(fixture.Player, spawns);
                rom.Call(7, 0x4872);
                string context = $"Own-bomb ring=${ring:x2}, offset={edge}";
                FailIf(fixture.Player.HealthQuarters != rom[0xc6aa] ||
                    fixture.Player.InvincibilityFrames != rom[0xd02b] ||
                    fixture.Player.KnockbackFrames != rom[0xd02d],
                    $"{context}: runtime health/counters={fixture.Player.HealthQuarters}/{fixture.Player.InvincibilityFrames}/{fixture.Player.KnockbackFrames}, ROM={rom[0xc6aa]}/{rom[0xd02b]}/{rom[0xd02d]}.");
                FailIf(fixture.World.Sounds.Count != 0 || sounds.Any(sound => sound != 0x6f),
                    $"{context}: native own-bomb collision bypasses ordinary Link damage audio.");
                // Removing grace must not permit a second hit from this blast.
                fixture.Player.ClearInteractionKnockback(clearInvincibility: true);
                rom[0xd02a] = rom[0xd02b] = rom[0xd02d] = 0;
                int health = fixture.Player.HealthQuarters;
                bomb.UpdateFrame(fixture.Player, spawns);
                rom.Call(7, 0x4872);
                FailIf(fixture.Player.HealthQuarters != rom[0xc6aa] || fixture.Player.HealthQuarters != health,
                    $"{context}: ITEM.var37 must prevent repeated self damage even after grace is cleared.");
                cases++;
            }
            finally { bomb.Free(); fixture.Player.Free(); }
        }
        GD.Print($"Validated {cases} executed-ROM bomb self-damage XY/Z edges, raw-to-quarter damage, bypassed armor/power modifiers, Blast/Bombproof/Protection/Steadfast and one-hit lifetime.");
    }

    private void CompareBombTileProbesRom()
    {
        foreach (bool batched in new[] { false, true })
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
            _player.WarpTo(new(72, 112));
            StepGameplayUpdates(39, Vector2.Up);
            FailIf(_player.Position != new Vector2(72, 73) || _collision.Collides(_player.Position),
                "Bomb probe fixture must walk to its planting point before bushes are installed.");
            var seed = _random.CaptureState();
            var rom = new BombRom(0, 0xff, _inventory.Bombs, seed.Rng1 | seed.Rng2 << 8);
            rom[0xd00d] = 72;
            rom[0xd00b] = 73;
            rom[0xcc33] = (byte)_currentRoom.ActiveCollisions;
            rom[0xcc39] = rom[0xccaa] = 0xff;
            rom[0xd029] = 1;
            rom[0xc6aa] = (byte)_inventory.HealthQuarters;
            rom[0xc6ab] = (byte)_inventory.MaxHealthQuarters;
            CopyBombRoomToRom(rom);
            for (int tile = 0; tile < 256; tile++) rom.SetTileCollision(tile, _currentRoom.GetCollision((byte)tile));
            int update = 0;
            void Step(int count, bool press = false, Vector2 movement = default)
            {
                StepGameplayUpdates(count, movement, press ? ["attack"] : [], press ? ["attack"] : [], batched,
                    afterUpdate: () =>
                    {
                        rom.Update(press, press && (update == 0 || update == 20));
                        CompareBombRom(rom, $"Bomb tile probe batch={batched}, update={update++}");
                    });
            }
            Step(1, true);
            Step(19);
            Step(1, true); // In-place drop, then move Link out of the blast.
            Step(9);
            Step(40, movement: Vector2.Down);
            FailIf(_player.Position != new Vector2(72, 113), "Bomb probe fixture must leave the planting area through floor geometry.");
            rom[0xd00b] = 113; // Explicit surrounding actor input, not a bomb output.
            for (int y = 56; y <= 88; y += 16)
            for (int x = 56; x <= 88; x += 16)
            {
                Vector2 point = new(x, y);
                _currentRoom.SetPositionTileAndCollision(point, 0xc5, 0x0f, 0);
                _currentRoom.SetUnderlyingMetatile(point, 0xc5);
                rom.SetUnderlying((y / 16) * 16 + x / 16, 0xc5);
            }
            CopyBombRoomToRom(rom);
            long randomCalls = _random.CaptureState().Calls;
            int nativeRandomCalls = rom.RandomCalls;
            int[] order = [0x44, 0x34, 0x45, 0x54, 0x43, 0x35, 0x55, 0x53, 0x33];
            int broken = 0;
            StepGameplayUpdates(91, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                rom.Update();
                CompareBombRom(rom, $"Bomb tile explosion batch={batched}, update={update++}");
                for (int i = 0; i < order.Length; i++)
                {
                    int offset = order[i];
                    Vector2 point = new((offset & 15) * 16 + 8, (offset >> 4) * 16 + 8);
                    byte actual = _currentRoom.GetMetatile(point);
                    FailIf(actual != rom[0xcf00 + offset] || _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(offset) ||
                        _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + offset],
                        $"Bomb source $05 probe ${offset:x2} differs at update={update}, batch={batched}.");
                    if (actual == 0x3a && i == broken)
                    {
                        FailIf(Enumerable.Range(i + 1, order.Length - i - 1).Any(index => rom[0xcf00 + order[index]] != 0xc5),
                            "Bomb probes must break center, cardinal edges, then corners in source counter order.");
                        broken++;
                    }
                }
            });
            FailIf(broken != 9, $"Bomb must visit nine unique source-ordered bushes, got {broken}.");
            var final = _random.CaptureState();
            FailIf(final.Rng1 != rom[0xff94] || final.Rng2 != rom[0xff95] ||
                final.Calls - randomCalls != rom.RandomCalls - nativeRandomCalls,
                "Bomb tile drop RNG order/consumption differs from executed ROM.");
        }
        GD.Print("Validated executed-ROM nine ordered bomb tile probes, source $05 bush replacements/collision/underlying bytes and drop RNG through individual/batched gameplay updates.");
    }
}
