using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateWallArrowShooterRom()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (int room in new[] { 0x2b, 0x2e })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, room);
            var parts = (Dictionary<IRoomEntity, int>)typeof(RoomEntityManager).GetField("_partSlots", flags)!.GetValue(_entities)!;
            var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities", flags)!.GetValue(_entities)!;
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", flags)!;
            var shooters = parts.Where(pair => pair.Key is WallArrowShooterRoomEntity).ToArray();
            var columns = room == 0x2b ? new[] { 72, 152 } : new[] { 136, 152, 168 };
            FailIf(shooters.Length != columns.Length || shooters.Select(pair => pair.Value).Order().Where((slot, i) => slot != i).Any(),
                $"Room $5:${room:x2} must admit its ordered PART$25:$02 stream into slots $00..${columns.Length - 1:x2}.");
            foreach (var pair in shooters)
                FailIf(pair.Key.Node.Position != new Vector2(columns[pair.Value], 8) || ((WallArrowShooterRoomEntity)pair.Key).SubId != 2,
                    "Wall shooter source positions $04/$09 and $08/$09/$0a were not preserved.");
            // Isolate this hazard from the room's Octoroks/Bari and unrelated
            // interactions. Keep its admitted source objects and actual walls.
            foreach (var entity in active.Where(entity => entity is not WallArrowShooterRoomEntity).ToArray())
            { active.Remove(entity); free.Invoke(_entities, [entity]); }
            Vector2 floor = (from y in Enumerable.Range(2, _currentRoom.HeightInTiles - 3)
                from x in Enumerable.Range(1, _currentRoom.WidthInTiles - 2)
                let point = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_collision.Collides(point) && _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None
                orderby columns.Count(column => System.Math.Abs(column - point.X) <= 16) descending, y
                select point).First();
            _player.WarpTo(floor);
            FailIf(_collision.Collides(_player.Position) || !columns.Any(column => System.Math.Abs(column - floor.X) <= 16),
                "Wall shooter target must be reachable on the room's source floor.");
            // Late Link damage is covered separately by the shared arrow
            // contact tests. This fixture compares the native PART walk.
            typeof(Player).GetField("_enemyInvincibilityFrames", flags)!.SetValue(_player, 100000f);
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcdd1] = 0;
            foreach (var pair in shooters)
            {
                int p = 0xd0c0 + pair.Value * 256;
                rom[p] = 1; rom[p + 1] = 0x25; rom[p + 2] = 2;
                rom[p + 11] = 8; rom[p + 13] = (byte)columns[pair.Value];
            }
            var seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            bool text = true; _entities.TextActiveSource = () => text;
            int update = 0, shots = 0;
            var priorArrows = new HashSet<EnemyArrowProjectile>();
            void Compare()
            {
                rom[0xcba0] = (byte)(text ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                foreach (var pair in shooters)
                {
                    var shooter = (WallArrowShooterRoomEntity)pair.Key;
                    int p = 0xd0c0 + pair.Value * 256;
                    FailIf(shooter.State != rom[p + 4] || shooter.Counter != rom[p + 6] || shooter.Angle != rom[p + 9] || shooter.Visible,
                        $"PART$25 $5:${room:x2} update{update}: state/cooldown/angle {shooter.State}/{shooter.Counter}/{shooter.Angle}, ROM {rom[p + 4]}/{rom[p + 6]}/{rom[p + 9]}.");
                }
                var arrows = parts.Where(pair => pair.Key.Node is EnemyArrowProjectile).ToArray();
                int nativeCount = Enumerable.Range(0, 16).Count(slot => rom[0xd0c0 + slot * 256] != 0 && rom[0xd0c1 + slot * 256] == 0x1a);
                FailIf(arrows.Length != nativeCount, $"Wall arrow update{update}: ordered allocation/deletion differs {arrows.Length}/{nativeCount}.");
                foreach (var pair in arrows)
                {
                    var arrow = (EnemyArrowProjectile)pair.Key.Node;
                    int p = 0xd0c0 + pair.Value * 256;
                    var xy = new Vector2(rom.Word(p + 12) / 256f, rom.Word(p + 10) / 256f);
                    FailIf(arrow.SubId != 1 || arrow.NativeState != rom[p + 4] || arrow.Angle != rom[p + 9] ||
                        arrow.Counter != rom[p + 6] || arrow.Position != xy || (arrow.ZFixed & 0xffff) != rom.Word(p + 14),
                        $"PART$1a:$01 $5:${room:x2} slot${pair.Value:x2} update{update} batch={batched}: " +
                        $"runtime state{arrow.NativeState}/angle{arrow.Angle}/counter{arrow.Counter}/XY{arrow.Position}/Z{arrow.ZFixed}; " +
                        $"ROM state{rom[p + 4]}/angle{rom[p + 9]}/counter{rom[p + 6]}/XY{xy}/Z{rom.Word(p + 14)}.");
                    if (priorArrows.Add(arrow)) shots++;
                }
                var rng = _random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                    "Wall shooters and arrows must preserve RNG consumption.");
                update++;
            }
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            var reservations = new List<(EnemyAiSlotReservation Owner, int Slot)>();
            bool fullPool = room == 0x2e && batched;
            if (fullPool)
            {
                for (int slot = columns.Length; slot < 16; slot++)
                {
                    var owner = new EnemyAiSlotReservation(); _entities.AddEntity(owner); parts.Add(owner, slot);
                    reservations.Add((owner, slot)); int p = 0xd0c0 + slot * 256;
                    rom[p] = 1; rom[p + 1] = 0x0a; rom[p + 4] = 1; rom[p + 41] = 1;
                }
            }
            Step(3); // state0 runs through text; later PART states freeze.
            FailIf(shooters.Where(pair => System.Math.Abs(pair.Key.Node.Position.X - floor.X) <= 16)
                .Any(pair => ((WallArrowShooterRoomEntity)pair.Key).Counter != 33),
                "Wall shooter initial shot/cooldown must occur in state0 and remain frozen during text.");
            text = false;
            if (fullPool)
            {
                Step(33); FailIf(shots != 0, "A full PART pool must drop both initial and repeated shots.");
                foreach (var pair in reservations)
                {
                    active.Remove(pair.Owner); free.Invoke(_entities, [pair.Owner]);
                    int p = 0xd0c0 + pair.Slot * 256;
                    for (int i = 0; i < 64; i++) rom[p + i] = 0;
                }
                Step(32); FailIf(shots != 0, "Failed wall shot must retain its entire 33-update cooldown after capacity returns.");
                Step(); FailIf(shots == 0, "Wall shooter must fire again at the next cooldown boundary.");
            }
            int previousShots = shots;
            Step(75);
            FailIf(shots <= previousShots, "Wall shooters must continue firing after the first arrows leave or bounce.");
            // Freeze an initialized shot/counter, then resume the same walk.
            text = true; Step(5); text = false; Step(12);

            // Incoming state0 can shoot before the scroll starts. Its arrow
            // must initialize in the same native PART walk, then stay frozen.
            _entities.Clear(); _player.WarpTo(floor);
            _entities.BeginScreenTransition(5, _currentRoom, Vector2.Left * _currentRoom.Width, _player);
            shooters = parts.Where(pair => pair.Key is WallArrowShooterRoomEntity).ToArray();
            foreach (var entity in active.Where(entity => entity is not WallArrowShooterRoomEntity && entity.Node is not EnemyArrowProjectile).ToArray())
            { active.Remove(entity); free.Invoke(_entities, [entity]); }
            rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            rom[0xcdd1] = 0;
            foreach (var pair in shooters)
            {
                int p = 0xd0c0 + pair.Value * 256;
                rom[p] = 1; rom[p + 1] = 0x25; rom[p + 2] = 2;
                rom[p + 11] = 8; rom[p + 13] = (byte)columns[pair.Value];
            }
            seed = _random.CaptureState(); rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            priorArrows.Clear(); Compare();
            var preload = parts.Values.Count;
            var snapshot = shooters.Select(pair => ((WallArrowShooterRoomEntity)pair.Key).Counter).ToArray();
            var incomingArrows = parts.Keys.Select(entity => entity.Node).OfType<EnemyArrowProjectile>()
                .Select(arrow => (Arrow: arrow, arrow.Position, arrow.Counter)).ToArray();
            FailIf(incomingArrows.Length == 0 || incomingArrows.Any(pair => pair.Arrow.NativeState != 1 || pair.Counter != 8),
                "Wall shooter preload must complete the arrow's state0 with counter8 before scrolling.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(parts.Count != preload || shooters.Where((pair, i) => ((WallArrowShooterRoomEntity)pair.Key).Counter != snapshot[i]).Any() ||
                incomingArrows.Any(pair => pair.Arrow.Position != pair.Position || pair.Arrow.Counter != pair.Counter),
                "Destination wall shooter/arrow counters and movement must remain frozen throughout scrolling.");
            _entities.FinishScreenTransition(); Step();
        }
        GD.Print("Validated clean-US PART$25 wall shooters in real Mermaid rooms $5:$2b/$2e: ordered native slots, repeated 33-update shots, eight-update wall escape, terrain bounce/deletion, text freeze/resume, full-pool dropped shots, scroll preload/freeze/resume, and split/batched gameplay updates.");
    }
}
