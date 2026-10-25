using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

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
            void Step(int count = 1, bool move = false, Action? observe = null) => StepGameplayUpdates(count, move ? Vector2.Left : Vector2.Zero,
                batched: batch, afterUpdate: () => {
                    rom.UpdateGameplay(0, move ? 0x20 : 0, move ? 24 : 0xff, _entities.FrameCounter);
                    rom.AdvanceScreenShake();
                    rom.AdvanceTileGraphics(); update++;
                    CompareSomariaMotionRom(rom, $"Ancient Tomb panels update{update}");
                    for (int i = 0; i < 2; i++)
                    {
                        var p = panels[i]; int s = slots[i];
                        FailIf(p.State != rom[s + 4] || p.Substate != rom[s + 5] || p.Counter != rom[s + 6] ||
                            p.PrecisePosition != new Vector2(rom.Word(s + 12) / 256f, rom.Word(s + 10) / 256f) ||
                            p.Position != new Vector2(rom[s + 13], rom[s + 11]),
                            $"Stone panel${i:x2} update{update}: state/substate/counter/XY {p.State}/{p.Substate}/{p.Counter}/{p.PrecisePosition}, drawn {p.Position}; ROM {rom[s + 4]}/{rom[s + 5]}/{rom[s + 6]}/{rom.Word(s + 12) / 256f},{rom.Word(s + 10) / 256f}, drawn {rom[s + 13]},{rom[s + 11]}.");
                    }
                    FailIf(_entities.ActiveTriggers != rom[0xcca0] || _saveData.GetRoomFlags(5, roomId) != rom[0xca00 + roomId] ||
                        _runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) != rom[0xcc8a] ||
                        _runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != rom[0xcc02],
                        $"Stone panels room${roomId:x2} update{update}: trigger {_entities.ActiveTriggers}/{rom[0xcca0]}, flags {_saveData.GetRoomFlags(5, roomId)}/{rom[0xca00+roomId]}, masks {_runtimeState.ReadWramByte(AgesWramAddress.wDisabledObjects)}/{rom[0xcc8a]}, menu {_runtimeState.ReadWramByte(AgesWramAddress.wMenuDisabled)}/{rom[0xcc02]}; stair state {stairs.State}/{rom[stairSlot+4]}.");
                    foreach (int pos in new[] { 0x46, 0x56, 0x66, 0x67, 0x68, 0x48, 0x58 })
                    {
                        Vector2 point = new((pos & 15) * 16 + 8, (pos >> 4) * 16 + 8);
                        FailIf(_currentRoom.GetTerrainInfo(point).Collision != rom[0xce00 + pos],
                            $"Stone panel collision${pos:x2} update{update} differs.");
                    }
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), $"Stone panel update{update}: ordered cues differ.");
                    FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                        $"Panel puzzle room${roomId:x2} update{update}: shake must preserve shared RNG draws.");
                    observe?.Invoke();
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
            int[] pixelSteps = new int[2];
            Vector2[] before = panels.Select(p => p.Position).ToArray();
            void CheckMovement()
            {
                movementUpdates++;
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i].Position == before[i]) continue;
                    FailIf(panels[i].Position - before[i] != new Vector2(i == 0 ? -1 : 1, 0),
                        $"Stone panel $7b:${i:x2} must draw whole one-pixel steps while retaining its subpixels.");
                    pixelSteps[i]++;
                    before[i] = panels[i].Position;
                }
                // SPEED_20 adds +/-$0020 on odd updates. Left xh borrows
                // on step 1; right xh carries on step 8, then every 8 steps.
                int steps = panels[0].Substate == 1 ? 128 - panels[0].Counter : 128;
                FailIf(panels[0].Position.X != 112 - (steps + 7) / 8 ||
                    panels[1].Position.X != 128 + steps / 8,
                    $"Stone panels $7b: movement step {steps} must draw source xh, including asymmetric carry/borrow.");
            }
            while (panels[0].Substate == 1 && movementUpdates < 256)
                Step(panels[0].Counter > 2 ? 4 : 1, move: true, observe: CheckMovement);
            FailIf(movementUpdates is not (255 or 256) || panels.Any(p => p.Substate != 2 || p.Counter != 30),
                "128 odd-frame panel movements must take 255/256 updates depending on entry parity.");
            FailIf(pixelSteps.Any(steps => steps != 16),
                "Stone panels $7b must each draw exactly 16 one-pixel steps across their 128 subpixel movements.");
            Step(30, true); Step(2);
            FailIf(panels.Any(p => p.State != 3) || !stairs.Finished ||
                _currentRoom.GetMetatile(new(120, 120)) != 0x52 || !_saveData.HasRoomFlag(5, roomId, 0x40),
                "Both panels must persist ROOMFLAG$40, release bit7 and create the original north stairs.");
            LoadValidationRoom(5, roomId); StepGameplayUpdates(2, Vector2.Zero);
            FailIf(_entities.Entities<StonePanelRoomEntity>().Any(p => p.State != 3) ||
                _entities.Entities<SlateSlotRoomEntity>().Any(), "Re-entry must restore open panels and suppress already filled sockets.");
        }
        GD.Print("Validated clean-US D8 panel delay/subpixel movement/whole-pixel drawing/completion, shared signal order, collision, cues, locks and re-entry in split/batched gameplay.");
    }

    private void CompareStonePanelPixelsRom(SomariaRom rom, StonePanelRoomEntity panel, int slot)
    {
        FailIf(panel.CurrentTexture.GetSize() != new Vector2I(32, 32),
            "Stone panel texture must retain its complete 16x32 native drawing.");
        CompareInteractionSpritePixelsRom(rom, panel.CurrentTexture, panel.CurrentDrawOffset, slot, 4,
            $"Stone panel $7b:${rom[slot + 2]:x2}");
    }
}
