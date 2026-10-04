using Godot;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGalePromptFramesRom() => RunMapPromptFramesRom(true);
    private void ValidateMapAreaTextFramesRom() => RunMapPromptFramesRom(false);
    private void ValidateMapConditionalTextFramesRom() => RunMapPromptFramesRom(false, true);

    private void RunMapPromptFramesRom(bool gale, bool conditional = false)
    {
        var destinations = new List<(int Group, int Room, int Kind, bool Enabled, int ExpectedText)>();
        if (conditional)
        {
            foreach (int group in new[] { 0, 1 })
            foreach (int kind in new[] { 0, 1, 2 })
            foreach (bool enabled in new[] { false, true })
            {
                // Independent bank2 mapGetRoomText/table expectations:
                // Maku advice, dungeon 0/12 entrance, Moblin/Advance Shop.
                int room = kind == 0 ? 0x38 : kind == 1 ? group == 0 ? 0x48 : 0x3c :
                    group == 0 ? 0x09 : 0x58;
                int text = kind == 0 ? enabled ? group == 0 ? 0x054f : 0x05d6 : 0x0323 + group :
                    kind == 1 ? enabled ? group == 0 ? 0x0200 : 0x020c : group == 0 ? 0x0307 : 0x0306 :
                    group == 0 ? enabled ? 0x0317 : 0x0318 : enabled ? 0x0325 : 0x0326;
                destinations.Add((group, room, kind, enabled, text));
            }
        }
        else
            foreach (var destination in new (int Group, int Room)[] { (0, 0x13), (0, 0xc1), (1, 0x08), (1, 0x80) })
                destinations.Add((destination.Group, destination.Room, -1, false, -1));
        int hostCase1 = 0;
        foreach (int speed in new[] { 0, 1, 2, 3, 4 })
        foreach (var destination in destinations)
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            // Text speed is owned by the shared printer. Exercise all speeds
            // on one message; every source/placement branch prints at speed 4.
            if (speed != 4 && destination != destinations[0]) continue;
            var save = OracleSaveData.CreateStandardGame();
            save.SetGlobalFlag(GlobalFlag.IntroDone); save.SetTextSpeed(speed);
            if (conditional)
            {
                save.SetGlobalFlag(destination.Group == 0 ? GlobalFlag.MakuGivesAdviceFromPresentMap :
                    GlobalFlag.MakuGivesAdviceFromPastMap, destination.Kind == 0 && destination.Enabled);
                save.SetMakuMapTextPresent(0x4f); save.SetMakuMapTextPast(0xd6);
                save.SetRoomFlag(destination.Group == 0 ? 4 : 5, destination.Group == 0 ? 0x04 : 0x44,
                    OracleSaveData.RoomFlagVisited, destination.Kind == 1 && destination.Enabled);
                save.SetGlobalFlag(GlobalFlag.MoblinsKeepDestroyed, destination.Kind == 2 && destination.Enabled);
                save.SetRoomFlag(1, 0xfe, OracleSaveData.RoomFlagVisited, destination.Kind == 2 && destination.Enabled);
            }
            InitializeTransientSession(save);
            var application = new ApplicationValidationFixture(this);
            foreach (int room in new[] { 0xac, 0x13, 0x08, 0x25, 0x2d, 0x78, 0x80, 0xc1 })
                _saveData.SetRoomFlag(destination.Group, room, 0xff, false);
            _saveData.SetRoomFlag(destination.Group, destination.Room, OracleSaveData.RoomFlagVisited);
            LoadValidationRoom(destination.Group, gale || conditional ? 0x33 : destination.Room); _entities.Clear();
            _player.WarpTo(new Vector2(80, 64));
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.EnableVramDmaTransfers(); rom.LoadHudGraphics();
            if (gale) { _player.BeginGale(); _mapMenu.OpenGale(); }
            else _mapMenu.BeginOpeningForValidation();
            application.Step(22, Vector2.Zero, [], [], batched);
            // Explicit full-white menu entry. The other Gale scenarios cover
            // actual item capture and the original cutscene16 caller handoff.
            int menu = gale ? 5 : 2;
            rom.OpenImmediately(menu);
            for (int frame = 0; frame < 11; frame++) rom.Update(0, 0, frame);
            rom.ClearSounds();
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            string context = $"Map text gale={gale} ${destination.Group:x1}:${destination.Room:x2} conditional={destination.Kind}/{destination.Enabled} speed={speed} batch={batched}";
            void Step(int count = 1, int pressed = 0)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette(); rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622));
                    rom.AdvanceText(); edge = 0; update++;
                    FailIf(_mapMenu.IsActive != (rom[0xcbcb] == menu) ||
                        _gameplayPause.IsLeased != (rom[0xcbcb] == menu), context + $" update={update}: modal ownership differs.");
                    if (_mapScreen.Visible && rom[0xcbcc] is 1 or 2)
                    {
                        using Image frame = _mapScreen.ComposeImage();
                        using Image panel = _dialogue.ComposeImage();
                        frame.BlendRect(panel, new Rect2I(0, 0, panel.GetWidth(), panel.GetHeight()),
                            new Vector2I((int)_dialogue.Position.X, (int)_dialogue.Position.Y));
                        CompareMenuFramePixelsRom(rom, frame, context + $" update={update} text=${rom.TextState:x2}/${rom[0xcba0]:x2} glyphs={_dialogue.VisibleGlyphCount} line=${rom.Text(0xd400):x2}/${rom.Text(0xd401):x2} timer={rom.Text(0xd0c5)} message='{_dialogue.CurrentMessage.Replace('\n', '|')}'");
                    }
                    FailIf(sounds.Requests.Count != rom.Sounds.Count,
                        context + $" update={update}: native prompt/menu sound count differs: runtime=[{string.Join(',', sounds.Requests)}], native=[{string.Join(',', rom.Sounds)}], message='{_dialogue.CurrentMessage.Replace('\n', '|')}', native index=${rom[0xcba3]:x2}${rom[0xcba2]:x2}, state=${rom.TextState:x2}, glyphs={_dialogue.VisibleGlyphCount}, line=${rom.Text(0xd400):x2}/${rom.Text(0xd401):x2}.");
                    for (int sound = 0; sound < sounds.Requests.Count; sound++)
                        FailIf(sounds.Requests[sound] != rom.Sounds[sound], context + ": prompt/menu sound order differs.");
                });
            }
            void WaitForChoice()
            {
                int began = update;
                while ((!_dialogue.ChoiceCursorVisible || rom.TextState != 2) && update - began < 600) Step();
                FailIf(!_dialogue.ChoiceCursorVisible || rom.TextState != 2, context + ": prompt did not become input-ready.");
            }
            if (!gale)
            {
                while (_mapScreen.CursorRoom != destination.Room)
                    Step(1, (_mapScreen.CursorRoom & 15) != (destination.Room & 15) ? 0x10 : 0x80);
                foreach (int close in conditional ? new[] { 1, 2 } : new[] { 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80 })
                {
                    Step(10); Step(1, 1);
                    if (conditional)
                        FailIf(!_mapScreen.TryGetSelectedAreaText(out MapText selected) ||
                            selected.TextId != destination.ExpectedText ||
                            ((rom[0xcba3] - 4) << 8 | rom[0xcba2]) != destination.ExpectedText,
                            context + $": expected source text ${destination.ExpectedText:x4}.");
                    int began = update;
                    // PrintingComplete denotes the non-exitable publication
                    // handoff, not an ordinary textbox's final input gate.
                    while (rom.TextState != 0x0f && update - began < 1800)
                    {
                        if (rom.TextState == 5) { Step(1, 1); Step(); }
                        else Step();
                    }
                    FailIf(rom.TextState != 0x0f,
                        context + $": area text did not reach its final exit gate: runtime={_dialogue.IsOpen}/{_dialogue.PrintingComplete}, native=${rom.TextState:x2}/${rom[0xcba0]:x2}, cursor=${rom[0xcbb6]:x2}, message='{_dialogue.CurrentMessage.Replace('\n', '|')}'.");
                    Step(1, close); Step(4);
                    FailIf(_dialogue.BlocksPlayerInput || rom[0xcba0] != 0 || !_mapMenu.IsOpen ||
                        _mapScreen.CursorRoom != destination.Room || rom[0xcbb6] != destination.Room,
                        context + $": closing text with ${close:x2} leaked a navigation/menu edge.");
                }
                Step(1, 2); Step(22);
                FailIf(_mapMenu.IsActive || _gameplayPause.IsLeased, context + ": repeated area text retained the closing menu lease.");
                continue;
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(40); Step(1, 1); WaitForChoice(); // TX_0300: Yes/Cancel.
                Step(1, 2); Step(); Step(1, 1); Step(5);
                FailIf(_mapMenu.GaleState != 1 || rom[0xcbcd] != 1 || _dialogue.BlocksPlayerInput,
                    context + ": Cancel did not restore tree navigation.");
                Step(1, 2); WaitForChoice(); // TX_0301: Reselect/Go back.
                Step(1, 1); Step(5);
                FailIf(_mapMenu.GaleState != 1 || rom[0xcbcd] != 1 || _dialogue.BlocksPlayerInput,
                    context + ": Reselect did not restore tree navigation.");
            }
            Step(1, 2); WaitForChoice(); Step(1, 2); Step(); Step(1, 1); Step(27);
            FailIf(_mapMenu.IsActive || _gameplayPause.IsLeased, context + ": Go back did not release menu ownership.");
        }
        GD.Print(conditional
            ? "Validated clean-US conditional map-text frames: both eras, Maku advice/formatting, dungeon entrance/fallback, Moblin Keep, Advance Shop fallthrough, complete frames/cues, A/B close and repeat; representative all-speed/split-batched comparison."
            : gale
            ? "Validated clean-US Gale prompt frames: both eras/placements, choice cursor/No/Reselect/Go back, cue order, cancellation and repeat; representative all-speed/split-batched comparison."
            : "Validated clean-US area-text frames: both eras/placements, all eight exit buttons, text-to-navigation handoff, modal close and repeat; representative all-speed/split-batched comparison.");
    }
}
