using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePeahatMovementMemoryRom() => CompareSharedMovementMemoryRom(0);
    private void ValidateStalfosMovementMemoryRom() => CompareSharedMovementMemoryRom(1);
    private void ValidateMovingOrbMovementMemoryRom() => CompareSharedMovementMemoryRom(2);
    private void ValidateMovingPlatformMovementMemoryRom() => CompareSharedMovementMemoryRom(3);
    private void ValidateCircularPlatformMovementMemoryRom() => CompareSharedMovementMemoryRom(4);

    private void CompareSharedMovementMemoryRom(int kind)
    {
        foreach (bool batched in new[] { false, true })
        foreach (int sub in kind == 4 ? new[] { 0, 1, 2 } : new[] { 0 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(kind == 4 ? 6 : 0, kind == 4 ? 0x2b : 0x33); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(new(24, 24)); _player.BeginCutsceneControl();
            // Declared safe geometry isolates executed movement and its shared
            // scratch from unrelated room actors, terrain and Link input.
            FillValidationRoom(0xa0);
            var database = EnemyDatabase.Shared;
            var visuals = new DungeonInteractionVisualDatabase();
            IRoomEntity actor;
            int id = kind switch { 0 => 0x3e, 1 => 0x31, 2 => 0x0b, 3 => 0x79, _ => 0xa4 };
            Vector2 initial = kind == 2 ? new(128.25f, 72.5f) : new(80.75f, 72.5f);
            if (kind < 2)
            {
                var source = new RoomObjectRecord(0, 0x33, 0, RoomObjectKind.FixedEnemy, id, 0, 2, 1, 72, 80, 0x45, 0);
                actor = new RoomEnemyFactory(_entities, database, new RoomEntityResources(_rooms), _rooms)
                    .CreateOrderedEnemy(database.EnemyHandlers.ResolveHandler(source), source, _currentRoom,
                        initial, 0, 0, EnemyPlacementContext.Unrestricted)
                    ?? throw new InvalidOperationException($"Missing movement-memory ENEMY ${id:x2}.");
                _entities.RegisterEnemySlot(actor, 1);
            }
            else if (kind == 2)
            {
                var record = new SkullDungeonDatabase().GetRoomRecords(4, 0x92).Single(row => row.Id == 0x0b);
                actor = new MovingOrbRoomEntity(record, visuals.Visual("grotto-orb"), _runtimeState,
                    _sound.PlaySound, SoundId.SndSwitch);
                actor.Node.Position = initial;
            }
            else if (kind == 3)
            {
                // Declare dungeon4/script2, retaining the original source mini-script.
                initial = new(112.25f, 64.5f);
                var data = new DungeonInteractionDatabase();
                actor = new MovingPlatformRoomEntity(visuals.Visual("platform-01"), initial, 0x11,
                    data.MovingPlatformCollisionRadii(0x11), data, new MovingPlatformDatabase().Script(4, 2),
                    _entities.PlatformRiding, _entities.ReadPlayingInstrument);
            }
            else
            {
                var record = new WingDungeonDatabase().GetRoomRecords(4, 0x2b)
                    .Single(row => row.Kind == DungeonObjectKind.CircularSidePlatform && row.SubId == sub);
                actor = new CircularSideScrollPlatformRoomEntity(record, visuals.Visual("circular-side-platform"),
                    SideViewInteractionData.CircularSidePlatform(sub));
                initial = Vector2.Zero;
            }
            _entities.AddEntity(actor);
            int slot = kind < 2 ? 0xd180 : kind == 2 ? 0xd0c0 : 0xd000 + _entities.InteractionSlot(actor.Node) * 256 + 0x40;
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 24, 24);
            native[slot] = 1; native[slot + 1] = (byte)id;
            native[slot + 2] = kind == 3 ? (byte)0x11 : (byte)sub;
            native[slot + 3] = kind == 2 ? (byte)2 : (byte)0;
            native.Word(slot + 0x0a, (int)(initial.Y * 256)); native.Word(slot + 0x0c, (int)(initial.X * 256));
            if (kind == 3) native[0xcc39] = 4;
            byte[] sentinel = [0xa5, 0x5a, 0xc3, 0x3c], observed = new byte[4];
            int updates = 0, staged = 0, observations = 0, movementWrites = 0, preserved = 0;
            void AddProbes()
            {
                var stage = new PartMovementScratchProbe(() =>
                {
                    for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, sentinel[i]);
                    staged++;
                });
                _entities.RegisterEnemySlot(stage, 0); _entities.AddEntity(stage);
                _entities.AddEntity(new PartMovementScratchProbe(() =>
                {
                    for (int i = 0; i < 4; i++) observed[i] = _runtimeState.ReadWramByte(0xcec0 + i);
                    observations++;
                }));
            }
            AddProbes();
            var previousText = _entities.TextActiveSource;
            bool held = false, cancelled = false;
            _entities.TextActiveSource = () => held;
            void Pause(bool value) { held = value; native[0xcba0] = value ? (byte)1 : (byte)0; }
            Vector2 Precise() => actor switch
            {
                MovingPlatformRoomEntity platform => platform.PrecisePosition,
                CircularSideScrollPlatformRoomEntity circle => circle.PrecisePosition,
                _ => actor.Node.Position
            };
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    for (int i = 0; i < 4; i++) native[0xcec0 + i] = sentinel[i];
                    if (kind < 2)
                    {
                        SomariaPrivate<FrontendRom>(native, "_rom").Call(0x3616, 0); // setEnemyTargetToLinkPosition.
                        native.AdvanceEnemies(_entities.FrameCounter);
                    }
                    else if (kind == 2) native.AdvanceParts(_entities.FrameCounter);
                    else native.AdvanceInteractions(_entities.FrameCounter);
                    string context = $"Movement memory ${id:x2}:${sub:x2}, batch={batched}, update={++updates}";
                    FailIf(staged != updates || observations != updates, context + ": missing phase observation.");
                    FailIf(!Enumerable.Range(0, 4).All(i => observed[i] == native[0xcec0 + i]),
                        $"{context}: scratch differs: runtime={string.Join(' ', observed.Select(b => b.ToString("x2")))}, native={string.Join(' ', Enumerable.Range(0, 4).Select(i => native[0xcec0 + i].ToString("x2")))}.");
                    if (observed.SequenceEqual(sentinel)) preserved++; else movementWrites++;
                    if (cancelled)
                    {
                        FailIf(_entities.EntityAdapters<IRoomEntity>().Contains(actor), context + ": cancelled actor survived.");
                        return;
                    }
                    FailIf(Precise() != new Vector2(native.Word(slot + 0x0c) / 256f, native.Word(slot + 0x0a) / 256f),
                        $"{context}: fixed position differs: runtime={Precise()}, native={native.Word(slot + 0x0c)/256f},{native.Word(slot + 0x0a)/256f}.");
                    if (actor.Node is PeahatCharacter peahat)
                        FailIf((int)peahat.State != native[slot + 4] || peahat.Counter != native[slot + 6] ||
                            unchecked((byte)peahat.ZHigh) != native[slot + 15], context + ": Peahat state/counter/Z differs.");
                    if (actor.Node is StalfosCharacter stalfos)
                        FailIf((int)stalfos.State != native[slot + 4] || stalfos.Counter1 != native[slot + 6] ||
                            stalfos.Angle != native[slot + 9], context + ": Stalfos state/counter/angle differs.");
                    if (actor is MovingOrbRoomEntity orb)
                        FailIf(orb.State != native[slot + 4], context + ": orb endpoint state differs.");
                    if (actor is MovingPlatformRoomEntity platform)
                        FailIf(platform.Counter != native[slot + 6] || platform.Angle != native[slot + 9] ||
                            platform.Moving != (native[slot + 5] != 0), context + ": platform script state differs.");
                    if (actor is CircularSideScrollPlatformRoomEntity circle)
                        FailIf(circle.Counter != native[slot + 6] || circle.Angle != native[slot + 9],
                            context + ": circular angle/counter differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Calls - seed.Calls != native.RandomCalls || random.Rng1 != native[0xff94] ||
                        random.Rng2 != native[0xff95], context + ": RNG changed while restoring movement writes.");
                });
            }
            try
            {
                Step(1);
                if (kind == 0)
                {
                    Step(64);
                    FailIf(Precise() != initial + new Vector2(0, -0.25f) ||
                        !observed.SequenceEqual(new byte[] { 0xe0, 0xff, 0, 0 }),
                        "Peahat's first two SPEED_20/up steps must publish Y=$ffe0/X=$0000.");
                    Pause(true); Step(2); Pause(false); Step(5);
                    foreach (int angle in new[] { 0, 8, 16, 24 })
                    {
                        var peahat = (PeahatCharacter)actor.Node;
                        F1Set(peahat, "_state", PeahatState.Flying); F1Set(peahat, "_counter", 5);
                        F1Set(peahat, "_angle", angle); F1Set(peahat, "_speedRaw", 0x28);
                        native[slot + 4] = 0x0a; native[slot + 6] = 5;
                        native[slot + 9] = (byte)angle; native[slot + 0x10] = 0x28;
                        Step(4); Step(1); // Counter zero stops movement before slowing.
                        FailIf(!observed.SequenceEqual(sentinel), "Peahat flight completion must not apply speed.");
                        Step(1);
                    }
                }
                else if (kind == 1)
                {
                    foreach (int angle in new[] { 8, 24 })
                    {
                        var stalfos = (StalfosCharacter)actor.Node;
                        F1Set(stalfos, "_state", StalfosState.Walking); F1Set(stalfos, "_counter1", 3);
                        F1Set(stalfos, "_angle", angle);
                        native[slot + 4] = 9; native[slot + 6] = 3; native[slot + 9] = (byte)angle;
                        Pause(true); Step(2); Pause(false); Step(3);
                        FailIf(stalfos.State != StalfosState.Deciding || observed.SequenceEqual(sentinel),
                            "Stalfos must still publish movement on its zero-counter walking update.");
                        Step(1);
                    }
                }
                else
                {
                    if (kind == 4)
                    {
                        byte[] arc = sub switch
                        {
                            0 => [0, 0xcb, 0, 0], 1 => [0, 0, 0, 0x35], _ => [0, 0x35, 0, 0]
                        };
                        FailIf(!observed.SequenceEqual(arc), "INTERAC $a4 setup must publish the radius-$35 scaled words.");
                    }
                    Pause(true); Step(2); Pause(false);
                    Step(kind == 2 ? 210 : kind == 3 ? 300 : 7 + 32 * 14);
                    Pause(true); Step(2); Pause(false); Step(8);
                }
                FailIf(movementWrites == 0 || preserved == 0, $"${id:x2} must exercise movement and no-movement writes.");
                _entities.Clear(); native[slot] = 0; cancelled = true; AddProbes(); Step(2);
                FailIf(!observed.SequenceEqual(sentinel), "Cancelled movement must leave the staged scratch untouched.");
            }
            finally { _entities.TextActiveSource = previousText; }
        }
        ReinitializeGameplayForValidation();
    }
}
