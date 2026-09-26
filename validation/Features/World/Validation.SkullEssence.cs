using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullEssenceSequence()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        bool batched = false;
        void Step(int count = 1, Vector2 movement = default) =>
            StepGameplayUpdates(count, movement, [], [], batched: batched);
        var data = new SkullDungeonDatabase();
        var visual = new DungeonInteractionVisualDatabase().Visual("burning-flame");
        FailIf(data.GetRoomRecords(4, 0x69) is not [{ Id: InteractionId.Essence, SubId: 0, Order: 0, Y: 0x28, X: 0x78,
                Kind: DungeonObjectKind.Essence, Predicate: DungeonObjectCondition.Always }] ||
            data.Essence.Index != 3 || visual.TileBase != 8 || visual.Palette != 2 ||
            !data.Essence.Message.StartsWith("\\pos(2)", StringComparison.Ordinal) ||
            !data.Essence.Message.Contains("Burning Flame", StringComparison.Ordinal) ||
            !data.Essence.Message.Contains("wavering hearts", StringComparison.Ordinal),
            "D4 Essence lost source7f00 placement, fourth OAM row, or TX_0011 formatting/content.");
        var remote = _roomEvents.Get<RemoteMakuFourthEssenceEvent>();
        var text = (CutsceneShowTextVariantsCommand)remote.Database.Commands[10];
        FailIf(!text.StandardMessage.Contains("The ridge north", StringComparison.Ordinal) ||
            text.StandardMessage != text.LinkedMessage,
            "Source TX_05b5/TX_05c5 aliases must preserve the same ridge message for both game modes.");
        FailIf(remote.Record is not { Group: 0, Room: 3, SubId: 0, Var03: 5, EssenceMask: 8,
                StandardTextId: 0x05b5, LinkedTextId: 0x05c5, ConfettiKind: RemoteMakuConfettiKind.Present } ||
            remote.Database.Commands[7] is not CutsceneNativeCommand { Handler: "SpawnPresentConfetti" },
            "D4 exit must select remote Maku val05 and its present-confetti branch.");
        _saveData.SetRoomFlag(4, 0x69, OracleSaveData.RoomFlagItem, false);
        LoadValidationRoom(4, 0x69);
        _player.WarpTo(new Vector2(123, 58));
        FailIf(_currentRoom.IsSolid(_player.Position), "Essence boundary fixture must remain below the real pedestal collision.");
        Step(8);
        FailIf(_roomEvents.Get<DungeonEssenceEvent>().HasState,
            "Essence distance must be Manhattan: delta(3,18) lies outside the source distance20 gate.");
        var counterField = typeof(RoomEntityManager).GetField("_enemyFrameCounter", flags)!;
        while (((int)counterField.GetValue(_entities)! & 3) != 0) Step();
        _player.WarpTo(new Vector2(124, 55));
        Step(3);
        FailIf(_roomEvents.Get<DungeonEssenceEvent>().HasState,
            "Essence approach must not run on the three updates between frame-counter multiples of4.");
        Step();
        FailIf(!_roomEvents.Get<DungeonEssenceEvent>().HasState || !_player.CutsceneControlled,
            "Essence centered gate must include X+4 when delta(4,15) is below Manhattan distance20.");
        LoadValidationRoom(4, 0x6b);
        FailIf(_roomEvents.Get<DungeonEssenceEvent>().HasState || _player.CutsceneControlled ||
            _saveData.HasRoomFlag(4, 0x69, OracleSaveData.RoomFlagItem),
            "Canceling Essence approach by changing rooms must release ownership without awarding the Essence.");
        foreach (bool linked in new[] { false, true })
        {
            batched = linked;
            _saveData.SetLinkedGame(linked);
            _saveData.SetRoomFlag(4, 0x69, 0xff, false);
            _saveData.SetRoomFlag(4, 0x6b, OracleSaveData.RoomFlag80);
            _saveData.SetRoomFlag(4, 0x6b, OracleSaveData.RoomFlagItem);
            _saveData.SetRoomFlag(0, 3, 0x40, false);
            _saveData.WriteWramByte(WramAddress.wEssencesObtained, 7); _saveData.CommitInventoryChange();
            _saveData.SetMakuTreeState(3);
            _saveData.SetMakuMapTextPast(0x5a);
            _saveData.SetMakuMapTextPresent(0);
            LoadValidationRoom(0, 3); Step(3);
            FailIf(remote.HasState, "Remote Maku val05 ignored missing Essence bit3.");
            LoadValidationRoom(4, 0x6b);
            _player.WarpTo(new Vector2(120, 40));
            FailIf(_currentRoom.IsSolid(_player.Position), "Essence approach must begin on actual defeated-boss floor.");
            for (int i = 0; !IsTransitioning && i < 100; i++) Step(movement: Vector2.Up);
            FailIf(!IsTransitioning, "Actual north approach did not reach D4's Essence room.");
            for (int i = 0; IsTransitioning && i < 160; i++) Step();
            FailIf(IsTransitioning || _currentRoom.Id != 0x69, "Essence-room scroll failed.");
            var essence = _entities.Entities<DungeonEssence>().Single();
            FailIf(essence.EssenceIndex != 3 || essence.Collected || essence.Position != new Vector2(120, 40) ||
                essence.ExitWarp is not { DestinationGroup: 0, DestinationRoom: 3, DestinationPosition: 0x35, DestinationTransition: WarpDestinationTransition.XShifted } ||
                _currentRoom.GetTerrainInfo(essence.Position).Collision != 15,
                "Burning Flame lost its pedestal collision or TRANSITION_DEST_X_SHIFTED exit mapping.");
            _sound.ClearPlayRequestAudit();
            var preciseField = typeof(DungeonEssence).GetField("_precisePosition", flags)!;
            for (int i = 0; !_dialogue.IsOpen && i < 220; i++)
            {
                Vector2 before = (Vector2)preciseField.GetValue(essence)!;
                Step(movement: Vector2.Up);
                if (essence.ReadyForDialogue)
                {
                    Vector2 held = (Vector2)preciseField.GetValue(essence)!;
                    FailIf(held - held.Floor() != before - before.Floor() ||
                        held.Floor() != _player.Position.Floor() + new Vector2(0, -14),
                        "Essence state4 must copy Link's high XY bytes while preserving its own low bytes.");
                }
            }
            FailIf(!_dialogue.IsOpen || !essence.ReadyForDialogue || _player.IsHoldingItemTwoHands ||
                (_inventory.Essences & 8) == 0 || !_saveData.HasRoomFlag(4, 0x69, OracleSaveData.RoomFlagItem) ||
                !_dialogue.CurrentMessage.Contains("Burning Flame", StringComparison.Ordinal) ||
                _sound.PlayRequestsFor(SoundId.SndDropEssence) != 1 ||
                _sound.PlayRequestsFor(SoundId.MusGetEssence) != 1,
                "Walking to the pedestal did not collect Burning Flame through the actual gameplay loop.");
            Step();
            FailIf(_player.IsHoldingItemTwoHands || _player.NativeNormalStateForInteraction,
                "Essence state04 consumption must precede the held pose.");
            Step();
            FailIf(!_player.IsHoldingItemTwoHands || _player.NativeNormalStateForInteraction,
                "Essence state04 must initialize its held pose on the following dispatch.");
            Step(6);
            FailIf(essence.SwirlActive || IsTransitioning, "Essence cutscene advanced past an open textbox.");
            _dialogue.Close(); Step();
            FailIf(essence.SwirlActive || _roomEvents.Get<DungeonEssenceEvent>().Counter != 0,
                "Native Essence state5 must install its script before state6 executes music/energy/waits.");
            Step();
            FailIf(!essence.SwirlActive || _roomEvents.Get<DungeonEssenceEvent>().Counter != 360,
                "Burning Flame did not begin the shared two180-update energy waits.");
            var energyParts = _entities.Entities<BlueEnergyBeadRoomEntity>();
            FailIf(energyParts.Count != 8 || energyParts.Any(part => part.Initialized || part.Visible),
                "The Essence script runs after PARTs: its eight new energy beads must await the next native pass.");
            Step();
            FailIf(energyParts.Any(part => !part.Initialized || part.Visible || part.Delay is < 1 or > 8),
                "The first energy PART update must initialize all eight delays without revealing a bead.");
            Step(358);
            FailIf(_sound.PlayRequestsFor(SoundId.SndFadeOut) != 0, "Essence fade cadence started before360 swirl updates.");
            Step();
            FailIf(_sound.PlayRequestsFor(SoundId.SndFadeOut) != 1, "Essence fade cadence missed the360-update boundary.");
            Step(20); Step(20); Step(40);
            FailIf(essence.SwirlActive || _sound.PlayRequestsFor(SoundId.SndFadeOut) != 4,
                "Essence fade sound/stop-swirl cadence lost source20/20/40 waits.");
            FailIf(_roomEvents.Get<DungeonEssenceEvent>().Counter != 29,
                "Essence scriptend must fall through to state7 and decrement its new30 counter in that same update.");
            Step(28);
            FailIf(_entities.Entities<BlueEnergyBeadRoomEntity>().Count != 0,
                "The Essence script's shared stop signal must delete its energy PARTs before warping.");
            FailIf(IsTransitioning, "Essence final warp delay ended before its last source counter update.");
            Step();
            FailIf(!IsTransitioning, "D4 Essence did not start its delayed white exit transition.");
            for (int i = 0; IsTransitioning && i < 180; i++) Step();
            FailIf(IsTransitioning || _rooms.ActiveGroup != 0 || _currentRoom.Id != 3 ||
                _player.Position != new Vector2(80, 56) || _player.IsHoldingItemTwoHands || !remote.HasState,
                $"D4 Essence must exit to 0:03/$35 with X shifted left8: room={_rooms.ActiveGroup}:{_currentRoom.Id:x2}, Link={_player.Position}.");
            for (int i = 0; !_dialogue.IsOpen && i < 700; i++) Step();
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("The ridge north", StringComparison.Ordinal) ||
                !_dialogue.CurrentMessage.Contains("Nayru's House", StringComparison.Ordinal) ||
                _saveData.MakuMapTextPresent != (linked ? 0xc5 : 0xb5) || _saveData.MakuMapTextPast != 0x5a ||
                !remote.BlocksGameplay || !_hud.StatusBarHidden,
                "D4 remote Maku did not deliver TX_05b5/05c5 and update only present map text during its native HUD lock.");
            _dialogue.Close();
            for (int i = 0; remote.HasState && i < 180; i++) Step();
            FailIf(remote.HasState || _player.CutsceneControlled || _hud.StatusBarHidden ||
                !_saveData.HasRoomFlag(0, 3, 0x40) || _saveData.MakuTreeState != 4 ||
                _sound.ActiveMusic != _sound.Data.RoomMusic(0, 3),
                "D4 remote Maku failed to restore HUD/input/music or persist its completion and Maku state.");
            LoadValidationRoom(0, 3); Step(5);
            FailIf(remote.HasState, "Completed D4 remote Maku message replayed on reentry.");
            LoadValidationRoom(4, 0x69); Step(5);
            var collected = _entities.Entities<DungeonEssence>().Single();
            FailIf(!collected.Collected || _currentRoom.GetTerrainInfo(new Vector2(120, 40)).Collision != 15,
                "Collected D4 Essence must keep its pedestal collision on reentry.");
            _player.WarpTo(new Vector2(120, 72));
            Step(40, Vector2.Up);
            FailIf(_dialogue.IsOpen || _roomEvents.Get<DungeonEssenceEvent>().HasState,
                "Walking back to the collected pedestal triggered another Essence award.");
        }
    }
}
