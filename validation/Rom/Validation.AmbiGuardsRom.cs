using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAmbiGuardsRom()
    {
        var database = new EnemyDatabase();
        var rooms = (from groupId in new[] { 1, 5 } from room in Enumerable.Range(0, 256)
            where database.GetRoomObjects(groupId, room).Any(row => row.Id == 0x54)
            select (groupId, room)).ToArray();
        int placements = 0, fixture = 0;
        foreach (var (group, room) in rooms)
        foreach (bool batch in RomHostSchedules(group == 1 && room == 0x37 ? 0 : fixture++))
        {
            ReinitializeGameplayForValidation();
            _inventory.GiveTreasure(TreasureId.Essence, 5);
            _saveData.SetGlobalFlag(0x0b); _saveData.SetGlobalFlag(0x32);
            LoadValidationRoom(group, room); _inventory.EquipA(0); _inventory.EquipB(0);
            var guards = _entities.EntityAdapters<AmbiGuardRoomEntity>().ToArray();
            if (!batch) placements += guards.Length;
            // Use the room's actual floor; native sighting parts remain active.
            var floor = (from y in Enumerable.Range(1, _currentRoom.HeightInTiles - 2)
                from x in Enumerable.Range(1, _currentRoom.WidthInTiles - 2)
                let position = new Vector2(x * 16 + 8, y * 16 + 8)
                where !_collision.Collides(position)
                orderby guards.Min(guard => position.DistanceSquaredTo(guard.Actor.Position)) descending
                select position).First();
            _player.WarpTo(floor);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)floor.X, (int)floor.Y)
            { HostileEnemiesEnabled = true, HostilePartsEnabled = true, CompanionDispatchEnabled = true };
            rom[0xcc08] = 0x4d; rom[0xcc0a] = 0;
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            rom[0xd024] = 0x80; // Link's initialized collision identity.
            rom[0xd026] = rom[0xd027] = 6;
            foreach (var guard in guards)
            {
                int address = 0xd080 + guard.Slot * 256;
                rom[address] = 0x11; rom[address + 1] = 0x54; rom[address + 2] = (byte)guard.Actor.SubId;
                rom.Word(address + 10, (int)(guard.Actor.Position.Y * 256));
                rom.Word(address + 12, (int)(guard.Actor.Position.X * 256));
            }
            int update = 0;
            bool warningSeen = false;
            StepGameplayUpdates(group == 1 && room == 0x37 ? 270 : 180, Vector2.Zero, batched: batch, afterUpdate: () =>
            {
                rom.AdvanceWarpPalette();
                if (rom[0xc2ef] == 0x14) rom.AdvancePalaceBoot();
                rom.UpdateGameplay(0, 0, 0xff, ++update);
                rom.SelectTriggeredCutscene();
                FailIf(_runtimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled) != rom[0xcc02] ||
                    _runtimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != rom[WramAddress.wDisableScreenTransitions],
                    $"Guard capture masks at update={update}, batch={batch}: objects=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/${rom[0xcc8a]:x2}, menu=${_runtimeState.ReadWramByte(WramAddress.wMenuDisabled):x2}/${rom[0xcc02]:x2}, transitions=${_runtimeState.ReadWramByte(WramAddress.wDisableScreenTransitions):x2}/${rom[WramAddress.wDisableScreenTransitions]:x2}, room=${_rooms.ActiveGroup:x}:${_rooms.CurrentRoom.Id:x2}.");
                FailIf(_rooms.ActiveGroup != rom[0xcc2d] || _rooms.CurrentRoom.Id != rom[0xcc30],
                    $"Palace capture room handoff differs at update {update}: port=${_rooms.ActiveGroup:x}:${_rooms.CurrentRoom.Id:x2}, ROM=${rom[0xcc2d]:x}:${rom[0xcc30]:x2}.");
                if (_rooms.CurrentRoom.Id == 0x46)
                {
                    FailIf(_player.PalaceTossSubstate >= 0 && _player.Position != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f),
                        $"Palace toss Link motion differs at update {update}: port={_player.Position}, ROM={rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, substate={_player.PalaceTossSubstate}/{rom[0xd005]}.");
                    if (_dialogue.IsOpen)
                    {
                        FailIf(rom[0xcba0] == 0 || _player.PalaceTossSubstate != 2 || rom[0xd005] != 2,
                            "Palace warning must begin after the native bounce, with text active in both implementations.");
                        warningSeen = true; _dialogue.Close(); rom[0xcba0] = 0;
                    }
                }
                foreach (var guard in guards.Where(guard => _entities.EntityAdapters<AmbiGuardRoomEntity>().Contains(guard)))
                {
                    int address = 0xd080 + guard.Slot * 256;
                    var actor = guard.Actor;
                    FailIf(actor.State != rom[address + 4] || actor.Counter != rom[address + 6] || actor.Counter2 != rom[address + 7] ||
                        actor.Angle != rom[address + 9] || actor.Speed != rom[address + 0x10] ||
                        actor.Position != new Vector2(rom.Word(address + 12) / 256f, rom.Word(address + 10) / 256f) ||
                        actor.Seen != rom[address + 0x34] || actor.Alert != rom[address + 0x35] || actor.Delay != rom[address + 0x36],
                        $"Ambi guard ${group:x}:${room:x2}/$54:${actor.SubId:x2}, update={update}, batch={batch}: " +
                        $"port state/counters={actor.State:x2}/{actor.Counter}/{actor.Counter2}, XY={actor.Position}, angle/speed={actor.Angle:x2}/{actor.Speed:x2}, seen/alert/delay={actor.Seen}/{actor.Alert}/{actor.Delay}; " +
                        $"ROM={rom[address+4]:x2}/{rom[address+6]}/{rom[address+7]}, XY={rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, angle/speed={rom[address+9]:x2}/{rom[address+0x10]:x2}, seen/alert/delay={rom[address+0x34]}/{rom[address+0x35]}/{rom[address+0x36]}.");
                }
            });
            if (group == 1 && room == 0x37)
                FailIf(!warningSeen || _roomEvents.Get<PalaceBootEvent>().HasState ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wMenuDisabled) != 0 ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != 0 ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != 0,
                    "Palace capture must finish its warning and restore controls and transitions.");
        }
        FailIf(placements != 26, $"Ambi guard rooms must admit all 26 placed source records, got {placements}.");
        // Contrast a direct strike, an indirect bomb, and the masked-out
        // boomerang without expanding the placement matrix into item cases.
        foreach(int collision in new[]{4,0x18,0x17})
        {
            ReinitializeGameplayForValidation();_inventory.GiveTreasure(TreasureId.Essence,5);
            _saveData.SetGlobalFlag(0x0b);_saveData.SetGlobalFlag(0x32);LoadValidationRoom(1,0x35);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x18,0x68));
            var guards=_entities.EntityAdapters<AmbiGuardRoomEntity>().ToArray();var guard=guards[0];
            var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,2,0x18,0x68)
                {CompanionDispatchEnabled=true,HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom[0xcc08]=0x4d;rom[0xcc0a]=0;
            foreach(var enemy in guards)
            {
                int address=0xd080+enemy.Slot*256;rom[address]=0x11;rom[address+1]=0x54;rom[address+2]=(byte)enemy.Actor.SubId;
                rom.Word(address+10,(int)(enemy.Actor.Position.Y*256));rom.Word(address+12,(int)(enemy.Actor.Position.X*256));
            }
            int update=0,addressGuard=0xd080+guard.Slot*256;
            void Compare()
            {
                rom.UpdateGameplay(0,0,0xff,++update);
                FailIf(guard.Actor.State!=rom[addressGuard+4] || guard.Actor.Seen!=rom[addressGuard+0x34] ||
                    guard.Actor.Alert!=rom[addressGuard+0x35] || guard.Actor.Delay!=rom[addressGuard+0x36] ||
                    guard.Actor.InvincibilityCounter!=unchecked((sbyte)rom[addressGuard+0x2b]) ||
                    guard.Actor.Health!=rom[addressGuard+0x29],
                    $"Ambi guard collision${collision:x2} update={update}: port state/seen/alert/delay/inv/HP={guard.Actor.State:x2}/{guard.Actor.Seen}/{guard.Actor.Alert}/{guard.Actor.Delay}/{guard.Actor.InvincibilityCounter}/{guard.Actor.Health}, ROM={rom[addressGuard+4]:x2}/{rom[addressGuard+0x34]}/{rom[addressGuard+0x35]}/{rom[addressGuard+0x36]}/{unchecked((sbyte)rom[addressGuard+0x2b])}/{rom[addressGuard+0x29]}.");
            }
            StepGameplayUpdates(2,Vector2.Zero,afterUpdate:Compare);
            rom.HitEnemyWithItem(guard.Slot,collision,1);
            bool accepted=guard.ApplyItemCollision((RoomEntityItemCollision)collision,guard.Actor.CollisionBounds,guard.Actor.Position-Vector2.Right,1,new System.Collections.Generic.List<RoomEntitySpawn>());
            FailIf(!accepted,$"Ambi guard collision${collision:x2} violates its native active-collision mask.");
            StepGameplayUpdates(4,Vector2.Zero,afterUpdate:Compare);
        }
        ReinitializeGameplayForValidation();LoadValidationRoom(1,0x37);
        _roomEvents.Get<PalaceBootEvent>().Begin();StepGameplayUpdates(10,Vector2.Zero);
        LoadValidationRoom(1,0x36);StepGameplayUpdates(2,Vector2.Zero);
        FailIf(_roomEvents.Get<PalaceBootEvent>().HasState || _player.PalaceTossSubstate>=0 || !_player.Visible ||
            _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0,
            "Cancelling palace capture must release its fade, toss, and object lock.");
    }
}
