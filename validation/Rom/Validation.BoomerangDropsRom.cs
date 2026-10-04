using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBoomerangDropsRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int ring in new[] { 0xff, (int)RingId.RedJoy, (int)RingId.GoldJoy })
        foreach (int outcome in Enumerable.Range(0, 5))
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(0, 0x33); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Boomerang, 0);
            _inventory.EquipA(primary ? TreasureId.Boomerang : 0);
            _inventory.EquipB(primary ? 0 : TreasureId.Boomerang);
            if (ring != 0xff)
            {
                _inventory.GiveTreasure(TreasureId.RingBox, 1);
                _inventory.GrantAppraisedRingForDebug(ring);
                FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                    $"Could not equip Boomerang drop ring ${ring:x2}.");
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8), 0xa0, 0, 0);
            _player.WarpTo(new(40, 64)); _player.Face(Vector2I.Right);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 40, 64) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay();
            var sounds = _sound.AttachPlayRequestAudit();
            const int part = 0xd0c0;
            int button = primary ? 1 : 2, update = 0;
            ItemDropEffect? drop = null;
            void Step(int count = 1, bool press = false)
            {
                int edge = press ? button : 0;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(edge), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, press ? button : 0, 0xff, _entities.FrameCounter); edge = 0;
                    string context = $"Boomerang drop A={primary} ring=${ring:x2} outcome={outcome} update={++update}";
                    FailIf(drop is null || drop.Finished != (rom[part] == 0), context + ": PART$01 deletion differs.");
                    if (!drop.Finished)
                        FailIf((int)drop.State != rom[part + 4] || drop.Counter != rom[part + 6] ||
                            drop.PrecisePosition != new Vector2(rom.Word(part + 0x0c) / 256.0f, rom.Word(part + 0x0a) / 256.0f) ||
                            (drop.ZFixed & 0xffff) != rom.Word(part + 0x0e) ||
                            (drop.SpeedZ & 0xffff) != rom.Word(part + 0x14) ||
                            drop.CollisionEnabled != ((rom[part + 0x24] & 0x80) != 0),
                            context + $": drop state/count/XY/Z/speedZ/collision differs: runtime={drop.State}/{drop.Counter}/{drop.PrecisePosition}/${drop.ZFixed & 0xffff:x4}/${drop.SpeedZ & 0xffff:x4}/{drop.CollisionEnabled}, native={rom[part + 4]}/{rom[part + 6]}/{rom.Word(part + 0x0c) / 256.0f},{rom.Word(part + 0x0a) / 256.0f}/${rom.Word(part + 0x0e):x4}/${rom.Word(part + 0x14):x4}/{(rom[part + 0x24] & 0x80) != 0}.");
                    int[] children = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                        .Where(slot => rom[slot] != 0 && rom[slot + 1] == 6).ToArray();
                    var items = _entities.Entities<BoomerangItem>();
                    FailIf(items.Count != children.Length, context + ": carrier ITEM$06 allocation/deletion differs.");
                    if (items.Count == 1)
                    {
                        var item = items[0]; int child = children[0];
                        FailIf(item.State != rom[child + 4] || item.Counter != rom[child + 6] || item.Angle != rom[child + 9] ||
                            item.PrecisePosition != new Vector2(rom.Word(child + 0x0c) / 256.0f, rom.Word(child + 0x0a) / 256.0f),
                            context + ": carrier flight/contact/return differs.");
                    }
                    foreach (int address in new[] { 0xc627, 0xc628, 0xc6ad, 0xc6ae })
                        FailIf(_saveData.ReadWramByte(address) != rom[address], context + $": rupee grant byte ${address:x4} differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls - seed.Calls != rom.RandomCalls,
                        context + ": shared RNG differs.");
                    // Synthetic text supplies only the native freeze gate;
                    // text printing and the HUD's rupee display run separately.
                    FailIf(!sounds.Requests.Where(id => id is SoundId.SndBoomerang or SoundId.SndSwordBeam)
                        .SequenceEqual(rom.Sounds.Where(id => id is SoundId.SndBoomerang or SoundId.SndSwordBeam)),
                        context + ": ordered carrier sounds differ.");
                });
            }
            void ClearCarrier()
            {
                _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems();
            }
            void ReplaceCarrier(bool sameId)
            {
                ClearCarrier();
                if (sameId)
                    _entities.Spawn<BoomerangItem>(new BoomerangSpawn(new(120, 64), ObjectAngle.Left));
                else
                    _entities.Spawn<SwordBeamEffect>(new SwordBeamSpawn(new(120, 64), ObjectDirection.Left));
                // Both physical state00 routines initialize from their source
                // spawn point; the beam applies its own $f3 X offset.
                rom[0xd700] = 1; rom[0xd701] = sameId ? (byte)6 : (byte)0x27;
                rom[0xd708] = 3; rom[0xd709] = 24;
                rom[0xd70b] = 64; rom[0xd70d] = 120; rom[0xd734] = 24;
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                ClearCarrier();
                int rupees = _inventory.Rupees;
                drop = _entities.Spawn<ItemDropEffect>(new ItemDropSpawn(ItemDropDatabase.OneRupee, new(88.25f, 64.5f)));
                rom[part] = 1; rom[part + 1] = 1; rom[part + 2] = 2;
                rom[part + 0x0a] = 0x80; rom[part + 0x0b] = 64;
                rom[part + 0x0c] = 0x40; rom[part + 0x0d] = 88;
                Step(70);
                FailIf(drop.State != DropState.Grounded || !drop.CanAttachToItem,
                    "Boomerang drop fixture did not settle before the throw.");
                Step(1, true);
                int started = update;
                while ((rom[part + 0x2a] & 0x80) == 0 && update - started < 80) Step();
                FailIf((rom[part + 0x2a] & 0x80) == 0 || drop.CanAttachToItem || drop.State != DropState.Grounded,
                    "Boomerang effect$24 did not publish attachment before the following PART update.");
                _dialogue.ShowMessage("Boomerang attachment pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6);
                _dialogue.Close(); rom[0xcba0] = 0;
                if (outcome == 4) ReplaceCarrier(false); // Replacement before state3 caches the slot ID.
                Step();
                FailIf(drop.State != DropState.Attached || drop.Collected,
                    "PARTSTATUS_JUST_HIT did not enter the attachment state on its next eligible update.");
                if (outcome is 1 or 2 or 3)
                {
                    if (outcome == 1) ClearCarrier();
                    else ReplaceCarrier(outcome == 2);
                }
                if (outcome == 4) ClearCarrier();
                started = update;
                while (!drop.Finished && update - started < 160) Step();
                FailIf(!drop.Finished || drop.Collected != (outcome is 0 or 2) ||
                    _inventory.Rupees != rupees + (outcome is 0 or 2 ? ring == 0xff ? 1 : 2 : 0),
                    "Boomerang drop completion/replacement/clearing granted an incorrect number of rupees.");
                Step(6);
            }
        }
        GD.Print("Validated clean-US Boomerang PART$01 contact publication, dialogue retention, following-update attachment, full fixed motion, collection/Joy Rings, missing/different/same-ID carrier reuse, pre-initialization replacement, sounds/RNG and repeated use through split/batched application updates.");
    }
}
