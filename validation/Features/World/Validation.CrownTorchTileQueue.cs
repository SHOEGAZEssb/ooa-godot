using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownTorchTileQueue()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        int[] positions = [0x22,0x2a,0x82,0x8a];
        foreach (bool batch in new[] { false,true })
        foreach (bool full in new[] { false,true })
        {
            void Step(int count=1)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                if(batch) scheduler.Advance(count/60.0,update);
                else for(int i=0;i<count;i++) scheduler.Advance(1.0/60.0,update);
            }
            byte[] Graphics(int p) => Enumerable.Range(0,4).SelectMany(i => new[] {
                _currentRoom.GetBackgroundSubtileForValidation((p&15)*2+i%2,(p>>4)*2+i/2),
                _currentRoom.GetBackgroundAttributeForValidation((p&15)*2+i%2,(p>>4)*2+i/2)
            }).ToArray();
            LoadValidationRoom(4,0xa6);
            _player.WarpTo(new(56,88));
            FailIf(_currentRoom.IsSolid(_player.Position),"Torch queue fixture requires floor at $53.");
            Step(2);
            var before = positions.Select(Graphics).ToArray();
            var torches = _entities.Entities<LightableTorchRoomEntity>().ToArray();
            FailIf(torches.Length!=4 || torches.Any(t=>!t.Initialized),"Torch queue fixture requires four initialized source parts.");
            foreach(var torch in torches)
                FailIf(torch.ApplySeedHit(torch.CollisionBounds,torch.Position,0x20,new List<RoomEntitySpawn>())!=SeedHitResult.Consume,
                    "Torch queue fixture must register each Ember collision.");
            for(int i=0;i<(full?31:9);i++)
                FailIf(!_rooms.TrySetTile(0x53,0xa0),"Fixture failed to fill the native queue with repeated floor writes.");
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(_entities.Entities<LightableTorchRoomEntity>().Count!=0 || (_entities.ActiveTriggers&1)==0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndLightTorch)!=4 ||
                _rooms.PendingTileGraphics!=(full?27:9) ||
                positions.Any(p=>_currentRoom.Layout[p]!=(full?0x08:0x09)) ||
                positions.Where((p,i)=>!Graphics(p).SequenceEqual(before[i])).Any(),
                "PART$06 must count/sound/delete regardless of queue capacity; accepted logical writes precede graphics, rejected writes change neither.");
            if(!full)
            {
                Step(2); // Four graphics entries per update: only the fourth torch remains queued.
                FailIf(_rooms.PendingTileGraphics!=1 ||
                    positions.Take(3).Where((p,i)=>Graphics(p).SequenceEqual(before[i])).Any() ||
                    !Graphics(positions[3]).SequenceEqual(before[3]),
                    "Ordered queue drain must display the first three lit torches while retaining the fourth unlit graphic.");
                Step();
                FailIf(_rooms.PendingTileGraphics!=0 || Graphics(positions[3]).SequenceEqual(before[3]),
                    "The next drain must display the final accepted torch write.");
            }
            else
            {
                Step(7);
                FailIf(_rooms.PendingTileGraphics!=0 || positions.Any(p=>_currentRoom.Layout[p]!=0x08) ||
                    _entities.Entities<LightableTorchRoomEntity>().Count!=0 ||
                    positions.Where((p,i)=>!Graphics(p).SequenceEqual(before[i])).Any(),
                    "Freeing queue capacity must not retry a deleted torch or reconstruct its rejected write.");
            }
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndLightTorch)!=4,"Queue drain must not replay torch sounds.");
            LoadValidationRoom(0,0x60);
            FailIf(_rooms.PendingTileGraphics!=0,"Room departure must clear the changed-tile queue.");
        }
    }
}
