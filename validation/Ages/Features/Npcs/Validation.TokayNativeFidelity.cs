using Godot;
using System;
using System.Linq;

namespace Oracles;

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
        _inventory.LoseTreasure(TreasureId.Shovel);
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
        FailIf(shovel.ParentOffset != new Vector2(-9, 0) || shovel.ZIndex != ObjectDrawPriority.FixedLowPriorityZIndex,
            "Rosa's shovel did not move to -$09 with priority $83.");
        digging.Cancel();
        _dialogue.Close();

        // Linked Rosa escape runs through ValidateTokayEscapePublicationRom.

        // Vine proximity, script and reaction are covered by
        // ValidateTokayVineScriptRom and the admission regression.

        // Shield handoffs run through ValidateTokayShieldScriptRom.

        var theft = new TokayTheftEventDatabase();
        foreach (int treasure in theft.Record.StolenItems.Where(item => item != TreasureId.Shield))
            _inventory.GiveTreasure(treasure, 1);
        _inventory.LoseTreasure(TreasureId.Feather);
        _saveData.SetRoomFlag(5, 0xca, OracleSaveData.RoomFlag40);
        LoadValidationRoom(5, 0xca);
        TokayHoldingItemCharacter returned = _entities.Entities<TokayHoldingItemCharacter>().Single();
        _inventory.GiveTreasure(TreasureId.Feather, 1);
        FailIf(returned.ReturnedItemDialogue != 0x0a0c,
            "Returned holder recomputed its initialization snapshot without leaving.");
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
            _inventory.GiveTreasure(TreasureId.Shield, level);
            _inventory.LoseTreasure(TreasureId.Shield);
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
            FailIf(!_inventory.HasTreasure(TreasureId.Shield) ||
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
        _entities.Update(1.0 / 60.0, _player);
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

        _inventory.GiveTreasure(TreasureId.TokayEyeball, 0);
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
