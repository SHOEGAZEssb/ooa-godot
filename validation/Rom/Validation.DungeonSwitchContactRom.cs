using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareDungeonSwitchContactsRom()
    {
        FailIf(OracleRuntimeState.ToggleBlocksStateAddress != 0xcdd2 ||
            OracleRuntimeState.SwitchStateAddress != 0xcdd3 || OracleRuntimeState.SpinnerStateAddress != 0xcdd4,
            "Dungeon state must use Ages $cdd2-$cdd4, separate from room pack/modifier/collision selection.");
        int fixture = 0;
        foreach (var c in new[] {
            (Room:0x89,Mask:4,Switch:0x62,Rail:0x67,Row:0x0b,Off:0x5c,On:0x5d,Start:0x92,Direction:0),
            (Room:0x8f,Mask:8,Switch:0x81,Rail:0x52,Row:0x0c,Off:0x59,On:0x5e,Start:0x51,Direction:2) })
        foreach (var item in new[] { (Treasure:TreasureId.SwitchHook,Beam:false,Full:false),
            (Treasure:TreasureId.Sword,Beam:false,Full:false),(Treasure:TreasureId.Sword,Beam:true,Full:false),
            (Treasure:TreasureId.Shooter,Beam:false,Full:false),(Treasure:TreasureId.Sword,Beam:false,Full:true) }
        .Where(item => c.Room == 0x89 || item.Treasure != TreasureId.Shooter && !item.Full))
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            int treasure = item.Treasure;
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,c.Room);
            _entities.Clear();
            _entities.RuntimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,0x80);
            var data = new DungeonMechanicDatabase();
            var record = data.GetRoomRecords(4,c.Room).Single(row => row.Id == 5);
            FailIf(record.SubId != c.Mask || record.PackedPosition != c.Switch || _currentRoom.Layout[c.Switch] != 0x0a,
                $"Native switch fixture requires original PART$05:${c.Mask:x2} at room$4:${c.Room:x2}:${c.Switch:x2}.");
            var part = new DungeonSwitchRoomEntity(record,_currentRoom,data,_entities.RuntimeState,
                () => (long)_animationTicks,_roomView.QueueRedraw,_sound.PlaySound,_rooms.TrySetTile,_saveData);
            _entities.AddEntity(part);
            // Both original streams order INTERAC$78 before PART$05.
            // Enemy AI and the cart are outside this stationary contact fixture.
            var railRecord = new SkullDungeonDatabase().GetRoomRecords(4,c.Room).Single();
            FailIf(railRecord.Id != 0x78 || railRecord.SubId != c.Mask || railRecord.Y != c.Rail ||
                railRecord.X != c.Row || railRecord.Order != 0,
                $"Original switch rail$4:${c.Room:x2} lost its placement, mask or replacement row.");
            var rail = new SwitchTileTogglerRoomEntity(railRecord,new DungeonInteractionDatabase(),
                _entities.RuntimeState,_roomView.QueueRedraw,_rooms.TrySetTile);
            _entities.AddEntity(rail);
            _inventory.GiveTreasure(treasure,item.Beam ? 2 : 1); _inventory.EquipA(treasure);
            bool seedShot = treasure == TreasureId.Shooter;
            bool zeroLockout = item.Beam || seedShot;
            if (seedShot)
            {
                _inventory.GiveTreasure(TreasureId.EmberSeeds,5);
                _inventory.SelectShooterSeeds(0);
            }
            if (!item.Beam) _inventory.ApplyDamage(4); // Keep ordinary Sword distinct from its full-health beam.
            Vector2 Point(int p) => new((p & 15)*16+8,(p >> 4)*16+8);
            Vector2 start = Point(c.Start);
            _player.WarpTo(start); _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(c.Direction*8));
            FailIf(_collision.Collides(_player.Position),"Switch contact approach must start on original room floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,c.Direction,(int)start.X,(int)start.Y) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcdd3] = 0x80;
            rom[0xd0c0] = 1; rom[0xd0c1] = 5; rom[0xd0c2] = (byte)c.Mask;
            rom[0xd0cb] = (byte)Point(c.Switch).Y; rom[0xd0cd] = (byte)Point(c.Switch).X;
            rom[0xd240] = 1; rom[0xd241] = 0x78; rom[0xd242] = (byte)c.Mask;
            rom[0xd24b] = (byte)c.Rail; rom[0xd24d] = (byte)c.Row;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int pressed = 0) =>
                StepSomariaMotionRom(rom,count,batch,angle,held:pressed,pressed:pressed,afterUpdate:() =>
                {
                    rom.AdvanceTileGraphics();
                    string context = $"PART$05 room$4:${c.Room:x2}, treasure${treasure:x2}, beam={item.Beam}, full={item.Full}, batch={batch}, update={++update}";
                    FailIf(_rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                        context+": shared tile queue rejection/draining differs.");
                    FailIf(part.Finished != (rom[0xd0c0] == 0) || part.HitLockout != -unchecked((sbyte)rom[0xd0eb]) ||
                        _entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != rom[0xcdd3],
                        context + $": part lifetime/lockout/shared switch state differs: runtime={part.HitLockout}/${_entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress):x2}, native={unchecked((sbyte)rom[0xd0eb])}/${rom[0xcdd3]:x2}, hit=${rom[0xd0ea]:x2}, state=${rom[0xd0c4]:x2}.");
                    FailIf(_currentRoom.Layout[c.Switch] != rom[0xcf00+c.Switch] ||
                        _currentRoom.GetTerrainInfo(Point(c.Switch)).Collision != rom[0xce00+c.Switch] ||
                        _currentRoom.GetUnderlyingMetatile(Point(c.Switch)) != rom.Underlying(c.Switch),
                        context + ": switch layout/collision/underlying tile differs.");
                    FailIf(SomariaPrivate<bool>(rail,"_initialized") != (rom[0xd244] != 0) ||
                        SomariaPrivate<int>(rail,"_lastSwitchState") != rom[0xd243] ||
                        _entities.InteractionSlot(rail) != 2,
                        context + ": ordered rail initialization/retained complete switch byte differs.");
                    if (treasure == TreasureId.SwitchHook)
                    {
                        var hook = _entities.SwitchHook!.Item;
                        bool active = rom[0xd600] != 0;
                        FailIf((hook is { Finished:false }) != active ||
                            _player.IsUsingSwitchHook != (rom[0xd200] != 0),context + ": Hook parent/weapon ownership differs.");
                        if (active)
                            FailIf(hook!.State != rom[0xd604] || hook.Counter != rom[0xd606] ||
                                hook.PrecisePosition != new Vector2(rom.Word(0xd60c) / 256f,rom.Word(0xd60a) / 256f) ||
                                hook.CollisionEnabled != ((rom[0xd624] & 0x80) != 0),context + ": Hook state/clock/full XY/collision mask differs.");
                    }
                    else if (!seedShot) FailIf(_player.IsAttacking != (rom[0xd200] != 0),context + ": Sword parent ownership differs.");
                    FailIf(_inventory.EmberSeeds != rom[0xc6b9],context + $": seed spending differs: runtime={_inventory.EmberSeeds}, native={rom[0xc6b9]}, parent=${rom[0xd200]:x2}/${rom[0xd204]:x2}/${rom[0xd207]:x2}, child=${rom[0xd600]:x2}/${rom[0xd604]:x2}, seed=${rom[0xd700]:x2}/${rom[0xd704]:x2}, post={_seedSatchel.ShooterPostShotCounter}.");
                    if (seedShot)
                    {
                        FailIf(_player.IsUsingSeedShooter != (rom[0xd200] != 0),context + ": Shooter parent ownership differs.");
                        var seeds = _entities.Entities<EmberSeedEffect>();
                        var slots = Enumerable.Range(0xd7,5).Select(page => page << 8)
                            .Where(slot => rom[slot] != 0 && rom[slot + 1] == 0x20).ToArray();
                        FailIf(seeds.Count != slots.Length || _entities.HasActiveShooterSeed != (rom[0xccda] != 0),
                            context + ": seed lifetime/shared Shooter signal differs.");
                        for (int i = 0; i < slots.Length; i++)
                        {
                            var shot = seeds[i]; int slot = slots[i];
                            int state = shot.State == EmberState.Initializing ? 0 : shot.State == EmberState.Flying ? 1 : 3;
                            FailIf(state != rom[slot + 4] || shot.PrecisePosition != new Vector2(rom.Word(slot + 0xc) / 256f,rom.Word(slot + 0xa) / 256f) ||
                                shot.CollisionEnabled != ((rom[slot + 0x24] & 0x80) != 0) || state == 3 && shot.FlameCounter != rom[slot + 6],
                                context + ": seed state/XY/collision/flame clock differs.");
                        }
                    }
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        context + ": gameplay cues/shared RNG differs.");
                });
            bool melee = treasure == TreasureId.Sword && !item.Beam;
            // Down-facing Sword reaches farther than the north pose. Keep
            // the beam's room$8f launch on its original floor tile$51 so
            // the sword tip cannot contact PART$05 before its projectile.
            int approach = melee ? 32 : item.Beam && c.Room == 0x8f ? 0 : 16;
            Step(); Step(approach,c.Direction*8);
            FailIf(_currentRoom.IsSolid(_player.Position) || _player.Position !=
                start + OracleObjectMath.StrictCardinalVector(c.Direction*8)*approach,
                "Switch item contact must approach through original safe floor.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                int before = repeat == 0 ? 0x80 : 0x80 | c.Mask;
                Step(pressed:1);
                for (int wait = 0; (rom[0xd0ea] & 0x80) == 0 && wait < 40; wait++) Step();
                FailIf((rom[0xd0ea] & 0x80) == 0 || part.HitLockout != (zeroLockout ? 0 : 28) || _entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != before ||
                    _currentRoom.Layout[c.Switch] != (repeat == 0 || item.Full ? 0x0a : 0x0b),
                    $"Actual contact must retain switch bit/tile until the next PART update: treasure${treasure:x2}, beam={item.Beam}, repeat={repeat}, update={update}, hit=${rom[0xd0ea]:x2}, lock={part.HitLockout}, bits=${rom[0xcdd3]:x2}, beamSlot=${rom[0xd700]:x2}:${rom[0xd701]:x2}, health={_inventory.HealthQuarters}/{_inventory.MaxHealthQuarters}.");
                _dialogue.ShowMessage("Pending switch pause.",120); rom[0xcba0] = 1;
                FailIf((rom[0xd0ea] & 0x80) == 0,"Native switch contact did not publish its pending hit flag.");
                Step(3);
                FailIf((rom[0xd0ea] & 0x80) == 0,"Native text pause cleared the pending switch hit flag.");
                _dialogue.Close(); rom[0xcba0] = 0;
                if (item.Full && repeat == 0)
                    for (int write = 0; write < 31; write++)
                    {
                        FailIf(!_rooms.TrySetTile(0x11,0xa0),"Switch queue fixture must accept31 writes before the pending hit dispatch.");
                        rom.SetTile(0x11,0xa0);
                    }
                Step();
                FailIf(part.HitLockout != (zeroLockout ? 0 : 27) || _entities.RuntimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != (before ^ c.Mask) ||
                    _currentRoom.Layout[c.Switch] != (repeat == 0 && !item.Full ? 0x0b : 0x0a) ||
                    _currentRoom.Layout[c.Rail] != (repeat == 0 && !item.Full ? c.On : c.Off),
                    "Next eligible PART update must decrement$e4->$e3, flip only bit$04, and retain the old tile when setTile is rejected.");
                if (seedShot)
                {
                    // itemAnimation1e818's two $02 frames reach parameter$80
                    // four eligible updates after collision activation.
                    Step(3);
                    FailIf(_entities.Entities<EmberSeedEffect>() is not [{ FlameCounter:55 }],
                        "Contacted Ember must remain live before its fourth animation update.");
                    Step();
                    FailIf(_entities.Entities<EmberSeedEffect>().Count != 0 || _entities.HasActiveShooterSeed,
                        "Contacted Ember must retire on animation parameter$80/update4, before counter$3a expires.");
                }
                Step(seedShot ? 22 : 26); FailIf(part.HitLockout != (zeroLockout ? 0 : 1),"Switch lockout must persist through update27.");
                Step(); Step(20);
                FailIf(part.HitLockout != 0 || _player.IsUsingSwitchHook || _player.IsAttacking ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSwitch) != repeat + 1,
                    "Switch contact must finish its exact lockout/item lifetime with one cue per repeated hit.");
            }
            if (treasure != TreasureId.SwitchHook) continue;
            // Complete one more real contact, then cancel the next pending
            // collision before PART dispatch can undo that completed state.
            Step(pressed:1);
            for (int wait = 0; (rom[0xd0ea]&0x80) == 0 && wait < 40; wait++) Step();
            FailIf((rom[0xd0ea]&0x80) == 0,"Cancellation setup must reach original Hook contact.");
            Step(); Step(48);
            FailIf(_player.IsUsingSwitchHook || rom[0xcdd3] != (0x80|c.Mask),
                "Cancellation must begin after the preceding switch action has completed.");
            Vector2 returnPoint = _player.PrecisePosition;
            Step(pressed:1);
            for (int wait = 0; (rom[0xd0ea]&0x80) == 0 && wait < 40; wait++) Step();
            FailIf(part.HitLockout != 28 || (rom[0xd0ea]&0x80) == 0 || rom[0xcdd3] != (0x80|c.Mask),
                "Cancellation must retain the completed bit while the next native hit is pending.");
            rom.SetOutgoingParts(); rom.SetOutgoingInteractions();
            rom.ClearOutgoingParts(); rom.ClearOutgoingInteractions();
            rom.ClearPhysicalItems(); rom.ClearItemParents(); rom.ClearRoomVariables(scrolling:false);
            FailIf(rom[0xd0c0] != 0 || rom[0xd240] != 0 || rom[0xcdd3] != (0x80|c.Mask),
                "Original teardown must discard the pending PART/rail objects and retain the completed switch byte.");
            LoadValidationRoom(4,0x91); LoadValidationRoom(4,c.Room);
            // Bounded reload comparison: execute original substitutions and
            // object parsing, before enemy/cart updates. Full native loading,
            // destination AI and its RNG remain outside this fixture.
            var reload = new FrontendRom();
            for (int address = 0xc5b0; address < 0xcb00; address++) reload[address] = _saveData.ReadWramByte(address);
            reload[0xcc2d] = 4; reload[0xcc30] = (byte)c.Room; reload[0xcc05] = 0xff;
            reload[0xcdd3] = (byte)(0x80|c.Mask); reload[0xcd00] = 1;
            reload.LoadRoomTileset();
            for (int p = 0; p < 0xb0; p++) reload[0xcf00+p] = _currentRoom.Layout[p];
            // Independent original room-layout inputs to the substitution
            // boundary, rather than supplying its already-restored output.
            reload[0xcf00+c.Switch] = 0x0a; reload[0xcf00+c.Rail] = (byte)c.Off;
            reload.ApplyRoomTileSubstitutions(); reload.Call(0x55b7,0x12);
            part = _entities.Entities<DungeonSwitchRoomEntity>().Single();
            rail = _entities.Entities<SwitchTileTogglerRoomEntity>().Single();
            int reloadRail = Enumerable.Range(0xd2,14).Select(page => (page<<8)|0x40)
                .Single(slot => reload[slot] != 0 && reload[slot+1] == 0x78);
            var layoutDoors = _entities.Entities<MinecartShutterRoomEntity>().OrderBy(_entities.InteractionSlot).ToArray();
            int[] nativeDoors = Enumerable.Range(0xd2,14).Select(page => (page<<8)|0x40)
                .Where(slot => reload[slot] != 0 && reload[slot+1] == 0x1e && reload[slot+2] >= 0x0c).ToArray();
            // Independent rooms/ages/large/room0489.bin / room048f.bin
            // transcription catches missing imported door tiles as well.
            int[] sourceDoors = c.Room == 0x89 ? [0xa9,0xa7] : [0xa2,0x09,0x07];
            FailIf(layoutDoors.Length != nativeDoors.Length || nativeDoors.Length != sourceDoors.Length,
                "Original room reload lost a layout minecart door allocation.");
            for (int index = 0; index < nativeDoors.Length; index++)
                FailIf(_entities.InteractionSlot(layoutDoors[index]) != (nativeDoors[index]>>8)-0xd0 ||
                    layoutDoors[index].PackedPosition != reload[nativeDoors[index]+0xb] ||
                    reload[nativeDoors[index]+0xb] != sourceDoors[index] ||
                    reload[nativeDoors[index]+2] != (sourceDoors[index] >= 0xa0 ? 0x0e : 0x0c) ||
                    layoutDoors[index].State != MinecartShutterState.Initialize,
                    "Layout minecart doors must retain the original descending tile scan and precede placed rails in the native pool.");
            FailIf(reload[0xcf00+c.Switch] != 0x0b || reload[0xcf00+c.Rail] != c.On ||
                _currentRoom.Layout[c.Switch] != reload[0xcf00+c.Switch] || _currentRoom.Layout[c.Rail] != reload[0xcf00+c.Rail] ||
                reload[0xd0c1] != 5 || reload[0xd0c2] != c.Mask || reload[0xd0ea] != 0 || reload[0xd0eb] != 0 ||
                reload[reloadRail+2] != c.Mask || reload[reloadRail+0xb] != c.Rail ||
                reload[reloadRail+0xd] != c.Row || reload[reloadRail+4] != 0 || part.HitLockout != 0 ||
                SomariaPrivate<bool>(rail,"_initialized") || _entities.InteractionSlot(rail) != (reloadRail>>8)-0xd0,
                $"Original reload$4:${c.Room:x2} differs: native tiles=${reload[0xcf00+c.Switch]:x2}/${reload[0xcf00+c.Rail]:x2}, " +
                $"runtime=${_currentRoom.Layout[c.Switch]:x2}/${_currentRoom.Layout[c.Rail]:x2}; " +
                $"PART=${reload[0xd0c1]:x2}:${reload[0xd0c2]:x2}/hit${reload[0xd0ea]:x2}/lock${reload[0xd0eb]:x2}, " +
                $"railSlot=${reloadRail:x4}/state${reload[reloadRail+4]:x2}, runtime lock={part.HitLockout}/init={SomariaPrivate<bool>(rail,"_initialized")}/slot={_entities.InteractionSlot(rail)}.");
            _entities.Clear();
            part = new DungeonSwitchRoomEntity(record,_currentRoom,data,_entities.RuntimeState,
                () => (long)_animationTicks,_roomView.QueueRedraw,_sound.PlaySound,_rooms.TrySetTile,_saveData);
            rail = new SwitchTileTogglerRoomEntity(railRecord,new DungeonInteractionDatabase(),
                _entities.RuntimeState,_roomView.QueueRedraw,_rooms.TrySetTile);
            _entities.AddEntity(part); _entities.AddEntity(rail);
            _player.WarpTo(returnPoint); _player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(c.Direction*8));
            seed = _random.CaptureState();
            rom = new SomariaRom(_saveData,seed,_currentRoom,c.Direction,(int)returnPoint.X,(int)returnPoint.Y) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xcdd3] = (byte)(0x80|c.Mask);
            for (int index = 0; index < 0x40; index++)
            {
                rom[0xd0c0+index] = reload[0xd0c0+index];
                rom[0xd240+index] = reload[reloadRail+index];
            }
            sounds = _sound.AttachPlayRequestAudit(); Step(); Step(3);
            FailIf(rom[0xcdd3] != (0x80|c.Mask) || part.HitLockout != 0 || sounds.Requests.Count != 0,
                "Post-reload updates must not replay the canceled switch hit or its cue.");
        }
    }
}
