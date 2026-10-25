using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMakuLinkedRevealActorsRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true); _saveData.SetMakuTreeState(0x0e);
            // scriptHelper.s:interaction6b_layoutSwapMakuTreeRooms selects the restored present layout.
            _saveData.SetRoomFlag(1, 0x48, 0x01);
            LoadValidationRoom(0, 0x38); _player.WarpTo(new(0x50, 0x68));
            CompareMakuRestoredBackgroundRom("before reveal");
            var display = new FrontendRom { VramDmaTransfersEnabled = true };
            display.LoadHudGraphics();
            foreach (int entry in new[] { 0x6f9e, 0x6fb0 })
            {
                display[0xc48a] = 0x0f; display[0xc48c] = 0xf0;
                for (int offset = 0; offset < 0x80; offset++) display.SetBankByte(2, 0xdf80 + offset, 0x55);
                display.Call(entry, 3); // Linked state2 / returning state6 use the same func_6fb0 display tail.
                FailIf(display[0xcbe7] != 0xff || display[0xc48a] != 0 || display[0xc48c] != 0,
                    $"Native Maku display entry ${entry:x4} must hide HUD and move the room BG to scanline $00.");
                for (int offset = 0; offset < 0x80; offset++)
                    FailIf(display.BankByte(2, 0xdf80 + offset) != 0,
                        $"Native Maku display entry ${entry:x4} must clear fading BG/OBJ palette byte ${0xdf80 + offset:x4} to black.");
            }
            var seed = _roomEvents.Get<MakuSeedEvent>();
            SomariaRom? native = null;
            int scene = 0, generation = 0, updates = 0, compared = 0;
            for (; updates < 2600 && _saveData.MakuTreeState != 0x0f;)
            {
                StepGameplayUpdates(batched ? 4 : 1, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    updates++;
                    if (seed.NativeState is 3 or 4 or 7 or 8)
                        FailIf(_hud.Visible || !_transitions.NativeRoomUsesFullScreen || !_hud.StatusBarHidden || _hud.HiddenStatusBarColorForValidation != Colors.Black,
                            $"Linked Maku reveal update{updates} must present the native black room BG across the status strip.");
                    int selected = seed.NativeState switch { 3 => 1, 5 => 2, 7 => 3, _ => scene };
                    bool initializedScene = selected != scene;
                    if (initializedScene)
                    {
                        scene = selected;
                        native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x50, 0x68);
                        native[0xcfc0] = (byte)(scene == 1 ? 2 : 3);
                        if (scene == 2)
                        {
                            int[] ids = [0x3c, 0x3a, 0x43, 0xad, 0x31], subids = [0x0a, 0x0a, 4, 5, 6];
                            int[] ys = [0x48, 0x48, 0x28, 0xe8, 0xe8], xs = [0x48, 0x38, 0x78, 0x58, 0x68];
                            for (int index = 0; index < ids.Length; index++)
                            {
                                int at = 0xd240 + index * 256;
                                native[at] = 1; native[at + 1] = (byte)ids[index]; native[at + 2] = (byte)subids[index];
                                native[at + 11] = (byte)ys[index]; native[at + 13] = (byte)xs[index];
                            }
                        }
                        else { native[0xd240] = 1; native[0xd241] = 0x93; native[0xd242] = (byte)(scene == 1 ? 4 : 6); }
                        generation = native.TextGeneration;
                    }
                    if (native is not null && seed.NativeState is >= 3 and <= 7)
                    {
                        native[0xc4ab] = (byte)_transitions.NativePaletteThreadMode;
                        native[AgesWramAddress.wDisabledObjects] = _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects);
                        native[AgesWramAddress.wScrollMode] = _runtimeState.ReadWramByte(AgesWramAddress.wScrollMode);
                        native.AdvanceInteractions(_entities.FrameCounter); compared++;
                        foreach (var actor in _entities.Entities<NpcCharacter>().Where(actor => actor.Record.Id is 0x93 or 0x3c or 0x3a or 0x43 or 0xad or 0x31))
                        {
                            int at = 0xd040 + _entities.InteractionSlot(actor) * 256;
                            if (_transitions.NativeRoomUsesFullScreen)
                                CompareFullscreenActorPositionRom(native, actor, at, $"Linked Maku reveal scene{scene} update{updates}");
                            FailIf(native[at] == 0 || actor.Position != new Vector2(native[at + 13], native[at + 11]) ||
                                actor.ScriptDrawOffset.Y != unchecked((sbyte)native[at + 15]) ||
                                actor.CurrentAnimationCounter != native[at + 0x20] ||
                                actor.Visible != ((native[at + 0x1a] & 0x80) != 0),
                                $"Linked reveal scene{scene} update{updates} actor${actor.Record.Id:x2}:${actor.Record.SubId:x2} differs from clean US: XY={actor.Position}/({native[at + 13]},{native[at + 11]}), Z={actor.ScriptDrawOffset.Y}/{unchecked((sbyte)native[at + 15])}, anim={actor.CurrentAnimationCounter}/{native[at + 0x20]}, visible={actor.Visible}/${native[at + 0x1a]:x2}.");
                            if (scene == 2 && actor.Record.Id is 0x3a or 0x43 &&
                                (initializedScene || native.TextGeneration != generation))
                            {
                                // PALH_a2 / paletteData44e8, independently pinned from the source.
                                FailIf((native[at + 0x1c] & 7) != 6 || actor.Record.Palette != 6 ||
                                    !actor.CurrentAnimationUsesColor(new(17/31f, 17/31f, 25/31f)) ||
                                    !actor.CurrentAnimationUsesColor(new(27/31f, 27/31f, 31/31f)),
                                    $"Linked reveal actor${actor.Record.Id:x2} must render the PALH_a2 stone colors at initialization and during dialogue.");
                            }
                        }
                        FailIf(native[0xcfd1] != _runtimeState.ReadWramByte(0xcfd1) ||
                            scene == 2 && native[0xcfdf] != _runtimeState.ReadWramByte(0xcfdf),
                            $"Linked reveal scene{scene} update{updates} changed its native shared signal boundary.");
                        if (native.TextGeneration != generation)
                        {
                            FailIf(!_dialogue.IsOpen || native.Word(0xcba2) != _dialogue.SourceTextId + 0x400,
                                $"Linked reveal scene{scene} update{updates} lost its native text boundary: native=${native.Word(0xcba2):x4}, port={_dialogue.SourceTextId}, open={_dialogue.IsOpen}, state={seed.NativeState}.");
                            generation = native.TextGeneration;
                        }
                    }
                    if (_dialogue.IsOpen) { _dialogue.Close(); if (native is not null) native[0xcba0] = 0; }
                });
            }
            FailIf(scene != 3 || compared < 500 || _saveData.MakuTreeState != 0x0f,
                "Linked Maku reveal must compare all three actor sets through the real gameplay loop and return to normal advice.");
            StepGameplayUpdates(4, Vector2.Zero, batched: batched);
            CompareMakuRestoredBackgroundRom("after reveal");
            LoadValidationRoom(0, 0x48); LoadValidationRoom(0, 0x38);
            CompareMakuRestoredBackgroundRom("re-entry");
        }
        GD.Print("Validated linked Maku reveal actors and text boundaries against clean-US execution, plus restored upper-right background pixels before/after/re-entry with split/batched gameplay.");
    }

    private void CompareMakuRestoredBackgroundRom(string phase)
    {
        var native = new FrontendRom { VramDmaTransfersEnabled = true };
        for (int address = 0xc5b0; address < 0xcb00; address++) native[address] = _saveData.ReadWramByte(address);
        native[0xcc2f] = native[0xcc30] = 0x38; native[0xcd00] = 1;
        native.LoadRoomTileset();
        var menu = new MenuRom(native); menu.LoadHudGraphics(); menu.LoadRoomMappings(); menu.LoadRoomGraphics();
        native.Call(0x38dc, 0); // loadRoomLayout, using the ROM's own tileset override and layout bytes.
        FailIf(native[0xcf28] != 0x7e || native[0xcf29] != 0x11 || native[0xcf38] != 0x7f || native[0xcf39] != 0x14,
            "Late Maku setup must select restored room 3:38, including the $11/$14 sign, via room 1:48 flag $01.");
        using var image = _currentRoom.Texture.GetImage();
        for (int y = 32; y < 64; y++) for (int x = 128; x < 160; x++)
        {
            int metatile = native[0xcf00 + y / 16 * 16 + x / 16], quarter = y / 8 % 2 * 2 + x / 8 % 2;
            int tile = native.BankByte(3, 0xd000 + metatile * 8 + quarter);
            int flags = native.BankByte(3, 0xd004 + metatile * 8 + quarter);
            FailIf(_currentRoom.GetBackgroundSubtileForValidation(x / 8, y / 8) != tile ||
                _currentRoom.GetBackgroundAttributeForValidation(x / 8, y / 8) != flags,
                $"Maku 0:38 {phase} mapping({x / 8},{y / 8}) differs from clean-US restored metatile${metatile:x2}.");
            int source = 0x9000 + unchecked((sbyte)tile) * 16, bank = (flags >> 3) & 1;
            int sx = (flags & 0x20) == 0 ? x % 8 : 7 - x % 8, sy = (flags & 0x40) == 0 ? y % 8 : 7 - y % 8;
            int shade = ((native.VramByte(bank, source + sy * 2) >> (7 - sx)) & 1) |
                (((native.VramByte(bank, source + sy * 2 + 1) >> (7 - sx)) & 1) << 1);
            var color = image.GetPixel(x, y);
            int rgb = Mathf.RoundToInt(color.R * 31) | Mathf.RoundToInt(color.G * 31) << 5 | Mathf.RoundToInt(color.B * 31) << 10;
            FailIf(color.A != 1 || rgb != menu.HudColor(false, flags & 7, shade),
                $"Maku 0:38 {phase} pixel({x},{y}) tile${tile:x2}/attr${flags:x2} differs from clean-US graphics/palettes.");
        }
    }
}
