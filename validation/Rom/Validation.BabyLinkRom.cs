using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBabyLinkRom()
    {
        foreach (bool batch in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5, 0x82);
            _inventory.GiveTreasure(TreasureId.Sword, 1); _inventory.SetScriptedEquippedItems(0, TreasureId.Sword);
            _player.WarpTo(new(120, 136)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Baby Link fixture must begin on original D8 miniboss-room floor.");
            StepGameplayUpdates(3, Vector2.Zero);
            var parent = _entities.Entities<BlueStalfosCharacter>().Single(actor => actor.Record.SubId == 1);
            var active = SomariaPrivate<List<IRoomEntity>>(_entities, "_activeEntities");
            var free = typeof(RoomEntityManager).GetMethod("FreeEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var entity in active.Where(entity => entity.Node != parent).ToArray()) { active.Remove(entity); free.Invoke(_entities, [entity]); }
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4);
            // Declare one newly charged ball at a reachable floor point. Its
            // native charge, firing, contact publication and next Link pass
            // remain live while enemy movement alone is frozen.
            FailIf(!_entities.TryCreatePart(new BlueStalfosProjectileSpawn(new(120,160), -256, parent.Slot, 0)),
                "Baby handoff fixture requires one free native projectile slot.");
            var shot = _entities.Entities<BlueStalfosProjectile>().Single();
            int partSlot = SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities, "_partSlots").Single().Value;
            var seed = _random.CaptureState(); var rom = new SomariaRom(_saveData, seed, _currentRoom, 0,120,136) { HostilePartsEnabled = true };
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 8;
            rom[0xcc8a] = 4; rom[0xcc08] = 0x8e;
            int parentAddress = 0xd080 + parent.Slot * 256;
            rom[parentAddress] = 0x11; rom[parentAddress+1] = 0x77; rom[parentAddress+2] = 1;
            rom[parentAddress+4] = (byte)parent.State; rom[parentAddress+0x29] = 18;
            int partAddress = 0xd0c0 + partSlot * 256;
            rom[partAddress] = 1; rom[partAddress+1] = 0x3d; rom[partAddress+0xb] = 160; rom[partAddress+0xd] = 120; rom[partAddress+0xf] = 0xff;
            rom[partAddress+0x16] = 0x80; rom[partAddress+0x17] = (byte)(parentAddress >> 8);
            int update = 0; var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1, int angle = 0xff, bool press = false) => StepGameplayUpdates(count,
                angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                press ? ["attack"] : [], press ? ["attack"] : [], batch, () => {
                    rom.UpdateGameplay(press ? 1 : 0, (press ? 1 : 0) | (angle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 }), angle, _entities.FrameCounter);
                    press = false; update++;
                    string context = $"Baby Link update{update}, batch={batch}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                        _player.ActiveTransformation != rom[0xd001] || _player.ActiveTransformation == 2 && _player.BabyRemaining != rom.Word(0xd006),
                        $"{context}: native ID/position/lifetime runtime={_player.ActiveTransformation}/{_player.PrecisePosition}/{_player.BabyRemaining}, ROM={rom[0xd001]}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}/{rom.Word(0xd006)}.");
                    FailIf(_player.IsAttacking || !sounds.Requests.SequenceEqual(rom.Sounds), $"{context}: forbidden item allocation or baby/footstep/restore cues differ.");
                    int nativePoofs = Enumerable.Range(0xd2,14).Count(page => rom[page*256+0x40] != 0 && rom[page*256+0x41] == 2);
                    FailIf(_entities.Entities<GreenPoofRoomEntity>().Count != nativePoofs, $"{context}: native GREENPOOF$02 allocation/lifetime differs.");
                    FailIf(_player.BabyRequested != (rom[0xcc57] == 0x82), $"{context}: baby-ball contact must queue its override for the following Link pass.");
                });
            Step(121);
            FailIf(!_player.BabyRequested || _player.ActiveTransformation != 0 || shot.State != 2,
                "The charged baby ball must queue transformation through the actual post-object Link collision pass.");
            Step(); FailIf(_player.ActiveTransformation != 2 || _player.BabyRemaining != 480,
                "Baby Link must initialize its 480-update lifetime on the following special-object pass.");
            FailIf(!shot.Finished || parent.State != 16 || rom[parentAddress+4] != 16,
                "The next part pass must consume Link's contact signal, delete the baby ball and select the parent's baby-chase state$10.");
            active.Remove(_entities.ResolveEnemySlot(parent.Slot)!); free.Invoke(_entities, [_entities.ResolveEnemySlot(parent.Slot)!]);
            for (int offset=0;offset<64;offset++) rom[parentAddress+offset] = 0;
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0); rom[0xcc8a] = 0;
            Step(12, angle: 0, press: true);
            FailIf(_player.Position != new Vector2(120,130) || _player.BabyRemaining != 468,
                "Baby form must move at half normal speed and reject the equipped sword.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0x81); rom[0xcc8a] = 0x81;
            Step(6, angle: 8); FailIf(_player.BabyRemaining != 468, "Disabled-$81 must pause the baby timer and movement.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 0); rom[0xcc8a] = 0;
            Step(467); FailIf(_player.BabyRemaining != 1 || _player.ActiveTransformation != 2,
                "Baby Link must remain transformed immediately before the final eligible countdown update.");
            Step(); FailIf(_player.ActiveTransformation != 2 || _player.BabyRemaining != 0,
                "Timer zero must queue restoration and its magic-powder cue without dispatching normal Link early.");
            Step(2); FailIf(_player.ActiveTransformation != 0, "Baby expiry must hand ownership back to normal Link.");
            _player.QueueBabyTransformation(); rom[0xcc57]=0x82;
            // setLinkIDOverride also clears subid/var03/state/substate.
            for(int address=0xd002;address<=0xd005;address++) rom[address]=0;
            Step(2);
            _player.RequestNativeForcedMovement(4,8,1);
            rom[0xcc4f]=0x0b; rom[0xcc51]=4; rom[0xd008]=1; rom[0xd009]=8;
            Step(7);
            FailIf(_player.ActiveTransformation!=0 || _player.BabyRemaining!=0,
                "A native force-state request must cancel baby Link and execute the normal Link movement handler.");
        }
        GD.Print("Validated clean-US baby-Link queued initialization, 480-update boundary, half speed, item rejection, disabled pause, footsteps/poofs and normal-form handoff.");
    }
}
