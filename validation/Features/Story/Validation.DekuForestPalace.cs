using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDekuForestPalaceCutscene()
    {
        const int group = 1;
        const int mysterySeeds = 0x24;
        DekuForestPalaceEvent palace = _roomEvents.DekuForestPalace;
        var database = new DekuForestPalaceEventDatabase();
        DekuForestPalaceEventRecord record = database.Record;
        CutsceneShowTextCommand[] palaceTextCommands =
        [
            .. database.EntranceCommands.OfType<CutsceneShowTextCommand>(),
            .. database.RewardGuardCommands.OfType<CutsceneShowTextCommand>(),
            .. database.EscortGuardCommands.OfType<CutsceneShowTextCommand>(),
            .. database.AmbiCommands.OfType<CutsceneShowTextCommand>(),
            .. database.NayruCommands.OfType<CutsceneShowTextCommand>(),
            .. database.ExitGuardCommands.OfType<CutsceneShowTextCommand>()
        ];
        var expectedCommandPositions = new Dictionary<int, int?>
        {
            [0x5904] = null,
            [0x5905] = 0,
            [0x5906] = 0,
            [0x5907] = 0,
            [0x5908] = 2,
            [0x590c] = 0,
            [0x1300] = 2,
            [0x1301] = 2,
            [0x1302] = 2,
            [0x1303] = 2,
            [0x1304] = 2,
            [0x1305] = 2,
            [0x1306] = 2,
            [0x1d01] = 2,
            [0x1d02] = 2,
            [0x1d03] = 1
        };
        FailIf(
            palaceTextCommands.Length != expectedCommandPositions.Count ||
            palaceTextCommands.Any(command =>
                !expectedCommandPositions.TryGetValue(
                    command.TextId, out int? expectedPosition) ||
                command.TextboxPosition != expectedPosition),
            "The imported palace showtext rows did not retain the exact " +
            "per-message automatic/$00/$01/$02 textbox positions. " +
            $"actual={string.Join(',', palaceTextCommands.Select(command =>
                $"{command.TextId:x4}:" +
                $"{command.TextboxPosition?.ToString() ?? "auto"}"))}");
        Dictionary<string, CutsceneShowTextCommand> palaceTextByMessage =
            palaceTextCommands.ToDictionary(
                command => DialogueBox.PlainText(command.Message),
                StringComparer.Ordinal);
        var observedDialoguePositions =
            new List<(int TextId, int Y, int Flags)>();
        byte[] originalSaveImage = _saveData.Serialize();

        _saveData.SetGlobalFlag(record.EntranceFlag, value: false);
        _saveData.SetGlobalFlag(record.CompletionFlag, value: false);
        _inventory.GiveTreasure(mysterySeeds, 0x20);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        LoadValidationRoom(group, record.EntranceRoom);
        // The room $1:$81 soldier's hardcoded destination is packed position
        // $34. Exercise the palace sequence from that retail handoff rather
        // than the validation room's generic spawn point.
        _player.WarpTo(new Vector2(0x48, 0x38), recordSafe: false);

        FailIf(
            palace.Stage != DekuForestPalaceStage.Entrance ||
            !palace.BlocksGameplay ||
            !palace.MenusDisabled ||
            _entities.Entities<NpcCharacter>().Count(npc =>
                npc.Record.Id == 0x40 &&
                npc.Record.SubId is 0x02 or 0x09 or 0x0b) != 3,
            "Room 1:46 did not begin the three-soldier Mystery Seeds escort.");

        var enteredRooms = new List<int> { _rooms.CurrentRoom.Id };
        var messages = new List<string>();
        int previousRoom = _rooms.CurrentRoom.Id;
        int scrollingUpdates = 0;
        int startedScrolls = 0;
        int completedScrolls = 0;
        int scrollTraceCount = 0;
        Vector2 scrollLinkStart = Vector2.Zero;
        Vector2 scrollGuardStart = Vector2.Zero;
        NpcCharacter? scrollingGuard = null;
        NpcCharacter? outgoingScrollingGuard = null;
        bool sawNormalLinkEscortSpeed = false;
        bool sawStairsLinkEscortSpeed = false;
        bool sawWrappedEscortEdge = false;
        int frames = 0;
        for (; frames < 9000; frames++)
        {
            if (_dialogue.IsOpen)
            {
                string message =
                    DialogueBox.PlainText(_dialogue.CurrentMessage);
                messages.Add(message);
                if (palaceTextByMessage.TryGetValue(
                        message, out CutsceneShowTextCommand? command))
                {
                    observedDialoguePositions.Add((
                        command.TextId,
                        Mathf.RoundToInt(_dialogue.Position.Y),
                        _dialogue.TextboxFlagsForValidation));
                }
                else if (
                    message.StartsWith("You got ten", StringComparison.Ordinal) &&
                    message.Contains("Bombs", StringComparison.Ordinal))
                {
                    observedDialoguePositions.Add((
                        0x004d,
                        Mathf.RoundToInt(_dialogue.Position.Y),
                        _dialogue.TextboxFlagsForValidation));
                }
                else
                {
                    FailIf(
                        true,
                        "The palace sequence opened an unrecognized dialogue " +
                        $"while auditing textbox positions: '{message}'.");
                }
                _dialogue.Close();
            }

            if (IsTransitioning)
            {
                int traceCountBefore = trace.Entries.Count;
                FailIf(
                    Fixed(_player.PrecisePosition.X) !=
                        Fixed(scrollLinkStart.X) ||
                    scrollingGuard is null ||
                    scrollingGuard.Position != scrollGuardStart ||
                    traceCountBefore != scrollTraceCount,
                    "The palace scroll changed Link's orthogonal coordinate, " +
                    "the destination escort soldier, or an event command before " +
                    "the camera handoff had completed.");

                UpdateScrollingTransition(1.0 / 60.0);
                Vector2 linkAfterTransitionUpdate = _player.PrecisePosition;
                bool scrollStillActive = IsTransitioning;
                _entities.Update(1.0 / 60.0, _player);
                _roomEvents.Update(1.0 / 60.0);
                _sound.Tick();
                scrollingUpdates++;

                if (scrollStillActive)
                {
                    FailIf(
                        Fixed(_player.PrecisePosition.X) !=
                            Fixed(scrollLinkStart.X) ||
                        scrollingGuard is null ||
                        scrollingGuard.Position != scrollGuardStart ||
                        outgoingScrollingGuard is not null &&
                        (!outgoingScrollingGuard.Active ||
                         !outgoingScrollingGuard.Visible ||
                         !_entities.OutgoingEntities<NpcCharacter>()
                             .Contains(outgoingScrollingGuard)) ||
                        trace.Entries.Count != scrollTraceCount,
                        "The palace scroll advanced Link's orthogonal coordinate, " +
                        "hid the frozen outgoing escort, changed the destination " +
                        "escort soldier, or advanced an event command while " +
                        "ordinary room state should have remained frozen.");
                }

                if (!scrollStillActive)
                {
                    FailIf(
                        scrollingUpdates != _transitions.ScrollFrames ||
                        Fixed(linkAfterTransitionUpdate.Y) != 0x7684,
                        "The palace north scroll did not retain the original " +
                        "32-update camera handoff and Link destination position " +
                        "$76.$84.");
                    completedScrolls++;
                    scrollingGuard = null;
                    outgoingScrollingGuard = null;
                }
            }
            else
            {
                int roomBefore = _rooms.CurrentRoom.Id;
                DekuForestPalaceStage stageBefore = palace.Stage;
                Vector2 playerBefore = _player.PrecisePosition;
                TerrainType terrainBefore =
                    _terrain.GetActiveTerrain(_player.Position).Terrain.Type;
                StepRoomEventFrames(1);

                if (stageBefore == DekuForestPalaceStage.Corridor &&
                    !IsTransitioning &&
                    _rooms.CurrentRoom.Id == roomBefore)
                {
                    int expectedSpeed = terrainBefore switch
                    {
                        TerrainType.Grass or TerrainType.Puddle =>
                            record.NormalSpeed * 3 / 4,
                        TerrainType.Stairs or TerrainType.Vines =>
                            record.SlowSpeed,
                        _ => record.NormalSpeed
                    };
                    int expectedDelta = OracleObjectMovement.Shared
                        .Velocity(expectedSpeed, angle: 0x00)
                        .YFixed;
                    int actualDelta =
                        Fixed(_player.PrecisePosition.Y) -
                        Fixed(playerBefore.Y);
                    FailIf(
                        actualDelta != expectedDelta,
                        "Link's palace simulated input did not use the exact " +
                        $"terrain-selected object speed. room={roomBefore:x2}, " +
                        $"terrain={terrainBefore}, expected={expectedDelta}, " +
                        $"actual={actualDelta}.");
                    if (terrainBefore is TerrainType.Stairs or TerrainType.Vines)
                        sawStairsLinkEscortSpeed = true;
                    else if (terrainBefore is not
                        (TerrainType.Grass or TerrainType.Puddle))
                    {
                        sawNormalLinkEscortSpeed = true;
                    }

                    NpcCharacter corridorGuard = _entities.Entities<NpcCharacter>()
                        .Single(npc =>
                            npc.Record.Id == 0x40 &&
                            npc.Record.SubId == 0x05);
                    Vector2 guardScreen =
                        OracleObjectMath.NormalizeSourceScreenPosition(
                            _transitions.WorldToGameplayScreen(
                                corridorGuard.Position));
                    if (guardScreen.Y is >= -7 and < 0)
                    {
                        sawWrappedEscortEdge = true;
                        FailIf(
                            !corridorGuard.Active ||
                            !corridorGuard.Visible ||
                            corridorGuard.SourceOamWrapOffset.Y != -256,
                            "soldierSubid05 did not retain his partially visible " +
                            "OAM rows while yh wrapped through $ff-$f9. " +
                            $"room={roomBefore:x2}, position=" +
                            $"{corridorGuard.Position}, screen={guardScreen}, " +
                            $"drawOffset={corridorGuard.SourceOamWrapOffset}, " +
                            $"active={corridorGuard.Active}, " +
                            $"visible={corridorGuard.Visible}.");
                    }
                }

                if (IsTransitioning)
                {
                    startedScrolls++;
                    scrollingUpdates = 0;
                    scrollTraceCount = trace.Entries.Count;
                    scrollLinkStart = _player.PrecisePosition;
                    int escortSubId =
                        _rooms.CurrentRoom.Id == record.ThroneRoom ? 0x06 : 0x05;
                    scrollingGuard = _entities.Entities<NpcCharacter>()
                        .SingleOrDefault(npc =>
                            npc.Active &&
                            npc.Record.Id == 0x40 &&
                            npc.Record.SubId == escortSubId);
                    FailIf(
                        Fixed(scrollLinkStart.X) != 0x507c ||
                        Fixed(scrollLinkStart.Y) != 0x0684 ||
                        scrollingGuard is null ||
                        scrollingGuard.Position != new Vector2(0x50, 0x68) ||
                        scrollingGuard.CurrentScriptAnimationSource !=
                            database.InitialEscortAnimation,
                        "The palace scroll did not begin with the source-exact " +
                        "SPEED_100 diagonal alignment, preserved 8.8 fractions, " +
                        "destination w1Link.xh=$50 write, and frozen escort " +
                        "soldier state-0 animation $00. " +
                        $"room={_rooms.CurrentRoom.Id:x2}, link={scrollLinkStart}, " +
                        $"guard={scrollingGuard?.Position}, animation=" +
                        $"{scrollingGuard?.CurrentScriptAnimationSource}.");
                    scrollGuardStart = scrollingGuard!.Position;

                    outgoingScrollingGuard = roomBefore is
                        var corridorRoom &&
                        (corridorRoom == record.CorridorRoom1 ||
                         corridorRoom == record.CorridorRoom2)
                        ? _entities.OutgoingEntities<NpcCharacter>()
                            .SingleOrDefault(npc =>
                                npc.Record.Id == 0x40 &&
                                npc.Record.SubId == 0x05)
                        : null;
                    Vector2 outgoingScreen = outgoingScrollingGuard is null
                        ? Vector2.Zero
                        : OracleObjectMath.NormalizeSourceScreenPosition(
                            _transitions.WorldToGameplayScreen(
                                outgoingScrollingGuard.Position));
                    bool outgoingShouldRemain =
                        outgoingScrollingGuard is not null &&
                        OracleObjectMath.IsInsideOriginalScreenBoundary(
                            outgoingScreen);
                    FailIf(
                        roomBefore != record.EntranceRoom &&
                        (outgoingScrollingGuard is null ||
                         outgoingScrollingGuard.Active != outgoingShouldRemain ||
                         outgoingShouldRemain &&
                         !outgoingScrollingGuard.Visible),
                        "The palace room-event handoff did not preserve the " +
                        "source objectCheckWithinScreenBoundary lifetime for " +
                        "the outgoing soldierSubid05. " +
                        $"source={roomBefore:x2}, guard=" +
                        $"{outgoingScrollingGuard?.Position}, screen=" +
                        $"{outgoingScreen}, active=" +
                        $"{outgoingScrollingGuard?.Active}, visible=" +
                        $"{outgoingScrollingGuard?.Visible}.");
                    if (!outgoingShouldRemain)
                        outgoingScrollingGuard = null;
                }
            }

            if (_rooms.CurrentRoom.Id != previousRoom)
            {
                previousRoom = _rooms.CurrentRoom.Id;
                enteredRooms.Add(previousRoom);
            }

            if (palace.Stage == DekuForestPalaceStage.GenericGuards &&
                palace.DirectExit)
            {
                break;
            }
        }

        int[] expectedRooms =
        [
            record.EntranceRoom,
            record.CorridorRoom1,
            record.CorridorRoom2,
            record.ThroneRoom,
            record.EntranceRoom
        ];
        NpcCharacter[] exitGuards = _entities.Entities<NpcCharacter>()
            .Where(npc =>
                npc.Active &&
                npc.Record.Id == 0x40 &&
                npc.Record.SubId is 0x07 or 0x02)
            .OrderBy(npc => npc.Position.X)
            .ToArray();
        HashSet<string> scripts = trace.Entries
            .Where(entry => entry.Phase == CutsceneCommandTracePhase.Started)
            .Select(entry => entry.Source.Script)
            .ToHashSet(StringComparer.Ordinal);
        string[] expectedScripts =
        [
            "soldierSubid02Script",
            "soldierSubid05Script",
            "soldierSubid04Script",
            "soldierSubid06Script",
            "ambiSubid00Script",
            "nayruScript01",
            "soldierSubid07Script"
        ];
        (int TextId, int Y, int Flags)[] expectedDialoguePositions =
        [
            (0x5904, 96, 0),
            (0x5905, 24, 0),
            (0x1300, 96, 0),
            (0x1301, 96, 0),
            (0x1302, 96, 0),
            (0x1303, 96, 0),
            (0x004d, 24, 0),
            (0x1304, 96, 0),
            (0x5906, 24, 0),
            (0x590c, 24, 0),
            (0x1305, 96, 0),
            (0x5907, 24, 0),
            (0x1d01, 96, 0),
            (0x1d02, 96, 0),
            (0x1306, 96, 0),
            (0x1d03, 56, 4),
            (0x5908, 96, 0)
        ];

        FailIf(
            frames >= 9000 ||
            !enteredRooms.SequenceEqual(expectedRooms) ||
            startedScrolls != 3 ||
            completedScrolls != 3 ||
            !sawNormalLinkEscortSpeed ||
            !sawStairsLinkEscortSpeed ||
            !sawWrappedEscortEdge ||
            _rooms.ActiveGroup != group ||
            _rooms.CurrentRoom.Id != record.EntranceRoom ||
            _player.Position !=
                new Vector2(record.ExitPlayerX, record.ExitPlayerY) ||
            _player.FacingVector != Vector2I.Up ||
            _player.CutsceneControlled ||
            palace.BlocksGameplay ||
            palace.MenusDisabled ||
            !_saveData.HasGlobalFlag(record.EntranceFlag) ||
            !_saveData.HasGlobalFlag(record.CompletionFlag) ||
            !_inventory.HasTreasure(mysterySeeds) ||
            _inventory.MysterySeeds != 0 ||
            !_inventory.HasTreasure(record.RewardTreasure) ||
            _inventory.Bombs != 0x10 ||
            _rooms.MinimapGroup != group ||
            _rooms.MinimapRoom != record.EntranceRoom ||
            exitGuards.Length != 2 ||
            exitGuards[0].Record.SubId != 0x07 ||
            exitGuards[0].Position != new Vector2(0x48, 0x28) ||
            exitGuards[1].Record.SubId != 0x02 ||
            exitGuards[1].Position != new Vector2(0x58, 0x28) ||
            expectedScripts.Any(script => !scripts.Contains(script)) ||
            !observedDialoguePositions.SequenceEqual(
                expectedDialoguePositions) ||
            !messages.Any(message => message.Contains(
                "Now come with me", StringComparison.Ordinal)) ||
            !messages.Any(message => message.Contains(
                "offer a reward", StringComparison.Ordinal)) ||
            !messages.Any(message => message.Contains(
                "Taking advantage", StringComparison.Ordinal)) ||
            !messages.Any(message => message.Contains(
                "Nice work, kid", StringComparison.Ordinal)),
            "The Mystery Seeds palace sequence did not complete its exact " +
            "1:46 -> 1:36 -> 1:26 -> 1:16 -> direct 1:46 handoffs, " +
            "terrain-aware Link escort, three frozen 32-update scrolls, " +
            "four-lane throne script, Bomb reward, Nayru interruption, or " +
            "final guard release. " +
            $"frames={frames}, stage={palace.Stage}, signal={palace.Signal:x2}, " +
            $"rooms={string.Join(',', enteredRooms.Select(room => room.ToString("x2")))}, " +
            $"scrolls={startedScrolls}/{completedScrolls}, " +
            $"speeds={sawNormalLinkEscortSpeed}/{sawStairsLinkEscortSpeed}, " +
            $"current={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}, " +
            $"player={_player.Position}, controlled={_player.CutsceneControlled}, " +
            $"flags={_saveData.HasGlobalFlag(record.EntranceFlag)}/" +
            $"{_saveData.HasGlobalFlag(record.CompletionFlag)}, " +
            $"seeds={_inventory.MysterySeeds}, bombs={_inventory.Bombs}, " +
            $"guards={exitGuards.Length}, scripts={string.Join(',', scripts)}, " +
            $"textboxes={string.Join(',', observedDialoguePositions.Select(
                dialogue =>
                    $"{dialogue.TextId:x4}@{dialogue.Y}/" +
                    $"{dialogue.Flags:x2}"))}, " +
            $"messages={string.Join('|', messages)}");

        FailIf(
            !palace.TryInteractNpc(exitGuards[0]) ||
            !_dialogue.IsOpen ||
            !DialogueBox.PlainText(_dialogue.CurrentMessage).Contains(
                "Don't loiter", StringComparison.Ordinal) ||
            Mathf.RoundToInt(_dialogue.Position.Y) != 96 ||
            _dialogue.TextboxFlagsForValidation != 0,
            "The direct-return soldier $40:$07 did not enter TX_5909's " +
            "ordinary bottom-position guard loop after TX_5908.");
        _dialogue.Close();
        _roomEvents.CommandTraceSink = null;

        FailIf(
            !OracleSaveData.TryDeserialize(
                originalSaveImage, out OracleSaveData? originalSave),
            "Could not restore the pre-palace save image after validation.");
        _saveData.RestoreFrom(originalSave!);
        var reloadInventory = typeof(InventoryState).GetMethod(
            "LoadFromSaveData",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        FailIf(
            reloadInventory is null,
            "Could not resolve InventoryState.LoadFromSaveData after palace validation.");
        reloadInventory.Invoke(_inventory, null);

        GD.Print(
            "Validated the complete Mystery Seeds palace sequence: 1:46/1:36/" +
            "1:26 terrain-aware simulated-input escort, exact 8.8 alignment, " +
            "three frozen 32-update scrolls, source-ordered throne scripts, " +
            "Mystery Seed transfer, ten-Bomb reward, cfd1 handoffs, guard jump, " +
            "per-line top/bottom/middle textbox positions, possessed Nayru " +
            "PALH_97 interruption, delayed black fade, direct " +
            "1:46 reload, minimap/music/input restore, TX_5908, and TX_5909.");
    }

    private static int Fixed(float coordinate) =>
        Mathf.FloorToInt(coordinate * 256.0f);
}
