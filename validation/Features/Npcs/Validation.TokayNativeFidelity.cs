using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayNativeFidelity()
    {
        var native = new TokayNativeDatabase();
        var texts = new TokayInteractionDatabase();
        FailIf(native.Constant("speed-020") != 0x05 ||
            native.Constant("speed-180") != 0x3c ||
            native.Constant("speed-300") != 0x78 ||
            !native.CookPaths.Select(path => path.Angle).SequenceEqual(new[] { 0x18, 0x0a, 0x02, 0x14, 0x06, 0x18 }),
            "Tokay native movement tables changed from the clean-US source.");

        // The native catch helper uses inclusive byte-coordinate rectangles,
        // including corners and high-byte truncation, before movement.
        FailIf(!WildTokayGameEvent.CanParticipantCatch(new Vector2(40, 40), new Vector2(50, 50)) ||
            !WildTokayGameEvent.CanParticipantCatch(new Vector2(40.5f, 40), new Vector2(50.9f, 30)) ||
            WildTokayGameEvent.CanParticipantCatch(new Vector2(40, 40), new Vector2(51, 40)),
            "Wild Tokay catching no longer matches inclusive $0a byte-axis checks.");

        ValidateTokayCookScript();

        _saveData.SetRoomFlag(1, 0xcb, OracleSaveData.RoomFlag40, value: false);
        _saveData.SetLinkedGame(linked: true);
        _inventory.LoseTreasure(TreasureDatabase.TreasureShovel);
        LoadValidationRoom(1, 0xcb);
        NpcCharacter rosa = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x68);
        TokayAttachedVisualRoomEntity shovel = _entities.Entities<TokayAttachedVisualRoomEntity>().Single();
        RosaShovelEvent digging = _roomEvents.Get<RosaShovelEvent>();
        FailIf(shovel.FollowParent || shovel.ParentOffset != new Vector2(0x48, 0x38) ||
            !digging.TryInteractNpc(rosa), "Rosa did not initialize her stationary shovel.");
        _dialogue.Close();
        StepRoomEventFrames(31);
        Vector2 rosaStart = rosa.Position;
        StepRoomEventFrames(47);
        FailIf(rosa.Position != rosaStart + new Vector2(5, 0),
            "Rosa did not retain 47 fractional SPEED_020 steps (5 whole pixels).");
        StepRoomEventFrames(2 + 20);
        FailIf(!shovel.FollowParent || shovel.ParentOffset != new Vector2(9, 0),
            "Rosa's related shovel did not move to +$09 after the source wait.");
        StepRoomEventFrames(20);
        FailIf(shovel.ParentOffset != new Vector2(-9, 0) || shovel.ZIndex != NpcCharacter.FixedLowPriorityZIndex,
            "Rosa's shovel did not move to -$09 with priority $83.");
        digging.Cancel();
        _dialogue.Close();

        _saveData.SetRoomFlag(1, 0xbb, OracleSaveData.RoomFlag80, value: false);
        LoadValidationRoom(1, 0xbb);
        FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress) != 1,
            "Rosa-escape initialization did not set wDiggingUpEnemiesForbidden $ccde.");
        TokayCharacter escape = _entities.Entities<TokayCharacter>().Single(npc => npc.Record.SubId == 0x0b);
        Vector2 escapeStart = escape.Position;
        _player.WarpTo(new Vector2(0x70, 0x50), recordSafe: false);
        StepRoomEventFrames(31);
        _dialogue.Close();
        StepRoomEventFrames(1);
        StepRoomEventFrames(16);
        FailIf(escape.Position != escapeStart + new Vector2(0, -24),
            "Rosa escape moveup $11 did not execute 16 SPEED_180 updates.");
        StepRoomEventFrames(2 + 30);
        FailIf(escape.FacingVector != Vector2I.Down,
            "Rosa escape did not turn down between its separate 30-update waits.");
        StepRoomEventFrames(30);
        FailIf(_roomEvents.Get<TokayRunningFromRosaEvent>().Stage != TokayRunningFromRosaStage.Jumping,
            "Rosa escape did not start its native -$01c0 jump after both waits.");
        for (int frame = 0; frame < 300 && _roomEvents.Get<TokayRunningFromRosaEvent>().HasState; frame++)
        {
            if (_dialogue.IsOpen) _dialogue.Close();
            StepRoomEventFrames(1);
        }
        FailIf(_roomEvents.Get<TokayRunningFromRosaEvent>().HasState ||
            !_saveData.HasRoomFlag(1, 0xbb, OracleSaveData.RoomFlag80),
            "Rosa escape did not finish its remaining source moves and room flag.");
        _roomEvents.Get<TokayRunningFromRosaEvent>().Cancel();
        LoadValidationRoom(1, 0xcb);
        FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.DiggingUpEnemiesForbiddenAddress) != 0,
            "Room loading retained the previous room's digging restriction.");

        _saveData.SetRoomFlag(0, 0xbb, OracleSaveData.RoomFlag40, value: false);
        LoadValidationRoom(0, 0xbb);
        TokayCharacter vine = _entities.Entities<TokayCharacter>().Single(npc => npc.Record.SubId == 0x1e);
        _player.WarpTo(vine.Position + Vector2.Down * 24, recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(_player.CutsceneControlled || _roomEvents.MenusDisabled,
            "Vine explanation triggered or disabled menus at excluded distance $18.");
        _player.WarpTo(vine.Position + Vector2.Up * 20, recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(_player.CutsceneControlled, "Vine explanation treated native direction zero as a trigger.");
        _player.WarpTo(vine.Position + Vector2.Right * 20, recordSafe: false);
        int vineJumpSounds = _sound.PlayRequestsFor(OracleSoundEngine.SndJump);
        StepRoomEventFrames(1);
        FailIf(!_player.CutsceneControlled || _player.FacingVector != Vector2I.Left || _dialogue.IsOpen ||
            !_entities.Entities<TokayAttachedVisualRoomEntity>().Any() ||
            _rooms.CurrentRoom.GetMetatile(vine.Position) != 0 ||
            _rooms.CurrentRoom.GetTerrainInfo(vine.Position).Collision != 0x0f,
            "Vine proximity did not turn Link, spawn the mark, and retain initialization tile effects.");
        StepRoomEventFrames(29);
        FailIf(_dialogue.IsOpen, "Vine explanation skipped its 30-update reaction.");
        StepRoomEventFrames(1);
        FailIf(_dialogue.CurrentMessage != DialogueBox.PlainText(texts.Text(0x0a6a)) ||
            _sound.PlayRequestsFor(OracleSoundEngine.SndJump) != vineJumpSounds,
            "Vine reaction added a jump sound or lost TX_0a6a.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(vine.FacingVector != Vector2I.Down || !_roomEvents.Get<TokayVineExplanationEvent>().TryInteractNpc(vine) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(texts.Text(0x0a6b)),
            "Vine repeat did not require A and select TX_0a6b.");
        _roomEvents.Get<TokayVineExplanationEvent>().Cancel();
        _dialogue.Close();

        foreach (int level in new[] { 1, 2 })
        {
            _saveData.SetRoomFlag(5, 0xe9, OracleSaveData.RoomFlag40, value: false);
            _inventory.GiveTreasure(TreasureDatabase.TreasureShield, level);
            LoadValidationRoom(5, 0xe9);
            TokayCharacter holder = _entities.Entities<TokayCharacter>().Single(npc => npc.Record.SubId == 0x1d);
            FailIf(holder.Accessory is null || !_roomEvents.Get<TokayShieldUpgradeEvent>().TryInteractNpc(holder),
                $"Shield Tokay did not hold level ${level:x2}.");
            _dialogue.Close();
            StepRoomEventFrames(31);
            FailIf(!_saveData.HasRoomFlag(5, 0xe9, OracleSaveData.RoomFlag40) ||
                !holder.Accessory!.Retired || !_dialogue.IsOpen ||
                _roomEvents.Get<TokayShieldUpgradeEvent>().Stage != TokayShieldUpgradeStage.Reward,
                "Shield handoff delayed bit $40 until after reward text or retained its accessory.");
            _roomEvents.Get<TokayShieldUpgradeEvent>().Cancel();
            _dialogue.Close();
        }

        var theft = new TokayTheftEventDatabase();
        foreach (int treasure in theft.Record.StolenItems.Where(item => item != TreasureDatabase.TreasureShield))
            _inventory.GiveTreasure(treasure, 1);
        _inventory.LoseTreasure(TreasureDatabase.TreasureFeather);
        _saveData.SetRoomFlag(5, 0xca, OracleSaveData.RoomFlag40);
        LoadValidationRoom(5, 0xca);
        TokayHoldingItemCharacter returned = _entities.Entities<TokayHoldingItemCharacter>().Single();
        _inventory.GiveTreasure(TreasureDatabase.TreasureFeather, 1);
        FailIf(returned.ReturnedItemDialogue != 0x0a0c || !_roomEvents.Get<TokayHoldingItemEvent>().TryInteractNpc(returned) ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(texts.Text(0x0a0c)),
            "Returned holder recomputed its initialization snapshot without leaving.");
        _roomEvents.Get<TokayHoldingItemEvent>().Cancel();
        _dialogue.Close();
        LoadValidationRoom(5, 0xca);
        returned = _entities.Entities<TokayHoldingItemCharacter>().Single();
        FailIf(returned.ReturnedItemDialogue != 0x0a0d,
            "Returned holder did not recompute var3c on room re-entry.");

        GD.Print("Validated Tokay motion/catch geometry, cook jumps during dialogue, Rosa shovel and digging state, vine proximity/reaction/repeat, held shields and reward flags, and returned-item snapshots.");
    }

    private void ValidateTokayBusinessScrubs()
    {
        foreach (int level in new[] { 1, 2, 3 })
        foreach (int room in new[] { 0xbc, 0x90 })
        {
            _inventory.GiveTreasure(TreasureDatabase.TreasureShield, level);
            _inventory.LoseTreasure(TreasureDatabase.TreasureShield);
            LoadValidationRoom(1, room);
            BusinessScrubRoomEntity scrub = _entities.EntityAdapters<BusinessScrubRoomEntity>().Single();
            int price = level * 50;
            FailIf(scrub.Offer.Price != price || scrub.Offer.Parameter != level,
                $"Business Scrub $ce:$00 in 1:{room:x2} lost its level ${level:x2} offer.");
            _inventory.AddRupees(999 - _inventory.Rupees);
            FailIf(!_roomEvents.Get<BusinessScrubEvent>().TryInteractNpc(scrub.Npc) ||
                !_dialogue.CurrentMessage.Contains(price.ToString(), StringComparison.Ordinal),
                "Island Business Scrub did not show its initialized price.");
            _dialogue.SubmitChoiceForValidation(0);
            StepRoomEventFrames(1);
            FailIf(!_inventory.HasTreasure(TreasureDatabase.TreasureShield) ||
                _inventory.ShieldLevel != level || _inventory.Rupees != 999 - price,
                "Island Business Scrub did not restore the priced shield and charge its BCD offer.");
            _dialogue.Close();
            StepRoomEventFrames(1);
            FailIf(scrub.Talking || _roomEvents.Get<BusinessScrubEvent>().HasState,
                "Island Business Scrub did not return to its native idle after result text.");
        }

        GD.Print("Validated both $ce:$00 Business Scrub placements at every shield level.");
    }

    private void ValidateTokayPresentationAndSocket()
    {
        foreach (Vector2 delta in new[] { new Vector2(10, 10), new Vector2(10, -10),
            new Vector2(-10, 10), new Vector2(-10, -10) })
        {
            LoadValidationRoom(0, 0xbd);
            NpcCharacter npc = _entities.Entities<NpcCharacter>().Single(npc => npc.Record.Id == 0x48);
            _player.WarpTo(npc.Position + delta, recordSafe: false);
            _entities.Update(1.0 / 60.0, _player);
            Vector2I expected = delta.X > 0 ? Vector2I.Right : Vector2I.Left;
            FailIf(npc.FacingVector != expected,
                "Ordinary Tokay did not choose horizontal facing on equal native X/Y distances.");
            _player.WarpTo(npc.Position - delta, recordSafe: false);
            for (int update = 0; update < 29; update++)
                _entities.Update(1.0 / 60.0, _player);
            FailIf(npc.FacingVector != expected, "Tokay facing cooldown expired before 30 updates.");
            _entities.Update(1.0 / 60.0, _player);
            FailIf(npc.FacingVector != -expected, "Tokay facing cooldown did not expire on update 30.");
        }

        LoadValidationRoom(2, 0xe5);
        TokayCharacter[] statues = _entities.Entities<TokayCharacter>()
            .Where(npc => npc.Record.SubId is >= 0x1a and <= 0x1c).ToArray();
        FailIf(statues.Length != 3 || statues.Single(npc => npc.Record.SubId == 0x1a).Record.Palette != 2 ||
            statues.Single(npc => npc.Record.SubId == 0x1c).Accessory is null,
            "Wild Tokay museum lost its red statue or held-meat accessory.");
        var poses = statues.Select(npc => (npc.CurrentAnimationFrame, npc.CurrentAnimationPixelHash)).ToArray();
        for (int update = 0; update < 120; update++)
            _entities.Update(1.0 / 60.0, _player);
        FailIf(!statues.Select(npc => (npc.CurrentAnimationFrame, npc.CurrentAnimationPixelHash)).SequenceEqual(poses),
            "Wild Tokay museum statues animated autonomously.");

        _inventory.GiveTreasure(0x4f, 0);
        _saveData.SetRoomFlag(1, 0xba, OracleSaveData.RoomFlag80, value: false);
        LoadValidationRoom(1, 0xba);
        TokayEyeballSlotRoomEntity slot = _entities.Entities<TokayEyeballSlotRoomEntity>().Single();
        _player.WarpTo(slot.Position + new Vector2(5, 11), recordSafe: false);
        _player.Face(Vector2I.Up);
        _entities.Update(1.0 / 60.0, _player);
        for (int update = 0; update < 12; update++)
        {
            Input.BeginOriginalUpdate(new ApplicationInputSnapshot(["item"], [], Vector2.Up));
            try
            {
                slot.UpdatePushAttempt(_player.Position, Vector2I.Up, Vector2I.Up);
                _entities.Update(1.0 / 60.0, _player);
            }
            finally { Input.EndOriginalUpdate(); }
        }
        FailIf(slot.PushCounter != 9 || slot.State != TokayEyeballSlotState.Waiting,
            "Holding B did not reset the Eyeball push counter before decrementing.");
        for (int update = 0; update < 8; update++)
        {
            slot.UpdatePushAttempt(_player.Position, Vector2I.Up, Vector2I.Up);
            _entities.Update(1.0 / 60.0, _player);
        }
        FailIf(slot.PushCounter != 1, "Eyeball centering excluded the source +$05 endpoint.");
        _player.BeginCutsceneControl();
        slot.UpdatePushAttempt(_player.Position, Vector2I.Up, Vector2I.Up);
        _entities.Update(1.0 / 60.0, _player);
        FailIf(slot.State != TokayEyeballSlotState.Waiting || slot.PushCounter != 10,
            "Eyeball insertion ignored disabled Link collisions on counter zero.");
        _player.EndCutsceneControl();
        _player.WarpTo(slot.Position + Vector2.Down * 11, recordSafe: false);
        FailIf(!slot.TryInteract(_player), "Eyeball socket omitted pirateSubid4Script's A-button path.");
        _entities.Update(1.0 / 60.0, _player);
        FailIf(slot.State != TokayEyeballSlotState.EyeWait ||
            !_saveData.HasRoomFlag(1, 0xba, OracleSaveData.RoomFlag80),
            "Eyeball A-button path did not start the common insertion script.");
        GD.Print("Validated native Tokay facing ties/cooldown, stationary museum visuals, and Eyeball A/B, centering, collision and reset/decrement boundaries.");
    }
}
