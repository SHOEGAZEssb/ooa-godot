using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAncientTombPanelsRom()
    {
        foreach (int roomId in new[] { 0x8a, 0xa9 })
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, roomId);
            _entities.Clear(); _inventory.EquipA(0); _inventory.EquipB(0);
            var data = new AncientTombDatabase(); var visuals = new DungeonInteractionVisualDatabase();
            var records = data.GetRoomRecords(5, roomId);
            var panels = records.Where(r => r.Kind == DungeonObjectKind.StonePanel).Select(r =>
                new StonePanelRoomEntity(r, visuals.Visual(r.SubId == 0 ? "stone-panel-left" : "stone-panel-right"),
                    data.PanelPalette, _currentRoom, _saveData, _runtimeState, () => _entities.ActiveTriggers,
                    _entities.SetTrigger, _entities.OnNativeSoundRequested, _entities.NativeActiveMusicSource)).ToArray();
            foreach (var panel in panels) _entities.AddEntity(panel);
            var stairsRecord = records.Single(r => r.Kind == DungeonObjectKind.PanelStairs);
            int torches = 0;
            var stairs = new PanelStairsRoomEntity(stairsRecord, _saveData, () => _entities.ActiveTriggers,
                _entities.SetTrigger, () => torches, _entities.BeginScreenShake, _entities.OnNativeSoundRequested,
                _entities.TryCreatePuzzlePuff, (pos, tile) => { if (_rooms.TrySetTile(pos, tile)) _entities.OnRoomTileChanged(); });
            _entities.AddEntity(stairs);
            _player.WarpTo(new(120, 152)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "D8 panel fixture must start on the source south floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 152);
            rom.CreateMenuView().EnableVramDmaTransfers();
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcc35] = (byte)_sound.NativeActiveMusic;
            int[] slots = panels.Select(p => 0xd040 + _entities.InteractionSlot(p) * 256).ToArray();
            for (int i = 0; i < 2; i++)
            {
                int s = slots[i]; rom[s] = 1; rom[s + 1] = 0x7b; rom[s + 2] = (byte)i;
                rom[s + 11] = 88; rom[s + 13] = (byte)(112 + i * 16);
            }
            int stairSlot = 0xd040 + _entities.InteractionSlot(stairs) * 256;
            rom[stairSlot] = 1; rom[stairSlot + 1] = 0x90; rom[stairSlot + 2] = (byte)stairsRecord.SubId;
            rom[stairSlot + 11] = rom[stairSlot + 13] = 120;
            int update = 0;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1, bool move = false) => StepGameplayUpdates(count, move ? Vector2.Left : Vector2.Zero,
                batched: batch, afterUpdate: () => {
                    rom.UpdateGameplay(0, move ? 0x20 : 0, move ? 24 : 0xff, _entities.FrameCounter);
                    rom.AdvanceScreenShake();
                    rom.AdvanceTileGraphics(); update++;
                    CompareSomariaMotionRom(rom, $"Ancient Tomb panels update{update}");
                    for (int i = 0; i < 2; i++)
                    {
                        var p = panels[i]; int s = slots[i];
                        FailIf(p.State != rom[s + 4] || p.Substate != rom[s + 5] || p.Counter != rom[s + 6] ||
                            p.Position != new Vector2(rom.Word(s + 12) / 256f, rom.Word(s + 10) / 256f),
                            $"Stone panel${i:x2} update{update}: state/substate/counter/XY {p.State}/{p.Substate}/{p.Counter}/{p.Position}; ROM {rom[s + 4]}/{rom[s + 5]}/{rom[s + 6]}/{rom.Word(s + 12) / 256f},{rom.Word(s + 10) / 256f}.");
                    }
                    FailIf(_entities.ActiveTriggers != rom[0xcca0] || _saveData.GetRoomFlags(5, roomId) != rom[0xca00 + roomId] ||
                        _runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02],
                        $"Stone panels room${roomId:x2} update{update}: trigger {_entities.ActiveTriggers}/{rom[0xcca0]}, flags {_saveData.GetRoomFlags(5, roomId)}/{rom[0xca00+roomId]}, masks {_runtimeState.ReadWramByte(WramAddress.wDisabledObjects)}/{rom[0xcc8a]}, menu {_runtimeState.ReadWramByte(WramAddress.wMenuDisabled)}/{rom[0xcc02]}; stair state {stairs.State}/{rom[stairSlot+4]}.");
                    foreach (int pos in new[] { 0x46, 0x56, 0x66, 0x67, 0x68, 0x48, 0x58 })
                    {
                        Vector2 point = new((pos & 15) * 16 + 8, (pos >> 4) * 16 + 8);
                        FailIf(_currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + pos],
                            $"Stone panel collision${pos:x2} update{update} differs.");
                    }
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"Stone panel update{update}: ordered cues differ.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        $"Panel puzzle room${roomId:x2} update{update}: shake must preserve shared RNG draws.");
                });
            Step(2);
            FailIf(panels.Any(p => p.State != 1), "Both original panels must initialize closed.");
            if (roomId == 0xa9 && !batch)
                for (int i = 0; i < panels.Length; i++)
                    CompareStonePanelPixelsRom(rom, panels[i], slots[i]);
            if (roomId == 0x8a)
            {
                // Socket approach is covered separately. Here its persisted
                // four bits are declared inputs to the later stair slot.
                _saveData.SetRoomFlag(5, roomId, 0x0f); rom[0xca00 + roomId] |= 0x0f;
                Step();
                FailIf(stairs.State != 1 || _entities.ActiveTriggers != 0x80,
                    "All four slate flags must request panel opening in the later interaction slot.");
            }
            else
            {
                void Inputs(int trigger, int lit)
                {
                    for (int bit = 0; bit < 8; bit++) _entities.SetTrigger(bit, (trigger & (1 << bit)) != 0);
                    rom[0xcca0] = (byte)trigger; torches = lit; rom[0xcc8f] = (byte)lit;
                }
                Inputs(7, 2); Step(2); Inputs(3, 1); Step(2);
                FailIf(stairs.State != 0, "Panel puzzle$90:$0d requires exactly one torch and triggers$07.");
                Inputs(7, 1); Step(); Step(29);
                FailIf(stairs.Counter != 1 || stairs.State != 1, "Panel puzzle must retain its 30-update gate wait.");
                Inputs(3, 1); Step();
                FailIf(stairs.State != 0 || panels.Any(panel => panel.State != 1),
                    "Changing triggers on the terminal wait update must cancel before opening either panel.");
                Inputs(7, 1); Step(); Step(30);
                FailIf(stairs.State != 2 || _entities.ActiveTriggers != 0x87,
                    "Repeating the exact torch/trigger gate must request the panels after all 30 wait updates.");
            }
            Step(); Vector2 locked = _player.Position;
            Step(59, true);
            FailIf(panels.Any(p => p.Counter != 1) || _player.Position != locked,
                "Panel opening must retain Link through the first 59 delay updates.");
            Step(); int movementUpdates = 0;
            while (panels[0].Substate == 1 && movementUpdates < 256) { Step(move: true); movementUpdates++; }
            FailIf(movementUpdates is not (255 or 256) || panels.Any(p => p.Substate != 2 || p.Counter != 30),
                "128 odd-frame panel movements must take 255/256 updates depending on entry parity.");
            Step(30, true); Step(2);
            FailIf(panels.Any(p => p.State != 3) || !stairs.Finished ||
                _currentRoom.GetMetatile(new(120, 120)) != 0x52 || !_saveData.HasRoomFlag(5, roomId, 0x40),
                "Both panels must persist ROOMFLAG$40, release bit7 and create the original north stairs.");
            LoadValidationRoom(5, roomId); StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_entities.Entities<StonePanelRoomEntity>().Any(p => p.State != 3) ||
                _entities.Entities<SlateSlotRoomEntity>().Any(), "Re-entry must restore open panels and suppress already filled sockets.");
        }
        GD.Print("Validated clean-US D8 panel delay/movement/completion, shared signal order, collision, cues, locks and re-entry in split/batched gameplay.");
    }

    private void CompareStonePanelPixelsRom(SomariaRom rom, StonePanelRoomEntity panel, int slot)
    {
        // Execute the native graphics loader, then decode its VRAM bytes and
        // selected OAM/palette independently of the imported PNG and records.
        var native = rom.CreateMenuView();
        var source = ValidationRom.LoadCleanUs();
        int address = 0x14 * 0x4000 + rom.Word(slot + 0x1e) - 0x4000;
        FailIf(source.Span[address] != 4, "INTERAC_STONE_PANEL $7b requires four native OBJ cells.");
        using Image actual = panel.CurrentTexture.GetImage();
        FailIf(actual.GetSize() != new Vector2I(32, 32), "Stone panel texture must retain its complete 16x32 native drawing.");
        var expected = new Color[32 * 32];
        for (int cell = 3; cell >= 0; cell--)
        {
            int row = address + 1 + cell * 4;
            int flags = source.Span[row + 3] ^ rom[slot + 0x1c];
            int tile = (source.Span[row + 2] + rom[slot + 0x1d]) & 0xfe;
            int originX = unchecked((sbyte)source.Span[row + 1]) - 8 - (int)panel.CurrentDrawOffset.X;
            int originY = unchecked((sbyte)source.Span[row]) - 16 - (int)panel.CurrentDrawOffset.Y;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 8; x++)
            {
                int sourceY = (flags & 0x40) == 0 ? y : 15 - y;
                int bit = (flags & 0x20) == 0 ? 7 - x : x;
                int gfx = 0x8000 + tile * 16 + sourceY * 2;
                int bank = flags >> 3 & 1;
                int shade = (native.MapGfx(bank, gfx) >> bit & 1) |
                    (native.MapGfx(bank, gfx + 1) >> bit & 1) << 1;
                if (shade == 0) continue;
                int rgb = native.HudColor(true, flags & 7, shade);
                expected[(originY + y) * 32 + originX + x] =
                    new Color((rgb & 31) / 31f, (rgb >> 5 & 31) / 31f, (rgb >> 10 & 31) / 31f);
            }
        }
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            Color pixel = actual.GetPixel(x, y), wanted = expected[y * 32 + x];
            FailIf(Math.Abs(pixel.A - wanted.A) > 1f / 255 || wanted.A != 0 &&
                (Math.Abs(pixel.R - wanted.R) > 1f / 255 || Math.Abs(pixel.G - wanted.G) > 1f / 255 ||
                 Math.Abs(pixel.B - wanted.B) > 1f / 255),
                $"Stone panel $7b:${rom[slot + 2]:x2} pixel ({x},{y})={pixel}, clean-US OBJ={wanted}.");
        }
    }
}
