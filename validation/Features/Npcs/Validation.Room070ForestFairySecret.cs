using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom070ForestFairySecret()
    {
        NpcRecord record = new NpcDatabase().GetRoomNpcs(0, 0x70).Single();
        var commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/forest_fairy_secret_commands.tsv");
        FailIf(record is not { Id: 0x49, SubId: 0x0b, Var03: 0,
                Y: 0x48, X: 0x48, Palette: 1,
                Implementation: NpcImplementationClassification.SpecializedNative } ||
            commands.Count != 28 ||
            commands[0] is not CutsceneSetCollisionRadiiCommand { RadiusY: 4, RadiusX: 4 } ||
            commands[18] is not CutsceneGiveItemCommand { TreasureId: 0x2a, Parameter: 2 } ||
            commands[20] is not CutsceneNativeCommand { Handler: "GenerateSecret" },
            "Room 0:70 $49:$0b source placement, script, $04/$04 radius or heart-container reward changed.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame, false);
            LoadValidationRoom(0, 0x70);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            NpcCharacter fairy = _entities.Entities<NpcCharacter>().Single();
            FailIf(fairy.Active || fairy.Visible || fairy.CanTalkTo(_player),
                "Room 0:70 $49:$0b appeared before GLOBALFLAG_FINISHEDGAME.");

            _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
            LoadValidationRoom(0, 0x70);
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            fairy = _entities.Entities<NpcCharacter>().Single();
            ForestFairySecretEvent secret = _roomEvents.Get<ForestFairySecretEvent>();
            FailIf(!fairy.Active || !fairy.Visible || !secret.HasState ||
                fairy.Position != new Vector2(0x48, 0x48) ||
                fairy.ObjectCollisionBounds.Size != new Vector2(8, 8) ||
                fairy.CurrentAnimationOpaquePixels == 0 ||
                !_entities.BlocksLink(fairy.Position),
                "Room 0:70 $49:$0b failed postgame state zero, sprite, or $04/$04 collision.");

            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Talk(bool repeat = false)
            {
                ApproachLibraryActor(fairy, batched,
                    counterpartReachable: () => _entities.FindTalkTarget(_player) == fairy);
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                FailIf(_dialogue.ChoiceActive == repeat ||
                    !PlainWords(_dialogue.CurrentMessage).Contains(repeat
                        ? "Here's the secret to being happy"
                        : "Are you looking for me?", StringComparison.Ordinal) ||
                    !_player.CutsceneControlled,
                    $"Room 0:70 $49:$0b A did not open TX_{(repeat ? 0x114d : 0x1148):x4} or disable input.");
            }
            void FinishText()
            {
                _dialogue.Close(); Step(3);
                FailIf(_player.CutsceneControlled,
                    "Room 0:70 $49:$0b did not enable input after conversation.");
            }

            Talk();
            _dialogue.SubmitChoiceForValidation(1);
            Step(30);
            FailIf(_dialogue.IsOpen, "Room 0:70 No branch crossed source wait 30 early.");
            Step(1);
            FailIf(PlainWords(_dialogue.CurrentMessage) != "Oh, really...",
                "Room 0:70 No branch did not show TX_1149.");
            FinishText();

            Talk();
            _dialogue.SubmitChoiceForValidation(0);
            Step(31);
            FailIf(!_secretEntry.IsActive || !_gameplayPause.IsLeased,
                "Room 0:70 Yes branch did not open MENU_SECRET $01.");
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            _secretEntry.Submit([0xff, 0xff, 0xff, 0xff, 0xff]);
            FailIf(!_secretEntry.IsActive || _saveData.HasGlobalFlag(0x65),
                "Room 0:70 invalid secret advanced the reward.");
            var codec = new LinkedGameNpcDatabase();
            _secretEntry.Submit(codec.GenerateSecretValues(0x01, _saveData));
            for (int frame = 0; frame < 22; frame++) _secretEntry.Update(1.0 / 60.0);
            Step(30);
            FailIf(_saveData.HasGlobalFlag(0x65),
                "Room 0:70 set GLOBALFLAG_BEGAN_FAIRY_SECRET before wait 30.");
            Step(1);
            FailIf(!_saveData.HasGlobalFlag(0x65) ||
                !PlainWords(_dialogue.CurrentMessage).Contains("secret we taught", StringComparison.Ordinal),
                "Room 0:70 valid secret did not set flag $65 and show TX_114a.");
            _dialogue.Close();
            Step(31);
            GroundTreasurePickup reward = _entities.Entities<GroundTreasurePickup>().Single();
            FailIf(reward.Record.TreasureObject != "TREASURE_OBJECT_HEART_CONTAINER_02" ||
                !_inventory.HasTreasure(TreasureId.HeartContainer) ||
                _saveData.HasGlobalFlag(0x6f),
                "Room 0:70 did not grant heart container $2a:$02 before return flag $6f.");
            _dialogue.Close();
            Step(31);
            FailIf(!_saveData.HasGlobalFlag(0x6f) ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x11 ||
                !_dialogue.IsOpen || _dialogue.CurrentMessage.Contains("\\secret1", StringComparison.Ordinal),
                "Room 0:70 did not generate FAIRY_RETURN_SECRET $11 and show TX_114c.");
            FinishText();
            Talk(repeat: true);
            FailIf(_dialogue.ChoiceActive ||
                _saveData.ReadWramByte(WramAddress.wShortSecretIndex) != 0x11 ||
                _dialogue.CurrentMessage.Contains("\\secret1", StringComparison.Ordinal),
                "Room 0:70 repeated talk did not show source TX_114d return secret.");
            LoadValidationRoom(0, 0x71);
            _dialogue.Close();
            FailIf(secret.HasState || _player.CutsceneControlled,
                "Room 0:70 cancellation retained fairy input control.");
        }
        GD.Print("Validated room 0:70 $49:$0b postgame gate, collision approach, No/invalid/valid secret, reward, return secret, repeat, cancellation and split/batched updates.");
    }
}
