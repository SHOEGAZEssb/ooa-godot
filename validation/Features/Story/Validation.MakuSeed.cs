using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMakuSeedSequence()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetMakuTreeState(0x0e);
            _saveData.SetGlobalFlag(GlobalFlag.MakuTreeDisappeared);
            _saveData.SetGlobalFlag(GlobalFlag.MakuTreeSaved);
            for (int essence = 0; essence < 8; essence++) _inventory.GiveTreasure(TreasureId.Essence, essence);
            LoadValidationRoom(0, 0x38);
            _player.WarpTo(new(0x50, 0x68));
            var seed = _roomEvents.Get<MakuSeedEvent>();
            FailIf(!seed.HasState || _roomEvents.Get<MakuTreeAdviceEvent>().HasState,
                "Maku state $0e must dispatch $87:$06 rather than stale $0d advice or disappearance.");
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            Step();
            FailIf(seed.Counter != 60 || !_player.CutsceneControlled || _saveData.HasTreasure(TreasureId.MakuSeed),
                "Maku Seed part1 must disable input and install its 60-update wait before granting $36.");
            Step(59);
            FailIf(_dialogue.IsOpen || seed.Counter != 1, "Maku Seed TX_0559 opened before the 60th decrement.");
            Step();
            FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != 0x0559 ||
                !_dialogue.CurrentMessage.Contains("all eight", StringComparison.Ordinal),
                "Maku Seed must acknowledge all eight essences in TX_0559.");
            _dialogue.Close();
            Step(30);
            FailIf(_entities.EntityAdapters<MakuSeedFallingRoomEntity>().Any(),
                "Maku Seed must retain the script's post-text 30-update wait before allocating $a6.");
            _sound.ClearPlayRequestAudit();
            Step();
            var falling = _entities.EntityAdapters<MakuSeedFallingRoomEntity>().Single();
            NpcCharacter seedActor = (NpcCharacter)falling.Node;
            FailIf(falling.ZFixed != -0x7480 || _runtimeState.ReadWramByte(0xcfc0) != 0 ||
                _sound.PlayRequestsFor(SoundId.SndMagicPowder) != 1,
                $"INTERAC$a6 state0 must execute its first $0080 descent: z={falling.ZFixed:x4}.");
            FailIf(!seedActor.CurrentAnimationUsesColor(Color.Color8(33, 132, 41)) ||
                !seedActor.CurrentAnimationUsesColor(Color.Color8(255, 123, 8)),
                "INTERAC$a6 must replace only sprite palette $07 with PALH_ab, retaining the orange palette $03 layer.");
            NpcCharacter sparkle = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x84);
            FailIf(sparkle.CurrentAnimationTextureSize != new Vector2I(48, 48) || sparkle.CurrentAnimationOpaquePixels == 0,
                "Maku Seed sparkle $84:$0f must use its $1c00 graphics slice and full source OAM bounds.");
            void CheckDescentPresentation(int movementUpdates)
            {
                int expectedZ = (-0x7500 + movementUpdates * 0x80) >> 8;
                Vector2 expectedPosition = new(0x50, 0x68);
                foreach (NpcCharacter actor in new[] { seedActor, sparkle })
                {
                    FailIf(actor.Position != expectedPosition || actor.ScriptDrawOffset != new Vector2(0, expectedZ),
                        $"Maku descent ${actor.Record.Id:x2}:${actor.Record.SubId:x2} update {movementUpdates} must stay over Link with source zh=${expectedZ & 0xff:x2}; position={actor.Position}, offset={actor.ScriptDrawOffset}.");
                    Vector2 drawnOrigin = actor.Position + actor.ScriptDrawOffset + actor.CurrentAnimationOffset;
                    FailIf(drawnOrigin.X < 0x30 || drawnOrigin.X >= 0x60,
                        $"Maku descent ${actor.Record.Id:x2} update {movementUpdates} flashed away from Link: OAM origin={drawnOrigin}.");
                }
                FailIf(!seedActor.Visible || sparkle.Visible != (movementUpdates == 1 || (_entities.FrameCounter & 1) == 0),
                    "Maku sparkle $84:$0f must show its copied creation position in state0, then flicker on even source updates.");
            }
            CheckDescentPresentation(1);
            for (int movementUpdates = 1; movementUpdates < 208;)
            {
                int count = batched ? Math.Min(7, 208 - movementUpdates) : 1;
                Step(count);
                movementUpdates += count;
                CheckDescentPresentation(movementUpdates);
            }
            FailIf(falling.ZFixed != -0x0d00 || _runtimeState.ReadWramByte(0xcfc0) != 0 ||
                _sound.PlayRequestsFor(SoundId.SndMagicPowder) != 4,
                "INTERAC$a6 must reach zh=$f3 without publishing cfc0=$01 on that movement update.");
            Step();
            FailIf(_runtimeState.ReadWramByte(0xcfc0) != 1,
                "INTERAC$a6 must publish cfc0=$01 on the following interaction dispatch.");
            var texts = new List<int>();
            for (int update = 0; update < 2400 && _saveData.MakuTreeState != 0x0f; update++)
            {
                Step();
                if (!_dialogue.IsOpen) continue;
                if (_dialogue.SourceTextId is null)
                    FailIf(!_dialogue.CurrentMessage.Contains("Maku Seed", StringComparison.Ordinal),
                        "The shared treasure grant must show the Huge Maku Seed pickup text TX_004f.");
                texts.Add(_dialogue.SourceTextId ?? -1);
                _dialogue.Close();
            }
            FailIf(_saveData.MakuTreeState != 0x0f || !_inventory.HasTreasure(TreasureId.MakuSeed) ||
                !_saveData.HasGlobalFlag(GlobalFlag.GotMakuSeed) ||
                !_saveData.HasGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame) ||
                !texts.SequenceEqual(new[] { -1, 0x2810, 0x2811, 0x2812, 0x2813, 0x2814, 0x055b, 0x055c }),
                $"Maku Seed/Twinrova sequence failed its reward, flags, dialogue sequence or $0e->$0f handoff: state=${_saveData.MakuTreeState:x2}, texts={string.Join(',', texts.Select(id => $"${id:x4}"))}, native={seed.NativeState}, command={seed.CurrentCommandIndex}.");
            Step(4);
            FailIf(seed.OwnsGameLogic || _player.CutsceneControlled || _entities.PlayerMenusDisabled ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 || _saveData.MakuMapTextPresent != 0x60,
                "Maku Seed completion must restore gameplay and map advice $60.");
            LoadValidationRoom(0, 0x48);
            LoadValidationRoom(0, 0x38);
            _player.WarpTo(new(0x50, 0x68));
            FailIf(_currentRoom.IsSolid(_player.Position), "Maku post-Seed approach starts in solid geometry.");
            StepGameplayUpdates(40, Vector2.Up, batched: batched);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            Step(2);
            FailIf(!_dialogue.IsOpen || _saveData.MakuMapTextPresent != 0x18 || seed.HasState,
                "Re-entering Maku state $0f must give TX_0518 Black Tower advice through real A-button interaction.");
            _dialogue.Close();
        }
        GD.Print("Validated Maku $0e seed acknowledgement, source wait/descent boundaries, unlinked Twinrova handoff, reward/flags/map state, actual repeat conversation, and batched updates.");
    }

    private void ValidateMakuSeedReturnAndCancellation()
    {
        foreach (bool linked in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(linked);
            _saveData.SetMakuTreeState(0x0e);
            _saveData.SetGlobalFlag(GlobalFlag.GotMakuSeed);
            _inventory.GiveTreasure(TreasureId.MakuSeed, 0);
            LoadValidationRoom(0, 0x38);
            var seed = _roomEvents.Get<MakuSeedEvent>();
            FailIf(!_entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x86)
                .CurrentScriptAnimationSource.StartsWith("127@203,16,0,5", StringComparison.Ordinal),
                "Maku $87:$06's direct animation $04 must leave var3b=$00 and its related $86 flower in animation $00.");
            var texts = new List<int>();
            for (int update = 0; update < 240 && _saveData.MakuTreeState != 0x0f; update += 4)
                StepGameplayUpdates(4, Vector2.Zero, batched: true, afterUpdate: () =>
                {
                    if (!_dialogue.IsOpen) return;
                    texts.Add(_dialogue.SourceTextId ?? -1); _dialogue.Close();
                });
            FailIf(_saveData.MakuTreeState != 0x0f || !texts.SequenceEqual(linked ? new[] { 0x055f, 0x0560 } : new[] { 0x055b, 0x055c }) ||
                !_saveData.HasGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame),
                "Maku $87:$06 part2 must retain its linked/unlinked return text and completion, without granting another seed.");
            StepGameplayUpdates(4, Vector2.Zero);
            _player.WarpTo(new(0x50, 0x68));
            StepGameplayUpdates(40, Vector2.Up);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
                StepGameplayUpdates(2, Vector2.Zero);
                FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != (linked ? 0x0560 : 0x055c),
                    "The retained Maku part2 tree must repeat its loaded endgame advice through collision-reachable A input.");
                _dialogue.Close(); StepGameplayUpdates(3, Vector2.Zero);
            }
            seed.Cancel();
            FailIf(seed.HasState || _player.CutsceneControlled, "Maku Seed return cancellation retained its control lease.");
            _saveData.SetMakuTreeState(0x0e);
            LoadValidationRoom(0, 0x38);
            _player.WarpTo(new(0x50, 0x68));
            StepGameplayUpdates(40, Vector2.Up);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"]);
            StepGameplayUpdates(2, Vector2.Zero);
            FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != (linked ? 0x0560 : 0x055c) ||
                _player.CutsceneControlled || _saveData.MakuTreeState != 0x0e,
                "Maku $87:$06 with the reveal flag set must select its generic part3 repeat branch without replaying completion.");
            _dialogue.Close();
        }
        foreach (int cancelAt in new[] { 18, 120, 400, 600 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetMakuTreeState(0x0e);
            LoadValidationRoom(0, 0x38);
            var seed = _roomEvents.Get<MakuSeedEvent>();
            StepGameplayUpdates(cancelAt, Vector2.Zero, batched: true, afterUpdate: () =>
            { if (_dialogue.IsOpen) _dialogue.Close(); });
            FailIf(cancelAt >= 400 && seed.NativeState < 0 ||
                cancelAt == 600 && !_entities.EntityAdapters<TwinrovaRevealRoomEntity>().Any(),
                "Maku Seed cancellation fixture did not reach the native reveal and its witch actors.");
            seed.Cancel();
            StepGameplayUpdates(4, Vector2.Zero, batched: true);
            FailIf(seed.HasState || _player.CutsceneControlled || _entities.PlayerMenusDisabled ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                _entities.EntityAdapters<MakuSeedTreeRoomEntity>().Any() ||
                _entities.EntityAdapters<MakuSeedFallingRoomEntity>().Any() ||
                _entities.EntityAdapters<MakuSeedSparkleRoomEntity>().Any() ||
                _entities.EntityAdapters<TwinrovaRevealRoomEntity>().Any() ||
                _saveData.HasGlobalFlag(GlobalFlag.SawTwinrovaBeforeEndgame),
                $"Canceling Maku Seed at update {cancelAt} retained a native actor/mask or completed the reveal.");
        }
        ReinitializeGameplayForValidation();
        _saveData.SetLinkedGame(true);
        _saveData.SetMakuTreeState(0x0e);
        bool rejected = false;
        try { LoadValidationRoom(0, 0x38); }
        catch (NotSupportedException exception)
        { rejected = exception.Message.Contains("CUTSCENE_TWINROVA_REVEAL", StringComparison.Ordinal); }
        FailIf(!rejected || _inventory.HasTreasure(TreasureId.MakuSeed),
            "The unsupported linked Maku Seed reveal must fail explicitly before granting $36 or substituting the normal-game scene.");
        GD.Print("Validated both Maku Seed return branches, reachable repeated conversation, and cancellation before/during descent and reveal.");
    }

    private void ValidateMakuLateAdviceProgression()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetMakuTreeState(0x0c);
            _saveData.SetGlobalFlag(GlobalFlag.MakuTreeDisappeared);
            _saveData.SetGlobalFlag(GlobalFlag.MakuTreeSaved);
            for (int essence = 0; essence < 8; essence++) _inventory.GiveTreasure(TreasureId.Essence, essence);
            _inventory.GiveTreasure(TreasureId.Harp, 1);
            void CompleteRemote(IRoomEvent remote, int expectedText)
            {
                bool sawText = false;
                for (int update = 0; update < 1600 && remote.HasState; update += 4)
                    StepGameplayUpdates(4, Vector2.Zero, batched: batched, afterUpdate: () =>
                    {
                        if (!_dialogue.IsOpen) return;
                        FailIf(_dialogue.SourceTextId != expectedText,
                            $"Late Maku progression expected TX_{expectedText:x4}, got ${_dialogue.SourceTextId:x4}.");
                        sawText = true; _dialogue.Close();
                    });
                FailIf(remote.HasState || !sawText, $"Late Maku conversation TX_{expectedText:x4} did not complete.");
            }
            LoadValidationRoom(1, 0x5c);
            CompleteRemote(_roomEvents.Get<RemoteMakuEighthEssenceEvent>(), 0x05ba);
            FailIf(_saveData.MakuTreeState != 0x0d || !_saveData.HasRoomFlag(1, 0x5c, 0x40),
                "The eighth-essence call must increment the source story byte once, independently of earlier missed conversations.");
            LoadValidationRoom(0, 0x3a);
            CompleteRemote(_roomEvents.Get<RemoteMakuHarpEvent>(), 0x05b2);
            FailIf(_saveData.MakuTreeState != 0x0e || !_saveData.HasRoomFlag(0, 0x3a, 0x40),
                "Completing a pending Harp conversation must supply its source story increment and persist room $0:3a bit $40.");
            LoadValidationRoom(0, 0x38);
            FailIf(!_roomEvents.Get<MakuSeedEvent>().HasState || _roomEvents.Get<MakuTreeAdviceEvent>().HasState,
                "The completed late-Maku handoff must select the Maku Seed branch, not TX_0516's last-essence hint.");
        }
        GD.Print("Validated eighth-essence completion and a pending Harp call against source story-byte progression, without deriving story state from inventory.");
    }
}
