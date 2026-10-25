using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerPatrolPublicationRom()
    {
        var placements = new[] { (Room: 0xe7, Pattern: 0), (Room: 0xe7, Pattern: 1),
            (Room: 0xe8, Pattern: 2), (Room: 0xe2, Pattern: 3), (Room: 0xf3, Pattern: 4) };
        (int Direction, int Counter)[][] legs =
        [
            [(2, 0x40), (1, 0x60), (3, 0x60), (0, 0x40)],
            [(2, 0x40), (1, 0x80), (0, 0x20), (2, 0x20), (3, 0x80), (0, 0x40)],
            [(1, 0xa0), (3, 0xa0)],
            [(2, 0x40), (1, 0xa0), (3, 0xa0), (0, 0x40)],
            [(1, 0x60), (3, 0x60)]
        ];
        foreach (var placement in placements)
        foreach (int gate in Enumerable.Range(0, 7))
        foreach (bool batched in gate == 0 && placement.Pattern is 0 or 4 ? new[] { false, true } : new[] { false })
        foreach (bool paired in placement.Pattern == 0 && gate == 0 ? new[] { false, true } : new[] { false })
        {
            ReinitializeGameplayForValidation();
            OracleRandomState seed = _random.CaptureState();
            if (paired)
            {
                // Declared shared RNG input makes the two ordered source text
                // draws distinguishable after the real 256-draw room parse.
                seed = seed with { Rng1 = 1, Rng2 = 1 };
                _random.RestoreState(seed);
            }
            LoadValidationRoom(4, placement.Room);
            _inventory.SetScriptedEquippedItems(0, 0);
            var cohort = _entities.EntityAdapters<BlackTowerPatrollingWorkerRoomEntity>()
                .Where(actor => paired || actor.Npc.Record.Var03 == placement.Pattern).ToArray();
            var worker = cohort.Single(actor => actor.Npc.Record.Var03 == placement.Pattern);
            NpcCharacter npc = worker.Npc;
            foreach (var actor in _entities.EntityAdapters<IRoomEntity>()
                .Where(actor => !cohort.Any(keep => ReferenceEquals(keep, actor))).ToArray())
                _entities.RetireEntity(actor);
            Vector2 start = new[] { new Vector2(0xd8, 0x98), new Vector2(0xc8, 0x68),
                new Vector2(0xd8, 0x28), new Vector2(0x78, 0x98), new Vector2(0x78, 0x68), new Vector2(0x38, 0x68) }
                .First(point => !_collision.Collides(point) && !_entities.BlocksLink(point));
            _player.WarpTo(start); _player.Face(Vector2I.Up);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled, 1);
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)start.X, (int)start.Y);
            native[0xcc05] = 1; // Actual ordered room parse, before any NPC script/RNG visit.
            native.InitializeRoomInteractions();
            native.InitializeLinkGameplay();
            native[AgesWramAddress.wWarpsDisabled] = 1;
            int at = 0xd040 + _entities.InteractionSlot(npc) * 256;
            FailIf(native[at] != 1 || native[at + 1] != 0x58 || native[at + 2] != 3 ||
                native[at + 3] != placement.Pattern || npc.Position != new Vector2(native[at + 13], native[at + 11]),
                $"Native room $4:{placement.Room:x2} must allocate the same physical $58:$03/v${placement.Pattern:x2} placement.");
            // Other placed actors retire before their first visit in both fixtures.
            var retainedSlots = cohort.Select(actor => 0xd040 + _entities.InteractionSlot(actor.Npc) * 256).ToHashSet();
            for (int slot = 0; slot < 16; slot++)
                if (!retainedSlots.Contains(0xd040 + slot * 256)) native[0xd040 + slot * 256] = 0;
            if (gate == 4)
            {
                // Declared retained low bytes, independently staged after placement.
                npc.Position += new Vector2(.25f, .5f);
                native[at + 12] = 0x40; native[at + 10] = 0x80;
            }
            void CheckRandom()
            {
                OracleRandomState actual = _random.CaptureState();
                FailIf(actual.Rng1 != native[0xff94] || actual.Rng2 != native[0xff95] ||
                    actual.Calls - seed.Calls != native.RandomCalls,
                    $"Patrol $58:$03/v${placement.Pattern:x2} room-parse/object RNG differs: {actual.Calls - seed.Calls}/{native.RandomCalls}.");
            }
            CheckRandom();
            byte[] observed = new byte[4], expected = new byte[4];
            var before = new PartMovementScratchProbe(() =>
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, (byte)(0xa5 + i));
                _runtimeState.SetWramByte(0xcddb, 0x55);
            });
            _entities.RegisterEnemySlot(before, 15); _entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(() =>
            { for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i); }));
            int update = 0;
            var observedLegs = new List<(int Direction, int Counter)>();
            void Step(int count, Vector2 movement, bool press = false)
            {
                bool first = true;
                StepGameplayUpdates(count, movement, held: press ? ["attack"] : null,
                    pressed: press ? ["attack"] : null, batched: batched, afterUpdate: () =>
                {
                    int oldCounter = native[at + 0x3c];
                    native.UpdateGameplay(first && press ? 1 : 0, (press ? 1 : 0) |
                        (movement == Vector2.Left ? 0x20 : movement == Vector2.Right ? 0x10 :
                            movement == Vector2.Up ? 0x40 : movement == Vector2.Down ? 0x80 : 0),
                        movement == Vector2.Left ? 0x18 : movement == Vector2.Right ? 0x08 :
                            movement == Vector2.Up ? 0 : movement == Vector2.Down ? 0x10 : 0xff, ++update,
                        beforeInteractions: () =>
                        {
                            for (int i = 0; i < 4; i++) native[0xcec0 + i] = (byte)(0xa5 + i);
                            native[0xcddb] = 0x55;
                        },
                        afterInteractions: () => { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; });
                    first = false;
                    if (oldCounter == 0 && native[at + 0x3c] != 0)
                        observedLegs.Add((native[at + 0x3e], native[at + 0x3c]));
                    CheckRandom();
                    foreach (var other in cohort.Where(actor => !ReferenceEquals(actor, worker)))
                    {
                        int otherAt = 0xd040 + _entities.InteractionSlot(other.Npc) * 256;
                        FailIf(other.Npc.Record.Var03 != native[otherAt + 3] ||
                            other.PrecisePosition.XFixed != native.Word(otherAt + 12) ||
                            other.PrecisePosition.YFixed != native.Word(otherAt + 10) ||
                            other.PatrolCounter != native[otherAt + 0x3c] || other.PatrolDirection != native[otherAt + 0x3e] ||
                            other.PatrolMode != native[otherAt + 0x3f] || other.Script!.Counter != native[otherAt + 6] ||
                            other.Npc.NativeAngle != native[otherAt + 9] || other.Npc.TextId != native.Word(otherAt + 0x32) ||
                            other.Npc.CurrentAnimationCounter != native[otherAt + 0x20] ||
                            other.Npc.CurrentAnimationParameter != native[otherAt + 0x21],
                            $"Paired room $4:e7 patroller v${other.Npc.Record.Var03:x2} differs at update{update}/host{batched}.");
                    }
                    Vector2 precise = worker.PrecisePosition.PrecisePosition;
                    FailIf(worker.Initialized != (native[at + 4] != 0) ||
                        worker.PatrolMode != native[at + 0x3f] || worker.PatrolDirection != native[at + 0x3e] ||
                        worker.PatrolCounter != native[at + 0x3c] || worker.Script!.Counter != native[at + 6] ||
                        worker.Script.ButtonPending != (native[at + 0x31] != 0) ||
                        npc.Position != new Vector2(native[at + 13], native[at + 11]) ||
                        precise != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                        npc.NativeAngle != native[at + 9] || npc.TextId != native.Word(at + 0x32) ||
                        npc.CurrentAnimationCounter != native[at + 0x20] || npc.CurrentAnimationParameter != native[at + 0x21] ||
                        npc.Visible != ((native[at + 0x1a] & 0x80) != 0) ||
                        npc.ZIndex != ObjectDrawPriority.FromVisible(native[at + 0x1a]) ||
                        npc.ObjectCollisionBounds.Size != new Vector2(native[at + 0x27] * 2, native[at + 0x26] * 2) ||
                        _player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                        _player.InvincibilityFrames != unchecked((sbyte)native[0xd02b]) || _player.KnockbackFrames != native[0xd02d] ||
                        _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != native[0xcc8a] ||
                        _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != native[0xcc02] ||
                        _dialogue.IsOpen != (native[0xcba0] != 0) ||
                        _runtimeState.ReadWramByte(0xcddb) != native[0xcddb] || !observed.SequenceEqual(expected),
                        $"Patrol $58:$03/v${placement.Pattern:x2} gate{gate}/host{batched}/pair{paired}/update{update}: XY={npc.Position}/{native[at + 13]},{native[at + 11]}, " +
                        $"precise={precise}/{native.Word(at + 12) / 256f},{native.Word(at + 10) / 256f}, " +
                        $"mode={worker.PatrolMode}/{native[at + 0x3f]}, counter={worker.PatrolCounter}/{native[at + 0x3c]}, wait={worker.Script.Counter}/{native[at + 6]}, " +
                        $"clock={npc.CurrentAnimationCounter}/{native[at + 0x20]}, text=${npc.TextId:x4}/${native.Word(at + 0x32):x4}, flags=${_runtimeState.ReadWramByte(0xcddb):x2}/${native[0xcddb]:x2}, " +
                        $"Link={_player.PrecisePosition}/{native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f}, " +
                        $"pendingA={worker.Script.ButtonPending}/{native[at + 0x31]:x2}, angle={npc.NativeAngle}/{native[at + 9]}, " +
                        $"invincibility={_player.InvincibilityFrames}/{unchecked((sbyte)native[0xd02b])}, knockback={_player.KnockbackFrames}/{native[0xd02d]}, " +
                        $"masks=${_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects):x2}/${native[0xcc8a]:x2},${_runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled):x2}/${native[0xcc02]:x2}, " +
                        $"CEC={Convert.ToHexString(observed)}/{Convert.ToHexString(expected)}.");
                });
            }
            void Mask(byte value) { _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, value); native[0xcc8a] = value; }
            void Text(bool enabled)
            {
                if (enabled) _dialogue.ShowMessage("Patrol gate.", _player.Position.Y); else _dialogue.Close();
                native[0xcba0] = (byte)(enabled ? 1 : 0);
            }
            void Scroll(byte value) { _runtimeState.SetWramByte(AgesWramAddress.wScrollMode, value); native[0xcd00] = value; }
            void RetainedText(bool closing)
            {
                if (closing)
                {
                    _dialogue.ShowGameplayMessage("Closing gate.", _player.Position.Y);
                    _dialogue.RequestClosing();
                    native[0xcba0] = 0xff;
                }
                else
                {
                    _dialogue.ShowGameplayMessageWithFlags("Ready gate.", _player.Position.Y, 2);
                    _dialogue.RevealCurrentPageForValidation();
                    _dialogue.AdvanceApplicationUpdate();
                    native[0xcba0] = 0x80;
                }
                FailIf(!_dialogue.IsOpen || !_dialogue.AllowsNativeScriptCommands,
                    "The retained-text fixture must use the renderer's actual ready/closing producer.");
            }
            if (gate == 1) Text(true);
            if (gate == 2) Mask(2);
            if (gate == 3) Scroll(8);
            if (gate is 5 or 6) RetainedText(gate == 6);
            Step(3, Vector2.Zero);
            if (paired) FailIf(cohort.Single(actor => actor.Npc.Record.Var03 == 0).Npc.TextId != 0x100b ||
                cohort.Single(actor => actor.Npc.Record.Var03 == 1).Npc.TextId != 0x100c,
                "Room $4:e7 must assign the two source RNG text draws in physical interaction-slot order.");
            if (gate is 1 or 5 or 6) Text(false);
            if (gate == 2) Mask(0);
            if (gate == 3) Scroll(0);
            Step(12, Vector2.Zero);
            if (gate is 1 or 2 or 3 or 5 or 6)
            {
                if (gate == 1) Text(true);
                if (gate == 2) Mask(2);
                if (gate == 3) Scroll(8);
                if (gate is 5 or 6) RetainedText(gate == 6);
                Step(3, Vector2.Zero);
                if (gate is 1 or 5 or 6) Text(false);
                if (gate == 2) Mask(0);
                if (gate == 3) Scroll(0);
            }
            Step(600, Vector2.Zero);
            FailIf(!observedLegs.Take(legs[placement.Pattern].Length).SequenceEqual(legs[placement.Pattern]),
                $"Patrol v${placement.Pattern:x2} did not execute every source direction/counter pair in order.");
            if (placement.Pattern != 4 || gate != 0) continue;

            // Replan a short actual-floor walk as the patroller moves. No
            // teleport or obstacle suppression can establish A reachability.
            for (int bound = 0; bound < 640 && !(npc.CanTalkTo(_player) && native[at + 6] != 0);)
            {
                if (npc.CanTalkTo(_player)) { Step(1, Vector2.Zero); bound++; continue; }
                Vector2I origin = (Vector2I)_player.Position;
                Vector2I[] offsets = [Vector2I.Down * 14, Vector2I.Up * 14, Vector2I.Left * 14, Vector2I.Right * 14];
                var goals = offsets.Select(offset => (Point: (Vector2I)npc.Position + offset,
                    Facing: new Vector2I(-Math.Sign(offset.X), -Math.Sign(offset.Y))))
                    .Where(goal => !_collision.Collides(goal.Point) && !_entities.BlocksLink(goal.Point))
                    .ToDictionary(goal => goal.Point, goal => goal.Facing);
                var queue = new Queue<Vector2I>(); queue.Enqueue(origin);
                var previous = new Dictionary<Vector2I, Vector2I> { [origin] = origin };
                Vector2I[] directions = [Vector2I.Up, Vector2I.Left, Vector2I.Down, Vector2I.Right];
                Vector2I? found = null;
                while (queue.Count != 0)
                {
                    Vector2I point = queue.Dequeue();
                    if (goals.ContainsKey(point)) { found = point; break; }
                    foreach (Vector2I direction in directions)
                    {
                        Vector2I next = point + direction;
                        if (previous.ContainsKey(next) || next.X < 8 || next.Y < 8 ||
                            next.X >= _currentRoom.Width - 8 || next.Y >= _currentRoom.Height - 8 ||
                            _collision.Collides(next) || _entities.BlocksLink(next)) continue;
                        previous[next] = point; queue.Enqueue(next);
                    }
                }
                FailIf(found is null, "Room $4:f3 must have an actual floor route to the moving worker's A probe.");
                var path = new List<Vector2I>();
                for (Vector2I point = found.Value; point != origin; point = previous[point]) path.Add(point - previous[point]);
                path.Reverse();
                if (path.Count == 0) { Step(1, goals[found.Value]); bound++; }
                else foreach (Vector2I direction in path.Take(8)) { Step(1, direction); bound++; }
            }
            FailIf(!npc.CanTalkTo(_player) || native[at + 6] == 0,
                "The actual floor approach must reach the worker during its source wait-20.");
            Step(1, Vector2.Zero, press: true);
            FailIf(_dialogue.IsOpen || !worker.Script!.ButtonPending,
                "A press during patrol wait-20 must remain queued until the original A branch.");
            for (int limit = 0; !_dialogue.IsOpen && limit < 64; limit++) Step(1, Vector2.Zero);
            FailIf(!_dialogue.IsOpen || npc.TextId != 0x100d || _player.CutsceneControlled ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81,
                "The queued patrol conversation must show TX_100d with normal Link and shared mask $81.");
            Step(3, Vector2.Zero);
            Text(false); // Explicit textbox completion; rendering is outside this comparison.
            Step(1, Vector2.Zero);
            FailIf(worker.Script.Counter != 30, "Patrol conversation completion must start the source wait-30.");
            Step(29, Vector2.Zero);
            FailIf(worker.Script.Counter != 1 || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81,
                "Patrol input must remain disabled on the update before the source wait-30 expires.");
            Step(1, Vector2.Zero);
            FailIf(_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0 || !npc.CanTalkTo(_player),
                "The wait's zero update must restore patrol animation/control and permit another reachable conversation.");
            Step(1, Vector2.Zero, press: true);
            Step(1, Vector2.Zero);
            FailIf(!_dialogue.IsOpen, "The worker must accept a second reachable A press after releasing control.");
            Step(2, Vector2.Zero);
            Text(false);
            Step(31, Vector2.Zero);
            FailIf(_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                "The repeated patrol conversation retained its input/menu masks.");
            Step(1, Vector2.Zero, press: true);
            Step(1, Vector2.Zero);
            FailIf(!_dialogue.IsOpen, "The cancellation fixture must first start another reachable conversation.");
            Text(false);
            Step(1, Vector2.Zero); // Cancel during the script's real post-text wait-30.
            var cancelled = worker.Script;
            LoadValidationRoom(0, 0x60); // Port debug-room cancellation, not a native warp claim.
            FailIf(cancelled.HasState || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0 || _player.CutsceneControlled,
                "Patrol cancellation retained its runner or input/menu control.");
            LoadValidationRoom(4, 0xf3);
            var restarted = _entities.EntityAdapters<BlackTowerPatrollingWorkerRoomEntity>().Single();
            F1Retain(restarted);
            _player.WarpTo(start);
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            FailIf(!restarted.Initialized || !restarted.Script!.HasState || restarted.PatrolCounter != 0x5d ||
                restarted.PrecisePosition.PrecisePosition != new Vector2(0x99, 0x58) ||
                _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                "Re-entering the room must create a fresh source-timed patrol after cancellation.");
        }
    }

    private void ValidateBlackTowerBlockerPublicationRom()
    {
        foreach (int variant in Enumerable.Range(0, 8))
        foreach (bool batched in RomHostSchedules(variant))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xe0);
            _inventory.SetScriptedEquippedItems(0, 0);
            var blocker = _entities.EntityAdapters<BlackTowerBlockingVillagerRoomEntity>().Single();
            NpcCharacter npc = blocker.Npc;
            bool fractional = variant == 1;
            if (fractional) npc.Position += new Vector2(.25f, .5f);
            F1Retain(blocker);
            Vector2 start = new(0x60 + (fractional ? .25f : 0), 0x98 + (fractional ? .5f : 0));
            FailIf(_collision.Collides(start) || _entities.BlocksLink(start),
                "Room $4:e0 blocker comparison must start on reachable clear floor.");
            _player.WarpTo(start); _player.Face(Vector2I.Left);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled, 1);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 3, (int)start.X, (int)start.Y);
            native.InitializeLinkGameplay();
            native[AgesWramAddress.wWarpsDisabled] = 1;
            int at = 0xd040 + _entities.InteractionSlot(npc) * 256;
            native[at] = 1; native[at + 1] = 0x3a; native[at + 2] = 2;
            native.Word(at + 10, (int)(npc.Position.Y * 256)); native.Word(at + 12, (int)(npc.Position.X * 256));
            native.Word(0xd00a, (int)(_player.PrecisePosition.Y * 256)); native.Word(0xd00c, (int)(_player.PrecisePosition.X * 256));
            byte[] observed = new byte[4], expected = new byte[4];
            var before = new PartMovementScratchProbe(() =>
            { for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, (byte)(0xa5 + i)); });
            _entities.RegisterEnemySlot(before, 15); _entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(() =>
            { for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i); }));
            int update = 0, moving = 0, armed = 0, completed = 0;
            void Step(int count, Vector2 movement, bool press = false)
            {
                bool first = true;
                StepGameplayUpdates(count, movement, held: press ? ["attack"] : null,
                    pressed: press ? ["attack"] : null, batched: batched, afterUpdate: () =>
                {
                    int previousSubstate = native[at + 5], oldX = native.Word(at + 12);
                    native.UpdateGameplay(first && press ? 1 : 0, (press ? 1 : 0) |
                        (movement == Vector2.Left ? 0x20 : movement == Vector2.Right ? 0x10 :
                            movement == Vector2.Up ? 0x40 : movement == Vector2.Down ? 0x80 : 0),
                        movement == Vector2.Left ? 0x18 : movement == Vector2.Right ? 0x08 :
                            movement == Vector2.Up ? 0 : movement == Vector2.Down ? 0x10 : 0xff, ++update,
                        beforeInteractions: () => { for (int i = 0; i < 4; i++) native[0xcec0 + i] = (byte)(0xa5 + i); },
                        afterInteractions: () => { for (int i = 0; i < 4; i++) expected[i] = native[0xcec0 + i]; });
                    first = false;
                    if (previousSubstate == 0 && native[at + 5] == 1) armed++;
                    if (previousSubstate == 1 && native[at + 5] == 0) completed++;
                    if (oldX != native.Word(at + 12)) moving++;
                    FailIf(blocker.Initialized != (native[at + 4] != 0) || blocker.Moving != (native[at + 5] != 0) ||
                        blocker.Direction != native[at + 8] || blocker.SavedLinkY != native[at + 0x39] ||
                        blocker.Script!.Counter != Math.Max(native[at + 6], native[at + 7]) ||
                        npc.Position != new Vector2(native[at + 13], native[at + 11]) ||
                        blocker.PrecisePosition.XFixed != native.Word(at + 12) || blocker.PrecisePosition.YFixed != native.Word(at + 10) ||
                        npc.CurrentAnimationCounter != native[at + 0x20] || npc.CurrentAnimationParameter != native[at + 0x21] ||
                        npc.Visible != ((native[at + 0x1a] & 0x80) != 0) ||
                        npc.ZIndex != ObjectDrawPriority.FromVisible(native[at + 0x1a]) ||
                        npc.NativeAngle != native[at + 9] ||
                        npc.TextId != native.Word(at + 0x32) ||
                        npc.ObjectCollisionBounds.Size != new Vector2(native[at + 0x27] * 2, native[at + 0x26] * 2) ||
                        _player.PrecisePosition != new Vector2(native.Word(0xd00c) / 256f, native.Word(0xd00a) / 256f) ||
                        _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != native[0xcc8a] ||
                        _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != native[0xcc02] ||
                        _player.InvincibilityFrames != unchecked((sbyte)native[0xd02b]) ||
                        _player.KnockbackFrames != native[0xd02d] ||
                        _dialogue.IsOpen != (native[0xcba0] != 0) ||
                        !observed.SequenceEqual(expected),
                        $"Blocker $3a:$02 case{variant}/host{batched}/update{update}: XY={npc.Position}/{native[at + 13]},{native[at + 11]}, " +
                        $"Link={_player.PrecisePosition}/{native.Word(0xd00c) / 256f},{native.Word(0xd00a) / 256f}, " +
                        $"substate={blocker.Moving}/{native[at + 5]}, direction={blocker.Direction}/{native[at + 8]}, savedY={blocker.SavedLinkY}/{native[at + 0x39]}, counter={blocker.Script!.Counter}/{native[at + 6]}/{native[at + 7]}, " +
                        $"clock={npc.CurrentAnimationCounter}/{native[at + 0x20]}, text=${npc.TextId:x4}/${native.Word(at + 0x32):x4}, radii={npc.ObjectCollisionBounds.Size}/{native[at + 0x27] * 2},{native[at + 0x26] * 2}, " +
                        $"visible={npc.Visible}/{native[at + 0x1a]:x2}, priority={npc.ZIndex}/{ObjectDrawPriority.FromVisible(native[at + 0x1a])}, " +
                        $"mask=${_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects):x2}/${native[0xcc8a]:x2}, " +
                        $"menu=${_runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled):x2}/${native[0xcc02]:x2}, " +
                        $"invincibility={_player.InvincibilityFrames}/{unchecked((sbyte)native[0xd02b])}, knockback={_player.KnockbackFrames}/{native[0xd02d]}, " +
                        $"textOpen={_dialogue.IsOpen}/{native[0xcba0]:x2}, CEC={Convert.ToHexString(observed)}/{Convert.ToHexString(expected)}.");
                    OracleRandomState actual = _random.CaptureState();
                    FailIf(actual.Rng1 != native[0xff94] || actual.Rng2 != native[0xff95] || actual.Calls - seed.Calls != native.RandomCalls,
                        $"Blocker $3a:$02 case{variant}/host{batched}/update{update} changed shared RNG consumption.");
                });
            }
            void Mask(byte value) { _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, value); native[0xcc8a] = value; }
            void Text(bool enabled)
            {
                if (!enabled) { _dialogue.Close(); native[0xcba0] = 0; return; }
                _dialogue.ShowGameplayMessageWithFlags("Blocker gate.", _player.Position.Y, variant == 6 ? 2 : 0);
                if (variant == 6)
                {
                    _dialogue.RevealCurrentPageForValidation();
                    _dialogue.AdvanceApplicationUpdate();
                    native[0xcba0] = 0x80;
                }
                else if (variant == 7) { _dialogue.RequestClosing(); native[0xcba0] = 0xff; }
                else native[0xcba0] = 1;
            }
            void Scroll(byte value) { _runtimeState.SetWramByte(AgesWramAddress.wScrollMode, value); native[0xcd00] = value; }
            if (variant is 2 or 6 or 7) Text(true);
            if (variant == 3) Mask(2);
            if (variant == 4) Scroll(8);
            Step(3, Vector2.Zero); // Fresh state0 is admitted, then initialized objects freeze.
            if (variant is 2 or 6 or 7) Text(false);
            if (variant == 3) Mask(0);
            if (variant == 4) Scroll(0);
            Step(4, Vector2.Zero);
            for (int limit = 0; armed == 0 && limit < 40; limit++) Step(1, Vector2.Left);
            FailIf(armed != 1, "Link must reach the blocker's open-side trigger through room $4:e0 floor.");
            Step(4, Vector2.Zero);
            if (variant == 5)
            {
                // Debug-room cancellation is a port boundary, not a native warp claim.
                var cancelled = blocker.Script!;
                LoadValidationRoom(0, 0x60);
                FailIf(cancelled.HasState || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                    "Cancelling the active blocker retained a script or shared input/menu mask.");
                LoadValidationRoom(4, 0xe0); _player.WarpTo(start);
                var restarted = _entities.EntityAdapters<BlackTowerBlockingVillagerRoomEntity>().Single();
                StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                StepGameplayUpdates(24, Vector2.Left, batched: batched);
                FailIf(!restarted.Moving || _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0x81,
                    "The blocker must accept a fresh reachable intervention after cancellation.");
                StepGameplayUpdates(48, Vector2.Zero, batched: batched);
                FailIf(restarted.Moving || restarted.Npc.Position.X != 0x47 ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != 0 ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0,
                    "The restarted blocker did not finish its source-derived 15-movement intervention.");
                continue;
            }
            if (variant is 2 or 3 or 4 or 6 or 7)
            {
                if (variant is 2 or 6 or 7) Text(true);
                if (variant == 3) Mask(0x83);
                if (variant == 4) Scroll(8);
                Step(3, Vector2.Zero);
                if (variant is 2 or 6 or 7) Text(false);
                if (variant == 3) Mask(0x81);
                if (variant == 4) Scroll(0);
            }
            if (fractional)
            {
                // Declared retained-coordinate input, not a room-specific writer:
                // high-byte restoration occurs once, preserving Link's low byte.
                _player.SetScriptedCoordinateHigh(false, 155); native[0xd00b] = 155;
                Step(1, Vector2.Zero);
                FailIf(_player.PrecisePosition.Y != 155.5f, "Moving visits must not restore Link Y before asm15.");
                for (int limit = 0; native[at + 6] == 0 && limit < 24; limit++) Step(1, Vector2.Zero);
                FailIf(_player.PrecisePosition.Y != 152.5f || native[at + 6] != 10,
                    "The restore helper must preserve Link's Y fraction and install the source wait.");
                _player.SetScriptedCoordinateHigh(false, 157); native[0xd00b] = 157;
                Step(2, Vector2.Zero);
                FailIf(_player.PrecisePosition.Y != 157.5f, "The following wait must not restore Link Y a second time.");
            }
            Step(48, Vector2.Zero);
            FailIf(armed != 1 || completed != 1 || moving != 15,
                $"Original blocker must finish one reachable 15-movement intervention: {armed}/{completed}/{moving}.");
            if (variant != 0) continue;

            void Walk(Vector2 destination, bool stopWhenArmed, bool avoidOpenSide = false)
            {
                Vector2I origin = (Vector2I)_player.Position, goal = (Vector2I)destination;
                var queue = new Queue<Vector2I>(); queue.Enqueue(origin);
                var previous = new Dictionary<Vector2I, Vector2I> { [origin] = origin };
                Vector2I[] directions = [Vector2I.Down, Vector2I.Left, Vector2I.Up, Vector2I.Right];
                while (queue.Count != 0 && !previous.ContainsKey(goal))
                {
                    Vector2I point = queue.Dequeue();
                    foreach (Vector2I direction in directions)
                    {
                        Vector2I next = point + direction;
                        if (previous.ContainsKey(next) || next.X < 8 || next.Y < 8 ||
                            next.X >= _currentRoom.Width - 8 || next.Y >= _currentRoom.Height - 8 ||
                            _collision.Collides(next) || _entities.BlocksLink(next)) continue;
                        // Route to conversation around the occupied lane without
                        // entering the opposite lane's native intervention box.
                        int openX = (int)npc.Position.X + (blocker.Direction == 0 ? 0x11 : -0x11);
                        if (avoidOpenSide && next.X - openX is >= -9 and < 9 &&
                            next.Y - (int)npc.Position.Y is >= -11 and < 11) continue;
                        previous[next] = point; queue.Enqueue(next);
                    }
                }
                FailIf(!previous.ContainsKey(goal), $"Room $4:e0 has no actual floor route from {origin} to {goal}.");
                var path = new List<Vector2I>();
                for (Vector2I point = goal; point != origin; point = previous[point]) path.Add(point - previous[point]);
                path.Reverse(); int oldArmed = armed;
                foreach (Vector2I direction in path)
                {
                    Step(1, direction);
                    if (stopWhenArmed && armed != oldArmed) return;
                }
            }
            Walk(npc.Position + Vector2.Left * 24, stopWhenArmed: true);
            Step(48, Vector2.Zero);
            FailIf(armed != 2 || completed != 2 || moving != 30 || npc.Position.X != 0x38,
                $"Reachable reversal must complete the second 15-movement intervention: {armed}/{completed}/{moving}.");
            Walk(npc.Position + Vector2.Up * 14, stopWhenArmed: false, avoidOpenSide: true);
            Step(1, Vector2.Down);
            FailIf(!npc.CanTalkTo(_player), $"The actual room-floor route must reach the blocker's conversation probe: " +
                $"NPC={npc.Position}, Link={_player.Position}, facing={_player.FacingVector}, radii={npc.ObjectCollisionBounds.Size}, " +
                $"sensitive={npc.ScriptButtonSensitive}, moving={blocker.Moving}.");
            Step(1, Vector2.Zero, press: true);
            FailIf(!_dialogue.IsOpen || npc.TextId != 0x1441 || _player.CutsceneControlled,
                "The blocker's reachable A press must show TX_1441 through normal Link and its native interaction slot.");
            Step(3, Vector2.Zero);
            Text(false); // Declared textbox completion; this fixture does not emulate the text renderer.
            Step(5, Vector2.Zero);
            Step(1, Vector2.Zero, press: true); // The native pressed-A byte survives the post-text wait.
            FailIf(_dialogue.IsOpen, "A queued second conversation must retain the native post-text wait.");
            Step(6, Vector2.Zero);
            FailIf(!_dialogue.IsOpen, "The blocker must consume the retained A press after its wait and animation yield.");
            Text(false);
            Step(15, Vector2.Zero);
        }
    }
}
