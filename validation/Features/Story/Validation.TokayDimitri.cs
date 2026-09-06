using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayDimitriDeparture()
    {
        void BeginDeparture()
        {
            _saveData.WriteWramByte(0xc647, 1);
            _inventory.GiveTreasure(TreasureDatabase.TreasureEssence, 2);
            _inventory.GiveTreasure(TreasureDatabase.TreasureEmberSeeds, 0x20);
            LoadValidationRoom(0, 0xaa);
            NpcCharacter first = _entities.Entities<NpcCharacter>().Single(npc => npc.Record is { Id: 0x48, SubId: 0x0f });
            FailIf(!_roomEvents.TokayDimitri.TryInteractNpc(first), "Tokay $48:$0f refused the Ember Seed trade.");
            _dialogue.Close();
            StepRoomEventFrames(1);
            FailIf(!_dialogue.ChoiceActive, "Tokay Ember Seed trade did not offer TX_0a20.");
            _dialogue.SubmitChoiceForValidation(0);
            StepRoomEventFrames(1);
            _dialogue.Close();
            StepRoomEventFrames(31);
            FailIf(!_dialogue.IsOpen, "Tokay trade did not reach TX_0a24 after its 30-update wait.");
            _dialogue.Close();
            StepRoomEventFrames(61);
            FailIf(!_dialogue.IsOpen, "Tokay trade did not reach TX_0a25 after its 60-update wait.");
            _dialogue.Close();
            StepRoomEventFrames(1);
        }

        BeginDeparture();
        NpcCharacter first = _entities.Entities<NpcCharacter>().Single(npc => npc.Record is { Id: 0x48, SubId: 0x0f });
        NpcCharacter second = _entities.Entities<NpcCharacter>().Single(npc => npc.Record is { Id: 0x48, SubId: 0x10 });
        FailIf(_roomEvents.TokayDimitri.Stage != TokayDimitriStage.Departing ||
            first.Position != new Vector2(0x18, 0x48) || second.Position != new Vector2(0x38, 0x58) ||
            !first.Visible || !second.Visible || (_saveData.ReadWramByte(0xc647) & 2) != 0,
            "Closing TX_0a25 must initialize both moveleft counters without moving/deleting actors or completing the rescue.");
        StepRoomEventFrames(1);
        FailIf(first.Position.X != 0x16 || second.Position.X != 0x36,
            "Tokay moveleft did not apply SPEED_200 on the first nonzero decrement.");
        StepRoomEventFrames(14);
        FailIf(first.Position.X != 0xfa || first.ScriptDrawOffset.X != -256 ||
            first.Position.X + first.ScriptDrawOffset.X != -6 || !first.Visible ||
            second.Position.X != 0x1a || first.CurrentAnimationFrame != 0 || second.CurrentAnimationFrame != 0,
            "Tokay $48:$0f did not retain its pose and render partially off the left edge after 15 moves.");
        StepRoomEventFrames(1);
        FailIf(!first.Active || first.Position.X != 0xfa,
            "Tokay $48:$0f moved/deleted on counter2's zero update.");
        StepRoomEventFrames(1);
        FailIf(first.Active || !second.Active || second.Position.X != 0x16 ||
            _roomEvents.TokayDimitri.MenusDisabled || !_player.CutsceneControlled ||
            (_saveData.ReadWramByte(0xc647) & 2) != 0,
            "First Tokay scriptend must delete only $48:$0f and enable menus while $48:$10 continues.");
        StepRoomEventFrames(14);
        FailIf(!second.Visible || second.Position.X != 0xfa || second.ScriptDrawOffset.X != -256,
            "Second Tokay did not retain its left-edge sprite after 31 moves.");
        StepRoomEventFrames(1);
        FailIf(!second.Active || (_saveData.ReadWramByte(0xc647) & 2) != 0,
            "Tokay $48:$10 completed the rescue on counter2's zero update.");
        StepRoomEventFrames(1);
        FailIf(second.Active || _roomEvents.TokayDimitri.Stage != TokayDimitriStage.Inactive || _player.CutsceneControlled ||
            (_saveData.ReadWramByte(0xc647) & 2) == 0,
            "Second Tokay scriptend did not set wDimitriState bit 1, delete $48:$10, and release Link.");
        LoadValidationRoom(0, 0xaa);
        FailIf(_entities.Entities<NpcCharacter>().Any(npc => npc.Record.Id == 0x48 &&
            npc.Record.SubId is 0x0f or 0x10 && npc.Active), "Completed rescue respawned the two Tokays.");

        BeginDeparture();
        StepRoomEventFrames(4);
        _roomEvents.TokayDimitri.Cancel();
        FailIf(_roomEvents.TokayDimitri.HasState || _player.CutsceneControlled ||
            (_saveData.ReadWramByte(0xc647) & 2) != 0 ||
            _entities.Entities<TokayRescueEmberRoomEntity>().Any(effect => !effect.Finished),
            "Cancelling a partial Tokay departure retained input ownership or marked the rescue complete.");
        LoadValidationRoom(0, 0xaa);
        FailIf(_entities.Entities<NpcCharacter>().Count(npc => npc.Record.Id == 0x48 &&
            npc.Record.SubId is 0x0f or 0x10 && npc.Active) != 2,
            "Cancelled Tokay departure failed to reconstruct both source placements.");
        GD.Print("Validated Tokay Ember Seed trade departure: SPEED_200, separate $10/$20 counters, " +
            "left-edge OAM wrap, zero-update yields, source deletion/menu/flag order, cancellation and re-entry.");
    }

    private void ValidateTokayDimitriScrollEntry()
    {
        const double update = 1.0 / 60.0;
        _saveData.WriteWramByte(0xc647, 0);
        _inventory.GiveTreasure(TreasureDatabase.TreasureEssence, 2);
        _saveData.SetRoomFlag(0, 0xba, OracleSaveData.RoomFlag40);
        LoadValidationRoom(0, 0xba);
        _player.WarpTo(new Vector2(0x18, 4));
        _player.UpdatePushingState(Vector2.Up);
        CheckRoomExit(_player);
        FailIf(!_transitions.ScrollActive || _rooms.CurrentRoom.Id != 0xaa,
            "Post-D3 room 0:ba did not begin its north scroll into 0:aa.");
        FailIf(_dialogue.IsOpen || _player.CutsceneControlled,
            "Room 0:aa Tokay $48:$0f opened TX_0a1d or locked Link during preload.");

        for (int frame = 0; frame < 80 && _transitions.ScrollActive; frame++)
        {
            _entities.Update(update, _player);
            _roomEvents.Update(update);
            FailIf(_dialogue.IsOpen || _player.CutsceneControlled,
                "Room 0:aa Tokay introduction advanced during the screen scroll.");
            _transitions.Update(update);
        }
        FailIf(_transitions.IsTransitioning || _dialogue.IsOpen,
            "Room 0:aa scroll did not finish before the first Tokay script update.");
        StepRoomEventFrames(1);
        FailIf(!_dialogue.IsOpen || !_player.CutsceneControlled ||
            _dialogue.Position.Y != 24 ||
            !_dialogue.CurrentMessage.Contains("This huge fish"),
            "Room 0:aa did not show top TX_0a1d using Link's south-edge " +
            $"destination position after scrolling: open={_dialogue.IsOpen}, " +
            $"locked={_player.CutsceneControlled}, box={_dialogue.Position}, " +
            $"Link={_player.Position}, text={_dialogue.CurrentMessage}.");
        _dialogue.Close();
        StepRoomEventFrames(30);
        FailIf(_dialogue.IsOpen,
            "Room 0:aa Tokay $48:$10 opened TX_0a1e before its 30-update wait.");
        StepRoomEventFrames(1);
        FailIf(!_dialogue.IsOpen || _dialogue.Position.Y != 24 ||
            !_dialogue.CurrentMessage.Contains("Red fish"),
            "Room 0:aa did not show top TX_0a1e after its 30-update wait.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(_roomEvents.Active || _player.CutsceneControlled,
            "Room 0:aa Tokay introduction did not release input after TX_0a1e.");
        StepRoomEventFrames(1);
        var dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        FailIf(!_dialogue.IsOpen || dimitri.Phase != DimitriPhase.IntroDialogue ||
            _dialogue.Position.Y != 24 || dimitri.Position != new Vector2(0x30, 0x48) ||
            dimitri.AnimationIndex != 0x24,
            "Room 0:aa lacks Dimitri's preset, source pose, or post-Tokay TX_2100.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf((_saveData.ReadWramByte(0xc647) & 1) == 0,
            "Dimitri did not remember the completed introduction in wDimitriState bit 0.");
        GD.Print("Validated room 0:ba -> 0:aa post-D3 Tokay scroll freeze, " +
            "destination-relative top dialogue, 30-update wait, and input release.");
    }

    private void ValidateTokayRescueEmberEffects()
    {
        _saveData.WriteWramByte(0xc647, 1);
        _inventory.GiveTreasure(TreasureDatabase.TreasureEssence, 2);
        _inventory.GiveTreasure(TreasureDatabase.TreasureEmberSeeds, 0x20);
        LoadValidationRoom(0, 0xaa);
        NpcCharacter tokay = _entities.Entities<NpcCharacter>().Single(npc => npc.Record is { Id: 0x48, SubId: 0x0f });
        FailIf(!_roomEvents.TokayDimitri.TryInteractNpc(tokay), "Tokay Ember Seed effect test could not start the trade.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        _dialogue.SubmitChoiceForValidation(0);
        StepRoomEventFrames(1);
        _dialogue.Close();
        StepRoomEventFrames(1);
        var effects = _entities.Entities<TokayRescueEmberRoomEntity>().ToArray();
        bool HasOpaquePixels(Texture2D texture)
        {
            Image image = texture.GetImage();
            return Enumerable.Range(0, image.GetHeight()).Any(y =>
                Enumerable.Range(0, image.GetWidth()).Any(x => image.GetPixel(x, y).A > 0));
        }
        FailIf(effects.Length != 2 || effects[0].Position != new Vector2(0x18, 0x48) ||
            effects[1].Position != new Vector2(0x38, 0x58) ||
            effects.Any(effect => effect.Phase != 1 || !effect.Visible || effect.ZFixed != 0 ||
                !HasOpaquePixels(effect.CurrentTexture)),
            "Tokay $48:$0f did not spawn two ordered $8f seed actors on its acceptance update.");
        StepRoomEventFrames(16);
        FailIf(effects.Any(effect => effect.ZFixed != -2176 || !effect.Visible),
            "Tokay $8f seed hop did not preserve -$100 speedZ/$10 gravity and signed 8.8 height.");
        StepRoomEventFrames(14);
        FailIf(!_dialogue.IsOpen || effects.Any(effect => effect.Phase != 1),
            "Tokay $8f seed arc should still be airborne when TX_0a24 opens.");
        StepRoomEventFrames(3);
        FailIf(effects.Any(effect => effect.Phase != 2 || effect.Visible || effect.ZFixed != 0),
            "Tokay $8f did not land, hide, and observe the dialogue while ordinary objects are frozen.");
        StepRoomEventFrames(10);
        FailIf(effects.Any(effect => effect.Phase != 2 || effect.Visible),
            "Tokay flames started before TX_0a24 closed.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(effects.Any(effect => effect.Phase != 3 || !effect.Visible || effect.Counter != 58 ||
            !HasOpaquePixels(effect.CurrentTexture)),
            "Closing TX_0a24 did not select the source $0b flame animation with a fresh 58-update counter.");
        byte[] firstFlame = effects[0].CurrentTexture.GetImage().GetData();
        bool animated = false;
        for (int frame = 0; frame < 57; frame++)
        {
            StepRoomEventFrames(1);
            animated |= !firstFlame.SequenceEqual(effects[0].CurrentTexture.GetImage().GetData());
        }
        FailIf(!animated || effects.Any(effect => !effect.Visible || effect.Counter != 1),
            "Tokay flame graphics did not animate for the full pre-zero lifetime.");
        StepRoomEventFrames(1);
        FailIf(effects.Any(effect => !effect.Finished || effect.Visible),
            "Tokay flames did not delete on the 58th animation/counter update.");
        StepRoomEventFrames(1);
        FailIf(_entities.Entities<TokayRescueEmberRoomEntity>().Any(),
            "Finished Tokay flames remained in the room entity owner.");

        // A bit-7 flame continues animating during any subsequent textbox.
        var record = new TokayRescueEmberDatabase().Record;
        var standalone = _entities.Spawn<TokayRescueEmberRoomEntity>(new TokayRescueEmberSpawn(record, new Vector2(24, 72)));
        standalone.UpdateNative(false);
        for (int frame = 0; frame < 33; frame++) standalone.UpdateNative(true);
        standalone.UpdateNative(false);
        for (int frame = 0; frame < 58; frame++) standalone.UpdateNative(true);
        FailIf(!standalone.Finished, "Tokay $8f flame timer froze under wTextIsActive.");
        _roomEvents.TokayDimitri.Cancel();
        GD.Print("Validated Tokay rescue $8f seed graphics/arc, ordered spawn updates, dialogue landing gate, " +
            "animated 58-update flames, always-update behavior, and room-owned cleanup.");
    }

    private void ValidateDimitriCompanion()
    {
        void StepInput(string action, Vector2 movement = default, int frames = 1)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                Input.BeginOriginalUpdate(new ApplicationInputSnapshot([action], frame == 0 ? [action] : [], movement));
                try { StepRoomEventFrames(1); }
                finally { Input.EndOriginalUpdate(); }
            }
        }
        CompanionRuntimeState.Clear(_runtimeState, CompanionRuntimeState.Read(_runtimeState).Id);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        _inventory.GiveTreasure(TreasureDatabase.TreasureEssence, 2);
        _saveData.WriteWramByte(0xc647, 3);
        LoadValidationRoom(0, 0xaa);
        var dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        var database = new DimitriDatabase();
        FailIf(database.Animations.Length != 40 || !database.CanSwallow(0x10) ||
            database.CanSwallow(0x13), "Dimitri source animations or mouth collision modes are incorrect.");
        _player.WarpTo(dimitri.Position + new Vector2(0, 16));
        _player.Face(Vector2I.Up);
        FailIf(!dimitri.TryInteract(_player) || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x2101)),
            "Rescued Dimitri did not offer his first-meeting TX_2101.");
        _dialogue.Close();
        for (int frame = 0; frame < 180 && !_dialogue.IsOpen; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(!_dialogue.IsOpen || !dimitri.LinkRiding || !_player.CompanionRideActive ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x2106)) ||
            (_saveData.ReadWramByte(0xc647) & 0x20) != 0,
            $"Dimitri rescue failed its mount/tutorial gate: {dimitri.Phase}, Link={_player.Position}.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf((_saveData.ReadWramByte(0xc647) & 0x20) == 0 ||
            !CompanionRuntimeState.IsActive(_runtimeState, 0x0c) ||
            _player.Position != dimitri.Position + new Vector2(0, -10),
            "Dimitri tutorial completion did not retain the companion slot and source Link offset.");

        Vector2 landPosition = dimitri.PrecisePosition;
        dimitri.SetScreenTransitionPosition(new Vector2(64, 16), Vector2.Zero, _player);
        StepRoomEventFrames(1);
        FailIf(!dimitri.InWater || dimitri.AnimationIndex != 6,
            "Dimitri did not select the down-facing swimming animation in room 0:aa's sea.");
        StepInput("item");
        FailIf(!dimitri.LinkRiding || dimitri.Phase != DimitriPhase.Riding,
            "Dimitri accepted a forbidden B dismount while swimming.");
        StepInput("move_right", Vector2.Right);
        FailIf(dimitri.PrecisePosition != new Vector2(64, 16),
            "Dimitri moved on the source angle-change update.");
        StepInput("move_right", Vector2.Right, 4);
        FailIf(dimitri.PrecisePosition != new Vector2(68, 16) ||
            _player.Position != dimitri.Position + new Vector2(-5, -10),
            $"Dimitri did not swim at SPEED_100 or use the right-facing Link offset: position={dimitri.PrecisePosition}, Link={_player.Position}, direction={dimitri.Direction}, phase={dimitri.Phase}, dialogue={_dialogue.IsOpen}, solid={_rooms.CurrentRoom.IsSolid(new Vector2(65, 24))}, tile={_rooms.CurrentRoom.GetMetatile(new Vector2(65, 24)):x2}.");
        dimitri.SetScreenTransitionPosition(landPosition, Vector2.Zero, _player);
        StepRoomEventFrames(1);
        FailIf(dimitri.InWater, "Dimitri did not return to his ground animation on leaving water.");

        StepInput("attack");
        FailIf(dimitri.Phase != DimitriPhase.Eating ||
            !_entities.Entities<DimitriMouthRoomEntity>().Any(),
            "Dimitri A did not create ITEM_DIMITRI_MOUTH $2b.");
        for (int frame = 0; frame < 80 && dimitri.Phase == DimitriPhase.Eating; frame++)
            StepRoomEventFrames(1);
        FailIf(dimitri.Phase != DimitriPhase.Riding || _entities.Entities<DimitriMouthRoomEntity>().Any(),
            "Dimitri bite did not retract and release its 12-update mouth item.");
        dimitri.SetScreenTransitionPosition(landPosition + new Vector2(-8, 0), Vector2.Zero, _player);
        StepRoomEventFrames(1);
        StepInput("item");
        for (int frame = 0; frame < 120 && dimitri.Phase == DimitriPhase.Dismounting; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(_player.CompanionRideActive || !CompanionRuntimeState.TryGetRemembered(
            _runtimeState, 0x0c, 0, 0xaa, out _), "Dimitri land dismount lost the remembered companion.");
        Vector2 rememberedPosition = dimitri.Position;
        LoadValidationRoom(0, 0xaa);
        dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        FailIf(dimitri.Phase != DimitriPhase.Waiting || dimitri.Position != rememberedPosition,
            "Dimitri preset did not preserve the remembered companion after wDimitriState bit $20.");
        _saveData.WriteWramByte(0xc647, 0x63);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        LoadValidationRoom(0, 0xaa);
        FailIf(_entities.Entities<DimitriCompanionRoomEntity>().Any(),
            "Dimitri preset ignored wDimitriState bit $40.");
        _saveData.WriteWramByte(0xc647, 0x23);
        OracleRoomData mainland = _world.LoadRoom(0, 0x98);
        Vector2 landing = (from y in Enumerable.Range(1, 6)
            from x in Enumerable.Range(1, 8).Reverse()
            select new Vector2(x * 16, y * 16))
            .First(point => mainland.GetTerrainInfo(point + new Vector2(0, 5)).Hazard == HazardType.None &&
                Enumerable.Range((int)point.X - 5, mainland.Width - (int)point.X + 5).All(x =>
                    !mainland.IsSolid(new Vector2(x, point.Y - 5)) &&
                    !mainland.IsSolid(new Vector2(x, point.Y + 8))));
        CompanionRuntimeState.Begin(_runtimeState, 0x0c, 0x98, landing, 1);
        LoadValidationRoom(0, 0x98);
        dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        for (int frame = 0; frame < 180 && !_dialogue.IsOpen; frame++)
        {
            _player._PhysicsProcess(1.0 / 60.0);
            StepRoomEventFrames(1);
        }
        FailIf(!_dialogue.IsOpen || _dialogue.CurrentMessage != DialogueBox.PlainText(database.Text(0x2104)) ||
            _player.CompanionRideActive || CompanionRuntimeState.ReadRemembered(_runtimeState).Id != 0,
            $"Mainland $71:$06 failed to dismount/forget Dimitri before TX_2104: {dimitri.Phase}.");
        _dialogue.Close();
        for (int frame = 0; frame < 240 && _entities.Entities<DimitriCompanionRoomEntity>().Any(); frame++)
            StepRoomEventFrames(1);
        FailIf(_entities.Entities<DimitriCompanionRoomEntity>().Any(),
            $"Dimitri's mainland departure did not finish walking offscreen: {dimitri.Phase}, {dimitri.Position}, landing={landing}.");
        GD.Print("Validated Dimitri source visuals, rescue mount/tutorial, eating item, land dismount, and preset re-entry predicates.");
    }
}
