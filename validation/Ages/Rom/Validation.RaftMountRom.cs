using Godot;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRaftMountRom() => ValidateRaftControlRom(false, false);
    private void ValidateRaftMovementPublicationRom() => ValidateRaftControlRom(false, true, nativeMovement: true);
    private void ValidateRaftWaterTilesRom() => ValidateRaftControlRom(true, false);
    private void ValidateRaftDismountRom() => ValidateRaftControlRom(false, true);
    private void ValidateRaftSlotLifecycleRom() => ValidateRaftControlRom(false, true, nativeSlots: true);
    private void ValidateRaftTransformedMountRom()
    {
        foreach (int ring in new[] { 0x2a, 0x2b, 0x2c, 0x2d, 0x2e })
            ValidateRaftControlRom(false, false, transformationRing: ring);
    }
    private void ValidateRaftUpdateAdmissionRom()
    {
        foreach (byte mask in new byte[] { 0, 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80, 0x81, 0xff })
            ValidateRaftControlRom(false, true, admission: ("wDisabledObjects", 0xcc8a, mask));
        foreach (byte scroll in new byte[] { 1, 2, 8, 0x80 })
            ValidateRaftControlRom(false, true, admission: ("wScrollMode", 0xcd00, scroll));
        foreach (byte palette in new byte[] { 1, 0xff })
            ValidateRaftControlRom(false, true, admission: ("wPaletteThread_mode", 0xc4ab, palette));
        foreach (byte text in new byte[] { 1, 0x80, 0xff })
            ValidateRaftControlRom(false, true, admission: ("wTextIsActive", 0xcba0, text));
    }
    private void ValidateRaftSwordARom() => ValidateRaftControlRom(false, false, 1);
    private void ValidateRaftSwordBRom() => ValidateRaftControlRom(false, false, 2);
    private void ValidateRaftShieldARom() => ValidateRaftControlRom(false, true, 0, 1);
    private void ValidateRaftShieldBRom() => ValidateRaftControlRom(false, true, 0, 2);
    private void ValidateRaftSatchelARom() => ValidateRaftControlRom(false, true, satchelButton: 1);
    private void ValidateRaftSatchelBRom() => ValidateRaftControlRom(false, true, satchelButton: 2);
    private void ValidateRaftPegasusARom() => ValidateRaftControlRom(false, true, satchelButton: 1, pegasusBeforeMount: true);
    private void ValidateRaftPegasusBRom() => ValidateRaftControlRom(false, true, satchelButton: 2, pegasusBeforeMount: true);

    private void ValidateRaftControlRom(bool allWaterTiles, bool dismount, int swordButton = 0, int shieldButton = 0,
        int satchelButton = 0, bool pegasusBeforeMount = false, int bombchuButton = 0, int heartRing = 0xff,
        bool nativeMovement = false, (string Field, int Address, byte Value)? admission = null,
        bool nativeSlots = false, int transformationRing = 0xff)
    {
        int hostCase1 = 0;
        foreach (int direction in bombchuButton != 0 || heartRing != 0xff || admission.HasValue || nativeSlots || transformationRing != 0xff ? new[] { 0 } : Enumerable.Range(0, dismount ? 4 : 8).Select(index => index * (dismount ? 8 : 4)))
        foreach (int water in allWaterTiles ? new[] { 0xfc, 0xfa, 0xe9, 0xe0, 0xe1, 0xe2, 0xe3 } : new[] { 0xfc })
        foreach (int shieldLevel in shieldButton == 0 ? new[] { 0 } : new[] { 1, 2, 3 })
        foreach (int seedType in satchelButton == 0 ? new[] { 0 } : pegasusBeforeMount ? new[] { 2 } : Enumerable.Range(0, 5))
        foreach (int ring in pegasusBeforeMount ? new[] { 0xff, 0x11 } : new[] { 0xff })
        foreach (bool batched in nativeMovement || admission.HasValue || nativeSlots || transformationRing != 0xff ? new[]{false,true} : RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(0x26);
            LoadValidationRoom(1, 0xa7); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            if (heartRing != 0xff) EquipHeartRingVehicle(heartRing);
            if (bombchuButton != 0)
            {
                _inventory.GiveTreasure(TreasureId.Bombchus, 0x10);
                if (bombchuButton == 1) _inventory.EquipA(TreasureId.Bombchus);
                else _inventory.EquipB(TreasureId.Bombchus);
            }
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
            var waiting = new RaftInteractionRoomEntity(new RaftInteractionSpawn(new(80, 88), 1, 0xa7, SubId: 1),
                _currentRoom, new RaftDatabase().Behavior, _saveData, _runtimeState, _entities);
            _entities.AddEntity(waiting);
            _entities.UpdateNativeInteractionPass(_player, _entities.FrameCounter);
            RaftRoomEntity raft = null!;
            Vector2 start = new(80.25f, 72.5f);
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            if (transformationRing != 0xff)
            {
                _inventory.SetScriptedActiveRing(transformationRing);
                _runtimeState.SetField("wDisableRingTransformations", 0);
            }
            var initialRandom = _random.CaptureState();
            var rom = new SomariaRom(_saveData, initialRandom, _currentRoom, 2, 80, 72)
                { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            // The isolated fixture leaves the native interaction pool empty;
            // its first allocatable slot is $d2, as in the shared allocator.
            const int slot = 0xd240;
            rom[slot] = 1; rom[slot + 1] = 0xe6; rom[slot + 2] = 1;
            rom[slot + 0x0b] = 88; rom[slot + 0x0d] = 80;
            // Room construction has initialized the waiting graphics. Native
            // state zero supplies the same setup before the approach begins.
            rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter);
            FailIf(rom[slot + 4] != 1, "Native raft placement must survive flag $26 and initialize its waiting state.");
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0, previousDirections = 0;
            bool transformedMount = false;
            byte[] observed=new byte[4],expectedScratch=new byte[4];int probes=0,publicationVisits=0;
            Vector2 observedLink=Vector2.Zero,expectedLink=Vector2.Zero;
            if(nativeMovement)
            {
                var probe=new PartMovementScratchProbe(()=>
                {probes++;observedLink=_player.PrecisePosition;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
                _entities.RegisterEnemySlot(probe,15);_entities.AddEntity(probe);
            }
            void SeedScratch()
            {
                if(!nativeMovement)return;
                for(int i=0;i<4;i++){rom[0xcec0+i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}
            }
            SeedScratch();
            int Field(string name) => _runtimeState.ReadWramByte(name switch
                { "_stateCounter" => 0xd106, "_dismountAngle" => 0xd13e, "_dismountCounter" => 0xd13f,
                    _ => throw new System.ArgumentOutOfRangeException(nameof(name)) });
            void Step(int count = 1, int angle = 0xff, int itemButtons = 0)
            {
                Vector2 movement = angle == 0xff ? Vector2.Zero : OracleObjectMovement.Shared.Direction(angle);
                int held = itemButtons | (movement.X > 0 ? 0x10 : movement.X < 0 ? 0x20 : 0) |
                    (movement.Y > 0 ? 0x80 : movement.Y < 0 ? 0x40 : 0);
                int edge = held & ~previousDirections; previousDirections = held;
                StepGameplayUpdates(count, movement, MenuRomActions(held), MenuRomActions(edge), batched, () =>
                {
                    raft = _entities.Entities<RaftRoomEntity>().SingleOrDefault() ?? raft;
                    bool movingRaft=rom[0xd100]!=0&&rom[0xd104]==1&&rom[0xcc2c]==0xd1;
                    rom.UpdateGameplay(edge, held, angle, _saveData.ReadField("wPlaytimeCounter"),afterLink:()=>
                    {if(nativeMovement){expectedLink=new(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f);for(int i=0;i<4;i++)expectedScratch[i]=rom[0xcec0+i];}}); edge = 0; update++;
                    string context = $"Raft 1:a7 tile=${water:x2} direction=${direction:x2} dismount={dismount} Sword=${swordButton:x2} Shield={shieldButton}/L{shieldLevel} Satchel={satchelButton}/ITEM${0x20 + seedType:x2} existingPegasus={pegasusBeforeMount}/ring${ring:x2} admission={admission?.Field}/${admission?.Value:x2} update={update} batch={batched}";
                    if (transformationRing != 0xff)
                    {
                        int pending = rom[0xcc57] == 0 ? 0 : (rom[0xcc57] & 0x7f) == 0 ? -1 : rom[0xcc57] & 0x7f;
                        FailIf(_player.ActiveTransformation != rom[0xd001] ||
                            SomariaPrivate<int>(_player, "_ringTransformationRequested") != pending,
                            $"{context} transform-ring${transformationRing:x2}: native current/pending Link ID differs.");
                        transformedMount |= rom[slot + 4] == 2 && rom[0xd001] != 0 && rom[0xcc57] == 0x80;
                    }
                    if(nativeMovement&&movingRaft)
                    {
                        FailIf(probes!=1||!observed.SequenceEqual(expectedScratch)||_player.KnockbackFrames!=rom[0xd02d]||observedLink!=expectedLink,
                            $"{context}: pre-PART original masked raft movement/knockback scratch differs: probes{probes}, [{string.Join(',',observed)}]/[{string.Join(',',expectedScratch)}], knockback{_player.KnockbackFrames}/{rom[0xd02d]}.");
                        publicationVisits++;
                    }
                    probes=0;SeedScratch();
                    if (nativeSlots)
                    {
                        foreach (var interaction in _entities.Entities<RaftInteractionRoomEntity>())
                        {
                            int at = 0xd040 + _entities.NativeInteractionSlot(interaction) * 0x100;
                            foreach (int offset in new[] { 0, 1, 2, 4, 8, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
                                0x1a, 0x1e, 0x1f, 0x22, 0x23, 0x26, 0x27 })
                                FailIf(_runtimeState.ReadWramByte(at + offset) != rom[at + offset],
                                    $"{context}: INTERAC $e6 native byte${at + offset:x4} differs.");
                        }
                        if (rom[0xd100] != 0 && rom[0xd104] == 0)
                            for (int offset = 0; offset < 0x40; offset++)
                                FailIf(_runtimeState.ReadWramByte(0xd100 + offset) != rom[0xd100 + offset],
                                    $"{context}: interaction mount changed retained D1 byte${0xd100 + offset:x4}.");
                        if (rom[0xd100] != 0)
                            foreach (int offset in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f,
                                0x1a, 0x1b, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27,
                                0x2a, 0x2b, 0x2c, 0x2d, 0x30, 0x31, 0x3e, 0x3f })
                                FailIf(_runtimeState.ReadWramByte(0xd100 + offset) != rom[0xd100 + offset],
                                    $"{context}: SPECIALOBJECT $13 byte${0xd100 + offset:x4} differs: ${_runtimeState.ReadWramByte(0xd100 + offset):x2}/${rom[0xd100 + offset]:x2}, state=${_runtimeState.ReadWramByte(0xd104):x2}/${rom[0xd104]:x2}, speed=${_runtimeState.ReadWramByte(0xd110):x2}/${rom[0xd110]:x2}, animation=${_runtimeState.ReadWramByte(0xd120):x2}/${rom[0xd120]:x2}.");
                    }
                    if (admission.HasValue && rom[0xd100] != 0 && rom[0xd104] == 2)
                        FailIf(Field("_stateCounter") != rom[0xd106],
                            $"{context}: raft state2 countdown differs: {Field("_stateCounter")}/${rom[0xd106]:x2}.");
                    if (heartRing != 0xff) CompareHeartRingVehicle(rom, context);
                    Vector2 expected = new(rom.Word(0xd00c) / 256.0f, rom.Word(0xd00a) / 256.0f);
                    FailIf(_player.PrecisePosition != expected || _player.RaftRideActive != (rom[0xcc2c] == 0xd1) ||
                        _player.HealthQuarters != rom[0xc6aa] || _player.TopDownSwimming || _player.IsDrowning ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008],
                        $"{context}: Link motion/ride/facing differs: runtime={_player.PrecisePosition}, native={expected}, facing={_player.FacingVector}/${rom[0xd008]:x2}, HP={_player.HealthQuarters}/${rom[0xc6aa]:x2}, swimming={_player.TopDownSwimming}, drowning={_player.IsDrowning}, companion=${rom[0xd100]:x2}:${rom[0xd101]:x2}/${rom[0xd104]:x2}, raftXY={rom.Word(0xd10c) / 256.0f},{rom.Word(0xd10a) / 256.0f}, parameter=${rom[0xd121]:x2}, interactionXY={rom[slot + 0x0d]},{rom[slot + 0x0b]}, rider=${rom[0xcc2c]:x2}.");
                    if (rom[0xd100] != 0 && rom[0xd104] != 0)
                    {
                        if (admission.HasValue && rom[0xd104] == 1)
                        {
                            var animation = (EnemyAnimationPlayer)typeof(RaftRoomEntity).GetField("_mountedAnimation",
                                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(raft)!;
                            FailIf(animation.RemainingUpdates != rom[0xd120] || animation.CurrentParameter != rom[0xd121],
                                $"{context}: native raft animation clock/parameter differs: {animation.RemainingUpdates}/{animation.CurrentParameter}, ${rom[0xd120]:x2}/${rom[0xd121]:x2}.");
                        }
                        Vector2 nativeRaft = new(rom.Word(0xd10c) / 256.0f, rom.Word(0xd10a) / 256.0f);
                        FailIf(raft.PrecisePosition != nativeRaft || raft.Direction != rom[0xd108] ||
                            raft.Angle != rom[0xd109] || rom[0xd104] == 1 && (Field("_dismountAngle") != rom[0xd13e] ||
                            Field("_dismountCounter") != rom[0xd13f]),
                            $"{context}: raft fixed motion/direction/dismount counters differ: runtime={raft.PrecisePosition}, native={nativeRaft}, angle=${raft.Angle:x2}/${rom[0xd109]:x2}, facing={raft.Direction}/{rom[0xd108]}, dismount={Field("_dismountAngle")}/{rom[0xd13e]},{Field("_dismountCounter")}/{rom[0xd13f]}.");
                    }
                    if (bombchuButton != 0)
                    {
                        int parent = rom[0xd300] != 0 && rom[0xd301] == 0x0d ? 0xd300 :
                            rom[0xd400] != 0 && rom[0xd401] == 0x0d ? 0xd400 : 0;
                        int child = Enumerable.Range(0xd7, 5).Select(page => page << 8)
                            .SingleOrDefault(address => rom[address] != 0 && rom[address + 1] == 0x0d);
                        var actual = _entities.Entities<BombchuItem>().SingleOrDefault();
                        FailIf(_entities.BombchuParent.Active != (parent != 0) || (actual != null) != (child != 0) ||
                            _inventory.Bombchus != rom[0xc6b3], context + ": Bombchu eligibility/child/ammo differs.");
                        if (parent != 0)
                            FailIf(_entities.BombchuParent.Counter != rom[parent + 0x20] || _entities.BombchuParent.Parameter != rom[parent + 0x21],
                                context + ": Bombchu parent clock differs.");
                        if (actual != null)
                            FailIf(actual.ItemState != rom[child + 4] || actual.Counter2 != rom[child + 7] ||
                                actual.Position != new Vector2(rom[child + 0xd], rom[child + 0xb]) ||
                                (ushort)actual.ZFixed != rom.Word(child + 0xe), context + ": shore Bombchu state/fuse/position differs.");
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
                            _player.ShieldParentButton != (rom[0xd500] == 0 ? 0 : rom[0xd503]) ||
                            _player.ShieldParentInitialized != (rom[0xd504] != 0),
                            $"{context}: Shield L{shieldLevel} parent/raised state differs: runtime={_player.IsUsingShield}/{_player.ShieldParentButton}, native=${rom[0xcc6f]:x2}/${rom[0xd500]:x2}/${rom[0xd504]:x2}.");
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
                                $"{context}: seed count differs at ${address:x4}.");
                        FailIf(_seedSatchel.Pegasus.RawCounter != rom.Word(0xcc6c),
                            $"{context}: Pegasus counter differs: runtime=${_seedSatchel.Pegasus.RawCounter:x4}, native=${rom.Word(0xcc6c):x4}.");
                        var dust = _entities.Entities<PegasusDustRoomEntity>();
                        bool nativeDust = rom[0xdf00] != 0 && rom[0xdf01] == 0x1a;
                        FailIf((dust.Count == 1) != nativeDust, context + ": reserved dust allocation/deletion differs.");
                        if (nativeDust)
                            FailIf(dust[0].Substate != rom[0xdf05] || dust[0].Position != new Vector2(rom[0xdf0d], rom[0xdf0b]) ||
                                dust[0].Visible != ((rom[0xdf1a] & 0x80) != 0) || dust[0].TileBase != rom[0xdf1d] ||
                                dust[0].OamFlags != rom[0xdf1c] ||
                                (int)typeof(PegasusDustRoomEntity).GetField("_subid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dust[0])! != rom[0xdf02] ||
                                !dust[0].Clouds.ToArray().SequenceEqual(Enumerable.Range(0, 8).Select(index => rom[0xdf30 + index])),
                                $"{context}: reserved dust differs: runtime={dust[0].Substate}/{dust[0].Position}/visible={dust[0].Visible}/tile${dust[0].TileBase:x2}/flags${dust[0].OamFlags:x2}/subid={typeof(PegasusDustRoomEntity).GetField("_subid", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dust[0])}/clouds={string.Join(',', dust[0].Clouds.ToArray())}, native={rom[0xdf05]}/{rom[0xdf0d]},{rom[0xdf0b]}/visible={(rom[0xdf1a] & 0x80) != 0}/tile${rom[0xdf1d]:x2}/flags${rom[0xdf1c]:x2}/subid={rom[0xdf02]}/clouds={string.Join(',', Enumerable.Range(0, 8).Select(index => rom[0xdf30 + index]))}, Linkstate=${rom[0xd004]:x2}, raftstate=${rom[0xd104]:x2}.");
                    }
                    FailIf(!sounds.Requests.Where(id => id != SoundId.SndText &&
                        (heartRing == 0xff || id != SoundId.SndGainHeart)).SequenceEqual(rom.Sounds),
                        context + ": gameplay sounds differ.");
                    FailIf(RandomDiffers(initialRandom, rom[0xff94], rom[0xff95], rom.RandomCalls), context + ": shared RNG differs.");
                });
            }
            Step();
            if (nativeSlots)
            {
                for (int offset = 0; offset < 0x40; offset++)
                {
                    byte value = unchecked((byte)(0x35 + offset * 17));
                    if (offset is 0 or 1 or 4 or 0x1a) value = 0;
                    // Retain a valid pending harmless recoil publication as
                    // well as opaque bytes; arbitrary angle bytes are not a
                    // valid native collision input.
                    if (offset == 0x25) value = 0;
                    if (offset == 0x2a) value = 1;
                    if (offset == 0x2b) value = 0x22;
                    if (offset == 0x2c) value = 8;
                    if (offset == 0x2d) value = 4;
                    _runtimeState.SetWramByte(0xd100 + offset, value); rom[0xd100 + offset] = value;
                }
                for (int approach = 0; !_entities.RaftSpecialObjectPresent && approach < 32; approach++) Step(1, 0x10);
                FailIf(!_entities.RaftSpecialObjectPresent || waiting.State != 2 || rom[slot + 4] != 2,
                    "Mount must retain INTERAC $e6 state2 alongside the pending SPECIALOBJECT $13.");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    var parent = _entities.Entities<RaftInteractionRoomEntity>().Single();
                    int parentAt = 0xd040 + _entities.NativeInteractionSlot(parent) * 0x100;
                    _runtimeState.SetField("wDisabledObjects", 2); rom[0xcc8a] = 2;
                    Step();
                    FailIf(!_player.RaftRideActive || parent.Finished || parent.State != 2 || rom[parentAt] == 0,
                        "State0 must initialize D1 while mask02 preserves its mounting interaction's occupancy.");
                    var fillers = new System.Collections.Generic.List<InteractionSlotValidationEntity>();
                    while (_entities.InteractionSlotAvailable)
                    {
                        var puff = new StationaryDebrisEffect();
                        puff.Initialize(_currentRoom.AssetRoot, new(24, 24), SoundId.MusNone);
                        var filler = new InteractionSlotValidationEntity(puff, _ => { });
                        _entities.AddEntity(filler); fillers.Add(filler);
                        int at = 0xd040 + _entities.NativeInteractionSlot(filler) * 0x100;
                        rom[at] = 1; rom[at + 1] = 0x80; rom[at + 4] = 1;
                    }
                    FailIf(fillers.Count != 13 || _entities.InteractionSlotAvailable,
                        "The mounting parent must retain one of the fourteen physical interaction slots.");
                    int dismountVisits = 0;
                    while (_player.RaftRideActive && dismountVisits < 300)
                    {
                        Step(1, 0); dismountVisits++;
                        // Fresh D1 has var3e=var3f=0. Immediately holding Up
                        // against this shore decrements zero to ff; state0
                        // does not manufacture the usual idle reset to04.
                        if (repeat == 1 && dismountVisits == 1)
                            FailIf(rom[0xd13f] != 0xff || _runtimeState.ReadWramByte(0xd13f) != 0xff,
                                "The freshly remounted raft must retain the native zero-counter underflow.");
                    }
                    FailIf(repeat == 1 && dismountVisits != 256,
                        $"Immediate blocked Up after remount must dismount on visit$100, got {dismountVisits}.");
                    FailIf(rom[0xd104] != 2, $"Slot fixture must dismount through the actual upper shore: state${rom[0xd104]:x2}, XY={rom.Word(0xd10c) / 256f},{rom.Word(0xd10a) / 256f}, direction/angle=${rom[0xd108]:x2}/${rom[0xd109]:x2}, dismount=${rom[0xd13e]:x2}/${rom[0xd13f]:x2}, walls=${rom[0xd12e]:x2}, immobilized=${rom[0xcc61]:x2}.");
                    Step(12); Step(3);
                    FailIf(rom[0xd100] != 1 || rom[0xd104] != 3 || !_entities.RaftSpecialObjectPresent ||
                        raft.Finished || raft.DisablesMenus || _runtimeState.ReadField("wMenuDisabled") != 0,
                        "Full interaction capacity must leave state3 alive, enabled01 and retrying with menus released.");
                    int releasedSlot = repeat == 0 ? 15 : 2;
                    var released = fillers.Single(filler => _entities.NativeInteractionSlot(filler) == releasedSlot);
                    released.Finished = true;
                    int childAt = 0xd040 + releasedSlot * 0x100;
                    for (int offset = 0; offset < 0x40; offset++) rom[childAt + offset] = 0;
                    Step();
                    var child = _entities.Entities<RaftInteractionRoomEntity>().Single(actor => actor.State == 1);
                    FailIf(_entities.RaftSpecialObjectPresent || rom[0xd100] != 0 ||
                        _entities.NativeInteractionSlot(child) != releasedSlot || rom[childAt + 4] != 1 ||
                        child.NativeInitializationPending || !child.Visible || parent.Finished || parent.State != 2 ||
                        child.PrecisePosition != child.PrecisePosition.Floor(),
                        "A state3 retry must allocate the first free slot and initialize e6:02 later in that update, retaining its masked parent.");
                    foreach (var filler in fillers)
                    {
                        if (ReferenceEquals(filler, released)) continue;
                        int at = 0xd040 + _entities.NativeInteractionSlot(filler) * 0x100;
                        filler.Finished = true;
                        for (int offset = 0; offset < 0x40; offset++) rom[at + offset] = 0;
                    }
                    _runtimeState.SetField("wDisabledObjects", 0); rom[0xcc8a] = 0;
                    Step();
                    FailIf(!parent.Finished || _entities.Entities<RaftInteractionRoomEntity>().Count != 1 ||
                        Enumerable.Range(0, 0x40).Any(offset => _runtimeState.ReadWramByte(parentAt + offset) != 0 || rom[parentAt + offset] != 0),
                        "Releasing mask02 must delete the original parent on its own next visit and clear all64 interaction bytes.");
                    for (int approach = 0; !_entities.RaftSpecialObjectPresent && approach < 64; approach++) Step(1, 0x10);
                    FailIf(!_entities.RaftSpecialObjectPresent || _player.RaftRideActive,
                        "A completed retry must leave a reachable raft whose next mount again waits for the D1 pass.");
                }
                Step();
                continue;
            }
            if (admission is { } gate)
            {
                void SetGate(bool enabled)
                {
                    _runtimeState.SetField(gate.Field, enabled ? gate.Value : (byte)0);
                    rom[gate.Address] = enabled ? gate.Value : (byte)0;
                }
                // Approach from real dry shore. Mounting publishes D1 after
                // its pass; the following visit must initialize under any gate.
                for (int approach = 0; !_entities.RaftSpecialObjectPresent && approach < 32; approach++) Step(1, 0x10);
                FailIf(!_entities.RaftSpecialObjectPresent || _player.RaftRideActive || rom[0xd104] != 0,
                    "Raft admission fixture must stop after interaction allocation and before special state0.");
                SetGate(true); Step();
                FailIf(!_player.RaftRideActive || rom[0xd104] != 1,
                    $"Raft state0 must initialize under {gate.Field}=${gate.Value:x2}.");
                Step(3);
                SetGate(false); Step(3, 0x10);
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    for (int approach = 0; _player.RaftRideActive && approach < 160; approach++) Step(1, 0);
                    FailIf(_player.RaftRideActive || rom[0xd104] != 2,
                        "Raft admission fixture must reach native state2 from the water's edge.");
                    SetGate(true); Step(6);
                    FailIf(!_entities.RaftSpecialObjectPresent || rom[0xd104] != 2,
                        $"Raft state2 must retain its body and advance under {gate.Field}=${gate.Value:x2}.");
                    SetGate(false); Step(18);
                    FailIf(_entities.RaftSpecialObjectPresent || rom[0xd100] != 0,
                        "Raft state2 must finish after the gate releases.");
                    for (int approach = 0; !_player.RaftRideActive && approach < 64; approach++) Step(1, 0x10);
                    FailIf(!_player.RaftRideActive || rom[0xcc2c] != 0xd1,
                        "Raft admission must support a repeated reachable mount after completion.");
                    Step(3, 0x10);
                }
                continue;
            }
            if (pegasusBeforeMount)
            {
                Step(1, 0xff, satchelButton); Step();
                FailIf((_seedSatchel.Pegasus.RawCounter & 0x7fff) != 0x03c0 - (ring == 0x11 ? 1 : 2),
                    "Dry-shore Satchel must activate Pegasus before the reachable mount approach.");
            }
            for (int approach = 0; !_player.RaftRideActive && approach < 32; approach++) Step(1, 0x10);
            FailIf(!_player.RaftRideActive || rom[0xcc2c] != 0xd1, "Shoreline approach did not allocate and initialize the raft on its native update.");
            FailIf(transformationRing != 0xff && (!transformedMount || _player.ActiveTransformation != 0),
                "A reachable transformed mount must retain its current Link ID for the allocation update and initialize the queued normal ID on the following pass.");
            Step(24);
            if(nativeMovement)
            {
                // Explicit post-contact boundary; normal raft dispatch owns
                // both counter decrements through zero, including text holds.
                F1Set(_player,"_enemyKnockbackFrames",8f);F1Set(_player,"_enemyKnockbackAngle",direction);
                rom[0xd02d]=8;rom[0xd02c]=(byte)direction;
                Step(2);FailIf(_player.KnockbackFrames!=4,"Raft and Link must each decrement the shared knockback counter.");
                _dialogue.ShowMessage("Raft knockback hold.",104);rom[0xcba0]=1;
                Step(3);FailIf(_player.KnockbackFrames!=4,"Text must freeze both raft and Link recoil.");
                _dialogue.Close();rom[0xcba0]=0;Step(2);
            }
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
                    int heldItem = shieldButton | satchelButton | bombchuButton;
                    int health = _inventory.HealthQuarters;
                    if (heartRing != 0xff)
                    {
                        SeedHeartRingVehicle(rom, heartRing);
                        _dialogue.ShowMessage("Raft Heart Ring pause.", _player.Position.Y); rom[0xcba0] = 1;
                        Step(3, direction); _dialogue.Close(); rom[0xcba0] = 0;
                    }
                    if (bombchuButton != 0)
                    {
                        Step(); Step(8, 0xff, bombchuButton);
                        FailIf(_entities.BombchuParent.Active || _entities.Entities<BombchuItem>().Count != 0,
                            $"SPECIALOBJECT_RAFT must reject Bombchu: ride={_player.RaftRideActive}, slot={_entities.RaftSpecialObjectPresent}, native ID=${rom[0xd101]:x2}, state=${rom[0xd104]:x2}, runtime children={_entities.Entities<BombchuItem>().Count}, repeat={repeat}.");
                        _dialogue.ShowMessage("Raft Bombchu pause.", _player.Position.Y); rom[0xcba0] = 1;
                        Step(3, direction, bombchuButton); _dialogue.Close(); rom[0xcba0] = 0;
                    }
                    if (satchelButton != 0)
                    {
                        Step(); Step(8, 0xff, satchelButton);
                        _dialogue.ShowMessage("Raft Satchel pause.", _player.Position.Y); rom[0xcba0] = 1;
                        Step(6, direction, satchelButton); _dialogue.Close(); rom[0xcba0] = 0;
                    }
                    for (int approach = 0; _player.RaftRideActive && approach < 160; approach++) Step(1, direction, heldItem);
                    FailIf(_player.RaftRideActive || rom[0xd104] != 2 || rom[0xcc2c] != 0xd0,
                        "Blocked raft approach must consume its native dismount counter and request the forced walk.");
                    Step(24, 0xff, heldItem);
                    FailIf(_entities.RaftSpecialObjectPresent || raft.DisablesMenus || rom[0xd100] != 0 || rom[0xcc92] != 0 ||
                        _currentRoom.IsSolid(_player.Position),
                        "Dismount must finish its twelve-update object wait and fourteen-update forced walk onto open shore.");
                    if (heartRing != 0xff)
                    {
                        AssertHeartRingVehicleRetained(rom, heartRing, health);
                        Step(1, direction); AssertHeartRingVehicleHeal(heartRing, health);
                    }
                    if (pegasusBeforeMount)
                    {
                        FailIf(_seedSatchel.Pegasus.Active || rom.Word(0xcc6c) != 0,
                            "Forced-walk initialization must clear Pegasus, and held input must not reactivate it.");
                        Step(); Step(1, 0xff, satchelButton); Step();
                        FailIf(!_seedSatchel.Pegasus.Active, "Fresh dry-shore input must reactivate Pegasus before remount.");
                    }
                    if (bombchuButton != 0)
                    {
                        FailIf(_entities.BombchuParent.Active || _entities.Entities<BombchuItem>().Count != 0,
                            "Held rejected Bombchu input must not initialize during raft dismount/forced walk.");
                        Step(); Step(1, 0xff, bombchuButton); Step(3);
                        FailIf(!_entities.BombchuParent.Active || _entities.Entities<BombchuItem>().Count != 1,
                            "Fresh input must initialize Bombchu once SPECIALOBJECT_RAFT $13 has been deleted.");
                        _entities.ClearPhysicalPlayerItems(); rom.ClearPhysicalItems(); Step();
                    }
                    // Release Bombchu during shore approach: a fresh shore
                    // edge legitimately creates a child before raft allocation.
                    int remountItem = shieldButton | satchelButton;
                    for (int approach = 0; !_player.RaftRideActive && approach < 64; approach++) Step(1, (direction + 16) & 0x1f, remountItem);
                    FailIf(!_player.RaftRideActive || rom[0xcc2c] != 0xd1,
                        "A completed native dismount must leave a raft that can be reached and remounted.");
                    Step(24, 0xff, remountItem);
                }
                FailIf(nativeMovement&&publicationVisits<50,"Raft publication must cover repeated blocked dismount/remount and knockback visits.");
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
