using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMovingPlatformAllocationRom()
    {
        int fixture = 0;
        foreach (int room in new[] {0x6c,0x75})
        foreach (int free in new[] {0,1,2,3})
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 shore = room == 0x6c ? new(40.25f,56.5f) : new(200.25f,40.5f);
            _player.WarpTo(shore); _player.Face(Vector2I.Down);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,(int)shore.X,(int)shore.Y);
            rom.Word(0xd00c,(int)(shore.X*256)); rom.Word(0xd00a,(int)(shore.Y*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = 4;
            for (int slot = 2; slot < 16-free; slot++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                int address = (0xd0+slot)*256+0x40;
                rom[address] = 1; rom[address+1] = 5; rom[address+2] = 0x80;
                rom[address+0xb] = 144; rom[address+0xd] = 224;
            }
            // Declare retained outgoing capacity and execute the actual parser
            // and incoming state0 pass. Room-edge scrolling is excluded here.
            rom.SetOutgoingInteractions(); rom[0xcc05] = 0xff;
            // Match this unrestricted entry's preceding tile substitutions,
            // including the layout shutter that consumes room$75's first slot.
            var frontend = SomariaPrivate<FrontendRom>(rom,"_rom");
            rom[0xcd00] = 4; frontend.ApplyRoomTileSubstitutions();
            int available = Enumerable.Range(0xd2,14).Count(page=>rom[page*256+0x40] == 0);
            rom[0xcd00] = 8;
            SomariaPrivate<FrontendRom>(rom,"_rom").Call(0x55b7,0x12);
            _entities.BeginScreenTransition(4,_currentRoom,Vector2.Zero,_player);
            int[] native = Enumerable.Range(0xd2,14).Select(page=>page*256+0x40)
                .Where(address=>rom[address] != 0 && rom[address+1] == 0x79).ToArray();
            var platforms = _entities.Entities<MovingPlatformRoomEntity>().OrderBy(_entities.InteractionSlot).ToArray();
            // Independent original mainData.s rows, not imported expectations.
            (int Sub,int X,int Y)[] expected = room == 0x6c
                ? [(3,160,88),(8,104,40)] : [(0x1a,168,40),(0x23,104,72),(0x29,104,144)];
            FailIf(platforms.Length != native.Length || platforms.Length != System.Math.Min(available,expected.Length),
                $"Original platform parser room$4:${room:x2}, free={free}: count={platforms.Length}/{native.Length}, slots=[{string.Join(',',native.Select(address=>$"{address:x4}:{rom[address+2]:x2}"))}], owners=[{string.Join(',',SomariaPrivate<System.Collections.Generic.Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots").Select(pair=>$"{pair.Value:x2}:{pair.Key.GetType().Name}"))}].");
            for (int index = 0; index < platforms.Length; index++)
            {
                int address = native[index]; var row = expected[index];
                FailIf((address>>8)-0xd0 != 16-available+index || _entities.InteractionSlot(platforms[index]) != (address>>8)-0xd0 ||
                    rom[address+2] != row.Sub || rom[address+0xd] != row.X || rom[address+0xb] != row.Y || rom[address+4] != 0,
                    "Original parser must preserve physical slot order and pending INTERAC$79 source parameters.");
            }
            rom.AdvanceInteractions(_entities.FrameCounter);
            for (int index = 0; index < platforms.Length; index++)
            {
                int address = native[index]; var platform = platforms[index];
                FailIf(!platform.Visible || rom[address+4] != 1 || platform.Counter != 8 || rom[address+6] != 8 ||
                    platform.PrecisePosition != new Vector2(rom.Word(address+0xc)/256f,rom.Word(address+0xa)/256f) ||
                    platform.CollisionRadii != new Vector2(rom[address+0x27],rom[address+0x26]),
                    "Incoming platform state0 must initialize its graphics/script/radii once without moving or claiming Link.");
            }
            var parsedRandom = _random.CaptureState();
            FailIf(parsedRandom.Rng1 != rom[0xff94] || parsedRandom.Rng2 != rom[0xff95] ||
                parsedRandom.Calls-seed.Calls != rom.RandomCalls,
                "Failed interaction placements must preserve full native room-parse RNG consumption.");
            _entities.FinishScreenTransition(); rom.ClearOutgoingInteractions(); rom[0xcd00] = 4;
            var sounds = _sound.AttachPlayRequestAudit();
            StepGameplayUpdates(3,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter,() => rom[0xcc96] = rom[0xcc8d]);
                FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != platforms.Length,
                    "Released outgoing slots must not recreate skipped original platform rows.");
                foreach (var platform in platforms)
                {
                    int address = (0xd0+_entities.InteractionSlot(platform))*256+0x40;
                    FailIf(platform.Counter != rom[address+6] || platform.PrecisePosition != new Vector2(rom.Word(address+0xc)/256f,rom.Word(address+0xa)/256f),
                        "First post-preload gameplay updates must resume the retained native wait8 exactly.");
                }
                FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    "Post-preload platform allocation must preserve Link and sound order.");
            });
        }
    }
}
