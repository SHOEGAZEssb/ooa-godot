using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSynchronizedBlockScroll()
    {
        foreach (bool batch in RomHostSchedules(0))
        {
            var (rom,synchronizer,seed) = PrepareSynchronizedBlockRom(0x9e,0x77,0);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Approach() => StepSomariaMotionRom(rom,1,batch,0,afterUpdate:() => {
                rom.AdvanceTileGraphics();
                CompareSynchronizedPushBlocksRom(rom,synchronizer,seed,sounds.Requests,
                    $"Before block scroll, batch={batch}, update{++update}");
            });
            for (int wait = 0; !_pushBlocks.Active && wait < 80; wait++)
            {
                Approach();
                FailIf(_currentRoom.IsSolid(_player.Position),"Statue push must approach through unchanged floor.");
            }
            FailIf(!_pushBlocks.NativeInitialized || _entities.Entities<PushBlockController>().Count != 1,
                "Actual statue contact must initialize reserved$d1 and descending partner$d3 before departure.");
            var partner = _entities.Entities<PushBlockController>().Single();
            FailIf(_entities.InteractionSlot(partner) != 3 || rom[0xd146] != 31 || rom[0xd346] != 31,
                "Both original statues must have advanced exactly once before the scroll boundary.");
            var source = _currentRoom;
            byte primaryTarget = source.Layout[0x47],partnerTarget = source.Layout[0x27];
            Vector2 primaryPoint = _pushBlocks.BlockTopLeft,partnerPoint = partner.BlockTopLeft;
            // Declare Link's departure boundary on the real south exit floor.
            // No route from the moving statue through the room's pits is claimed.
            _player.WarpTo(new(120.25f,156.5f)); _player.Face(Vector2I.Down);
            bool neighbor = _rooms.TryGetNeighbor(Vector2I.Down,out int target);
            FailIf(source.IsSolid(_player.Position) || !neighbor || target != 0xa2,
                "Outgoing blocks require source exit floor and imported neighbor4:9e ->4:a2.");
            var graphics = new ScreenTransitionGraphicsDatabase();
            int sourceUnique = graphics.ForTileset(source.TilesetId).Unique |
                (source.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(scrolling:true); rom[0xcd00] = 8;
            _transitions.BeginScroll(_player,Vector2I.Down,target);
            FailIf(_player.PrecisePosition != new Vector2(120.25f,169.5f) ||
                rom[0xd140] != 2 || rom[0xd340] != 2,
                "Native departure clamps only Link.yh and marks both initialized allocations as enabled02.");
            rom[0xcc30] = 0xa2; rom.CopyRoom(_currentRoom);
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var scroll = new ScrollRom(true,2,120*256+64,169*256+128,unique,sourceUnique);
            int soundStart = sounds.Requests.Count,nativeStart = rom.Sounds.Count;
            // Execute original scroll control and outgoing interaction dispatch.
            // Incoming allocation, enemy/PART AI, preload RNG and pixels have
            // separate comparisons and are excluded from this fixture.
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                bool scrolling = scroll[0xcd04] != 2;
                if (scrolling)
                {
                    rom.AdvanceInteractions(_entities.FrameCounter); scroll.Update();
                    if (scroll[0xcd04] == 2) rom.ClearOutgoingInteractions();
                }
                string context = $"Initialized block scroll batch={batch}, update{++update}";
                FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2),context+": scroll completion differs.");
                CompareNativePushBlockRom(rom,_pushBlocks,0xd140,context);
                bool alive = rom[0xd340] != 0;
                FailIf(_entities.OutgoingEntities<PushBlockController>().Count != (alive ? 1 : 0),
                    context+": dynamic block cleanup differs.");
                if (alive)
                {
                    CompareNativePushBlockRom(rom,partner,0xd340,context);
                    FailIf(_pushBlocks.BlockTopLeft != primaryPoint || partner.BlockTopLeft != partnerPoint ||
                        _pushBlocks.Position != partner.Position,
                        context+": initialized blocks must retain world coordinates and share the outgoing draw offset.");
                }
                if (scrolling)
                {
                    Vector2 point = new(((int)(_player.PrecisePosition.X*256)&0xffff)/256f,
                        ((int)(_player.PrecisePosition.Y*256)&0xffff)/256f);
                    FailIf(point != new Vector2(scroll.Word(0xd00c)/256f,scroll.Word(0xd00a)/256f),
                        context+": original scroll Link fixed coordinates differ.");
                }
                FailIf(source.Layout[0x47] != primaryTarget || source.Layout[0x27] != partnerTarget ||
                    sounds.Requests.Skip(soundStart).Any(cue => cue == SoundId.SndMoveBlock) ||
                    rom.Sounds.Skip(nativeStart).Any(),
                    context+": frozen/cancelled blocks must not emit delayed movement cues or destination writes.");
            });
            Step(_transitions.ScrollTotalFrames); Step(3);
            FailIf(_pushBlocks.Active || _pushBlocks.Visible || _pushBlocks.Position != Vector2.Zero ||
                _entities.OutgoingEntities<PushBlockController>().Count != 0,
                "Native scroll cleanup must clear both owners without completing either movement.");
        }
    }
}
