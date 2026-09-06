using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDimitriForestRescue()
        => ValidateDimitriForestRescueRoute(linked: false);

    private void ValidateDimitriForestRescueLinked()
        => ValidateDimitriForestRescueRoute(linked: true);

    private void ValidateDimitriForestRescueRoute(bool linked)
    {
        _saveData.SetLinkedGame(linked);
        _saveData.SetGlobalFlag(0x22);
        _saveData.SetGlobalFlag(0x42);
        _saveData.SetGlobalFlag(0x23, false);
        _saveData.SetGlobalFlag(0x24, false);
        _saveData.WriteWramByte(0xc647, 0x60);
        _inventory.GiveTreasure(0x0e, 0x0c);
        LoadValidationRoom(0, 0x81);
        var dimitri = _entities.Entities<DimitriCompanionRoomEntity>().Single();
        FailIf(dimitri.PrecisePosition != new Vector2(0x50, 0x58),
            "Forest companion spawner $67:$04 did not use preset $58/$50.");
        _player.WarpTo(new Vector2(0x50, 0x68), recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(!dimitri.TryInteract(_player), "Forest Dimitri did not accept the source A-button handoff.");
        var messages = new List<string>();
        bool reward = false;
        for (int update = 0; update < 2400 && !_saveData.HasGlobalFlag(0x23); update++)
        {
            if (_dialogue.IsOpen)
            {
                messages.Add(_dialogue.CurrentMessage);
                var frozen = _roomEvents.CompanionForest.Flights.Select(flight =>
                    (flight.XFixed, flight.YFixed, flight.Angle, flight.Counter1, flight.Counter2)).ToArray();
                for (int paused = 0; paused < 12; paused++) _roomEvents.Update(1.0 / 60.0);
                FailIf(!frozen.SequenceEqual(_roomEvents.CompanionForest.Flights.Select(flight =>
                    (flight.XFixed, flight.YFixed, flight.Angle, flight.Counter1, flight.Counter2))),
                    "Companion forest fairy movement advanced while text was active.");
                if (_saveData.ReadWramByte(0xc6b5) == 2 && _player.IsHoldingItemTwoHands)
                {
                    reward = true;
                    FailIf((_saveData.ReadWramByte(0xc647) & 0x80) == 0 ||
                        !_entities.Entities<NpcCharacter>().Any(npc => npc.Name == "CompanionFluteReward" && npc.Visible),
                        "Dimitri flute reward lost its native companion bit or overhead graphic.");
                }
                _dialogue.Close();
            }
            if (IsTransitioning) UpdateRoomWarpTransition(1.0 / 60.0);
            else
            {
                _player.AdvanceApplicationUpdate();
                StepRoomEventFrames(1);
            }
        }
        FailIf(!_saveData.HasGlobalFlag(0x24) || !_saveData.HasGlobalFlag(0x23) ||
            _rooms.CurrentRoom.Id != 0x63 || !reward,
            $"Forest rescue/flute handoff failed: room 0:{_rooms.CurrentRoom.Id:x2}, command {_roomEvents.CompanionForest.Instruction}, signal {_roomEvents.CompanionForest.Signal}, messages {messages.Count}.");
        for (int update = 0; update < 180 && !_saveData.HasGlobalFlag(0x2b); update++)
        {
            if (_dialogue.IsOpen) _dialogue.Close();
            _player.AdvanceApplicationUpdate(); StepRoomEventFrames(1);
        }
        FailIf(!_saveData.HasGlobalFlag(0x2b) || !_player.CompanionRideActive ||
            _roomEvents.CompanionForest.MenusDisabled,
            "Dimitri flute script did not wait for mounting before unscrambling the forest and releasing menus.");
        var text = new CompanionForestDatabase();
        int[] expectedText = [0x113a, linked ? 0x113c : 0x113b, 0x112a, 0x112b, 0x112c, 0x112d, 0x112e, 0x112f, linked ? 0x113e : 0x113d, 0x006a, 0x1140];
        FailIf(!messages.SequenceEqual(expectedText.Select(id => DialogueBox.PlainText(text.Text(id)))),
            $"Dimitri forest rescue/reward diverged from its eleven source texts (observed {messages.Count}).");
        GD.Print("Validated forest Dimitri rescue, fairy signals, fade to 0:63, named flute/pose, companion flags and mount-gated forest unscrambling.");
    }

    private void ValidateDimitriForestEntry()
    {
        _inventory.GiveTreasure(0x0e, 0x0c);
        _saveData.SetGlobalFlag(0x22);
        _saveData.SetGlobalFlag(0x23, false);
        _saveData.SetGlobalFlag(0x42, false);
        _saveData.SetGlobalFlag(0x2b);
        LoadValidationRoom(0, 0x34);
        FailIf(_roomEvents.CompanionForest.HasState, "Forest $71:$08 triggered without a leftward scroll.");
        LoadValidationRoom(0, 0x35);
        _player.WarpTo(new Vector2(1, 0x48), recordSafe: false);
        _transitions.BeginScroll(_player, Vector2I.Left, 0x34);
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            FailIf(_dialogue.IsOpen || _roomEvents.CompanionForest.Flights.Count != 0,
                "Forest $71:$08 began dialogue or flight during scrolling.");
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        _player.WarpTo(new Vector2(0x50, 0x48), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(_dialogue.IsOpen || _roomEvents.CompanionForest.Flights.Count != 0,
            "Forest introduction ignored the Link.xh < $50 trigger.");
        _player.WarpTo(new Vector2(0x4f, 0x48), recordSafe: false);
        StepRoomEventFrames(1);
        FailIf(_roomEvents.CompanionForest.Flights.Count != 1,
            "Forest introduction did not spawn fairy $49:$03 preset $0f.");
        // Exercise the native circular flight separately from the text command
        // stream: $20 angle steps, two updates each, then one signal increment.
        var flight = _roomEvents.CompanionForest.Flights.Single();
        for (int frame = 0; frame < 500 && flight.Stage != (int)FairyFlightStage.Circle; frame++)
            flight.UpdateFrame(frame);
        FailIf(flight.Stage != (int)FairyFlightStage.Circle || _roomEvents.CompanionForest.Signal != 1,
            "Forest fairy did not signal arrival before beginning its circle.");
        for (int frame = 0; frame < 63; frame++) flight.UpdateFrame(frame);
        FailIf(_roomEvents.CompanionForest.Signal != 1,
            "Forest fairy circle completed before its 64th update.");
        flight.UpdateFrame(63);
        FailIf(_roomEvents.CompanionForest.Signal != 2 || flight.Stage != (int)FairyFlightStage.WaitForSignal,
            "Forest fairy circle did not signal exactly on update 64.");
        int choices = 0;
        for (int frame = 0; frame < 600 && !_saveData.HasGlobalFlag(0x42); frame++)
        {
            if (_dialogue.IsOpen)
            {
                if (_dialogue.CurrentMessage.Contains("Can you help"))
                    _dialogue.SubmitChoiceForValidation(choices++ == 0 ? 1 : 0);
                else _dialogue.Close();
            }
            StepRoomEventFrames(1);
        }
        FailIf(choices != 2 || !_saveData.HasGlobalFlag(0x42) || _saveData.HasGlobalFlag(0x2b) ||
            !_saveData.HasRoomFlag(0, 0x34, 0x40),
            "Forest introduction failed its repeat-choice loop or persistent lost/scrambled flags.");
        _roomEvents.CompanionForest.Cancel();
        FailIf(_roomEvents.CompanionForest.HasState || _roomEvents.CompanionForest.MenusDisabled,
            "Cancelling forest introduction retained actors, effects or menu ownership.");
        LoadValidationRoom(0, 0x63);
        _player.WarpTo(new Vector2(0x48, 0x7f), recordSafe: false);
        _transitions.BeginScroll(_player, Vector2I.Down, 0x73);
        for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
        {
            FailIf(_dialogue.IsOpen, "Forest $71:$0b guide opened text during scrolling.");
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
            _roomEvents.Update(1.0 / 60.0);
        }
        int guideTexts = 0;
        for (int frame = 0; frame < 700 && _roomEvents.CompanionForest.HasState; frame++)
        {
            if (_dialogue.IsOpen)
            {
                guideTexts++;
                FailIf(_dialogue.CurrentMessage != DialogueBox.PlainText(new CompanionForestDatabase().Text(0x1126)),
                    "Forest $71:$0b guide selected the wrong source text.");
                _dialogue.Close();
            }
            StepRoomEventFrames(1);
        }
        FailIf(guideTexts != 1 || _roomEvents.CompanionForest.HasState || _roomEvents.CompanionForest.MenusDisabled,
            "Forest guide did not wait for its fairy to depart before releasing control.");
        GD.Print("Validated forest entry direction, scrolling/text freeze, Link coordinate guard, 64-update circle and cancellation.");
    }
}
