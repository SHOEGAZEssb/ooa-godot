using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMoblinSharedHandlerRom()
    {
        foreach (int id in new[] { 0x0c, 0x20, 0x22 })
        foreach (int sub in new[] { 0, 1 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x98);
            _entities.Clear();
            _entities.DisableLinkCollisionsAndMenu();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120, 24)); _player.BeginCutsceneControl();
            for (int y = 0; y < _currentRoom.HeightInTiles; y++)
            for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);

            var seed = _random.CaptureState() with { Rng1 = 0x37, Rng2 = 0x0d };
            _random.RestoreState(seed);
            FailIf(!_entities.TrySpawnEnemy(id, sub, new(80, 72), "native shared Moblin regression", out string error), error);
            var actor = _entities.Entities<ArrowMoblinCharacter>().Single();
            var animation = (EnemyAnimationPlayer)typeof(EnemyCharacter)
                .GetField("_animation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(actor)!;
            actor.Position = new(80.25f, 72.5f);
            FailIf(!actor.InitializationPending || actor.Visible || _random.Calls != seed.Calls,
                $"ENEMY${id:x2}:${sub:x2} constructed outside native state-zero dispatch.");
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 24);
            native.InitializeLinkGameplay(); // Includes the native collision-buffer boundary rows/columns.
            // Declare the resident sheet; execute original properties/AI/RNG without replacements.
            native[0xcc0a] = ValidationRom.LoadCleanUs().Span[0xfdd4b + id * 4];
            native[0xd080] = 1; native[0xd081] = (byte)id; native[0xd082] = (byte)sub;
            native.Word(0xd08a, 0x4880); native.Word(0xd08c, 0x5040);
            int updates = 0, turns = 0, expiries = 0;
            void Compare()
            {
                int oldState = native[0xd084], oldCounter = native[0xd086];
                Vector2 oldPosition = new(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f);
                AdvancePartAdmissionRom(native);
                updates++;
                string context = $"ENEMY${id:x2}:${sub:x2}/batch{batched}/update{updates}";
                var rng = _random.CaptureState();
                FailIf((int)actor.State != native[0xd084] || actor.Counter != native[0xd086] ||
                    actor.Angle != native[0xd089] || actor.MoveCycles != native[0xd0b0] ||
                    actor.Position != new Vector2(native.Word(0xd08c) / 256f, native.Word(0xd08a) / 256f) ||
                    actor.Health != native[0xd0a9] || actor.ScentAttractionCounter != native[0xd0bd] ||
                    actor.AnimationIndex != native[0xd088] || actor.AnimationParameter != native[0xd0a1] ||
                    animation.RemainingUpdates != native[0xd0a0] ||
                    rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                    $"{context}: shared state/counter/angle/XY/health/var3d/RNG differs from clean US; " +
                    $"state={(int)actor.State}/{native[0xd084]}, counter={actor.Counter}/{native[0xd086]}, " +
                    $"XY={actor.Position}/{native.Word(0xd08c) / 256f},{native.Word(0xd08a) / 256f}.");
                if (oldState == 8 && oldCounter == 1)
                {
                    expiries++;
                    FailIf(actor.Position != oldPosition || actor.Counter != 8 || actor.State != ArrowMoblinState.Turning,
                        $"{context}: native movement expiry must stand without another movement step.");
                }
                if (oldState == 9 && oldCounter == 1) turns++;
                if (updates == 1)
                    FailIf(_random.Calls != seed.Calls + 3 || !actor.Visible || actor.InitializationPending ||
                        actor.Record.RadiusY != native[0xd0a6] || actor.Record.RadiusX != native[0xd0a7] ||
                        actor.Record.RawDamage != native[0xd0a8] || actor.Record.Health != native[0xd0a9] ||
                        // wLoadedObjectGfx slot $01 adds $20 to native VRAM tile indices.
                        actor.Record.TileBase + 0x20 != native[0xd09d] || actor.Record.Palette != (native[0xd09b] & 7),
                        $"{context}: native initial three RNG draws and variant properties were lost: " +
                        $"tile={actor.Record.TileBase:x2}/{native[0xd09d]:x2}, palette={actor.Record.Palette:x2}/{native[0xd09b]:x2}.");
            }
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: Compare);
            Step(1);
            Step(400);
            FailIf(turns < 3 || expiries == 0, "Moblin fixture did not reach natural route and expiry boundaries.");
            _entities.Clear();
            FailIf(_entities.Entities<ArrowMoblinCharacter>().Count != 0 || _entities.Entities<EnemyArrowProjectile>().Count != 0,
                "Room cancellation retained a Moblin or its allocated arrow.");
        }
        foreach (bool batched in new[] { false, true }) CheckMoblinTransitionRom(batched);
        ReinitializeGameplayForValidation();
        GD.Print("Compared the one Moblin handler for ENEMY$0c/$20/$22:$00/$01 with clean-US initialization, properties, natural routes and cancellation under both schedules.");
    }

    private void CheckMoblinTransitionRom(bool batched)
    {
        ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x98); _entities.Clear();
        _entities.DisableLinkCollisionsAndMenu(); _player.WarpTo(new(120, 24)); _player.BeginCutsceneControl();
        var room = _world.LoadRoom(4, 0xab);
        var native = new SomariaRom(_saveData, _random.CaptureState(), room, 0, 120, 24);
        native.InitializeLinkGameplay(); native.GeneratePlacementBuffer();
        native[0xcc0a] = 0x91; native[0xcd00] = 8;
        // Crown $4:$ab's ordered source stream: four fixed $0c:$00/$01 actors.
        int[] subids = [0, 1, 0, 1];
        Vector2[] positions = [new(0x58, 0x48), new(0x98, 0x48), new(0x98, 0x68), new(0x58, 0x68)];
        for (int slot = 0; slot < 4; slot++)
        {
            int at = 0xd080 + slot * 256;
            native[at] = 1; native[at + 1] = 0x0c; native[at + 2] = (byte)subids[slot];
            native[at + 11] = (byte)positions[slot].Y; native[at + 13] = (byte)positions[slot].X;
        }
        _entities.BeginScreenTransition(4, room, new(240, 0), _player);
        var actors = _entities.Entities<ArrowMoblinCharacter>();
        FailIf(actors.Count != 4, "Crown $4:$ab preload lost the ordered four Moblins.");
        void Compare()
        {
            native.AdvanceEnemies(_entities.FrameCounter);
            var rng = _random.CaptureState();
            FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95],
                "Moblin preload repeated or reordered native initialization RNG.");
            for (int slot = 0; slot < 4; slot++)
            {
                int at = 0xd080 + slot * 256; var actor = actors[slot];
                FailIf(actor.InitializationPending || !actor.Visible || (int)actor.State != native[at + 4] ||
                    actor.Counter != native[at + 6] || actor.Angle != native[at + 9] ||
                    actor.Position != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f),
                    $"Crown $4:$ab Moblin slot${slot:x2} preload/freeze/arrival differs from clean US.");
            }
        }
        Compare();
        StepGameplayUpdates(24, Vector2.Zero, batched: batched, afterUpdate: Compare);
        _entities.FinishScreenTransition(); native[0xcd00] = 1;
        StepGameplayUpdates(1, Vector2.Zero, batched: batched, afterUpdate: Compare);
        _entities.Clear();
    }
}
