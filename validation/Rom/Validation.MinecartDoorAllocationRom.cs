using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMinecartDoorAllocationRom()
    {
        int fixture = 0;
        foreach (int free in new[] { 0,1 })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            // Declared short rail input, shared with the existing native door
            // fixtures; complete original-room routes are outside this test.
            for (int y = 0; y < _currentRoom.HeightInTiles; y++)
            for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                _currentRoom.SetPositionTileAndCollision(new(x*16+8,y*16+8),0xa0,0,0);
            foreach (int y in new[] { 72,88 }) _currentRoom.SetPositionTileAndCollision(new(88,y),0x5e,0,0);
            _currentRoom.SetPositionTileAndCollision(new(88,56),0x7c,0x0f,0);
            MinecartRuntimeState.Reset(_runtimeState,[]);
            MinecartRuntimeState.BeginRide(_runtimeState,0,0,new(88,72),0);
            var cart = new MinecartRoomEntity(new ActiveMinecart(-1,0,72,88,0,true),
                _currentRoom,new DungeonInteractionDatabase(),_runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"),_sound.PlaySound);
            _entities.AddEntity(cart);
            _player.WarpTo(new(88.25f,72.5f)); _player.Face(Vector2I.Up);
            _player.FinishMinecartMount(new(88,72),0,0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,88,72) { CompanionDispatchEnabled = true };
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom[0xd100] = 3; rom[0xd101] = 0x0a; rom[0xd10b] = 72; rom[0xd10d] = 88;
            rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
            var puffs = new List<(PuzzlePuffEffect Puff,int Slot)>();
            for (int index = 0; index < 14-free; index++)
            {
                var puff = _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(200,120),SoundId.MusNone));
                int slot = ((0xd2+index)<<8)|0x40;
                int x = rom.Word(0xd00c),y = rom.Word(0xd00a);
                rom.Word(0xd00c,200*256); rom.Word(0xd00a,120*256); rom[0xffae] = 0;
                byte[] caller = [0x01,0x80,0x05,0x16,0xd0,0xcd,0xc5,0x24,0xc9];
                for (int offset = 0; offset < caller.Length; offset++) rom[0xc100+offset] = caller[offset];
                SomariaPrivate<FrontendRom>(rom,"_rom").Call(0xc100,0);
                rom.Word(0xd00c,x); rom.Word(0xd00a,y);
                FailIf(_entities.InteractionSlot(puff) != index+2 || rom[slot+1] != 5 || rom[slot+2] != 0x80,
                    "Minecart capacity must use real native silent puffs in original first-free slots.");
                puffs.Add((puff,slot));
            }
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0; bool opened = false;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter-1); update++;
                string context = $"Minecart door capacity free={free}, batch={batch}, update={update}";
                FailIf(cart.Position != new Vector2(rom.Word(0xd10c)/256f,rom.Word(0xd10a)/256f) ||
                    cart.Direction != rom[0xd108] || cart.Angle != rom[0xd109] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    context+": failed admission reversal or resumed cart/rider movement differs.");
                var doors = _entities.Entities<MinecartShutterRoomEntity>().OrderBy(_entities.InteractionSlot).ToArray();
                int[] slots = Enumerable.Range(0xd2,14).Select(page => (page<<8)|0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x1e).ToArray();
                FailIf(doors.Length != slots.Length,context+": opener allocation/lifetime differs.");
                for (int index = 0; index < slots.Length; index++)
                    FailIf(_entities.InteractionSlot(doors[index]) != (slots[index]>>8)-0xd0 ||
                        doors[index].PackedPosition != rom[slots[index]+0x3e],
                        context+$": opener slot/packed doorway differs: runtime slot={_entities.InteractionSlot(doors[index])}/position${doors[index].PackedPosition:x2}, native slot${slots[index]:x4}/position${rom[slots[index]+0x3e]:x2}.");
                foreach (var (puff,slot) in puffs)
                    FailIf(puff.Finished == (rom[slot] != 0 && rom[slot+1] == 5),context+": original puff deletion differs.");
                FailIf(_currentRoom.Layout[0x35] != rom[0xcf35] ||
                    _currentRoom.GetTerrainInfo(new(88,56)).Collision != rom[0xce35],context+": door layout/collision differs.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls || !sounds.Requests.SequenceEqual(rom.Sounds),
                    context+": ordered cues/shared RNG differs.");
                opened |= rom[0xcf35] == 0x5e;
            });
            Step();
            FailIf(free == 0 && (cart.Direction != 2 || _entities.Entities<MinecartShutterRoomEntity>().Count != 0) ||
                free == 1 && (_entities.Entities<MinecartShutterRoomEntity>().Count != 1 ||
                    _entities.InteractionSlot(_entities.Entities<MinecartShutterRoomEntity>().Single()) != 15),
                "A full interaction pool must reverse the cart; one free slot must reserve $df before Link/item dispatch.");
            Step(19); FailIf(puffs.Any(row => !row.Puff.Finished),"Native capacity puffs must retire on update20.");
            Step(60); Step(3);
            FailIf(!opened || _currentRoom.IsSolid(new(88,56)),
                "After native puff completion the repeated door approach must admit an opener and release collision.");
        }
    }
}
