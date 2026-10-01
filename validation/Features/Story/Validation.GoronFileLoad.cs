using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronFileLoad()
    {
        // scriptHelper.s:goron_determineTextForGenericNpc, subid $0c rows
        // v$04/$05 in the past: $ff/$25/$26 and $ff/$27/$ff. Unlinked
        // TX_3127 is replaced with $ff. These are source expectations.
        foreach (bool linked in new[] { false, true })
        for (int phase = 0; phase < 3; phase++)
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            save.SetLinkedGame(linked);
            save.SetDeathRespawnPoint(2, 0xff, 0, 0, 0x64, 0x60);
            if (phase >= 1) save.SetGlobalFlag(GlobalFlag.SavedGoronElder);
            if (phase == 2) save.SetGlobalFlag(GlobalFlag.FinishedGame);
            int low4 = new[] { 0xff, 0x25, 0x26 }[phase];
            int low5 = new[] { 0xff, linked ? 0x27 : 0xff, 0xff }[phase];
            string split = RunGoronFileLoad(save, false, low4, low5);
            string batch = RunGoronFileLoad(save, true, low4, low5);
            FailIf(split != batch, $"Room 2:ff file load differs across host batching, phase {phase}, linked {linked}.");
        }
    }

    private string RunGoronFileLoad(OracleSaveData save, bool batched, int low4, int low5)
    {
        var observations = new List<string>();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            using var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                root.Initialize(save);
                // Original logo/title -> file select, using the host input loop.
                root.Step(241, batched);
                root.Step(1, batched, "inventory");
                root.Step(9, batched);
                root.Step(1, batched, "inventory");
                root.Step(32, batched);
                FailIf(root._mainMenu?.CurrentPage != Page.FileSelect,
                    "Goron file-load regression did not reach file select.");
                root.Step(1, batched, "inventory");
                root.Step(1, batched);
                FailIf(root._mainMenu?.CurrentPage != Page.TextSpeed,
                    "Selecting slot 1 did not reach the saved-file text-speed screen.");
                root.Step(1, batched, "inventory");
                // The accepting update already advances palette offset $01.
                root.Step(MainMenuController.WhiteFadeFrames - 1, batched);
                FailIf(root._mainMenu is null || root._entities is not null,
                    "Room objects appeared before the file menu's fade completed.");
                root.Step(1, batched);
                FailIf(root._mainMenu is not null || root._rooms.ActiveGroup != 2 || root._rooms.CurrentRoom.Id != 0xff,
                    "Slot 1 handoff did not resume room 2:ff.");
                var cave = root._roomEvents.Get<GoronCaveEvent>();
                var actors = cave.Actors.ToArray();
                // objects/ages: room 2:ff's ordered Goron records and positions.
                FailIf(!actors.Select(a => (a.Actor.Record.Id, a.Actor.Record.SubId,
                        a.Actor.Record.Var03, a.Actor.Position)).SequenceEqual(new[]
                    {
                        (0x66, 0x08, 0, new Vector2(0x88, 0x58)),
                        (0x66, 0x10, 0, new Vector2(0x38, 0x40)),
                        (0x66, 0x0c, 4, new Vector2(0x18, 0x68)),
                        (0x66, 0x0c, 5, new Vector2(0x38, 0x18))
                    }), "Room 2:ff file load lost its source Goron order or coordinates.");
                FailIf(actors.Length != 4 || actors.Any(a => !a.Actor.Active || a.Actor.Visible || a.Actor.IsVisibleInTree()),
                    "Room 2:ff exposed Gorons before their state-zero initialization update.");
                root.Step(1, batched);
                string Snapshot() => string.Join(";", actors.Select(a =>
                    $"{a.Actor.Record.SubId:x2}/{a.Actor.Record.Var03:x2}:{a.Actor.Active}:{a.Actor.Visible}:{a.Actor.Position}:{a.LoadedTextId:x4}:{a.CommandIndex}:{a.Counter}"));
                void CheckPopulation()
                {
                    foreach (var host in actors)
                    {
                        int low = host.Actor.Record.SubId == 0x0c
                            ? host.Actor.Record.Var03 == 4 ? low4 : low5 : -1;
                        bool expected = low != 0xff;
                        FailIf(host.Actor.Active != expected || host.Actor.Visible != expected ||
                            host.Actor.IsVisibleInTree() != expected || low >= 0 && host.LoadedTextId != (0x3100 | low),
                            $"Room 2:ff Goron ${host.Actor.Record.SubId:x2}/v${host.Actor.Record.Var03:x2} exposed the wrong file-load population (expected text ${low:x2}; active {host.Actor.Active}, visible {host.Actor.Visible}, loaded text ${host.LoadedTextId:x4}, tileset ${root._rooms.CurrentRoom.TilesetFlags:x2}, transitioning {root.IsTransitioning}, map {root.MapMenuOpen}).");
                    }
                }
                CheckPopulation();
                observations.Add(Snapshot());
                // Keep observing beyond initialization, including batched host frames.
                var application = new ApplicationValidationFixture(root);
                application.Step(8, Vector2.Zero, batched: batched, afterUpdate: CheckPopulation);
                observations.Add(Snapshot());
                cave.Cancel();
                FailIf(actors.Any(a => a.Actor.Active || a.Actor.Visible),
                    "Cancelling the Goron event retained file-loaded actors.");
                FailIf(!save.Serialize().SequenceEqual(root.FileSlotSnapshot(0)),
                    "Loading or cancelling Gorons wrote back to the isolated file slot.");
            }
            finally { root.Free(); }
        }
        return string.Join("\n", observations);
    }
}
