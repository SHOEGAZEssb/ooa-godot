using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownTorches()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        var setTrigger = (Action<int,bool>)typeof(RoomEntityManager).GetMethod("SetTrigger",flags)!.CreateDelegate(typeof(Action<int,bool>),_entities);
        int[] positions = [0x22,0x2a,0x82,0x8a]; // clean room04a6 layout, tile$08.
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count=1)
            {
                input.CaptureForValidation([],[],Vector2.Zero);
                if (batch) scheduler.Advance(count/60.0,update);
                else for(int i=0;i<count;i++) scheduler.Advance(1.0/60.0,update);
            }
            LoadValidationRoom(4,0xa6);
            _player.WarpTo(new(56,88));
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown torch fixture requires floor at $53.");
            var scanner = _entities.Entities<LightableTorchScannerRoomEntity>().Single();
            var translator = _entities.Entities<TorchTriggerTranslatorRoomEntity>().Single();
            var door = _entities.Entities<DungeonDoorRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(translator)>=_entities.InteractionSlot(door) ||
                _entities.InteractionSlot(door)>=_entities.InteractionSlot(scanner),
                "Crown INTERAC$24, $1e, $c7 must occupy native slots in placed source order.");
            FailIf(_entities.InteractionSlot(scanner)<2,"INTERAC$c7 must occupy a native dynamic interaction slot.");
            Step();
            var torches = _entities.Entities<LightableTorchRoomEntity>().OrderBy(t=>t.PackedPosition).ToArray();
            FailIf(!scanner.Finished || !torches.Select(t=>t.PackedPosition).SequenceEqual(positions) || torches.Any(t=>t.Initialized),
                "INTERAC$c7 must allocate PART$06 in layout order after the current PART pass, then delete itself.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource=()=>true;
                setTrigger(0,true);
                setTrigger(5,true);
                Step();
                FailIf(_entities.ActiveTriggers!=0x20,"State0 translator must clear only its mask under text.");
                FailIf(torches.Any(t=>!t.Initialized),"PART$06 state0 must initialize even while text is active.");
                foreach (var torch in torches)
                    FailIf(torch.ApplySeedHit(torch.CollisionBounds,torch.Position,0x20,new List<RoomEntitySpawn>())!=SeedHitResult.Consume,
                        "Initialized PART$06 must accept the isolated Ember collision.");
                Step(8);
                FailIf(torches.Any(t=>t.Finished) || positions.Any(p=>_currentRoom.Layout[p]!=0x08),
                    "Text must hold pending torch hits without lighting or deleting the initialized parts.");
            }
            finally { _entities.TextActiveSource=text; }
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(_entities.Entities<LightableTorchRoomEntity>().Count!=0 ||
                positions.Any(p=>_currentRoom.Layout[p]!=0x09) ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndLightTorch)!=4 || _entities.ActiveTriggers!=0x21 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)!=0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose)!=0,
                "The next PART pass must light four torches once, delete them, and publish the count before the translator runs.");
            Step(5); // Door setup resumes at setangle; playsound is script update7.
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle)!=1,
                "Torch shutter solve sound must follow its setup, contact and trigger script yields.");
            Step(); // setstate2
            Step();
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose)!=1 || !_currentRoom.IsSolid(door.Position),
                "The next door dispatch must begin interleaving while retaining solid collision.");
            Step(5);
            FailIf(!_currentRoom.IsSolid(door.Position),"The shutter must remain solid until its sixth interleave update.");
            Step();
            FailIf(_currentRoom.IsSolid(door.Position) || _sound.PlayRequestsFor(OracleSoundEngine.SndDoorClose)!=2,
                "The sixth interleave update must finish opening the torch-room shutter.");
            FailIf(_sound.PlayRequestsFor(OracleSoundEngine.SndLightTorch)!=4,"Deleted torches must not light repeatedly.");

            LoadValidationRoom(4,0xa6);
            _player.WarpTo(new(56,88));
            var outgoingTranslator = _entities.Entities<TorchTriggerTranslatorRoomEntity>().Single();
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            torches = _entities.Entities<LightableTorchRoomEntity>().ToArray();
            FailIf(torches.Length!=4 || torches.Any(t=>!t.Initialized || t.Visible) ||
                _entities.Entities<LightableTorchScannerRoomEntity>().Count!=0,
                "Destination preload must run the scanner and initialize its invisible torch children.");
            setTrigger(0,true);
            setTrigger(5,true);
            Step();
            FailIf(!outgoingTranslator.Finished || _entities.OutgoingEntities<TorchTriggerTranslatorRoomEntity>().Count!=0 ||
                _entities.ActiveTriggers!=0x20,
                "Scroll dispatch must delete outgoing enabled02 translator and let incoming state0 clear only its trigger mask.");
            Step(7);
            FailIf(positions.Any(p=>_currentRoom.Layout[p]!=0x08),"Preload must leave untouched torches unlit.");
            _entities.FinishScreenTransition();
            LoadValidationRoom(0,0x60);
        }

        LoadValidationRoom(4,0xa6);
        var record = new DungeonMechanicDatabase().GetRoomRecords(4,0xa6).Single(r=>r.Id==0xc7 && r.SubId==8);
        var attempts = new List<int>();
        var isolated = new LightableTorchScannerRoomEntity(record,_currentRoom,new LightableTorchState(),new DarkRoomDatabase(),
            (_,p)=>{ attempts.Add(p); return p==0x82; });
        var frame = new RoomEntityFrame(_player,0,false);
        isolated.UpdateFrame(frame,new List<RoomEntitySpawn>());
        isolated.UpdateFrame(frame,new List<RoomEntitySpawn>());
        FailIf(!isolated.Finished || !attempts.SequenceEqual(positions),
            "INTERAC$c7 must skip failed allocations, try later tiles, delete, and never retry the scan.");
        isolated.Free();
        var translatorRecord = new DungeonMechanicDatabase().GetRoomRecords(4,0xa6).Single(r=>r.Id==0x24);
        var counts = new LightableTorchState();
        counts.SetTotalTorches(5);
        int writes=0; bool active=false; bool outgoing=false;
        var translatorTest = new TorchTriggerTranslatorRoomEntity(translatorRecord,counts,
            (_,value)=>{ writes++; active=value; },_=>outgoing);
        for(int count=0;count<=5;count++)
        {
            translatorTest.UpdateFrame(frame,new List<RoomEntitySpawn>());
            FailIf(active!=(count==4),"INTERAC$24:$02 compares the lit count exactly to four, not at least four.");
            if(count<5) counts.IncrementLitCount();
        }
        outgoing=true;
        translatorTest.UpdateDuringScreenTransition();
        translatorTest.UpdateDuringScreenTransition();
        FailIf(!translatorTest.Finished || writes!=6,"Outgoing translator must delete before writing any trigger and stay retired.");
        translatorTest.Free();
        LoadValidationRoom(0,0x60);
    }
}
