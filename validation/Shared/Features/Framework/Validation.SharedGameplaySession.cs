
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedGameplaySession()
    {
        DebugSavestateData? previousState = null;
        foreach (OracleGame game in new[] { OracleGame.Ages,OracleGame.Seasons })
        foreach (bool batched in new[] { false,true })
        {
            var root = new FrontendValidationRoot();
            AddChild(root);
            FailIf(root.MapMenuOpen || root.InventoryMenuOpen || root.DebugFlagMenuOpen || root.IsTransitioning || root.DialogueOpen,
                "An unbound application must report all gameplay menus closed.");
            try
            {
                var save = OracleSaveData.CreateStandardGame(game);
                save.SetLinkName("LINK"); save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
                save.SetDeathRespawnPoint(0,game == OracleGame.Ages ? 0x45 : 0xb6,3,2,0x48,0x78);
                if (game == OracleGame.Ages)
                {
                    root.Initialize(save);
                    root.Step(241,batched); root.Step(1,batched,"inventory"); root.Step(9,batched);
                    root.Step(1,batched,"inventory"); root.Step(32,batched);
                    root.Step(1,batched,"inventory"); root.Step(1,batched);
                    root.Step(1,batched,"inventory"); root.Step(32,batched);
                    root.CompleteSavedGameLoading(batched);
                }
                else
                {
                    save.SetRoomFlag(0,0xb6,0x50);
                    var selector = root.InitializeGameSelect(); root.SeedFileSlot(0,save);
                    selector.RequestPointerSelection(game,true); root.Step(1,batched); root.CompleteBootLoading();
                    root.Step(1,batched); root.Step(1,batched,"inventory"); root.Step(32,batched);
                    root.Step(1,batched,"attack"); root.Step(1,batched); root.Step(1,batched,"attack"); root.Step(32,batched);
                }
                GameplaySession engine = root._gameplaySession ?? throw new InvalidOperationException($"{game}: real file entry omitted the shared gameplay session.");
                var native = root._seasonsOpening;
                RoomSession rooms = native?.Rooms ?? root._rooms;
                RoomEntityManager entities = native?.Entities ?? root._entities;
                Player player = native?.Link ?? root._player;
                StatusBarController status = native?.StatusBar ?? root._statusBar;
                var app = new ApplicationValidationFixture(root);
                FailIf(!engine.Active || !ReferenceEquals(engine.Menus,root._gameplayMenus) ||
                    native is not null && !ReferenceEquals(native.Engine,engine),
                    $"{game}: application, menus and native content must use one actual engine session.");
                var foreign = new InventoryState(TreasureDatabase.For(rooms.AssetRoot),OracleSaveData.CreateStandardGame(game));
                var owner = native is null ? (object)root : native;
                var unusedBindings = new GameplayMenuBindings(() => true,() => true,
                    (_,_) => { },() => SaveResult.Succeeded,() => { },() => { },() => { },() => true);
                bool rejected = false;
                try
                {
                    _ = new GameplaySession(root._scene,rooms,entities,foreign,root._sound,unusedBindings,
                        (IGameplayForegroundPhases)owner,(IGameplayObjectPhases)owner,() => false);
                }
                catch (ArgumentException) { rejected = true; }
                FailIf(!rejected || !engine.Active,$"{game}: a same-game foreign inventory owner must fail before disturbing the active session.");
                FailIf(!OracleSaveData.TryDeserialize(rooms.SaveData.Serialize(),game,out var splitSave),
                    "The isolated split-owner fixture requires a valid native save clone.");
                var splitInventory = new InventoryState(TreasureDatabase.For(rooms.AssetRoot),
                    splitSave!,runtimeState:rooms.RuntimeState);
                rejected = false;
                try
                {
                    _ = new GameplaySession(root._scene,rooms,entities,splitInventory,root._sound,unusedBindings,
                        (IGameplayForegroundPhases)owner,(IGameplayObjectPhases)owner,() => false);
                }
                catch (ArgumentException) { rejected = true; }
                FailIf(!rejected || !engine.Active,$"{game}: sharing runtime memory must not admit a foreign inventory/save owner.");
                CheckSharedObjectSpawner(root,rooms,entities,player,batched);
                previousState = CheckSharedSavestates(root,batched,previousState);
                engine = root._gameplaySession!;
                native = root._seasonsOpening;
                rooms = engine.Rooms; entities = native?.Entities ?? root._entities;
                player = root._scene.Player; status = engine.StatusBar;
                CheckSharedEnemyCore(root,batched);
                CheckSharedKeese(root,batched);
                CheckSharedRope(root,batched);
                CheckSharedGhini(root,batched);
                CheckSharedBeamos(root,batched);
                CheckSharedOctorok(root,batched);
                CheckSharedArchers(root,batched);
                CheckSharedPlacement(root,batched);
                CheckSharedCompanionMechanics(root);
                CheckSharedCompanionVisuals(root);
                CheckSharedCompanionEffects(root,batched);
                CheckSharedOpeningEffects(root,batched);
                CheckSharedDimitriBody(root,batched);
                CheckSharedRespawnInitialization(root,batched);
                CheckSharedForcedStates(root,batched);
                CheckSharedBossEntry(root,batched);
                CheckSharedSpinner(root,batched);
                CheckSharedMooshBody(root,batched);
                CheckSharedCompanionDebugGrants(root,batched);
                foreach(int companionId in new[]{0x0c,0x0d})CheckSharedCompanionEntrance(root,batched,companionId);
                CheckSharedRememberedInitialization(root,batched);
                CheckSharedMapleInitialization(root,batched);
                CheckSharedStaticObjects(root);
                CheckStrangeForceInitialization(root,batched);
                CheckSharedTileInteractionComposition(root,batched);
                CheckSharedPushContacts(root,batched);
                CheckSharedKeyContacts(root,batched);
                CheckSubrosiaKeyDoor(root,batched);
                CheckSeasonsOverworldKeyholes(root,batched);
                CheckSeasonsGhiniGraves(root,batched);
                CheckSharedDiggingEnemies(root,batched);
                CheckSharedBombs(root,batched);
                CheckSharedBombchus(root,batched);
                CheckSharedFairyFlight(root,batched);
                CheckSharedBoomerang(root,batched);
                CheckSeasonsMooshInitialization(root,batched);
                CheckSeasonsRickyInitialization(root,batched);
                CheckSharedCompanionEntrance(root,batched,0x0b);
                CheckSharedParentMenuButtons(root,batched);
                CheckSharedParentItem5(root,batched);
                CheckSharedFlute(root,batched);
                CheckSharedShieldParentRestore(root,batched);
                CheckSharedTerrainJumps(root,batched);
                CheckSharedActiveTiles(root,batched);
                CheckSharedFloorBreaking(root,batched);
                CheckSharedTransformations(root,batched);
                CheckSharedSideScrollRuntime(root,batched);
                CheckSharedSideScrollBubbles(root,batched);
                CheckSharedForcedTransitions(root,batched);
                CheckSharedLedgeScroll(root,batched);
                CheckSharedBracelet(root,batched);
                CheckSharedShovel(root,batched);
                CheckSharedSeedData(root);
                CheckSharedSeedFactory(root,batched);
                CheckSharedSatchel(root,batched);
                CheckSharedBurningEnemy(root,batched);
                CheckSharedRoomInfo(root,batched);
                CheckSharedTransitions(root,batched);
                CheckSharedWarpDispatch(root,batched);
                CheckSharedHoleWarp(root,batched);
                CheckSharedWarpState(root,batched);
                CheckSharedScrollLifetimes(root,batched);
                CheckSharedWalkingAnimation(root,batched);
                CheckSharedParentAnimation(root,batched);
                CheckSharedHudCurrency(root,batched);
                CheckSharedHudHealth(root,batched);
                CheckSharedCompanionReconstruction(root,batched);
                engine = root._gameplaySession!; native = root._seasonsOpening;
                rooms = engine.Rooms; entities = native?.Entities ?? root._entities;
                player = root._scene.Player; status = engine.StatusBar;
                entities.Clear();
                var inventory = player.Inventory;
                inventory.SetScriptedHealth(inventory.MaxHealthQuarters);
                status.SynchronizeHealth(); status.SynchronizeRupees();
                Vector2? dry = null;
                for (int y = 24; y < 104 && dry is null; y += 16)
                for (int x = 24; x < 136; x += 16)
                    if (!player.World.Collides(new(x,y)) && player.World.GetActiveTerrain(new(x,y)).Terrain.Hazard == HazardType.None)
                    { dry = new(x,y); break; }
                FailIf(dry is null,$"{game}: session fixture needs a native dry floor.");
                player.WarpTo(dry!.Value);
                var audit = root._sound.AttachPlayRequestAudit();
                int initialRupees = inventory.Rupees, calls = 0;
                entities.AddEntity(new ItemPhaseValidationEntity(() =>
                {
                    FailIf(rooms.RuntimeState.FrameCounter != rooms.SaveData.ReadField("wPlaytimeCounter") ||
                        status.DisplayedRupees != initialRupees + Math.Min(calls,3),
                        $"{game}: clock publication must precede objects, and native status must follow them exactly once.");
                    if (calls++ == 0) inventory.AddRupees(3);
                }));
                app.Step(3,Vector2.Zero,batched:batched);
                FailIf(calls != 3 || status.DisplayedRupees != initialRupees + 3 || audit.RequestsFor(SoundId.SndRupee) != 3,
                    $"{game}: shared foreground lost native object/status/cue order.");
                app.Step(1,Vector2.Zero,held:["debug_flags"],pressed:["debug_flags"],batched:batched);
                int frozenCalls = calls, start = rooms.RuntimeState.FrameCounter;
                app.Step(4,Vector2.Right,batched:batched);
                FailIf(!engine.Menus.Flags.IsActive || calls != frozenCalls || rooms.RuntimeState.FrameCounter != ((start + 4) & 0xff),
                    $"{game}: shared debug pause must freeze objects while the main thread keeps running; " +
                    $"flags={engine.Menus.Flags.IsActive}, calls={calls}/{frozenCalls}, clock={rooms.RuntimeState.FrameCounter}/{start}, " +
                    $"objects=${rooms.RuntimeState.ReadField("wDisabledObjects"):x2}, menu=${rooms.RuntimeState.ReadField("wMenuDisabled"):x2}, " +
                    $"events={root._roomEvents?.Active}, private={player.CutsceneControlled}.");
                app.Step(1,Vector2.Zero,held:["debug_flags"],pressed:["debug_flags"],batched:batched);
                FailIf(engine.Menus.Flags.IsActive || calls <= frozenCalls,$"{game}: releasing debug ownership must resume the same object dispatcher.");
                CheckSharedScriptInput(root, batched);
                CheckSharedKidNameMenu(root, batched);
                CheckSharedGameKeys(root, batched);
                CheckSharedCompanionReturns(root);
                if (game == OracleGame.Ages && !batched)
                {
                    FailIf(!engine.Menus.KidName.Open(), "The teardown fixture requires MENU_KIDNAME ownership.");
                    app.Step(22, Vector2.Zero, batched: batched);
                    FailIf(engine.Menus.KidName.Screen is null, "The teardown fixture requires the live child-name screen.");
                }
                else
                {
                    string retiringMenu = game == OracleGame.Seasons && batched ? "debug_object_spawner" : batched ? "inventory" : "debug_flags";
                    app.Step(1,Vector2.Zero,held:[retiringMenu],pressed:[retiringMenu],batched:batched);
                }
                FailIf(!engine.Menus.Pause.IsLeased,$"{game}: teardown fixture must retain a real modal lease.");
                // Scene retirement must dispose the engine even when teardown
                // bypasses the normal title/quit callback.
                root._scene.Dialogue.ShowMessage("Retirement dialogue input.",104);
                FailIf(!root.DialogueOpen,$"{game}: the active application must observe its live dialogue owner.");
                Action retiredGameOver=engine.Menus.BeginGameOver;
                var retiredInteractions=engine.Interactions;
                var retiredBlocks=entities.RequiredReservedPushBlock;
                var retiredKeys=entities.ReservedKeyDoor!;
                var retiredKeyholes=engine.Keyholes;
                root._scene.Free();
                FailIf(engine.Active || engine.Menus.Pause.IsLeased || engine.Menus.Lifecycle.IsActive || engine.Menus.Flags.IsActive || engine.Menus.Spawner.IsActive ||
                    engine.Menus.KidName.IsActive || engine.Menus.KidName.Screen is not null,
                    $"{game}: freeing the native scene must retire gameplay and release its modal owners.");
                FailIf(root.MapMenuOpen || root.InventoryMenuOpen || root.DebugFlagMenuOpen || root.IsTransitioning || root.DialogueOpen,
                    $"{game}: retired gameplay cannot publish menu, transition or dialogue activity through application accessors.");
                CheckRetiredMenuCallbacks(engine,root._sound,audit,retiredGameOver);
                byte pushClock=rooms.TilePushCounter;
                retiredBlocks.UpdatePushAttempt(Vector2.Zero,Vector2I.Up,Vector2.Up);
                retiredBlocks.Advance(1.0/60.0,player);
                retiredKeys.UpdatePushAttempt(Vector2.Zero,Vector2I.Up,Vector2.Up);
                retiredKeys.Advance(1.0/60.0,player);
                retiredKeyholes.UpdatePushAttempt(Vector2.Zero,Vector2I.Up,Vector2.Up);
                FailIf(rooms.TilePushCounter!=pushClock,$"{game}: retired tile owners must not publish contact writes.");
                retiredInteractions.Update(1.0/60.0,player);
                FailIf(retiredInteractions.DialogueOpen || retiredInteractions.TryInteract(player) ||
                    retiredInteractions.TrySecondaryInteract(player) || entities.TextActiveSource(),
                    $"{game}: a retired common interaction service must not retain scene callbacks or process input.");
                entities.OnRoomEntityDialogueRequested(0x510d,"Retired interaction callback.",Vector2.Zero);
                int stoppedRequests = audit.Requests.Count, stoppedMusic = root._sound.NativeActiveMusic,
                    stoppedRoomMusic = root._sound.NativeRoomMusic;
                entities.RequestSound(SoundId.SndRumble);
                entities.OnNativeSoundRequested(SoundId.SndJump);
                entities.SetNativeActiveMusic(0x11);
                entities.RequestNativeMusicSelection(0x12, 0x13);
                entities.OnRoomMusicRequested(0, game == OracleGame.Ages ? 0x45 : 0xb6);
                FailIf(audit.Requests.Count != stoppedRequests || root._sound.NativeActiveMusic != stoppedMusic ||
                    root._sound.NativeRoomMusic != stoppedRoomMusic,
                    $"{game}: retired entity callbacks must not change application audio after scene disposal.");
                foreach (Action access in new Action[]
                {
                    () => { _ = entities.NativeActiveMusicSource(); },
                    () => { _ = entities.NativeSoundAllocationAliasSource(0xc03f); },
                    () => entities.WriteSoundAllocationAlias(0xc03f, 0x55),
                    () => entities.WriteNativeChannelVolume(0, 0x55)
                })
                {
                    bool detached = false;
                    try { access(); }
                    catch (InvalidOperationException error) when (error.Message.Contains("sound", StringComparison.OrdinalIgnoreCase))
                    { detached = true; }
                    FailIf(!detached, $"{game}: retired native audio reads/writes must have no sound owner.");
                }
                int retiredCalls = calls, clock = rooms.RuntimeState.FrameCounter, rng = entities.Random.Calls;
                var audio = root._sound.Apu.Clocks;
                app.Step(3,Vector2.Right,batched:batched);
                FailIf(calls != retiredCalls || rooms.RuntimeState.FrameCounter != clock || entities.Random.Calls != rng || root._sound.Apu.Clocks <= audio,
                    $"{game}: disposal must stop the shared gameplay owner while application audio continues.");
            }
            finally { root.Free(); }
        }
    }
}
