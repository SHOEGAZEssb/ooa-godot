using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateInitialNayruPossessionCounters()
    {
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
        foreach (bool batched in new[] { false, true })
        {
            _saveData.SetRoomFlag(0, 0x39, OracleSaveData.RoomFlag40, false);
            LoadValidationRoom(0, 0x39);
            NayruIntroEvent intro = _roomEvents.Get<NayruIntroEvent>();
            ICutsceneCommandHost host = intro;
            host.RunNativeHandler("SetupNayruPossessionScene");
            host.RunNativeHandler("SpawnGhostVeran");
            NpcCharacter nayru = intro.ActorRegistry["Nayru"];
            NpcCharacter ralph = intro.ActorRegistry["Ralph"];
            nayru.Position = new Vector2(120, 24);
            ralph.Position = new Vector2(136, 80);
            host.RunNativeHandler("BeginNayruPossessionRecovery");
            FieldInfo stage = typeof(NayruIntroEvent).GetField("_nayruStage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(intro, Enum.ToObject(stage.FieldType, 12));
            intro.CommandRunner.Start([new CutsceneWaitFramesCommand(new NayruIntroEventDatabase().Commands[0].Source, 1000)]);
            int previous = 0;
            foreach (int frame in new[] { 154, 155, 224, 225, 282, 283, 284, 352, 353, 558, 559 })
            {
                StepGameplayUpdates(frame - previous, Vector2.Zero, batched: batched);
                previous = frame;
                float expectedNayruY = 24 + Math.Clamp(frame - 154, 0, 128) / 8.0f;
                float expectedRalphY = 80 + Math.Clamp(frame - 224, 0, 128) / 8.0f;
                FailIf(nayru.Position.Y != expectedNayruY || ralph.Position.Y != expectedRalphY ||
                    _saveData.HasRoomFlag(0, 0x39, OracleSaveData.RoomFlag40) != (frame == 559),
                    $"Possession source update {frame}, batch={batched}: Nayru Y={nayru.Position.Y}/{expectedNayruY}, Ralph Y={ralph.Position.Y}/{expectedRalphY}, room40={_saveData.HasRoomFlag(0, 0x39, OracleSaveData.RoomFlag40)}.");
            }
            intro.Cancel();
        }
    }

    private void ValidateInitialNayruSingingBoundary()
    {
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(0, 0x39);
            NayruIntroEvent intro = _roomEvents.Get<NayruIntroEvent>();
            _player.WarpTo(new Vector2(0x38, 0x68), recordSafe: false);
            StepGameplayUpdates(batched ? 5 : 2, Vector2.Zero);
            typeof(NayruIntroEvent).GetMethod("BeginNayruSingingScreen", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(intro, null);
            var frozen = _entities.Entities<NpcCharacter>().Where(actor => actor.Active)
                .Select(actor => (Actor: actor, actor.Position, Frame: actor.CurrentAnimationFrame)).ToArray();
            StepGameplayUpdates(11, Vector2.Zero, batched: batched);
            int randomCalls = _entities.RandomCalls;
            FailIf(intro.CurrentStage != 10, "Nayru singing did not begin after its 11-update fade.");
            // The first replay's screen is queued for deletion until this
            // host frame ends; inspect the current event-owned screen.
            NayruSingingScreen singing = (NayruSingingScreen)typeof(NayruIntroEvent)
                .GetField("_nayruSingingScreen", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(intro)!;
            int scroll = 0;
            for (int update = 0; update < 8; update++)
            {
                StepGameplayUpdates(1, Vector2.Zero);
                if ((_entities.FrameCounter & 7) == 0) scroll++;
                FailIf(singing.ScrollX != scroll,
                    $"Nayru singing SCX at frame ${_entities.FrameCounter:x2}, update {update}, batch={batched}: expected {scroll}, got {singing.ScrollX}.");
            }
            StepGameplayUpdates(351, Vector2.Zero, batched: batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(intro.CurrentStage != 10,
                "Nayru singing accepted A with exactly $00f0 updates remaining; the source requires less.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(frozen.Any(entry => entry.Actor.Position != entry.Position || entry.Actor.CurrentAnimationFrame != entry.Frame) ||
                _entities.RandomCalls != randomCalls,
                "Nayru singing updated room actors/effects or consumed world RNG while updateAllObjects was skipped.");
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            FailIf(intro.CurrentStage != 11, "Nayru singing rejected a new A press below $00f0 remaining.");
            StepGameplayUpdates(11, Vector2.Zero, batched: batched);
            FailIf(intro.CurrentStage != 12 || !_hud.Visible,
                "Skipping Nayru's song failed to restore the scene after its outgoing fade.");
            intro.Cancel();
        }
    }

    private void ValidateInitialNayruRecovery()
    {
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(0, 0x39);
            NayruIntroEvent intro = _roomEvents.Get<NayruIntroEvent>();
            ICutsceneCommandHost host = intro;
            host.RunNativeHandler("BeginNayruAftermath");
            var database = new NayruIntroEventDatabase();
            FieldInfo stage = typeof(NayruIntroEvent).GetField("_nayruStage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(intro, Enum.ToObject(stage.FieldType, 12));
            intro.CommandRunner.Start([new CutsceneWaitFramesCommand(database.Commands[0].Source, 1000)]);
            FailIf(_currentRoom.TemporaryBackgroundPaletteBlend != 1,
                "Nayru aftermath did not reinstall PALH_99 on returning to room 0:39.");
            host.RunNativeHandler("RestoreAftermathPalette");
            StepGameplayUpdates(31, Vector2.Zero, batched: batched);
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_currentRoom.TemporaryBackgroundPaletteBlend != 0,
                "Signal $1e did not finish restoring the normal background palette in 32 updates.");
            host.RunNativeHandler("FinishAftermathRalphDeparture");
            int randomCalls = _entities.RandomCalls;
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_entities.RandomCalls != randomCalls,
                "Impa shook on the signal-$20 observation update before installing counter $3c.");
            StepGameplayUpdates(59, Vector2.Zero, batched: batched);
            NpcCharacter impa = intro.ActorRegistry["AftermathImpaCollapsed"];
            FailIf(_entities.RandomCalls != randomCalls + 59 || impa.Position.Y != 0x68 ||
                impa.Position.X is not (0x37 or 0x38),
                "Impa recovery did not consume exactly 59 shared RNG values with the source one-pixel X shake.");
            StepGameplayUpdates(121, Vector2.Zero, batched: batched);
            FailIf(host.UpdateNativeHandler("WaitForImpaRecovery", null, 0, 1, string.Empty) ||
                _entities.RandomCalls != randomCalls + 59,
                "Impa recovery completed early or consumed RNG during its zero update/script wait.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!host.UpdateNativeHandler("WaitForImpaRecovery", null, 0, 1, string.Empty),
                "Impa recovery did not finish its source wait at update 182.");
            Vector2 shakenPosition = impa.Position;
            host.RunNativeHandler("RestoreAftermathImpa");
            FailIf(intro.ActorRegistry["AftermathImpa"].Position != shakenPosition,
                "Impa recovery discarded the final source shake position.");
            intro.Cancel();
            StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_player.CutsceneControlled || _saveData.HasGlobalFlag(GlobalFlag.IntroDone),
                "Cancelling aftermath recovery retained control or set completion.");
        }
    }

    private void ValidateInitialNayruStoneChild()
    {
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(0, 0x39);
            NayruIntroEvent intro = _roomEvents.Get<NayruIntroEvent>();
            ((ICutsceneCommandHost)intro).RunNativeHandler("BeginNayruVignette2");
            FieldInfo stage = typeof(NayruIntroEvent).GetField("_nayruStage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            stage.SetValue(intro, Enum.ToObject(stage.FieldType, 12));
            intro.CommandRunner.Start([new CutsceneWaitFramesCommand(
                new NayruIntroEventDatabase().Commands[0].Source, 1000)]);
            var vignette = (NayruStoneChildVignette)typeof(NayruIntroEvent)
                .GetField("_stoneChildVignette", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(intro)!;
            NpcCharacter boy = intro.ActorRegistry["VignetteBoy"];
            NpcCharacter lady = intro.ActorRegistry["VignetteLady"];
            int previous = 0;
            // Independently counted from scripts.s: SPEED_100 moves $50/$50/$30,
            // SPEED_040 applyspeed $40, then lady SPEED_280 moves $0e/$0d.
            // Movement initialization and its zero update do not translate.
            foreach ((int frame, float x, int signal) in new[]
            {
                (2, 120f, 0), (81, 41f, 0), (82, 41f, 0),
                (91, 41f, 0), (170, 120f, 0), (180, 120f, 0),
                (227, 73f, 0), (278, 73f, 0), (279, 73f, 1),
                (368, 73f, 1), (369, 73f, 2), (370, 73f, 2),
                (433, 57.25f, 2), (434, 57.25f, 2), (464, 57.25f, 2),
                (465, 57.25f, 3), (480, 57.25f, 3), (498, 57.25f, 3),
                (516, 57.25f, 3), (576, 57.25f, 3), (596, 57.25f, 3),
                (655, 57.25f, 3), (656, 57.25f, 3)
            })
            {
                StepGameplayUpdates(frame - previous, Vector2.Zero, batched: batched);
                previous = frame;
                FailIf(boy.Position != new Vector2(x, 72) ||
                    _entities.RuntimeState.ReadWramByte(0xcfd1) != signal || vignette.Complete != (frame == 656),
                    $"Stone-child source boundary {frame}, batch={batched}: boy={boy.Position}, signal=${_entities.RuntimeState.ReadWramByte(0xcfd1):x2}, complete={vignette.Complete}.");
                // checkmemoryeq yields on its successful update before setspeed.
                Vector2 expectedLady = frame < 480 ? new(104, 40) : frame < 498 ? new(104, 72.5f) : new(74, 72.5f);
                FailIf(lady.Position != expectedLady,
                    $"Old lady $3d:$01 boundary {frame}: expected {expectedLady}, got {lady.Position}.");
                if (frame >= 369)
                {
                    Color[] palette = (Color[])typeof(NpcCharacter).GetField("_paletteOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(boy)!;
                    FailIf(!palette.SequenceEqual(new NayruIntroEventDatabase().StoneSpritePalette),
                        "The boy did not retain stone palette $06 through script fallthrough and lady recovery.");
                }
            }
            intro.Cancel();
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_player.CutsceneControlled || intro.ActorRegistry.Count != 0 || _saveData.HasGlobalFlag(GlobalFlag.IntroDone),
                "Cancelling the stone-child vignette retained actors/control or marked the intro complete.");
        }
    }

    private void ValidateInitialNayruBearReplay()
    {
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
        _saveData.SetRoomFlag(0, 0x39, OracleSaveData.RoomFlag80);
        foreach (bool batched in new[] { false, true })
        {
            LoadValidationRoom(0, 0x39);
            NayruIntroEvent intro = _roomEvents.Get<NayruIntroEvent>();
            NpcCharacter bear = intro.ActorRegistry["Bear"];
            _player.WarpTo(new Vector2(0x58, 0x48), recordSafe: false);
            StepGameplayUpdates(24, Vector2.Up, ["move_up"], batched: batched);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != "Sit here and\nlisten. How\ncharming..." ||
                    bear.Position != new Vector2(0x58, 0x28) || intro.BlocksGameplay,
                    $"Room 0:39 bear $5d:$00 did not retain @alreadyMovedDown/TX_5703 on repeat {repeat}, batch={batched}.");
                _dialogue.Close();
                StepGameplayUpdates(110, Vector2.Zero, batched: batched);
                FailIf(bear.Position != new Vector2(0x58, 0x28) || _player.CutsceneControlled,
                    "Replaying the bear conversation moved him again or retained cutscene control.");
            }
        }
    }

    private void ValidateInitialNayruObjectArithmetic()
    {
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
        LoadValidationRoom(0, 0x39);
        NayruIntroEvent intro = _roomEvents.Get<NayruIntroEvent>();
        ICutsceneCommandHost host = intro;
        host.RunNativeHandler("SetupNayruPossessionScene");
        host.RunNativeHandler("SpawnGhostVeran");
        NpcCharacter ghost = intro.ActorRegistry["GhostVeran"];
        ghost.Position = new Vector2(100, 100);
        // speedTable SPEED_200/$1c = -$016a on both axes. The helper
        // decrements var38=$11 first, then applies that vector twice.
        for (int update = 0; update < 17; update++)
        {
            bool done = host.UpdateNativeHandler("ObjectMovement", new CutsceneActorId("GhostVeran"),
                update, 17, "80,28,-1,2,1,0");
            int moves = Math.Min(update + 1, 16);
            Vector2 expected = new(100 - moves * 0x16a * 2 / 256.0f, 100 - moves * 0x16a * 2 / 256.0f);
            FailIf(ghost.Position != expected || done != (update == 16),
                $"Ghost Veran $3e:$00 helper update {update}: {ghost.Position}, expected {expected}; zero must not move.");
        }
        ghost.Position = new Vector2(100, 100);
        for (int update = 0; update < 35; update++)
        {
            bool done = host.UpdateNativeHandler("GhostFlight", new CutsceneActorId("GhostVeran"),
                update, 35, "80,28,-1,2,1,0");
            int moves = Math.Min((update + 1) / 2, 16);
            Vector2 expected = new(100 - moves * 0x16a * 2 / 256.0f, 100 - moves * 0x16a * 2 / 256.0f);
            FailIf(ghost.Position != expected || done != (update == 34),
                $"Copied ghost script lost wait 1 / in-buffer jump cadence at update {update}.");
        }
        host.RunNativeHandler("BeginNayruAftermath");
        NpcCharacter ralph = intro.ActorRegistry["AftermathRalph"];
        ralph.Position = new Vector2(100, 100);
        foreach ((int speed, int counter, int expectedFixed) in new[] { (5, 0x41, 92 * 256), (10, 0x25, 83 * 256) })
        {
            for (int update = 0; update <= counter; update++)
                host.UpdateNativeHandler("ObjectMovement", new CutsceneActorId("AftermathRalph"),
                    update, counter + 1, $"{speed},24,-1,1,1,1");
            FailIf(ralph.Position.X * 256 != expectedFixed,
                $"Ralph $37:$02 lost SPEED_020/$41 then SPEED_040/$25: X={ralph.Position.X}.");
        }
        OracleRandom replay = new();
        replay.RestoreState(_random.CaptureState());
        int randomCalls = _entities.RandomCalls;
        host.RunNativeHandler("BeginNayruVignette1");
        var monkeys = (System.Collections.Generic.List<NayruVignetteMonkeyState>)typeof(NayruIntroEvent)
            .GetField("_nayruVignetteMonkeys", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(intro)!;
        FailIf(_entities.RandomCalls != randomCalls + 20 ||
            !monkeys.Select(monkey => monkey.Record.Index).SequenceEqual(new[] { 0, 9, 8, 7, 6, 5, 4, 3, 2, 1 }),
            "Monkey cutscene lost parent/descending-child object order or two RNG calls per initializer.");
        foreach (NayruVignetteMonkeyState monkey in monkeys)
        {
            int delay = replay.Next().Value & 0x0f;
            int[] speeds = [-0x80, -0xa0, -0x70, -0x90];
            int speed = speeds[replay.Next().Value & 3];
            FailIf(monkey.StartupDelay != (delay == 0 ? 256 : delay) || monkey.JumpSpeedZ != speed,
                $"Monkey var03=${monkey.Record.Index:x2} did not receive its ordered source RNG bytes.");
        }
        intro.Cancel();
    }

    private void ValidateInitialNayruLinkedGift()
    {
        _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        _saveData.SetLinkedGame(true);
        _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false);
        LoadValidationRoom(0, 0x39);
        NayruIntroEvent intro = _roomEvents.Get<NayruIntroEvent>();
        ICutsceneCommandHost host = intro;
        host.RunNativeHandler("BeginNayruAftermath");
        host.RunNativeHandler("RestoreAftermathImpa");
        host.ShowText(0x0112, string.Empty);
        FailIf(!DialogueBox.PlainText(new NayruIntroEventDatabase().Text(0x0113).Message).Contains("hero of\nHolodrum.", StringComparison.Ordinal) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(new NayruIntroEventDatabase().Text(0x0113).Message),
            "Linked Impa aftermath did not select TX_0113.");
        _dialogue.Close();
        host.ShowText(0x0115, string.Empty);
        FailIf(!DialogueBox.PlainText(new NayruIntroEventDatabase().Text(0x0116).Message).EndsWith("shield to me.\nPlease use it.", StringComparison.Ordinal) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(new NayruIntroEventDatabase().Text(0x0116).Message),
            "Linked Impa aftermath did not select TX_0116.");
        _dialogue.Close();
        var commands = new NayruIntroEventDatabase().Commands;
        int first = commands.ToList().FindIndex(command => command is CutsceneNativeYieldCommand { Handler: "BeginNayruSwordGift" });
        intro.CommandRunner.Start(commands, first);
        // Arrange the already-running aftermath at its gift instruction, then
        // exercise inventory/pose/dialogue/cleanup through the application loop.
        FieldInfo stage = typeof(NayruIntroEvent).GetField("_nayruStage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        stage.SetValue(intro, Enum.ToObject(stage.FieldType, 12));
        _player.BeginCutsceneControl(owner: intro);
        StepGameplayUpdates(3, Vector2.Zero, batched: true);
        FailIf(!_inventory.HasTreasure(TreasureId.Shield) || _inventory.HasTreasure(TreasureId.Sword) ||
            !_player.IsHoldingItemOneHand || !_dialogue.IsOpen ||
            !_dialogue.CurrentMessage.Contains("Wooden Shield", StringComparison.Ordinal),
            "Linked impaScript1 did not grant TREASURE_SHIELD $01:$00 with its item pose and TX_001f.");
        intro.Cancel();
        _dialogue.Close();
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(_player.IsHoldingItemOneHand || _player.CutsceneControlled ||
            !_inventory.HasTreasure(TreasureId.Shield) || _saveData.HasGlobalFlag(GlobalFlag.IntroDone),
            "Cancelling the linked gift failed to clean up control/pose or changed persistent grant/completion.");
    }
}
