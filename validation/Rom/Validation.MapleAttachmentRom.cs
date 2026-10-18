using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMapleBodyPickupRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool pause in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.SetScriptedEquippedItems(0, 0);
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            _player.WarpTo(new(40, 64)); _player.Face(Vector2I.Right);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 40, 64) { HostilePartsEnabled = true };
            native.InitializeLinkGameplay(); native.InitializeLinkWalkingAnimation();
            var encounter = new MapleEncounterState(_runtimeState);
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(88, 64), 0));
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); drop.Visible = true;
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = 13; native[at + 4] = 2;
            native[at + 0x24] = 0x94; native[at + 0x25] = 1; native[at + 0x29] = 1;
            native[at + 0x26] = native[at + 0x27] = 6; native[at + 11] = 64; native[at + 13] = 88;
            int rupees = _inventory.Rupees, update = 0;
            void Step(int count, bool walk = false) => StepGameplayUpdates(count, walk ? Vector2.Right : Vector2.Zero,
                batched: batched, afterUpdate: () =>
            {
                native.UpdateGameplay(0, walk ? 0x10 : 0, walk ? 8 : 0xff, _entities.FrameCounter); update++;
                FailIf(drop.Finished != (native[at] == 0) || encounter.LinkScore != native[0xd12a] ||
                    _inventory.Rupees != native[0xc6ad],
                    $"Maple body pickup visit={update}, pause={pause}: source contact publication/next-PART reward differs.");
            });
            for (int wait = 0; native[at + 0x29] != 0 && wait < 60; wait++) Step(1, true);
            FailIf(native[at + 0x29] != 0 || drop.Finished || _inventory.Rupees != rupees,
                "Walking into a Maple drop publishes health0 after PART dispatch; it cannot grant on the contact update.");
            if (pause)
            {
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 1); native[0xcc8a] = 1;
                Step(3);
                FailIf(drop.Finished, "DISABLE_LINK retains a health0 Maple pickup until reward admission is allowed.");
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); native[0xcc8a] = 0;
            }
            Step(1);
            FailIf(!drop.Finished || _inventory.Rupees != rupees + 1 || encounter.LinkScore != 1,
                "The next eligible PART visit must grant the source value once.");
            Step(3); _entities.Clear();
        }
    }

    private void ValidateMapleSwitchHookAttachmentRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool cancel in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SwitchHook, 1); _inventory.EquipA(TreasureId.SwitchHook); _inventory.EquipB(0);
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            _player.WarpTo(new(40, 64)); _player.Face(Vector2I.Right);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 40, 64) { HostilePartsEnabled = true };
            native.InitializeLinkGameplay();
            var encounter = new MapleEncounterState(_runtimeState);
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(88, 64), 0));
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); drop.Visible = true;
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = 13; native[at + 4] = 2;
            native[at + 0x24] = 0x94; native[at + 0x25] = 1; native[at + 0x29] = 1;
            native[at + 0x26] = native[at + 0x27] = 6; native[at + 11] = 64; native[at + 13] = 88;
            int rupees = _inventory.Rupees, update = 0;
            void Step(int count = 1, bool press = false)
            {
                int edge = press ? 1 : 0;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(edge), MenuRomActions(edge), batched, () =>
                {
                    native.UpdateGameplay(edge, press ? 1 : 0, 0xff, _entities.FrameCounter); edge = 0; update++;
                    FailIf(drop.Finished != (native[at] == 0) || encounter.LinkScore != native[0xd12a],
                        $"Maple hook cancel={cancel} visit={update}: item lifetime/score differs.");
                    if (!drop.Finished)
                        FailIf(SomariaPrivate<Vector2>(drop, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                            (drop.ZFixed & 0xffff) != native.Word(at + 14), "Maple state3 must copy the Switch Hook's source high XYZ.");
                    foreach (int address in new[] { 0xc6ad, 0xc6ae })
                        FailIf(_saveData.ReadWramByte(address) != native[address], "Maple Switch Hook attachment must grant exactly the source treasure.");
                });
            }
            Step(1, true);
            for (int wait = 0; (native[at + 0x2a] & 0x80) == 0 && wait < 80; wait++) Step();
            FailIf((native[at + 0x2a] & 0x80) == 0, "Actual Switch Hook flight must publish effect$24 to PART$14.");
            Step();
            FailIf(drop.State != MapleDroppedItemState.Attached, "Maple drop must enter native state3 on the next PART visit.");
            if (cancel) { _entities.SwitchHook!.Cancel(); _entities.ClearPhysicalPlayerItems(); native.ClearPhysicalItems(); }
            for (int wait = 0; !drop.Finished && wait < 120; wait++) Step();
            FailIf(!drop.Finished || _inventory.Rupees != rupees + (cancel ? 0 : 1),
                "Switch Hook completion/cancellation must respectively collect or delete the Maple drop.");
            Step(3); _entities.Clear();
        }
    }

    private void ValidateMapleBoomerangAttachmentRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int outcome in Enumerable.Range(0, 5))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Boomerang, 0); _inventory.EquipA(TreasureId.Boomerang); _inventory.EquipB(0);
            for (int y = 8; y < 128; y += 16)
            for (int x = 8; x < 160; x += 16) _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            _player.WarpTo(new(40, 64)); _player.Face(Vector2I.Right);
            var native = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 1, 40, 64) { HostilePartsEnabled = true };
            native.InitializeLinkGameplay();
            var encounter = new MapleEncounterState(_runtimeState);
            var drop = _entities.Spawn<MapleDroppedItem>(new MapleDroppedItemSpawn(new MapleEventDatabase().Item(13), encounter, 0, new(88, 64), 0));
            F1Set(drop, "_state", MapleDroppedItemState.Grounded); F1Set(drop, "_precisePosition", new Vector2(88.25f, 64.5f));
            drop.Position = new(88, 64); drop.Visible = true;
            const int at = 0xd0c0;
            native[at] = 1; native[at + 1] = 0x14; native[at + 2] = 13; native[at + 4] = 2;
            native[at + 0x24] = 0x94; native[at + 0x25] = 1; native[at + 0x29] = 1;
            native[at + 0x26] = native[at + 0x27] = 6; native[at + 0x1a] = 0xc3;
            native.Word(at + 0x0a, 0x4080); native.Word(at + 0x0c, 0x5840);
            int update = 0, rupees = _inventory.Rupees;
            void Step(int count = 1, bool press = false)
            {
                int edge = press ? 1 : 0;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(edge), MenuRomActions(edge), batched, () =>
                {
                    native.UpdateGameplay(edge, press ? 1 : 0, 0xff, _entities.FrameCounter); edge = 0; update++;
                    FailIf(drop.Finished != (native[at] == 0) || encounter.LinkScore != native[0xd12a],
                        $"Maple attachment outcome={outcome} visit={update}: finished={drop.Finished}/native enabled{native[at]}, score={encounter.LinkScore}/{native[0xd12a]}, native state{native[at + 4]}.{native[at + 5]}/subid${native[at + 2]:x2}/status${native[at + 0x2a]:x2}, cacheID${native[at + 0x30]:x2}, related${native.Word(at + 0x16):x4}, ITEMd7=${native[0xd700]:x2}:${native[0xd701]:x2}, text${native[0xcba0]:x2}, mask${native[0xcc8a]:x2}.");
                    if (!drop.Finished)
                        FailIf(SomariaPrivate<Vector2>(drop, "_precisePosition") != new Vector2(native.Word(at + 12) / 256f, native.Word(at + 10) / 256f) ||
                            (drop.ZFixed & 0xffff) != native.Word(at + 14),
                            $"Maple state3 must retain fractional bytes while copying live carrier high XYZ (outcome={outcome}, visit={update}).");
                    foreach (int address in new[] { 0xc6ad, 0xc6ae })
                        FailIf(_saveData.ReadWramByte(address) != native[address], "Maple attachment must grant through the source treasure policy exactly once.");
                });
            }
            void ClearCarrier() { _entities.ClearPhysicalPlayerItems(); native.ClearPhysicalItems(); }
            void ReplaceCarrier(bool sameId)
            {
                ClearCarrier();
                if (sameId) _entities.Spawn<BoomerangItem>(new BoomerangSpawn(new(120, 64), ObjectAngle.Left));
                else _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(new(120, 64), ObjectDirection.Left));
                native[0xd700] = 1; native[0xd701] = sameId ? (byte)6 : (byte)0x27;
                native[0xd708] = 3; native[0xd709] = 24; native[0xd70b] = 64; native[0xd70d] = 120; native[0xd734] = 24;
            }
            Step(1, true);
            for (int wait = 0; (native[at + 0x2a] & 0x80) == 0 && wait < 80; wait++) Step();
            FailIf((native[at + 0x2a] & 0x80) == 0, "The actual thrown boomerang must reach the Maple drop through source collision geometry.");
            _dialogue.ShowMessage("Maple attachment pause.", _player.Position.Y); native[0xcba0] = 1;
            Step(6); _dialogue.Close(); native[0xcba0] = 0;
            if (outcome == 4) ReplaceCarrier(false);
            Step();
            if (outcome is 1 or 2 or 3) { if (outcome == 1) ClearCarrier(); else ReplaceCarrier(outcome == 2); }
            if (outcome == 4) ClearCarrier();
            for (int wait = 0; !drop.Finished && wait < 160; wait++) Step();
            // The preceding attached visit republishes effect$24. Maple's
            // JUST_HIT branch clears substate, so outcome3 recaches the beam
            // ID on the next visit and follows it back to Link as well.
            FailIf(!drop.Finished || _inventory.Rupees != rupees + (outcome is 0 or 2 or 3 ? 1 : 0),
                "Maple attachment must collect or delete according to live carrier slot/ID lifetime.");
            Step(3); _entities.Clear();
        }
    }
}
