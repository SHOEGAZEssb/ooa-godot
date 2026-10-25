using Godot;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidatePreparedIntroHandoff()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = typeof(GameRoot);
        void Invoke(string name, params object[] args) => root.GetMethod(name, flags)!.Invoke(this, args);
        T? Read<T>(string name) where T : class => (T?)root.GetField(name, flags)!.GetValue(this);
        static bool SamePayloadExceptPlaytime(byte[] before, OracleSaveData save)
        {
            byte[] after = save.Serialize();
            // Checksum changes with playtime; all other file payload remains
            // unchanged by host-only resource preparation.
            return Enumerable.Range(0x0a, before.Length - 0x0a)
                .Where(offset => offset is < 0x72 or > 0x75)
                .All(offset => before[offset] == after[offset]);
        }

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
                int playtime = save.ReadWramByte(0xc622) | save.ReadWramByte(0xc623) << 8;
                FailIf(playtime != frames || !SamePayloadExceptPlaytime(before, save) || _random.Calls != rng,
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
                !SamePayloadExceptPlaytime(before, save) || _random.Calls != rng ||
                _entities.EntityAdapters<IRoomEntity>().Any() ||
                _sound.ActiveMusic != SoundId.MusEssenceRoom ||
                !_runtimeState.CaptureState().Wram.SequenceEqual(AgesGameContent.CreateRuntimeState().CaptureState().Wram),
                "Prepared intro did not stop at the dormant, side-effect-free handoff boundary.");
            Read<NewGameIntroScreen>("_newGameIntroScreen")!.Dialogue.Close();
            base._Process(1.0 / 60.0);
            for (int frame = 0; frame < intro.TotalVanishFrames + 59; frame++)
                base._Process(1.0 / 60.0);
            FailIf(Read<NewGameIntroController>("_newGameIntro") is null ||
                !SamePayloadExceptPlaytime(before, save) || _random.Calls != rng,
                "Prepared gameplay entered before the original post-vanish $3c hold completed.");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            base._Process(1.0 / 60.0);
            FailIf(intro.CurrentStage != Stage.RestartGame ||
                !save.HasGlobalFlag(AgesGlobalFlag.LinkSummoned) ||
                save.HasGlobalFlag(AgesGlobalFlag.PregameIntroDone) || _random.Calls != rng,
                "Pregame state $0c did not defer initializeGame to the next update.");
            base._Process(1.0 / 60.0);
            FailIf(intro.CurrentStage != Stage.LoadingArrival || _random.Calls != rng,
                "initializeGame did not defer arrival graphics to game state $03.");
            base._Process(1.0 / 60.0);
            GD.Print($"Prepared intro handoff: {timer.Elapsed.TotalMilliseconds:F2} ms.");
            FailIf(Read<NewGameIntroController>("_newGameIntro") is not null || GameplayPrepared ||
                save.HasGlobalFlag(AgesGlobalFlag.PregameIntroDone) ||
                !save.HasGlobalFlag(AgesGlobalFlag.LinkSummoned) ||
                save.HasRoomFlag(save.RespawnGroup, save.RespawnRoom, OracleSaveData.RoomFlagVisited) ||
                save.GashaMaturity != 0 ||
                _random.Calls != rng || _player.Visible ||
                !_scene!.Visible || !_scene.InterfaceLayer.Visible || !_roomCamera.Enabled ||
                _rooms.ActiveGroup != save.RespawnGroup || _currentRoom.Id != save.RespawnRoom ||
                _newGameArrivalFadeFrames != 65,
                "Prepared handoff entered objects early or lost the source 65-update arrival fade.");
            FailIf(_runtimeState.ReadWramByte(AgesWramAddress.wScrollMode) != 1,
                "linkSummonedCutscene state $00 did not enable the room boundary pass before the arrival fade.");
            var scene = _scene;
            int handoffRng = _random.Calls;
            for (int frame = 0; frame < 10; frame++)
                base._Process(1.0 / 60.0);
            FailIf(_scene != scene || _random.Calls != handoffRng ||
                _newGameArrivalPhase != 10 || _player.Visible || save.GashaMaturity != 0,
                "After handoff, gameplay was rebuilt or advanced objects during the arrival fade.");
            StepGameplayUpdates(55 + intro.Record.SummonFrames, Vector2.Zero, batched: batched);
            FailIf(_random.Calls != rng || save.HasGlobalFlag(AgesGlobalFlag.PregameIntroDone) ||
                save.GashaMaturity != 0 || _entities.EntityAdapters<IRoomEntity>().Any(),
                "Arrival created room objects before the final wave update completed.");
            Vector2 arrivalPosition = _player.Position;
            StepGameplayUpdates(1, Vector2.Up, batched: batched);
            FailIf(_random.Calls <= rng || !save.HasGlobalFlag(AgesGlobalFlag.PregameIntroDone) ||
                save.GashaMaturity != 0 || _player.Position != arrivalPosition ||
                _player.FacingVector != Vector2I.Down ||
                save.HasRoomFlag(save.RespawnGroup, save.RespawnRoom, OracleSaveData.RoomFlagVisited),
                "Arrival initialization moved Link, recorded a visit early, or added room-scroll maturity.");
            StepGameplayUpdates(1, Vector2.Up, batched: batched);
            FailIf(save.GashaMaturity != 0 || _player.Position != arrivalPosition + Vector2.Up ||
                !save.HasRoomFlag(save.RespawnGroup, save.RespawnRoom, OracleSaveData.RoomFlagVisited),
                "cutscene00 did not resume Link and record the arrival visit after object updates.");
            StepGameplayUpdates(60, Vector2.Up, batched: batched);
            FailIf(!_transitions.IsTransitioning,
                $"New-game room $00:$8a did not start its north exit; Link is at {_player.Position}.");
            StepGameplayUpdates(64, Vector2.Zero, batched: batched);
            FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 0 || _currentRoom.Id != 0x7a,
                "New-game north exit did not finish in room $00:$7a.");
            foreach (var (movement, destination) in new[] { (Vector2.Down, 0x8a), (Vector2.Up, 0x7a) })
            {
                StepGameplayUpdates(24, movement, batched: batched);
                FailIf(!_transitions.IsTransitioning,
                    $"New-game return/repeated exit to $00:${destination:x2} did not start at {_player.Position}.");
                StepGameplayUpdates(64, Vector2.Zero, batched: batched);
                FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 0 || _currentRoom.Id != destination ||
                    _runtimeState.ReadWramByte(AgesWramAddress.wScrollMode) != 1,
                    $"New-game return/repeated exit did not finish in room $00:${destination:x2} with boundaries enabled.");
            }
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
        StepGameplayUpdates(unfinishedIntro.TotalVanishFrames + 63, Vector2.Zero, batched: true);
        FailIf(Read<NewGameIntroController>("_newGameIntro") is not null ||
            unfinished.GashaMaturity != 0 || !_scene!.Visible,
            "An unfinished preparation did not drain without entering room objects early.");
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
            !SamePayloadExceptPlaytime(cancelledBytes, cancelled) || cancelled.ReadWramByte(0xc622) != 1,
            "Cancelling partial preparation retained the intro or committed its save state.");

        // Deterministically stop between the new entity manager and event
        // controller. A time-budgeted host can also end at this boundary;
        // its old event context belongs to the scene we have already freed.
        var retiredEntities = _entities;
        var retiredEvents = _roomEvents;
        ValidateRoomEventsBeforeReset();
        Invoke("ReleaseGameplayScene", true);
        _scene = null!;
        Invoke("StartSelectedFile", 0, OracleSaveData.CreateStandardGame());
        var partial = Read<System.Collections.Generic.IEnumerator<bool>>("_gameplayPreparation")!;
        int preparationSteps = 0;
        while (ReferenceEquals(_entities, retiredEntities) && preparationSteps++ < 256 && partial.MoveNext()) { }
        FailIf(ReferenceEquals(_entities, retiredEntities) || _entities.IsDisposed ||
            !ReferenceEquals(_roomEvents, retiredEvents) || !retiredEntities.IsDisposed,
            "Partial-preparation cleanup fixture missed the new-entities/old-events boundary.");
        ReinitializeGameplayForValidation();
        GD.Print("Validated staged intro preparation and original handoff through individual and batched host updates.");
        ValidatePreparedSavedGameHandoff();
    }

    private void ValidatePreparedSavedGameHandoff()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = typeof(GameRoot);
        void Invoke(string name, params object[] args) => root.GetMethod(name, flags)!.Invoke(this, args);
        bool Loading() => (bool)root.GetField("_loadingSavedGame", flags)!.GetValue(this)!;
        void ClearScene()
        {
            ReinitializeGameplayForValidation();
            Invoke("ReleaseGameplayScene", true);
            _scene = null!;
            _random = new OracleRandom();
        }
        static OracleSaveData Save()
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);
            save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.WriteWramByte(AgesWramAddress.wLinkHealth, 0);
            return save;
        }
        void Select(OracleSaveData save)
        {
            var persist = root.GetField("_persistSaveData", flags)!;
            persist.SetValue(this, true);
            try { Invoke("StartSelectedFile", 0, save); }
            finally { persist.SetValue(this, false); }
        }

        // Compare with the original synchronous initialization order, including
        // native initializeGame's health restore and mainThreadStart's first tick.
        ClearScene();
        var expected = Save();
        expected.AdvancePlaytime();
        _sound.RestartSound();
        Invoke("InitializeGameplay", expected, true, null!, InitialRoomLoadKind.Ordinary);
        byte[] expectedSave = expected.Serialize();
        byte[] expectedWram = _runtimeState.CaptureState().Wram;
        int expectedRng = _random.Calls;
        Vector2 expectedPosition = _player.Position;
        int expectedEntities = _entities.EntityAdapters<IRoomEntity>().Count();
        int expectedMusic = _sound.ActiveMusic;
        foreach (bool batched in new[] { false, true })
        {
            ClearScene();
            var save = Save();
            Select(save);
            byte[] selected = save.Serialize();
            FailIf(!Loading() || _random.Calls != 0 || save.ReadWramByte(AgesWramAddress.wLinkHealth) != 12,
                "Saved-file selection did not restore source health before dormant preparation.");
            for (int host = 0; Loading() && host < 256; host++)
            {
                base._Process((batched ? 4 : 1) / 60.0);
                if (!Loading()) break;
                FailIf(_random.Calls != 0 || !selected.SequenceEqual(save.Serialize()),
                    "Saved-file preparation advanced playtime, save bytes or object RNG.");
                if (_scene is not null)
                    FailIf(_scene.Visible || _scene.InterfaceLayer.Visible || _roomCamera.Enabled ||
                        _scene.ProcessMode != ProcessModeEnum.Disabled,
                        "Saved-file preparation exposed or enabled a partial gameplay scene.");
            }
            FailIf(Loading() || !expectedSave.SequenceEqual(save.Serialize()) ||
                !expectedWram.SequenceEqual(_runtimeState.CaptureState().Wram) ||
                _random.Calls != expectedRng || _player.Position != expectedPosition ||
                _entities.EntityAdapters<IRoomEntity>().Count() != expectedEntities ||
                _sound.ActiveMusic != expectedMusic || !_scene!.Visible || !_roomCamera.Enabled ||
                save.ReadWramByte(AgesWramAddress.wPlaytimeCounter) != 1 ||
                !save.HasRoomFlag(0, 0x8a, OracleSaveData.RoomFlagVisited) ||
                _runtimeState.ReadWramByte(AgesWramAddress.wScrollMode) != 1,
                $"Staged saved-file entry mismatch: loading={Loading()}, save={expectedSave.SequenceEqual(save.Serialize())}, " +
                $"WRAM={expectedWram.SequenceEqual(_runtimeState.CaptureState().Wram)}, RNG={_random.Calls}/{expectedRng}, " +
                $"spawn={_player.Position}/{expectedPosition}, entities={_entities.EntityAdapters<IRoomEntity>().Count()}/{expectedEntities}, " +
                $"music=${_sound.ActiveMusic:x2}/${expectedMusic:x2}, visible={_scene!.Visible}, camera={_roomCamera.Enabled}, " +
                $"playtime={save.ReadWramByte(AgesWramAddress.wPlaytimeCounter)}, visited={save.HasRoomFlag(0, 0x8a, OracleSaveData.RoomFlagVisited)}, " +
                $"scroll=${_runtimeState.ReadWramByte(AgesWramAddress.wScrollMode):x2}.");
            int rng = _random.Calls;
            int maturity = save.GashaMaturity;
            base._Process(1.0 / 60);
            FailIf(save.ReadWramByte(AgesWramAddress.wPlaytimeCounter) != 2 ||
                _random.Calls != rng || save.GashaMaturity != maturity,
                "First post-load update repeated room entry or failed to resume playtime.");
        }

        ClearScene();
        var cancelled = Save();
        Select(cancelled);
        byte[] before = cancelled.Serialize();
        // Stop after exactly one resource step, independently of CPU speed.
        ((System.Collections.Generic.IEnumerator<bool>)root.GetField("_gameplayPreparation", flags)!.GetValue(this)!).MoveNext();
        ReinitializeGameplayForValidation();
        AdvanceGameplayPreparation();
        FailIf(Loading() || !before.SequenceEqual(cancelled.Serialize()) ||
            cancelled.HasRoomFlag(0, 0x8a, OracleSaveData.RoomFlagVisited),
            "Cancelling staged saved-file loading committed entry or retained pending work.");
        FailIf(!ReferenceEquals(RoomEventSourceData.Shared.MakuFirst,
                _roomEvents.Get<RemoteMakuFirstEssenceEvent>().Database),
            "Remote Maku definitions were rebuilt for a fresh event instance.");
        GD.Print("Validated saved-file preparation, single entry commit, resumed play, cancellation and repeated selections across host batching.");
    }

    private void ValidateDeferredGameplayAssets()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        static object? Field(object owner, string name) =>
            owner.GetType().GetField(name, fields)!.GetValue(owner);

        byte[] save = _saveData.Serialize();
        int rngCalls = _random.Calls;
        var factory = (RoomEntityFactory)Field(_entities, "_factory")!;
        PlayerSpriteLibrary sprites = _player.Sprites;
        object? preparedSmog = Field(factory.Resources, "_smogProjectileData");
        FailIf(preparedSmog is not null && !ReferenceEquals(preparedSmog,
                Field(RoomEntityResources.CreateSession(_rooms), "_smogProjectileData")),
            "Prewarmed immutable boss definitions were duplicated for a session.");
        FailIf(Field(sprites, "_swordTextureCache") is not null ||
            Field(sprites, "_seedShooterTexturesCache") is not null,
            "Gameplay initialization eagerly prepared unused boss/item assets.");

        // Same source-derived spr_sword/OAM pixels asserted by the item
        // regression, now exercised on first access after gameplay startup.
        FailIf(_player.SwordAtlasPixelHash != 0x61e9abb0e1173ec7UL,
            "First-use sword atlas changed the original OAM pixels.");
        object? sword = Field(sprites, "_swordTextureCache");
        FailIf(sword is null || _player.SwordAtlasPixelHash != 0x61e9abb0e1173ec7UL ||
            !ReferenceEquals(sword, Field(sprites, "_swordTextureCache")) ||
            Field(sprites, "_seedShooterTexturesCache") is not null,
            "Repeated sword presentation rebuilt its atlas or prepared unrelated item graphics.");

        Texture2D walking = sprites.WalkTexture;
        _player.Initialize(_playerWorld, _inventory, _player.PrecisePosition, _random);
        FailIf(!ReferenceEquals(sprites, _player.Sprites) ||
            !ReferenceEquals(sword, Field(sprites, "_swordTextureCache")) ||
            ReferenceEquals(walking, sprites.WalkTexture) ||
            _player.SwordAtlasPixelHash != 0x61e9abb0e1173ec7UL,
            "Reinitializing Link must refresh walking graphics while retaining alternate atlases for that player node.");
        var otherPlayer = new Player();
        try
        {
            FailIf(ReferenceEquals(sprites, otherPlayer.Sprites) ||
                Field(otherPlayer.Sprites, "_swordTextureCache") is not null,
                "A different player node must own a fresh, unprepared sprite library.");
        }
        // This off-tree node has no scene owner to free its native instance.
        finally { otherPlayer.Free(); }
        FailIf(!save.SequenceEqual(_saveData.Serialize()) || _random.Calls != rngCalls,
            "Deferred player graphics changed save state or consumed gameplay RNG.");

        _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(40, 40), 1));
        object? database = Field(factory.Resources, "_smogProjectileData");
        _entities.Spawn<SmogProjectilePart>(new SmogProjectileSpawn(new(72, 40), 1));
        FailIf(database is null || !ReferenceEquals(database, Field(factory.Resources, "_smogProjectileData")) ||
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
