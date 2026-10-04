using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRaftMountRom() => ValidateRaftControlRom(false, false);
    private void ValidateRaftWaterTilesRom() => ValidateRaftControlRom(true, false);
    private void ValidateRaftDismountRom() => ValidateRaftControlRom(false, true);
    private void ValidateRaftSwordARom() => ValidateRaftControlRom(false, false, 1);
    private void ValidateRaftSwordBRom() => ValidateRaftControlRom(false, false, 2);
    private void ValidateRaftShieldARom() => ValidateRaftControlRom(false, true, 0, 1);
    private void ValidateRaftShieldBRom() => ValidateRaftControlRom(false, true, 0, 2);
    private void ValidateRaftSatchelARom() => ValidateRaftControlRom(false, true, satchelButton: 1);
    private void ValidateRaftSatchelBRom() => ValidateRaftControlRom(false, true, satchelButton: 2);
    private void ValidateRaftPegasusARom() => ValidateRaftControlRom(false, true, satchelButton: 1, pegasusBeforeMount: true);
    private void ValidateRaftPegasusBRom() => ValidateRaftControlRom(false, true, satchelButton: 2, pegasusBeforeMount: true);

    private void ValidateRaftControlRom(bool allWaterTiles, bool dismount, int swordButton = 0, int shieldButton = 0,
        int satchelButton = 0, bool pegasusBeforeMount = false)
    {
        int hostCase1 = 0;
        foreach (int direction in Enumerable.Range(0, dismount ? 4 : 8).Select(index => index * (dismount ? 8 : 4)))
        foreach (int water in allWaterTiles ? new[] { 0xfc, 0xfa, 0xe9, 0xe0, 0xe1, 0xe2, 0xe3 } : new[] { 0xfc })
        foreach (int shieldLevel in shieldButton == 0 ? new[] { 0 } : new[] { 1, 2, 3 })
        foreach (int seedType in satchelButton == 0 ? new[] { 0 } : pegasusBeforeMount ? new[] { 2 } : Enumerable.Range(0, 5))
        foreach (int ring in pegasusBeforeMount ? new[] { 0xff, 0x11 } : new[] { 0xff })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(0x26);
            LoadValidationRoom(1, 0xa7); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (swordButton != 0)
            {
                _inventory.GiveTreasure(TreasureId.Sword, 1);
                if (swordButton == 1) _inventory.EquipA(TreasureId.Sword);
                else _inventory.EquipB(TreasureId.Sword);
            }
            if (shieldButton != 0)
            {
                _inventory.GiveTreasure(TreasureId.Shield, shieldLevel);
                if (shieldButton == 1) _inventory.EquipA(TreasureId.Shield);
                else _inventory.EquipB(TreasureId.Shield);
            }
            if (satchelButton != 0)
            {
                _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
                for (int seed = 0; seed < 5; seed++) _inventory.GiveTreasure(0x20 + seed, 0x20);
                _inventory.SelectSatchelSeeds(seedType);
                if (satchelButton == 1) _inventory.EquipA(TreasureId.SeedSatchel);
                else _inventory.EquipB(TreasureId.SeedSatchel);
                if (ring != 0xff)
                {
                    _inventory.GiveTreasure(TreasureId.RingBox, 1);
                    _inventory.GrantAppraisedRingForDebug(ring);
                    FailIf(!_inventory.SetRingBoxSlotFromList(0, ring) || !_inventory.EquipRingAt(0),
                        "Could not equip Pegasus Ring $11 for raft approach.");
                }
            }
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 10; x++)
            {
                bool afloat = y is 5 or 6 && (dismount ? x is 4 or 5 : x is > 0 and < 9);
                _currentRoom.SetPositionTileAndCollision(new(x * 16 + 8, y * 16 + 8),
                    afloat ? (byte)water : (byte)0xa0,
                    x is 0 or 9 || y == 0 ? (byte)0x0f : afloat ? (byte)0x10 : (byte)0, 0);
            }
            var raft = new RaftRoomEntity(new RaftSpawn(new(80, 88), 0, 1, 0xa7),
                _currentRoom, new RaftDatabase().Behavior, _runtimeState);
            _entities.AddEntity(raft);
            Vector2 start = new(80.25f, 72.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 2, 80, 72)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            // The isolated fixture leaves the native interaction pool empty;
            // its first allocatable slot is $d2. The port's raft owner spans
            // both native object categories without reserving that pool slot.
            const int slot = 0xd240;
            rom[slot] = 1; rom[slot + 1] = 0xe6; rom[slot + 2] = 1;
            rom[slot + 0x0b] = 88; rom[slot + 0x0d] = 80;
            // Room construction has initialized the waiting graphics. Native
            // state zero supplies the same setup before the approach begins.
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            FailIf(rom[slot + 4] != 1, "Native raft placement must survive flag $26 and initialize its waiting state.");
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0;
            int Field(string name) => (int)typeof(RaftRoomEntity).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(raft)!;
            T PlayerField<T>(string name) => (T)typeof(Player).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_player)!;
            void Step(int count = 1, int angle = 0xff, int itemButtons = 0)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = itemButtons | (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    rom.UpdateGameplay(edge, held, angle, _entities.FrameCounter - 1); edge = 0; update++;
                    string context = $"Raft 1:a7 tile=${water:x2} direction=${direction:x2} dismount={dismount} Sword=${swordButton:x2} Shield={shieldButton}/L{shieldLevel} Satchel={satchelButton}/ITEM${0x20 + seedType:x2} existingPegasus={pegasusBeforeMount}/ring${ring:x2} update={update} batch={batched}";
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(_player.PrecisePosition != expected || _player.RaftRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.HealthQuarters != rom[0xc6aa] || _player.TopDownSwimming || _player.IsDrowning ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        context + $": Link motion/ride/facing differs: runtime={_player.PrecisePosition}, native={expected}, companion=${rom[0xd100]:x2}:${rom[0xd101]:x2}/${rom[0xd104]:x2}, raftXY={rom.Word(0xd10c) / 256.0f},{rom.Word(0xd10a) / 256.0f}, parameter=${rom[0xd121]:x2}, interactionXY={rom[slot + 0x0d]},{rom[slot + 0x0b]}, rider=${rom[0xcc2c]:x2}.");
                    if (rom[0xd100] != 0 && rom[0xd104] != 0)
                    {
                        Vector2 nativeRaft = new(rom.Word(0xd10c) / 256.0f, rom.Word(0xd10a) / 256.0f);
                        FailIf(raft.PrecisePosition != nativeRaft || raft.Direction != rom[0xd108] ||
                            raft.Angle != rom[0xd109] || rom[0xd104] == 1 && (Field("_dismountAngle") != rom[0xd13e] ||
                            Field("_dismountCounter") != rom[0xd13f]),
                            context + $": raft fixed motion/direction/dismount counters differ: runtime={raft.PrecisePosition}, native={nativeRaft}, angle=${raft.Angle:x2}/${rom[0xd109]:x2}, facing={raft.Direction}/{rom[0xd108]}, dismount={Field("_dismountAngle")}/{rom[0xd13e]},{Field("_dismountCounter")}/{rom[0xd13f]}.");
                    }
                    if (swordButton != 0)
                    {
                        FailIf(_player.IsAttacking != (rom[0xd200] != 0), context + ": Sword parent lifetime differs.");
                        Rect2 hitbox = _player.GetSwordHitbox();
                        bool collision = (rom[0xd624] & 0x80) != 0;
                        FailIf((hitbox.Size != Vector2.Zero) != collision, context + ": Sword child eligibility differs.");
                        if (collision)
                            FailIf(hitbox != new Rect2(new(rom[0xd60d] - rom[0xd627], rom[0xd60b] - rom[0xd626]),
                                new(2 * rom[0xd627], 2 * rom[0xd626])) || _player.SwordDamage != -unchecked((sbyte)rom[0xd628]) ||
                                _player.MeleeItemZ != unchecked((sbyte)rom[0xd60f]), context + ": Sword child geometry/damage/Z differs.");
                    }
                    if (shieldButton != 0)
                        FailIf(_player.IsUsingShield != (rom[0xcc6f] != 0) ||
                            PlayerField<int>("_shieldParentButton") != (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                            PlayerField<bool>("_shieldParentInitialized") != (rom[0xd504] != 0),
                            context + $": Shield L{shieldLevel} parent/raised state differs: runtime={_player.IsUsingShield}/{PlayerField<int>("_shieldParentButton")}, native=${rom[0xcc6f]:x2}/${rom[0xd500]:x2}/${rom[0xd504]:x2}.");
                    if (satchelButton != 0)
                    {
                        FailIf(_player.IsUsingSeedSatchel ||
                            Enumerable.Range(0xd2, 4).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] == 0x19) ||
                            Enumerable.Range(0xd7, 5).Any(page => rom[page << 8] != 0 && rom[(page << 8) + 1] is >= 0x20 and <= 0x24) ||
                            _entities.Entities<EmberSeedEffect>().Count != 0,
                            context + ": forbidden Satchel parent/physical child.");
                        for (int address = 0xc6b9; address <= 0xc6bd; address++)
                            FailIf(_saveData.ReadWramByte(address) != rom[address] ||
                                !pegasusBeforeMount && rom[address] != 0x20,
                                context + $": seed count differs at ${address:x4}.");
                        FailIf(_seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c),
                            context + $": Pegasus counter differs: runtime=${_seedSatchel.Pegasus.RawCounter:x4}, native=${rom.Word(0xcc6c):x4}.");
                        var dust = _entities.Entities<PegasusDustRoomEntity>();
                        bool nativeDust = rom[0xdf00] != 0 && rom[0xdf01] == 0x1a;
                        FailIf((dust.Count == 1) != nativeDust, context + ": reserved dust allocation/deletion differs.");
                        if (nativeDust)
                            FailIf(dust[0].Substate != rom[0xdf05] || dust[0].Position != new Vector2(rom[0xdf0d], rom[0xdf0b]) ||
                                dust[0].Visible != ((rom[0xdf1a] & 0x80) != 0) || dust[0].TileBase != rom[0xdf1d] ||
                                dust[0].OamFlags != rom[0xdf1c] ||
                                (int)typeof(PegasusDustRoomEntity).GetField("_subid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dust[0])! != rom[0xdf02] ||
                                !dust[0].Clouds.ToArray().SequenceEqual(Enumerable.Range(0, 8).Select(index => rom[0xdf30 + index])),
                                context + $": reserved dust differs: runtime={dust[0].Substate}/{dust[0].Position}/visible={dust[0].Visible}/tile${dust[0].TileBase:x2}/flags${dust[0].OamFlags:x2}/subid={typeof(PegasusDustRoomEntity).GetField("_subid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dust[0])}/clouds={string.Join(',', dust[0].Clouds.ToArray())}, native={rom[0xdf05]}/{rom[0xdf0d]},{rom[0xdf0b]}/visible={(rom[0xdf1a] & 0x80) != 0}/tile${rom[0xdf1d]:x2}/flags${rom[0xdf1c]:x2}/subid={rom[0xdf02]}/clouds={string.Join(',', Enumerable.Range(0, 8).Select(index => rom[0xdf30 + index]))}, Linkstate=${rom[0xd004]:x2}, raftstate=${rom[0xd104]:x2}.");
                    }
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": gameplay sounds differ.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - initialRandom.Calls != rom.RandomCalls, context + ": shared RNG differs.");
                });
            }
            Step();
            if (pegasusBeforeMount)
            {
                Step(1, 0xff, satchelButton); Step();
                FailIf((_seedSatchel.Pegasus.RawCounter & 0x7fff) != 0x03c0 - (ring == 0x11 ? 1 : 2),
                    "Dry-shore Satchel must activate Pegasus before the reachable mount approach.");
            }
            for (int approach = 0; !raft.LinkRiding && approach < 32; approach++) Step(1, 0x10);
            FailIf(!raft.LinkRiding || rom[0xcc2c] != 0xd1, "Shoreline approach did not allocate and initialize the raft on its native update.");
            Step(24);
            if (shieldButton != 0)
            {
                Step(8, 0xff, shieldButton);
                FailIf(!_player.IsUsingShield || rom[0xcc6f] != shieldLevel, "Shield must initialize while riding a raft.");
                _dialogue.ShowMessage("Raft Shield pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, direction, shieldButton); _dialogue.Close(); rom[0xcba0] = 0;
            }
            if (dismount)
            {
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    int heldItem = shieldButton | satchelButton;
                    if (satchelButton != 0)
                    {
                        Step(); Step(8, 0xff, satchelButton);
                        _dialogue.ShowMessage("Raft Satchel pause.", _player.Position.Y); rom[0xcba0] = 1;
                        Step(6, direction, satchelButton); _dialogue.Close(); rom[0xcba0] = 0;
                    }
                    for (int approach = 0; raft.LinkRiding && approach < 160; approach++) Step(1, direction, heldItem);
                    FailIf(raft.LinkRiding || rom[0xd104] != 2 || rom[0xcc2c] != 0xd0,
                        "Blocked raft approach must consume its native dismount counter and request the forced walk.");
                    Step(24, 0xff, heldItem);
                    FailIf(raft.UsesSpecialObjectSlot || raft.DisablesMenus || rom[0xd100] != 0 || rom[0xcc92] != 0 ||
                        _currentRoom.IsSolid(_player.Position),
                        "Dismount must finish its twelve-update object wait and fourteen-update forced walk onto open shore.");
                    if (pegasusBeforeMount)
                    {
                        FailIf(_seedSatchel.Pegasus.Active || rom.Word(0xcc6c) != 0,
                            "Forced-walk initialization must clear Pegasus, and held input must not reactivate it.");
                        Step(); Step(1, 0xff, satchelButton); Step();
                        FailIf(!_seedSatchel.Pegasus.Active, "Fresh dry-shore input must reactivate Pegasus before remount.");
                    }
                    for (int approach = 0; !raft.LinkRiding && approach < 64; approach++) Step(1, (direction + 16) & 0x1f, heldItem);
                    FailIf(!raft.LinkRiding || rom[0xcc2c] != 0xd1,
                        "A completed native dismount must leave a raft that can be reached and remounted.");
                    Step(24, 0xff, heldItem);
                }
                continue;
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                if (swordButton != 0)
                {
                    Step(1, direction, swordButton);
                    Step(4, (direction + 8) & 0x1f, swordButton);
                    _dialogue.ShowMessage("Raft Sword pause.", _player.Position.Y); rom[0xcba0] = 1;
                    Step(6, direction, swordButton); _dialogue.Close(); rom[0xcba0] = 0;
                    Step(12, direction, swordButton);
                    Step(6, 0xff, swordButton);
                    FailIf(_player.IsAttacking || rom[0xd200] != 0, "Held Sword on a raft must finish its swing without entering charge.");
                    Step();
                }
                Step(4, direction); Step(16); Step(4, (direction + 16) & 0x1f); Step(16);
                _dialogue.ShowMessage("Raft pause.", _player.Position.Y); rom[0xcba0] = 1;
                Step(6, direction); _dialogue.Close(); rom[0xcba0] = 0;
                Step(4, direction); Step(4, (direction + 16) & 0x1f); Step(16);
            }
        }
        GD.Print($"Validated clean-US shoreline raft support/mount allocation, next-pass initialization, Link/raft fixed XY and facing, directions, animation bob, dismount counters, dialogue, sounds/RNG and repeated split/batched gameplay, all water tiles={allWaterTiles}, dismount/remount={dismount}, Sword=${swordButton:x2}, Shield={shieldButton}, Satchel={satchelButton}, existing Pegasus={pegasusBeforeMount}.");
    }
}
