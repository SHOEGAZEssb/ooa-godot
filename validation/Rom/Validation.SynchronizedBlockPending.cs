using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockPending()
    {
        int fixture = 0;
        foreach (byte tile in new byte[] { 0x2a,0xa0,0xda })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x9e); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Bracelet,1); _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f,156.5f)); _player.Face(Vector2I.Down);
            bool neighbor = _rooms.TryGetNeighbor(Vector2I.Down,out int destination);
            FailIf(_currentRoom.IsSolid(_player.Position) || !neighbor || destination != 0xa2,
                "Pending block scroll must use original south-exit floor and imported neighbor4:a2.");
            // Declare the allocation boundary below the original synchronizer's
            // cursor. Its normal producer is exercised by the gameplay cases;
            // this child has not dispatched state0 when scrolling takes over.
            FailIf(!_entities.TryCreateSynchronizedBlock(0x37,0),"Pending$14 must allocate first-free$d2.");
            var block = _entities.Entities<PushBlockController>().Single();
            FailIf(block.Active || _entities.InteractionSlot(block) != 2,
                "A newly allocated child must retain pending state0 in$d2.");
            var target = _rooms.GetRoom(4,destination);
            byte sourceTarget = _currentRoom.Layout[0x27],targetTile = target.Layout[0x27];
            var source = _currentRoom;
            var graphics = new ScreenTransitionGraphicsDatabase();
            int sourceUnique = graphics.ForTileset(source.TilesetId).Unique |
                (source.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,source,2,120,156);
            rom.Word(0xd00c,120*256+64); rom.Word(0xd00a,156*256+128);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x14; rom[0xd249] = 0;
            rom[0xd24b] = 54; rom[0xd24d] = 120; rom[0xd270] = 0x37;
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(scrolling:true); rom[0xcd00] = 8;
            _transitions.BeginScroll(_player,Vector2I.Down,destination);
            // Declare another tile owner's change after room loading and before
            // the next interaction pass. State0 reads this current-room byte.
            FailIf(!_rooms.TrySetTile(0x37,tile),"Declared current tile must fit its canonical queue.");
            target.SetUnderlyingStorageMetatile(0x37,0xa0);
            rom[0xcc30] = 0xa2; rom.CopyRoom(_currentRoom);
            FailIf(_currentRoom.Layout[0x37] != tile || rom[0xd240] != 2,
                "Scroll boundary must retain the declared current tile and outgoing pending allocation.");
            int unique = graphics.ForTileset(target.TilesetId).Unique |
                (target.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var scroll = new ScrollRom(true,2,(int)(_player.PrecisePosition.X*256),
                (int)(_player.PrecisePosition.Y*256),unique,sourceUnique);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            // Incoming parser/enemies/PARTs, preload RNG and pixels are outside
            // this outgoing state0/dispatcher/scroll-control comparison.
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                bool scrolling = scroll[0xcd04] != 2;
                if (scrolling)
                {
                    rom.AdvanceInteractions(_entities.FrameCounter); scroll.Update();
                    if (scroll[0xcd04] == 2) rom.ClearOutgoingInteractions();
                }
                string context = $"Pending$14 tile${tile:x2}, batch={batch}, update{++update}";
                bool alive = rom[0xd240] != 0;
                FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2) || alive && !block.Active ||
                    _entities.OutgoingEntities<PushBlockController>().Count != (alive ? 1 : 0),
                    context+": outgoing allocation/cleanup differs.");
                if (alive) CompareNativePushBlockRom(rom,block,0xd240,context);
                if (scrolling)
                {
                    Vector2 point = new(((int)(_player.PrecisePosition.X*256)&0xffff)/256f,
                        ((int)(_player.PrecisePosition.Y*256)&0xffff)/256f);
                    FailIf(point != new Vector2(scroll.Word(0xd00c)/256f,scroll.Word(0xd00a)/256f),
                        context+": native scroll Link coordinates differ.");
                }
                FailIf(target.Layout[0x37] != rom[0xcf37] ||
                    target.GetTerrainInfo(new(120,56)).Collision != rom[0xce37] ||
                    target.GetUnderlyingMetatile(new(120,56)) != rom.Underlying(0x37) ||
                    target.Layout[0x27] != targetTile || source.Layout[0x27] != sourceTarget ||
                    _rooms.BlockPushAngle != rom[0xcca6] ||
                    !sounds.Requests.Where(cue => cue == SoundId.SndMoveBlock).SequenceEqual(rom.Sounds),
                    context+": source replacement/shared direction/cues or cancelled destination writes differ.");
            });
            Step();
            FailIf(!block.NativeInitialized || block.ActiveTile != tile ||
                block.BlockTopLeft != new Vector2(112,47.5f) || target.Layout[0x37] != 0xa0 ||
                rom[0xd246] != 31 || sounds.Requests.Count(cue => cue == SoundId.SndMoveBlock) != 1,
                "Scroll state0 must read the current tile, restore its floor, move once and spend one counter update.");
            Step(_transitions.ScrollTotalFrames-1); Step(3);
            FailIf(_entities.OutgoingEntities<PushBlockController>().Count != 0 || rom[0xd240] != 0,
                "Native scroll cleanup must cancel pending-child movement without a later destination write.");
        }
    }
}
