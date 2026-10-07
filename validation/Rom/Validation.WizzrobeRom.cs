using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateWizzrobeRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batched in new[] { false, true })
        foreach (int room in new[] { 0x45, 0x20, 0x86 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(5, room);
            Vector2 floor = (from y in Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
                from x in Enumerable.Range(1, _currentRoom.WidthInTiles - 2)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None && !_collision.Collides(point)
                select point).First();
            _player.WarpTo(floor);
            FailIf(_collision.Collides(_player.Position), $"Wizzrobe room $5:${room:x2} fixture must keep Link on real floor.");
            // This movement fixture disables the separate late Link scan;
            // native object dispatch and its complete PART walk remain real.
            typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 100000f);
            var slots = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_enemySlots", flags)!.GetValue(_entities)!;
            var parts = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            FailIf(slots.Count != (room == 0x20 ? 2 : 3) || slots.Keys.Any(entity => entity.Node is not WizzrobeCharacter),
                $"Room $5:${room:x2} must admit its complete source Wizzrobe stream.");
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcc08] = rom[0xcc0a] = 0x9f;
            rom[0xcdd1] = (byte)_entities.RoomEnemyCount;
            foreach (var pair in slots)
            {
                var actor = (WizzrobeCharacter)pair.Key.Node;
                int address = 0xd080 + pair.Value * 256;
                // All three source streams use consecutive counted placements;
                // their independently traced one-based kill indices are $01..$03.
                rom[address] = (byte)(((pair.Value + 1) << 4) | 1);
                rom[address + 1] = 0x40; rom[address + 2] = (byte)actor.Record.SubId;
                rom.Word(address + 10, (int)(actor.Position.Y * 256)); rom.Word(address + 12, (int)(actor.Position.X * 256));
            }
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            bool text = true; _entities.TextActiveSource = () => text;
            int update = 0, shots = 0, cycles = 0;
            var previous = slots.Keys.ToDictionary(entity => entity, _ => 0);
            void Compare()
            {
                var camera = -_entities.ToScreen(Vector2.Zero);
                rom[0xffaa] = (byte)OracleObjectPosition.HighByte(camera.Y);
                rom[0xffac] = (byte)OracleObjectPosition.HighByte(camera.X);
                rom[0xcba0] = (byte)(text ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                var actual = _random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls - seed.Calls != rom.RandomCalls,
                    $"Wizzrobe $5:${room:x2} update={update}, batch={batched}: RNG runtime=${actual.Rng2:x2}{actual.Rng1:x2}/{actual.Calls - seed.Calls}, ROM=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}.");
                foreach (var pair in slots)
                {
                    var enemy = (WizzrobeCharacter)pair.Key.Node;
                    int address = 0xd080 + pair.Value * 256;
                    int timer = (int)typeof(EnemyAnimationPlayer).GetField("_frameCounter", flags)!.GetValue(enemy.Animation)!;
                    FailIf(enemy.State != rom[address + 4] || enemy.SwitchHookSubstate != rom[address + 5] ||
                        enemy.Counter1 != rom[address + 6] || enemy.Counter2 != rom[address + 7] ||
                        enemy.Angle != rom[address + 9] || enemy.Speed != rom[address + 0x10] ||
                        enemy.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        enemy.ZFixed != unchecked((short)rom.Word(address + 14)) ||
                        enemy.Reservation != rom[address + 0x30] ||
                        enemy.Record.SubId == 2 && enemy.TargetPosition != new Vector2(rom[address + 0x32], rom[address + 0x31]) ||
                        enemy.Health != rom[address + 0x29] || enemy.StunCounter != rom[address + 0x2e] ||
                        (enemy.InvincibilityCounter & 0xff) != rom[address + 0x2b] || enemy.KnockbackCounter != rom[address + 0x2d] ||
                        enemy.Visible != ((rom[address + 0x1a] & 0x80) != 0) ||
                        enemy.CollisionEnabled != ((rom[address + 0x24] & 0x80) != 0) || timer != rom[address + 0x20],
                        $"Wizzrobe $5:${room:x2} slot${pair.Value:x2} update={update}, batch={batched}: runtime state/counters={enemy.State}/{enemy.Counter1}/{enemy.Counter2}, angle={enemy.Angle}, XY={enemy.Position}, Z={enemy.ZFixed}, visible/collision={enemy.Visible}/{enemy.CollisionEnabled}, reservation=${enemy.Reservation:x2}, animation={enemy.AnimationIndex}/{timer}; " +
                        $"ROM={rom[address + 4]}/{rom[address + 6]}/{rom[address + 7]}, angle={rom[address + 9]}, XY=${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, Z=${rom.Word(address + 14):x4}, visible/collision=${rom[address + 0x1a]:x2}/${rom[address + 0x24]:x2}, reservation=${rom[address + 0x30]:x2}, animation={rom[address + 0x20]}.");
                    if (previous[pair.Key] == 11 && enemy.State == 8) cycles++;
                    previous[pair.Key] = enemy.State;
                }
                for (int i = 0; i < 16; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcee0 + i) != rom[0xcee0 + i],
                        $"Wizzrobe shared reservation byte${0xcee0 + i:x4} differs at update{update}.");
                var projectiles = parts.Where(pair => pair.Key.Node is WizzrobeProjectile).ToArray();
                int nativeParts = Enumerable.Range(0, 16).Count(slot => rom[0xd0c0 + slot * 256] != 0);
                FailIf(projectiles.Length != nativeParts, $"PART$1f allocation/lifetime differs at update{update}: runtime={projectiles.Length}, ROM={nativeParts}.");
                foreach (var pair in projectiles)
                {
                    var projectile = (WizzrobeProjectile)pair.Key.Node;
                    int address = 0xd0c0 + pair.Value * 256;
                    FailIf(rom[address + 1] != 0x1f || projectile.State != rom[address + 4] || projectile.Angle != rom[address + 9] ||
                        projectile.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        projectile.Visible != ((rom[address + 0x1a] & 0x80) != 0) || projectile.ZHigh != unchecked((sbyte)rom[address + 15]),
                        $"PART$1f slot${pair.Value:x2}, update{update}: runtime state/angle/XY={projectile.State}/{projectile.Angle}/{projectile.Position}, native={rom[address + 4]}/{rom[address + 9]}/${rom.Word(address + 12):x4}/${rom.Word(address + 10):x4}, speed=${rom[address + 0x10]:x2}, velocity=${rom.Word(0xcec0):x4}/${rom.Word(0xcec2):x4}.");
                    if (projectile.State == 1 && rom[address + 0x20] == 127) shots++;
                }
                FailIf(_entities.RoomEnemyCount != rom[0xcdd1], "Wizzrobe phase changes must retain their counted enemy ownership.");
                update++;
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(3); text = false;
            Step(400); text = true; Step(3); text = false;
            Step(350);
            FailIf(shots == 0 || cycles == 0, $"Wizzrobe $5:${room:x2} must execute firing and a complete phase cycle.");
            if (room == 0x86)
            {
                var redPair = slots.OrderBy(pair => pair.Value).First();
                var red = (WizzrobeCharacter)redPair.Key.Node;
                int guard = 0;
                while (!red.CollisionEnabled && guard++ < 400) Step(1);
                FailIf(!red.CollisionEnabled || red.Reservation == 0, "Red Wizzrobe must claim a shared destination before an attack.");
                int reservationAddress = 0xce00 | red.Reservation;
                rom.HitWithSword(ItemCollisionType.L1Sword, 1, redPair.Value);
                FailIf(!((ISwordHittableRoomEntity)redPair.Key).ApplySwordHit(red.CollisionBounds,
                    red.Position.Floor() - Vector2.Right, 1, EnemyKnockbackStrength.Low, new List<RoomEntitySpawn>()),
                    "A phased-in red Wizzrobe must accept its native nonlethal sword hit.");
                text = true; Step(3); text = false;
                Step(1);
                FailIf(_runtimeState.ReadWramByte(reservationAddress) != 0 || _runtimeState.ReadWramByte(reservationAddress - 1) != 0,
                    "Red Wizzrobe JUST_HIT must release its owned reservation before recoil, after text eligibility resumes.");
                Step(20);
            }
            // The existing Switch Hook ROM suite owns ITEM/Link exchange.
            // Here its published enemy inputs exercise $40's distinct return
            // states through the complete object walk, including release fall.
            if (room != 0x86)
            foreach (var targetPair in slots.OrderBy(pair => pair.Value)
                .GroupBy(pair => ((WizzrobeCharacter)pair.Key.Node).Record.SubId).Select(group => group.First()).ToArray())
            {
                var target = (WizzrobeCharacter)targetPair.Key.Node;
                int guard = 0, address = 0xd080 + targetPair.Value * 256;
                while (!target.CollisionEnabled && guard++ < 450) Step(1);
                FailIf(!target.CollisionEnabled, "Wizzrobe must become reachable for its declared hook collision.");
                rom.HitWithSword(ItemCollisionType.SwitchHook, 0, targetPair.Value);
                target.BeginSwitchHook(target.Position.Floor() - Vector2.Right);
                text = true; Step(3); text = false;
                Step(4);
                FailIf(target.SwitchHookSubstate != 1, "Wizzrobe must enter its held hook substate after eligible dispatch.");
                target.CopySwitchHookPosition(floor, -4);
                rom[address + 11] = (byte)floor.Y; rom[address + 13] = (byte)floor.X;
                rom[address + 15] = 0xfc;
                target.SwapSwitchHook(); rom[address + 5] = 2; Step(2);
                target.ReleaseSwitchHook(); rom[address + 5] = 3;
                guard = 0;
                while (target.State == 3 && guard++ < 30) Step(1);
                FailIf(target.State != (target.Record.SubId == 2 ? 9 : 11) || target.CollisionEnabled ||
                    target.Counter1 != (target.Record.SubId == 0 ? 30 : target.Record.SubId == 1 ? 150 : 0),
                    "Wizzrobe hook landing must restore its source color-specific state/counter and keep collision disabled.");
                Step(170);
            }
        }
        foreach (bool batched in new[] { false, true }) ValidateWizzrobeHookGameplay(batched);
        GD.Print("Validated all three Wizzrobe profiles against complete clean-US ENEMY/PART walks in Mermaid's Cave: phase boundaries, motion, shared RNG, ordered reservations and hit release, held animations, projectile allocation/movement/wall deletion, and text gates in individual/batched gameplay updates.");
    }

    private void ValidateWizzrobeHookGameplay(bool batched)
    {
        ReinitializeGameplayForValidation();
        _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
        _inventory.EquipA(TreasureId.SwitchHook);
        LoadValidationRoom(5, 0x45);
        _player.WarpTo((from y in Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
            from x in Enumerable.Range(1, _currentRoom.WidthInTiles - 2)
            let point = new Vector2(x * 16 + 8, y * 16 + 8)
            where _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None && !_collision.Collides(point)
            select point).First());
        var green = _entities.Entities<WizzrobeCharacter>().First(enemy => enemy.Record.SubId == 0);
        bool Approach()
        {
            foreach (Vector2I direction in new[] { Vector2I.Down, Vector2I.Up, Vector2I.Left, Vector2I.Right })
            {
                Vector2 origin = green.Position.Floor() + (Vector2)direction * 24;
                Vector2 sideways = new(-direction.Y, direction.X);
                if (!Enumerable.Range(0, 25).All(i => Enumerable.Range(-5, 11).All(j =>
                {
                    Vector2 point = green.Position.Floor() + (Vector2)direction * i + sideways * j;
                    return point.X >= 12 && point.X < _currentRoom.Width - 12 && point.Y >= 12 && point.Y < _currentRoom.Height - 12 &&
                        !_currentRoom.IsSolid(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None;
                }))) continue;
                if (_entities.Entities<WizzrobeCharacter>().Any(other => other != green && other.CollisionEnabled &&
                    other.Position.DistanceTo((origin + green.Position) / 2) < 20)) continue;
                _player.WarpTo(origin); _player.Face(-direction); return true;
            }
            return false;
        }
        void Step(int count = 1, bool press = false) => StepGameplayUpdates(count, Vector2.Zero,
            press ? ["attack"] : [], press ? ["attack"] : [], batched: batched);
        for (int repeat = 0; repeat < 2; repeat++)
        {
            int guard = 0;
            while ((!green.CollisionEnabled || !Approach()) && guard++ < 450) Step();
            FailIf(!green.CollisionEnabled || guard >= 450, "Green Wizzrobe needs a real collision-free hook corridor in room$5:$45.");
            Vector2 origin = _player.Position, destination = green.Position.Floor();
            FailIf(_collision.Collides(origin), "Wizzrobe hook approach must start outside solid geometry.");
            Step(1, press: true);
            guard = 0;
            while (!green.SwitchHookHeld && guard++ < 18) Step();
            FailIf(!green.SwitchHookHeld || !_player.IsUsingSwitchHook, "Actual Switch Hook flight must latch Wizzrobe while Link retains its item parent.");
            Step();
            FailIf(_entities.SwitchHook?.ExchangeActive != true, "The following ITEM update must consume the Wizzrobe latch and begin exchange.");
            guard = 0;
            while ((_player.IsUsingSwitchHook || green.State == 3) && guard++ < 100) Step();
            FailIf(guard >= 100 || _entities.SwitchHook?.ExchangeActive == true || _player.Position != destination || green.Position.Floor() != origin ||
                green.State != 11 || green.CollisionEnabled,
                "Actual Wizzrobe exchange must finish, swap reachable positions, release Link, then use green phase-out recovery.");
            Step(1); // Release the button and permit a second full exchange.
        }
    }
}
