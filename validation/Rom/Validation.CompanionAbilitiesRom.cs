using Godot;
using System;
using System.Reflection;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static T CompanionField<T>(object actor, string name) =>
        (T)actor.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;

    private (IRoomEntity Actor, CompanionRom Rom) PrepareMountedCompanionRom(int id, int direction = 2)
    {
        ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
        Vector2 p = new(72, 64);
        IRoomEntity actor = id switch
        {
            0x0b => _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(p, direction, 0, 0x2a, Riding: true)),
            0x0c => _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(p, direction, 0, 0x2a, Riding: true)),
            _ => _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(p, direction, 0, 0x2a, Riding: true))
        };
        CompanionRuntimeState.Begin(_runtimeState, id, 0x2a, p, direction);
        var rom = new CompanionRom(id, p, direction, _currentRoom);
        rom[0xcc21] = 40; rom[0xcc22] = 40; rom[0xcc23] = (byte)direction;
        rom[0xc6aa] = (byte)_player.HealthQuarters;
        var rng = _random.CaptureState(); rom[0xff94] = rng.Rng1; rom[0xff95] = rng.Rng2;
        return (actor, rom);
    }

    private void CompareCompanionMotion(IRoomEntity actor, CompanionRom rom, string context)
    {
        Vector2 p = CompanionField<Vector2>(actor, "_precisePosition");
        int z = actor switch { RickyCompanionRoomEntity r => r.ZFixed, MooshCompanionRoomEntity m => m.ZFixed,
            DimitriCompanionRoomEntity d => d.ZFixed, _ => throw new InvalidOperationException() };
        var animation = CompanionField<EnemyAnimationPlayer>(actor, "_animation");
        (int state, int substate) = actor switch
        {
            RickyCompanionRoomEntity r => r.Phase switch
            {
                RickyCompanionPhase.Riding => (5, 0), RickyCompanionPhase.Hopping => (5, 1),
                RickyCompanionPhase.JumpingOverHole => (5, 2), RickyCompanionPhase.Landing => (5, 3),
                RickyCompanionPhase.JumpingUpCliff => (2, 0), RickyCompanionPhase.JumpingDownCliff => (7, 0),
                RickyCompanionPhase.Punching => (8, 0), RickyCompanionPhase.Charging => (8, 1),
                RickyCompanionPhase.HazardFalling => (4, 0), RickyCompanionPhase.Waiting => (1, 0),
                _ => (-1, -1)
            },
            MooshCompanionRoomEntity m => m.Phase switch
            {
                MooshCompanionPhase.Riding => (5, 0),
                MooshCompanionPhase.Airborne => (8, CompanionField<bool>(m, "_airborneInitialized") ? 1 : 0),
                MooshCompanionPhase.Charging => (8, 2), MooshCompanionPhase.Falling => (8, 3),
                MooshCompanionPhase.StompRecovery => (8, 4), MooshCompanionPhase.HoveringOverWater => (8, 5),
                MooshCompanionPhase.CliffJump => (7, 0), MooshCompanionPhase.HazardFalling => (4, 0),
                MooshCompanionPhase.Waiting => (1, 0), _ => (-1, -1)
            },
            DimitriCompanionRoomEntity d => d.Phase switch
            {
                DimitriPhase.Riding => (5, 0), DimitriPhase.Eating => (8, CompanionField<int>(d, "_bitePhase")),
                DimitriPhase.CliffJump => (7, 0), DimitriPhase.Waiting => (1, 0),
                DimitriPhase.Hazard => (4, 0), DimitriPhase.ReturningToLand => (0x0b, 0),
                DimitriPhase.Carried => (2, 1), DimitriPhase.Thrown => (2, 2), DimitriPhase.ThrownLanding => (2, 3),
                _ => (-1, -1)
            },
            _ => (-1, -1)
        };
        if (actor is RickyCompanionRoomEntity && CompanionField<bool>(actor, "_fluteEntrance") ||
            actor is MooshCompanionRoomEntity && CompanionField<bool>(actor, "_fluteEntrance") ||
            actor is DimitriCompanionRoomEntity { Phase: DimitriPhase.FlutePending or DimitriPhase.FluteEntering })
            (state, substate) = (0x0c, 0);
        // Substate bytes can survive a state change in the original. Only
        // compare them where the handler actually dispatches on the byte.
        FailIf(state >= 0 && (state != rom[0xd104] ||
            (state == 8 || state == 5 && actor is RickyCompanionRoomEntity || state == 2 && actor is DimitriCompanionRoomEntity) && substate != rom[0xd105]),
            $"{context}: state native=${rom[0xd104]:x2}:${rom[0xd105]:x2}, runtime=${state:x2}:${substate:x2}, XY={rom.Position}/{p}, dir={rom[0xd108]}.");
        FailIf(p != rom.Position || z != rom.SignedWord(0xd10e),
            $"{context}: position/Z native={rom.Position}/{rom.SignedWord(0xd10e)}, runtime={p}/{z}; native state=${rom[0xd104]:x2}:${rom[0xd105]:x2}.");
        FailIf(animation.CurrentParameter != rom[0xd121] || animation.AnimationIndex != rom[0xd130],
            $"{context}: animation parameter native=${rom[0xd121]:x2}, runtime=${animation.CurrentParameter:x2}, animation native=${rom[0xd130]:x2}/runtime=${animation.AnimationIndex:x2}, counters={rom[0xd120]}/{CompanionField<int>(animation, "_frameCounter")}.");
        var rng = _random.CaptureState();
        FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95], $"{context}: shared RNG differs.");
        FailIf(_entities.ScreenShakeCounter != rom[0xcd18] || _entities.HorizontalScreenShakeCounter != rom[0xcd19],
            $"{context}: screen-shake counters differ.");
        var weapons = _entities.EntityAdapters<IRoomEntity>().Where(e => e is
            RickyPunchAttackRoomEntity or MooshStompAttackRoomEntity or DimitriMouthRoomEntity or RickyTornadoRoomEntity)
            .Where(e => e is not IRoomEntityLifetime life || !life.Finished).ToArray();
        var slots = Enumerable.Range(0xd6, 10).Where(page => rom[page << 8] != 0 &&
            rom[(page << 8) + 1] is 0x28 or 0x2a or 0x2b).ToArray();
        FailIf(weapons.Length != slots.Length, $"{context}: native attack count={slots.Length}, runtime={weapons.Length}.");
        foreach (var weapon in weapons)
        {
            int id = weapon is DimitriMouthRoomEntity ? 0x2b : weapon is RickyTornadoRoomEntity ? 0x2a : 0x28;
            int address = slots.Single(page => rom[(page << 8) + 1] == id) << 8;
            var projectile = (IPlayerProjectileRoomEntity)weapon;
            var attributes = CompanionWeaponDatabase.Shared.Weapon(id);
            FailIf(projectile.Damage != -(sbyte)rom[address + 0x28] ||
                attributes.CollisionType != (rom[address + 0x24] & 0x7f) ||
                projectile.CollisionBounds.Size != new Vector2(rom[address + 0x27] * 2, rom[address + 0x26] * 2),
                $"{context}: attack ${id:x2} damage/type/radii differ from native item initialization.");
            Vector2 native = new(rom[address + 0x0d], rom[address + 0x0b]);
            FailIf(weapon.Node.Position != native, $"{context}: attack ${id:x2} position native={native}, runtime={weapon.Node.Position}.");
            if (weapon is not RickyTornadoRoomEntity)
                FailIf(CompanionField<int>(weapon, "_counter") != rom[address + 6], $"{context}: attack ${id:x2} lifetime differs.");
        }
    }

    private void StepCompanionRom(IRoomEntity actor, CompanionRom rom, int count, Vector2 movement,
        bool batched, bool attack = false, bool edge = false, Action? compare = null)
    {
        int frame = 0;
        string start = $"${rom[0xd104]:x2}:${rom[0xd105]:x2}, frame=${rom[0xcc00]:x2}";
        var sounds = _sound.AttachPlayRequestAudit();
        StepGameplayUpdates(count, movement, attack ? ["attack"] : [], edge ? ["attack"] : [], batched,
            () =>
            {
                rom.Update(CompanionMovement.AngleForInput(movement), edge && frame == 0 ? (byte)1 : (byte)0, attack ? (byte)1 : (byte)0);
                frame++;
                CompareCompanionMotion(actor, rom, $"Companion ${rom[0xd101]:x2} update {frame}, batch={batched}");
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Companion ${rom[0xd101]:x2} update {frame}, start={start}: sounds native=[{string.Join(',', rom.Sounds.Select(s => s.ToString("x2")))}], runtime=[{string.Join(',', sounds.Requests.Select(s => s.ToString("x2")))}].");
                sounds.Clear();
                compare?.Invoke();
            });
    }

    private void ValidateRickyAbilitiesRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0b);
            // Repeated periodic hops, including input changes and idle airborne drift.
            foreach (Vector2 movement in new[] { Vector2.Right, Vector2.Left, Vector2.Up, Vector2.Down })
                StepCompanionRom(actor, rom, 28, movement, batched);
            StepCompanionRom(actor, rom, 30, Vector2.Zero, batched);
            foreach (int held in new[] { 1, 20, 45, 80 })
            {
                (actor, rom) = PrepareMountedCompanionRom(0x0b);
                StepCompanionRom(actor, rom, held, Vector2.Zero, batched, attack: true, edge: true);
                StepCompanionRom(actor, rom, 45, Vector2.Zero, batched);
            }
        }
    }

    private void ValidateMooshAbilitiesRom()
    {
        int hostCase7 = 0;
        foreach (int held in new[] { 1, 32, 60, 160 })
        foreach (bool batched in RomHostSchedules(hostCase7++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0d);
            StepCompanionRom(actor, rom, held, Vector2.Zero, batched, attack: true, edge: true);
            StepCompanionRom(actor, rom, 65, Vector2.Zero, batched);
            FailIf(((MooshCompanionRoomEntity)actor).Phase != MooshCompanionPhase.Riding || rom[0xd104] != 5,
                "Moosh did not finish jump/stomp recovery.");
        }
    }

    private void ValidateDimitriAbilitiesRom()
    {
        int hostCase6 = 0;
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase6++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0c, direction);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepCompanionRom(actor, rom, 1, Vector2.Zero, batched, attack: true, edge: true);
                StepCompanionRom(actor, rom, 45, Vector2.Zero, batched);
            }
        }
    }

    private void SetCompanionRomTile(CompanionRom rom, int x, int y, byte tile, byte collision = 0)
    {
        _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), tile, collision, 0);
        rom.SetTile(x, y, tile, collision);
    }

    private void ValidateCompanionTraversalRom()
    {
        int hostCase5 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool batched in RomHostSchedules(hostCase5++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(id);
            SetCompanionRomTile(rom, 4, 4, 0xd4, 3);
            StepCompanionRom(actor, rom, 45, Vector2.Down, batched);
            StepCompanionRom(actor, rom, 25, Vector2.Zero, batched);
        }
        int hostCase4 = 0;
        foreach (byte tile in new byte[] { 0xfe, 0xff })
        foreach (bool batched in RomHostSchedules(hostCase4++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0c, 0);
            for (int y = 1; y < 7; y++) SetCompanionRomTile(rom, 4, y, tile);
            StepCompanionRom(actor, rom, 28, Vector2.Up, batched, compare: () =>
                FailIf(((DimitriCompanionRoomEntity)actor).InWater != (rom[0xd138] != 0), "Dimitri water flag differs."));
            StepCompanionRom(actor, rom, 28, Vector2.Left, batched);
            StepCompanionRom(actor, rom, 15, Vector2.Zero, batched);
        }
    }

    private void ValidateCompanionHazardsRom()
    {
        int hostCase3 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (byte tile in new byte[] { 0xf3, 0xfe })
        foreach (bool mounted in new[] { false, true })
        foreach (bool fallback in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            if (id == 0x0c && tile == 0xfe) continue;
            var (actor, rom) = PrepareMountedCompanionRom(id);
            if (!mounted)
            {
                _entities.Clear();
                actor = SpawnFidelityCompanion(id, new(72, 64));
                _player.WarpTo(new(8, 8), recordSafe: false);
                rom.WaitForMount(new(8, 8));
            }
            Vector2 expectedRespawn = fallback ? new(112, 88) : new(40, 40);
            CompanionRuntimeState.SetLastAnimalMountPosition(_runtimeState, new(112, 88));
            rom[0xc638] = 88; rom[0xc639] = 112;
            if (fallback) SetCompanionRomTile(rom, 2, 2, 0, 15);
            SetCompanionRomTile(rom, 4, 4, tile);
            StepCompanionRom(actor, rom, 150, Vector2.Zero, batched, compare: () =>
                FailIf(_player.HealthQuarters != rom[0xc6aa], $"Companion ${id:x2} hazard damage differs: native={rom[0xc6aa]}, runtime={_player.HealthQuarters}."));
            FailIf(rom[0xd104] != (mounted ? 5 : 1) || actor.Node.Position != expectedRespawn,
                "Companion did not recover from hazard at the native respawn point.");
        }
    }

    private void ValidateRickyLongJumpsRom()
    {
        int hostCase2 = 0;
        foreach (int terrain in new[] { 0, 1, 2 })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0b, terrain == 0 ? 1 : 0);
            Vector2 movement = terrain == 0 ? Vector2.Right : Vector2.Up;
            if (terrain == 0) SetCompanionRomTile(rom, 5, 4, 0xf3);
            else
            {
                SetCompanionRomTile(rom, 4, 3, 0, terrain == 1 ? (byte)3 : (byte)15);
                if (terrain == 2) SetCompanionRomTile(rom, 4, 2, 0, 3);
            }
            StepCompanionRom(actor, rom, 35, movement, batched);
            StepCompanionRom(actor, rom, 30, Vector2.Zero, batched);
        }
    }

    private void ValidateMooshFlutterAndWaterRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            var (actor, rom) = PrepareMountedCompanionRom(0x0d);
            StepCompanionRom(actor, rom, 1, Vector2.Zero, batched, attack: true, edge: true);
            StepCompanionRom(actor, rom, 21, Vector2.Zero, batched);
            for (int flap = 0; flap < 18; flap++)
            {
                StepCompanionRom(actor, rom, 1, Vector2.Zero, batched, attack: true, edge: true);
                StepCompanionRom(actor, rom, 7, Vector2.Zero, batched, compare: () =>
                    FailIf(((MooshCompanionRoomEntity)actor).FlapCount != rom[0xd13a], "Moosh flutter limit differs."));
            }
            StepCompanionRom(actor, rom, 60, Vector2.Zero, batched);
            (actor, rom) = PrepareMountedCompanionRom(0x0d);
            StepCompanionRom(actor, rom, 1, Vector2.Zero, batched, attack: true, edge: true);
            StepCompanionRom(actor, rom, 10, Vector2.Zero, batched);
            SetCompanionRomTile(rom, 4, 4, 0xfe);
            StepCompanionRom(actor, rom, 150, Vector2.Zero, batched);
        }
    }

    private void ValidateCompanionFluteArrivalRom()
    {
        int hostCase1 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (bool offscreen in new[] { false, true })
        foreach (bool blocked in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom(); _entities.Clear();
            Vector2 p = direction switch { 0 => new(72, 112), 1 => new(24, 64), 2 => new(72, 24), _ => new(136, 64) };
            if (offscreen) p = direction switch { 0 => new(72, 136), 1 => new(-8, 64), 2 => new(72, -8), _ => new(168, 64) };
            Vector2 destination = new(72, 64);
            IRoomEntity actor = id switch
            {
                0x0b => _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(p, direction, 0, 0x2a, FluteDestination: destination)),
                0x0c => _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(p, direction, 0, 0x2a, FluteDestination: destination)),
                _ => _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(p, direction, 0, 0x2a, FluteDestination: destination))
            };
            var rom = new CompanionRom(id, p, direction, _currentRoom);
            rom.EnterFromFlute(direction);
            if (blocked)
            {
                Vector2 obstacle = p + OracleObjectMath.StrictCardinalVector(direction * 8) * (offscreen ? 40 : 24);
                SetCompanionRomTile(rom, (int)obstacle.X / 16, (int)obstacle.Y / 16, 0, 15);
            }
            var rng = _random.CaptureState(); rom[0xff94] = rng.Rng1; rom[0xff95] = rng.Rng2;
            StepCompanionRom(actor, rom, 90, Vector2.Zero, batched);
            FailIf(rom[0xd104] != 1, $"Companion ${id:x2} flute arrival did not reach waiting state.");
        }
    }
}
