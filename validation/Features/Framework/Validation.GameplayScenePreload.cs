using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePreparedIntroHandoff()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = typeof(GameRoot);
        void Invoke(string name, params object[] args) => root.GetMethod(name, flags)!.Invoke(this, args);
        T? Read<T>(string name) where T : class => (T?)root.GetField(name, flags)!.GetValue(this);

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            Invoke("ReleaseGameplayScene", true);
            _scene = null!;
            var save = OracleSaveData.CreateStandardGame();
            byte[] before = save.Serialize();
            int rng = _random.Calls;
            Invoke("StartSelectedFile", 0, save);
            var intro = Read<NewGameIntroController>("_newGameIntro")!;
            int frames = 0;
            while (frames < 360)
            {
                int updates = batched ? 4 : 1;
                base._Process(updates / 60.0);
                frames += updates;
                FailIf(!before.SequenceEqual(save.Serialize()) || _random.Calls != rng,
                    "Intro preparation changed save bytes or consumed object RNG before room entry.");
                if (_scene is not null)
                    FailIf(_scene.Visible || _scene.InterfaceLayer.Visible || _roomCamera.Enabled ||
                        _scene.ProcessMode != ProcessModeEnum.Disabled,
                        "Prepared gameplay became visible or eligible for node updates during the intro.");
            }
            // Hosts with fewer rendered frames may still be preparing while
            // the original dialogue waits for input.
            for (int host = 0; !GameplayPrepared && host < 256; host++)
                AdvanceGameplayPreparation();
            FailIf(!GameplayPrepared || intro.CurrentStage != Stage.Dialogue ||
                !before.SequenceEqual(save.Serialize()) || _random.Calls != rng ||
                _entities.EntityAdapters<IRoomEntity>().Any() ||
                _sound.ActiveMusic != SoundId.MusEssenceRoom ||
                !_runtimeState.CaptureState().Wram.SequenceEqual(new OracleRuntimeState().CaptureState().Wram),
                "Prepared intro did not stop at the dormant, side-effect-free handoff boundary.");
            Read<NewGameIntroScreen>("_newGameIntroScreen")!.Dialogue.Close();
            base._Process(1.0 / 60.0);
            for (int frame = 0; frame < intro.TotalVanishFrames + 59; frame++)
                base._Process(1.0 / 60.0);
            FailIf(Read<NewGameIntroController>("_newGameIntro") is null ||
                !before.SequenceEqual(save.Serialize()) || _random.Calls != rng,
                "Prepared gameplay entered before the original post-vanish $3c hold completed.");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            base._Process(1.0 / 60.0);
            GD.Print($"Prepared intro handoff: {timer.Elapsed.TotalMilliseconds:F2} ms.");
            FailIf(Read<NewGameIntroController>("_newGameIntro") is not null || GameplayPrepared ||
                !save.HasGlobalFlag(GlobalFlag.PregameIntroDone) ||
                !save.HasGlobalFlag(GlobalFlag.LinkSummoned) ||
                !save.HasRoomFlag(save.RespawnGroup, save.RespawnRoom, OracleSaveData.RoomFlagVisited) ||
                save.GashaMaturity != 5 ||
                _random.Calls <= rng || _player.Visible ||
                !_scene!.Visible || !_scene.InterfaceLayer.Visible || !_roomCamera.Enabled ||
                _rooms.ActiveGroup != save.RespawnGroup || _currentRoom.Id != save.RespawnRoom ||
                _newGameArrivalFadeFrames != 65,
                "Prepared handoff lost room entry, RNG, visibility, or the source 65-update arrival fade.");
            var scene = _scene;
            int handoffRng = _random.Calls;
            for (int frame = 0; frame < 10; frame++)
                base._Process(1.0 / 60.0);
            FailIf(_scene != scene || _random.Calls != handoffRng ||
                _newGameArrivalPhase != 10 || _player.Visible || save.GashaMaturity != 5,
                "After handoff, gameplay was rebuilt or advanced objects during the arrival fade.");
        }
        // Simulate an intro advanced without any preparation host frames.
        // Completion must drain the same iterator and enter exactly once.
        ReinitializeGameplayForValidation();
        Invoke("ReleaseGameplayScene", true);
        _scene = null!;
        var unfinished = OracleSaveData.CreateStandardGame();
        Invoke("StartSelectedFile", 0, unfinished);
        var unfinishedIntro = Read<NewGameIntroController>("_newGameIntro")!;
        StepGameplayUpdates(360, Vector2.Zero, batched: true);
        Read<NewGameIntroScreen>("_newGameIntroScreen")!.Dialogue.Close();
        StepGameplayUpdates(unfinishedIntro.TotalVanishFrames + 61, Vector2.Zero, batched: true);
        FailIf(Read<NewGameIntroController>("_newGameIntro") is not null ||
            unfinished.GashaMaturity != 5 || !_scene!.Visible,
            "An unfinished preparation did not drain and enter the arrival room exactly once.");
        ReinitializeGameplayForValidation();
        Invoke("ReleaseGameplayScene", true);
        _scene = null!;
        var cancelled = OracleSaveData.CreateStandardGame();
        byte[] cancelledBytes = cancelled.Serialize();
        Invoke("StartSelectedFile", 0, cancelled);
        base._Process(1.0 / 60.0);
        ReinitializeGameplayForValidation();
        AdvanceGameplayPreparation();
        FailIf(GameplayPrepared || Read<NewGameIntroController>("_newGameIntro") is not null ||
            !cancelledBytes.SequenceEqual(cancelled.Serialize()),
            "Cancelling partial preparation retained the intro or committed its save state.");
        GD.Print("Validated staged intro preparation and original handoff through individual and batched host updates.");
    }

    private void ValidateDeferredGameplayAssets()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        static object? Field(object owner, string name) =>
            owner.GetType().GetField(name, fields)!.GetValue(owner);

        byte[] save = _saveData.Serialize();
        int rngCalls = _random.Calls;
        object factory = Field(_entities, "_factory")!;
        FailIf(Field(factory, "_smogProjectileData") is not null ||
            Field(_player, "_swordTextureCache") is not null ||
            Field(_player, "_seedShooterTexturesCache") is not null,
            "Gameplay initialization eagerly prepared unused boss/item assets.");

        // Same source-derived spr_sword/OAM pixels asserted by the item
        // regression, now exercised on first access after gameplay startup.
        FailIf(_player.SwordAtlasPixelHash != 0x61e9abb0e1173ec7UL,
            "First-use sword atlas changed the original OAM pixels.");
        object? sword = Field(_player, "_swordTextureCache");
        FailIf(sword is null || _player.SwordAtlasPixelHash != 0x61e9abb0e1173ec7UL ||
            !ReferenceEquals(sword, Field(_player, "_swordTextureCache")) ||
            Field(_player, "_seedShooterTexturesCache") is not null,
            "Repeated sword presentation rebuilt its atlas or prepared unrelated item graphics.");
        FailIf(!save.SequenceEqual(_saveData.Serialize()) || _random.Calls != rngCalls,
            "Deferred player graphics changed save state or consumed gameplay RNG.");

        _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(40, 40), 1));
        object? database = Field(factory, "_smogProjectileData");
        _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(72, 40), 1));
        FailIf(database is null || !ReferenceEquals(database, Field(factory, "_smogProjectileData")) ||
            _entities.EntityAdapters<SmogProjectileRoomEntity>().Count() != 2,
            "PART $4a first/repeated dispatch did not resolve and retain its session database.");
        GD.Print("Validated deferred gameplay assets, first-use sword pixels, repeated dispatch and graphics state isolation.");
    }

    private void ValidateGameplayScenePreload()
    {
        byte[] save = _saveData.Serialize();
        int rngCalls = _random.Calls;
        int children = GetChildCount();
        OracleRoomData room = _currentRoom;
        var resource = new GameplaySceneResource();
        resource.BeginPreload();
        resource.BeginPreload();
        PackedScene prepared = resource.Load();
        resource.BeginPreload();
        FailIf(prepared != resource.Load() || prepared.ResourcePath != GameSceneGraph.ScenePath,
            "Gameplay scene preload did not retain its resource across repeated requests.");
        FailIf(!save.SequenceEqual(_saveData.Serialize()) || _random.Calls != rngCalls ||
            _currentRoom != room || GetChildCount() != children,
            "Gameplay resource preparation changed save, RNG, room identity or live nodes before the intro handoff.");
        GameSceneGraph instance = prepared.Instantiate<GameSceneGraph>();
        try
        {
            FailIf(instance.IsInsideTree() || instance.GetNodeOrNull<Player>("World/Link") is null,
                "Prepared gameplay scene did not preserve its off-tree Link hierarchy.");
        }
        finally { instance.Free(); }
        var direct = new GameplaySceneResource();
        FailIf(direct.Load() != prepared,
            "Direct gameplay initialization did not reuse the same packed scene resource.");
        GD.Print("Validated gameplay scene background preload, repeated/direct loads, and side-effect-free preparation before intro completion.");
    }
}
