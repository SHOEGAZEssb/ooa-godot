using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsPersistence()
    {
        foreach (bool batched in new[] { false, true })
        {
            var root = new FrontendValidationRoot();
            var app = new ApplicationValidationFixture(root);
            AddChild(root);
            try
            {
                var selector = root.InitializeGameSelect(persistSaveData: true);
                var ages = OracleSaveData.CreateStandardGame(OracleGame.Ages); ages.SetLinkName("AGES");
                root.SeedFileSlot(0, ages);
                byte[] agesDisk = root.FileSlotSnapshot(0, OracleGame.Ages);
                var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
                save.SetLinkName("SEAS"); save.SetGlobalFlag(0x21); save.SetGlobalFlag(0x0a);
                save.SetDeathRespawnPoint(0, 0xb6, 3, 2, 0x48, 0x78);
                save.SetRoomFlag(0, 0xb6, 0x50);
                save.WriteField("wLinkHealth", 0x14); save.WriteField("wLinkMaxHealth", 0x20);
                root.SeedFileSlot(0, save);
                selector.RequestPointerSelection(OracleGame.Seasons, true); root.Step(1, batched);
                root.CompleteBootLoading();
                OpenExistingFile();
                SeasonsOpeningSession session = Current();
                FailIf(session.Phase != SeasonsOpeningPhase.Complete || session.RoomId != 0xb6 ||
                    session.Memory.ReadField("wRoomStateModifier") != 3 || session.Inventory.HealthQuarters != 0x14 ||
                    session.Link.PrecisePosition != new Vector2(0x78, 0x48) || session.Link.LocalRespawnPosition != new Vector2(0x78, 0x48) ||
                    session.Link.LocalRespawnFacingVector != Vector2I.Down || session.Link.InvincibilityFrames is < -120 or >= 0,
                    $"A Seasons existing file must enter its native winter checkpoint with saved positive health, local respawn direction and non-flashing load protection: " +
                    $"phase={session.Phase}, room=${session.RoomId:x2}, season={session.Memory.ReadField("wRoomStateModifier")}, health=${session.Inventory.HealthQuarters:x2}, " +
                    $"position={session.Link.PrecisePosition}, local={session.Link.LocalRespawnPosition}/{session.Link.LocalRespawnFacingVector}, invincibility={session.Link.InvincibilityFrames}.");
                FailIf(!ObservesSave(session), "The active Seasons entity manager must observe its live save image.");
                session.Inventory.GiveTreasure(0x05, 1);
                byte[] beforeSave = Disk();
                OpenSaveMenu(); Press("move_down");
                root.NextSaveFailure = "injected Seasons store failure";
                Press("attack");
                FailIf(!Current().SaveQuitScreen.SaveErrorVisible || !Current().Menus!.Pause.IsLeased ||
                    Current().Menus!.Inventory.LastSaveError != "injected Seasons store failure" || !Disk().SequenceEqual(beforeSave),
                    "A failed Seasons Save and Continue must retain the menu/lease, show the store error and preserve the committed generation.");
                Press("attack"); // Acknowledges the error without another write.
                FailIf(root._saveWriteRequests != 1 || Current().SaveQuitScreen.SaveErrorVisible,
                    "Acknowledging the shared save error must not retry or close the menu.");
                Press("attack");
                app.Step(30, Vector2.Zero, batched: batched);
                CloseFade();
                FailIf(root._saveWriteRequests != 2 || (Committed().ReadField("wObtainedTreasureFlags") & 0x20) == 0,
                    "Retrying Seasons Save and Continue must commit through the common slot-store boundary.");

                // Walk normally after the menu, then die away from the maintained checkpoint.
                Vector2 checkpoint = new(0x78, 0x48);
                app.Step(4, Vector2.Right, batched: batched);
                FailIf(Current().Link.PrecisePosition == checkpoint,
                    "Seasons control must resume after the shared save menu closes.");
                Current().Inventory.GiveTreasure(0x28, 0x0c); // getRupeeValue index $0c is BCD $0100.
                FailIf(Current().Inventory.Rupees != 100, "The unsaved currency setup must use native rupee index $0c ($0100).");
                byte[] priorDeathDisk = Disk();
                EnterGameOver();
                int stoppedClock = Playtime(Current().Save);
                app.Step(11, Vector2.Zero, batched: batched);
                FailIf(!Current().Menus!.Inventory.SaveMenuOpen || Playtime(Current().Save) != stoppedClock || Current().Save.DeathCount != 1,
                    "Seasons game over must stop the main thread and count the death once while the shared menu fades in.");
                SeasonsOpeningSession dying = Current();
                Press("attack");
                app.Step(29, Vector2.Zero, batched: batched);
                FailIf(Current() != dying || dying.IsQueuedForDeletion() || dying.Inventory.HealthQuarters != 0 || !Disk().SequenceEqual(priorDeathDisk),
                    "Continue must retain depleted live health and the committed image through update $1d of its $1e delay.");
                app.Step(1, Vector2.Zero);
                FailIf(Current() == dying || !dying.IsQueuedForDeletion() || Current().Inventory.HealthQuarters != 0x10 ||
                    Current().Inventory.Rupees != 100 || Current().Link.PrecisePosition != checkpoint ||
                    Current().Link.InvincibilityFrames != -120 || Playtime(Current().Save) != stoppedClock + 1 ||
                    root._saveWriteRequests != 2 || !Disk().SequenceEqual(priorDeathDisk) || ObservesSave(dying) || !ObservesSave(Current()),
                    $"Continue must reconstruct the live Seasons file at the maintained checkpoint, preserve unsaved inventory, restore native half-health/protection and consume the initialization update without a disk write: " +
                    $"changed={Current() != dying}, retired={dying.IsQueuedForDeletion()}, health={Current().Inventory.HealthQuarters}, rupees={Current().Inventory.Rupees}, " +
                    $"position={Current().Link.PrecisePosition}, protection={Current().Link.InvincibilityFrames}, clock={Playtime(Current().Save)}/{stoppedClock}, writes={root._saveWriteRequests}.");

                EnterGameOver(); app.Step(11, Vector2.Zero, batched: batched);
                Press("move_down"); Press("attack");
                FailIf(Committed().ReadField("wLinkHealth") != 0 || Committed().DeathCount != 2,
                    "Seasons Save and Continue must commit the depleted image before initialization repairs live health.");
                app.Step(30, Vector2.Zero, batched: batched);
                FailIf(Current().Inventory.HealthQuarters != 0x10 || Committed().ReadField("wLinkHealth") != 0 || root._saveWriteRequests != 3,
                    "Seasons health restoration after Save and Continue must not rewrite the committed zero-health image.");

                SeasonsOpeningSession quitting = Current();
                OpenSaveMenu(); Press("move_down"); Press("move_down"); Press("attack");
                app.Step(29, Vector2.Zero, batched: batched);
                FailIf(root._seasonsOpening is null || root._mainMenu is not null,
                    "Seasons Save and Quit must keep gameplay through update $1d of the selection delay.");
                app.Step(1, Vector2.Zero);
                FailIf(root._seasonsOpening is not null || root._mainMenu is null || root._saveWriteRequests != 4 || ObservesSave(quitting),
                    "Seasons Save and Quit must release the gameplay hierarchy and return to its native title.");
                OpenExistingFile();
                FailIf(Current().Inventory.Rupees != 100 || Current().Save.DeathCount != 2 ||
                    Current().Inventory.HealthQuarters != 0x10 || Current().Link.PrecisePosition != checkpoint ||
                    Current().Memory.ReadField("wRoomStateModifier") != 3 || !root.FileSlotSnapshot(0, OracleGame.Ages).SequenceEqual(agesDisk),
                    "Reopening the explicitly saved Seasons file must preserve native checkpoint/season/inventory while leaving the Ages slot untouched.");

                SeasonsOpeningSession Current() => root._seasonsOpening ?? throw new InvalidOperationException("The Seasons gameplay owner is absent.");
                byte[] Disk() => root.FileSlotSnapshot(0, OracleGame.Seasons);
                OracleSaveData Committed()
                {
                    if (!OracleSaveData.TryDeserialize(Disk(), OracleGame.Seasons, out OracleSaveData? result))
                        throw new InvalidOperationException("The Seasons store boundary committed an invalid image.");
                    return result!;
                }
                void OpenExistingFile()
                {
                    root.Step(1, batched); root.Step(1, batched, "inventory"); root.Step(32, batched);
                    FailIf(root._mainMenu?.CurrentPage != Page.FileSelect, "Seasons title did not reach the shared file selection.");
                    root.Step(1, batched, "attack"); root.Step(1, batched);
                    root.Step(1, batched, "attack"); root.Step(32, batched);
                }
                void Press(string action) => app.Step(1, Vector2.Zero, [action], [action]);
                void OpenSaveMenu()
                {
                    app.Step(1, Vector2.Zero, ["inventory", "map"], ["inventory", "map"]);
                    for (int update = 0; update < 30 && !Current().Menus!.Inventory.SaveMenuOpen; update++) app.Step(1, Vector2.Zero);
                    FailIf(!Current().Menus!.Inventory.SaveMenuOpen, "Seasons Start+Select did not open the common save menu.");
                }
                void CloseFade()
                {
                    for (int update = 0; update < 30 && Current().Menus!.Lifecycle.IsActive; update++) app.Step(1, Vector2.Zero);
                    FailIf(Current().Menus!.Lifecycle.IsActive || Current().Menus!.Pause.IsLeased,
                        "Seasons save closing failed to release common modal ownership.");
                }
                void EnterGameOver()
                {
                    FailIf(!Current().Link.ApplyDamage(Current().Inventory.HealthQuarters), "The real Link damage handler rejected the lethal damage setup.");
                    for (int update = 0; update < 240 && !Current().Menus!.Inventory.GameOver; update++)
                        app.Step(1, Vector2.Zero);
                    FailIf(!Current().Menus!.Inventory.GameOver, "Seasons death did not request the shared game-over menu through the actual player update loop.");
                }
            }
            finally { root.Free(); }

        }

        static int Playtime(OracleSaveData save) => save.ReadField("wPlaytimeCounter") |
            save.ReadField("wPlaytimeCounter", 1) << 8 | save.ReadField("wPlaytimeCounter", 2) << 16;
        static bool ObservesSave(SeasonsOpeningSession session) =>
            typeof(OracleSaveData).GetField("Changed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session.Save) is Delegate callbacks &&
            callbacks.GetInvocationList().Any(callback => ReferenceEquals(callback.Target, session.Entities));
    }
}
