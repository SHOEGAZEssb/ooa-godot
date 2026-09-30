using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private (Player Player, ValidationRingPlayerWorld World, OracleRandom Random) CreateSwordRomPlayer(
        int direction, int level, int ring = 0xff, int seed = 0x1234, int health = 12)
    {
        OracleSaveData save = OracleSaveData.CreateStandardGame();
        save.WriteWramByte(WramAddress.wActiveRing, (byte)ring);
        save.WriteWramByte(WramAddress.wLinkHealth, (byte)health);
        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(TreasureId.Sword, level);
        var random = new OracleRandom();
        random.RestoreState(random.CaptureState() with { Rng1 = (byte)seed, Rng2 = (byte)(seed >> 8) });
        var world = new ValidationRingPlayerWorld();
        var player = new Player();
        player.Initialize(world, inventory, new(80, 64), random);
        player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(direction * 8));
        return (player, world, random);
    }

    private static void CompareSwordRom(Player player, SwordRom rom, string context)
    {
        SwordActionState expected = rom[0xd200] == 0 ? SwordActionState.None : rom[0xd204] switch
        {
            1 => SwordActionState.Swing,
            2 => SwordActionState.Held,
            3 => SwordActionState.Charged,
            4 => SwordActionState.Spin,
            5 or 6 => SwordActionState.Poke,
            _ => throw new InvalidOperationException($"{context}: unexpected native sword state ${rom[0xd204]:x2}.")
        };
        FailIf(player.SwordState != expected,
            $"{context}: parent $d2 state=${rom[0xd204]:x2}, child $d6 state=${rom[0xd604]:x2}; runtime={player.SwordState}, expected={expected}, frame={player.SwordStateFrame}.");
        bool collision = (rom[0xd624] & 0x80) != 0;
        Rect2 actual = player.GetSwordHitbox();
        FailIf((actual.Size != Vector2.Zero) != collision,
            $"{context}: child $d6 collision=${rom[0xd624]:x2}, runtime hitbox={actual}.");
        if (collision)
        {
            FailIf(SwordCollision.Type(player.SwordCollisionState, player.Inventory.SwordLevel) != (rom[0xd624] & 0x7f),
                $"{context}: native collision type=${rom[0xd624]:x2} differs from the runtime sword collision state.");
            Rect2 bounds = new(new(rom[0xd60d] - rom[0xd627], rom[0xd60b] - rom[0xd626]),
                new(2 * rom[0xd627], 2 * rom[0xd626]));
            FailIf(actual != bounds || player.SwordDamage != -unchecked((sbyte)rom[0xd628]),
                $"{context}: ROM arc=${rom[0xd630]:x2}, bounds={bounds}, damage={-unchecked((sbyte)rom[0xd628])}; runtime arc=${player.SwordArcIndex:x2}, bounds={actual}, damage={player.SwordDamage}.");
        }
        FailIf(player.IsAttacking && player.SwordAllowsMovement != (rom[0xcc61] == 0),
            $"{context}: native immobilized=${rom[0xcc61]:x2}, runtime sword movement={player.SwordAllowsMovement}.");
    }

    private void ValidateSwordTimingRom()
    {
        foreach (int direction in Enumerable.Range(0, 4))
        foreach (int ring in new[] { 0xff, 0x16, 0x2f, 0x31 })
        foreach (bool hold in new[] { false, true })
        {
            var fixture = CreateSwordRomPlayer(direction, 1, ring);
            var rom = new SwordRom(direction, 1, ring);
            try
            {
                fixture.Player.StartSwordAttack();
                for (int update = 0; update < 130; update++)
                {
                    bool held = hold && update < 70;
                    rom.Update(held || update == 0, pressed: update == 0);
                    if (update != 0) fixture.Player.AdvanceSwordForValidation(1, held);
                    CompareSwordRom(fixture.Player, rom, $"ITEM $05 direction={direction}, ring=${ring:x2}, hold={hold}, update={update}");
                    // This fixture's beam allocator is a test double; its child
                    // initialization/sound is exercised in gameplay below.
                    FailIf(!fixture.World.Sounds.SequenceEqual(rom.Sounds.Where(id => id != SoundId.SndSwordBeam)),
                        $"ITEM $05 ring=${ring:x2}, update={update}: sound request order differs.");
                    // Source boundaries are independent of imported frame constants.
                    if (hold && ring == 0xff && update is 17 or 57 or 58 or 70 or 93)
                    {
                        int state = update switch { 17 or 57 => 2, 58 => 3, 70 => 4, _ => 0 };
                        FailIf(rom[0xd204] != state || update == 57 && rom[0xd206] != 0,
                            $"Native sword boundary update={update}: expected state=${state:x2} and zero-before-underflow charge.");
                    }
                }
                // A fresh press after completion must allocate and initialize again.
                rom.Update(true, true);
                fixture.Player.StartSwordAttack();
                CompareSwordRom(fixture.Player, rom, "ITEM $05 repeat after completion");
                FailIf(rom.RandomCalls != 2, "A repeated sword must consume exactly two slash RNG calls across two swings.");
            }
            finally { fixture.Player.Free(); }
        }
        GD.Print("Validated executed-ROM sword swing/hold/charge/spin/Energy timing, four directions, release and repeat, geometry, movement and sound boundaries.");
    }

    private void ValidateSwordCollisionRngRom()
    {
        int cases = 0, rare = 0;
        foreach (int level in new[] { 1, 2, 3 })
        foreach (int ring in new[] { 0xff, 1, 2, 3, 4, 5, 6, 7, 9, 0x0a, 0x32, 0x3e })
        foreach (int health in new[] { 4, 12 })
        for (int seed = 0; seed < 256; seed += ring == 0x3e ? 1 : 255)
        {
            var fixture = CreateSwordRomPlayer(seed & 3, level, ring, seed, health);
            var rom = new SwordRom(seed & 3, level, ring, seed);
            rom[0xc6aa] = (byte)health;
            try
            {
                fixture.Player.StartSwordAttack();
                rom.Update(true, true);
                CompareSwordRom(fixture.Player, rom, $"Sword level={level}, ring=${ring:x2}, health=${health:x2}, seed=${seed:x4}");
                OracleRandomState rng = fixture.Random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || fixture.Random.Calls != rom.RandomCalls ||
                    !fixture.World.Sounds.SequenceEqual(rom.Sounds),
                    $"ITEM $05 ring=${ring:x2}, seed=${seed:x4}: native slash/Whimsical RNG and sound order differ.");
                if (ring == 0x3e && rom[0xd628] == 0xf4) rare++;
                if (seed is 0 or 255)
                {
                    for (int update = 1; update < 95; update++)
                    {
                        rom.Update(update < 70);
                        fixture.Player.AdvanceSwordForValidation(1, update < 70);
                        CompareSwordRom(fixture.Player, rom,
                            $"Sword damage lifecycle level={level}, ring=${ring:x2}, seed=${seed:x4}, update={update}");
                    }
                }
                cases++;
            }
            finally { fixture.Player.Free(); }
        }
        FailIf(rare == 0, "The Whimsical seed sweep must execute the native $00 -> damage $f4/lightning branch.");
        GD.Print($"Validated {cases} ROM sword level/ring/health/RNG combinations including {rare} Whimsical rare hits.");
    }

    private void ValidateSwordPokeRom()
    {
        foreach (bool wall in new[] { false, true })
        foreach (bool held in new[] { false, true })
        foreach (bool triggerHeld in new[] { false, true })
        {
            var fixture = CreateSwordRomPlayer(0, 1);
            var rom = new SwordRom(0, 1);
            try
            {
                fixture.Player.StartSwordAttack();
                for (int i = 0; i < 20; i++)
                {
                    rom.Update(true, i == 0);
                    if (i != 0) fixture.Player.AdvanceSwordForValidation(1, true);
                }
                if (wall)
                {
                    // bank0.checkLinkPushingAgainstWall requires both probes
                    // and matching directional input; contact alone is insufficient.
                    rom[0xd033] = 0xc0;
                    rom.MovementKeys = 0x40;
                    fixture.World.PushingAgainstWall = true;
                }
                else
                {
                    rom[0xd62a] = 1; // enemy collision's native var3e signal
                    fixture.Player.QueueSwordEnemyContact();
                }
                for (int i = 0; i < 16; i++)
                {
                    bool button = i == 0 ? triggerHeld : held;
                    rom.Update(button);
                    fixture.Player.AdvanceSwordForValidation(1, button, wall ? Vector2.Up : Vector2.Zero);
                    CompareSwordRom(fixture.Player, rom, $"Sword poke wall={wall}, held={held}, triggerHeld={triggerHeld}, update={i}");
                    // Stop pressing against the wall after triggering one poke.
                    rom.MovementKeys = 0;
                    fixture.World.PushingAgainstWall = false;
                }
            }
            finally { fixture.Player.Free(); }
        }
        GD.Print("Validated executed-ROM wall/enemy poke branches, disabled collision, held/released input and the separate state $06 reinitialization update.");
    }
}
