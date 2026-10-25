using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateEnemyInitializationGatesRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x1d, 0x33, 0x38, 0x50, 0x52, 0x62, 0x63 })
        foreach (string gate in new[] { "text", "mask04", "mask80", "palette", "scroll02", "scroll04", "scroll08", "scroll0e", "none" })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xa1); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(120, 40)); _player.BeginCutsceneControl();
            var database = EnemyDatabase.Shared; var vines = new VineSproutDatabase();
            int callsBeforeConstruction = _random.Calls;
            IRoomEntity Baby()
            {
                var resources = new RoomEntityResources(_rooms);
                var actor = new BabyCuccoCharacter();
                actor.Initialize(database.ImportedEnemy(0x33), _currentRoom, new(80, 72), _random,
                    resources.Bracelet.Data, resources.Bomb.Data, _sound.PlaySound, (_, _, _, _) => { });
                return new BabyCuccoRoomEntity(actor);
            }
            IRoomEntity Crystal()
            {
                var visual = database.ImportedEnemy(0x63); string animation = visual.Animations[0];
                var actor = NpcCharacter.CreateFromRecord(new(4, 0xa1, AgesInteractionId.Accessory, 0, 0, 0, 0, 0,
                    visual.Sprites[0], visual.TileBase, visual.Palette, 0, false, animation, animation, animation, animation, "",
                    NpcImplementationClassification.EventOwned));
                actor.SetSourceGrayscaleInverted(visual.SourceGrayscaleInverted);
                return new TargetCartCrystalRoomEntity(actor, 0, _runtimeState, new GoronCaveDatabase(), _sound.PlaySound, _random);
            }
            IRoomEntity adapter = id switch
            {
                0x1d => new ArmosSpawnerRoomEntity(_currentRoom, 2, 0x0c, _random, _entities.CanAllocateEnemies),
                0x33 => Baby(),
                0x38 => new FountainFairyRoomEntity(database.ImportedEnemy(0x38), new FountainFairyDatabase(), new(80, 72),
                    _sound.PlaySound, (_, _, _) => { }, () => { }, () => _player.HealthQuarters, _entities.TryCreatePart, _random),
                0x50 => new FireballShooterRoomEntity(_currentRoom, new(80, 72), 0x80, 0, _random,
                    _entities.CanAllocateEnemies, () => _entities.PartSlotAvailable, () => _entities.RoomEnemyCount),
                0x52 => new FlyingTileSpawnerRoomEntity(0, database.ImportedEnemy(0x52), false, _random),
                0x62 => new VineSproutRoomEntity(vines, vines.Record(0), _currentRoom, _saveData,
                    vines.ResolvePosition(0, _currentRoom, _saveData), _sound.PlaySound, () => { }, () => _entities.FrameCounter, _random),
                _ => Crystal()
            };
            _entities.RegisterEnemySlot(adapter, 0); _entities.AddEntity(adapter);
            FailIf(_random.Calls != callsBeforeConstruction,
                $"ENEMY ${id:x2} construction/allocation must defer common RNG until its state-zero dispatch.");
            int State() => adapter switch
            {
                FountainFairyRoomEntity fairy => fairy.State, FireballShooterRoomEntity fire => fire.State,
                FlyingTileSpawnerRoomEntity tile => (int)tile.State, ArmosSpawnerRoomEntity armos => armos.State,
                VineSproutRoomEntity vine => CompanionField<bool>(vine, "_initialized") ? 1 : 0,
                TargetCartCrystalRoomEntity crystal => CompanionField<int>(crystal, "_state"),
                BabyCuccoRoomEntity baby => (int)((BabyCuccoCharacter)baby.Node).State,
                _ => throw new InvalidOperationException()
            };
            var text = _entities.TextActiveSource; var palette = _entities.PaletteFadeActiveSource;
            bool held = gate != "none";
            _entities.TextActiveSource = () => held && gate == "text";
            _entities.PaletteFadeActiveSource = () => held && gate == "palette";
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 120, 40);
            rom[0xd080] = 1; rom[0xd081] = (byte)id; rom[0xd082] = id == 0x50 ? (byte)0x80 : (byte)0;
            rom[0xd08b] = 72; rom[0xd08d] = 80;
            void Gates()
            {
                rom[0xcc8a] = held && gate == "mask04" ? (byte)4 : held && gate == "mask80" ? (byte)0x80 : (byte)0;
                rom[0xcba0] = held && gate == "text" ? (byte)1 : (byte)0;
                rom[0xc4ab] = held && gate == "palette" ? (byte)1 : (byte)0;
                rom[0xcd00] = (byte)(held ? gate switch
                { "scroll02" => 2, "scroll04" => 4, "scroll08" => 8, "scroll0e" => 0x0e, _ => 1 } : 1);
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, rom[0xcc8a]);
                _runtimeState.SetWramByte(AgesWramAddress.wScrollMode, rom[0xcd00]);
            }
            try
            {
                Gates(); StepGameplayUpdates(1, Vector2.Zero, batched: batched); rom.AdvanceEnemies(_entities.FrameCounter);
                int expected = id is 0x33 or 0x50 ? 8 : 1;
                FailIf(State() != expected || rom[0xd084] != expected || rom.RandomCalls != 1,
                    $"ENEMY ${id:x2} must initialize once under {gate}; port={State()}, source={rom[0xd084]}.");
                void CheckRandom()
                {
                    var random = _random.CaptureState();
                    FailIf(random.Calls - callsBeforeConstruction != rom.RandomCalls ||
                        random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95],
                        $"ENEMY ${id:x2} under {gate} lost common var3d RNG: port={random.Calls - callsBeforeConstruction}, source={rom.RandomCalls}.");
                }
                CheckRandom();
                if (held)
                {
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () => rom.AdvanceEnemies(_entities.FrameCounter));
                    FailIf(State() != expected || rom[0xd084] != expected || rom.RandomCalls != 1,
                        "Initialized enemies must freeze on subsequent restricted passes.");
                    CheckRandom();
                    held = false; Gates();
                    StepGameplayUpdates(1, Vector2.Zero, batched: batched); rom.AdvanceEnemies(_entities.FrameCounter);
                    int portResumed = State(), sourceResumed = rom[0xd084];
                    held = true; Gates();
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () => rom.AdvanceEnemies(_entities.FrameCounter));
                    FailIf(State() != portResumed || rom[0xd084] != sourceResumed,
                        "Reapplying the gate must freeze each initialized controller after release.");
                }
            }
            finally { _entities.TextActiveSource = text; _entities.PaletteFadeActiveSource = palette; _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0); _runtimeState.SetWramByte(AgesWramAddress.wScrollMode, 1); }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared state-zero admission and common RNG for five controller families, Baby Cucco and Armos control across126 text/mask/palette/raw-scroll cases.");
    }
}
