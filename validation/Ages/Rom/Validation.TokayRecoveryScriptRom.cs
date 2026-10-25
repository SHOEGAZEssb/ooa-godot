using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTokayRecoveryScriptRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int initFree in new[] { 0, 1 })
        foreach (int free in new[] { 0, 1 })
        foreach (var row in new[] { (6,5,0xca,5), (7,1,0xcb,0x15), (8,1,0xda,0x11), (9,5,0xcc,0x2e), (10,1,0xbb,0x19) })
        {
            var (subid,group,room,treasure)=row;
            int flagsAt=(group==5?0xca00:0xc800)+room;
            var save=OracleSaveData.CreateStandardGame(); save.SetLinkName("Link");
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone); save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);
            _inventory.GiveTreasure(treasure,1); _inventory.LoseTreasure(treasure);
            _inventory.SetScriptedEquippedItems(0,0); _saveData.SetRoomFlag(group,room,0x40,false);
            LoadValidationRoom(group,room);
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==subid);
            F1Retain(slot);
            var actor=(TokayHoldingItemCharacter)slot.Npc;
            Vector2 safe=(from y in Enumerable.Range(12,_currentRoom.Height-24)
                from x in Enumerable.Range(12,_currentRoom.Width-24)
                let p=new Vector2(x,y)
                where !_collision.Collides(p) && (p-actor.Position).LengthSquared()>24*24 &&
                    new[] { new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5) }
                        .All(o=>_currentRoom.GetTerrainInfo(p+o).Hazard==HazardType.None)
                select p).First();
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)safe.X,(int)safe.Y);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=(byte)subid;
            native[at+11]=(byte)actor.Position.Y; native[at+13]=(byte)actor.Position.X;
            _player.WarpTo(safe);
            int updates=0;
            string source=$"Tokay$48:${subid:x2}/initFree{initFree}/free{free}/batched{batched}";
            void Step(int count)
            {
                StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    native.AdvanceInteractions(_entities.FrameCounter); updates++;
                    FailIf(slot.Script!.NativeCounter1!=native[at+6] ||
                        _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a] ||
                        _runtimeState.ReadWramByte(0xcc02)!=native[0xcc02] ||
                        _saveData.GetRoomFlags(group,room)!=native[flagsAt] ||
                        _inventory.HasTreasure(treasure)!=((native[0xc69a+treasure/8]&(1<<(treasure&7)))!=0),
                        source+$" update{updates}: native counter, control masks or treasure publication differs (counter{slot.Script.Counter}/{native[at+6]}, mask{_runtimeState.ReadWramByte(0xcc8a):x2}/{native[0xcc8a]:x2}, item{_inventory.HasTreasure(treasure)}, nativeLink={native[0xd004]:x2}/{native[0xcc4f]:x2}/{native[0xcc5a]:x2} XY={native[0xd00d]},{native[0xd00b]}, sourceTreasures="+
                        string.Join(";",Enumerable.Range(2,14).Select(s=>0xd040+s*256).Where(a=>native[a]!=0)
                            .Select(a=>$"{a:x4}:{native[a]:x2}/{native[a+1]:x2}/{native[a+2]:x2}/v{native[a+3]:x2}/state{native[a+4]} pos{native[a+13]},{native[a+11]}"))+").");
                    native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
                });
            }
            while (14-InteractionAdmissionPool.Count>initFree)
            {
                var puff=new PuzzlePuffEffect(); puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int occupied=0xd040+_entities.InteractionSlot(puff)*256;
                native[occupied]=1;native[occupied+1]=0;native[occupied+4]=1;native[occupied+0x20]=0xff;
            }
            // Even under text and DISABLE_INTERACTIONS, state-zero actors
            // and their newly allocated accessory initialize once.
            _dialogue.ShowMessage("Tokay state0 hold.",104);native[0xcba0]=1;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;
            Step(3);
            foreach(var child in _entities.EntityAdapters<TokayHeldAccessoryRoomEntity>())
            {
                int childAt=0xd040+_entities.InteractionSlot(child.Node)*256;
                FailIf(child.Node.Position!=new Vector2(native[childAt+13],native[childAt+11])||
                    child.Node.ZIndex!=ObjectDrawPriority.FromVisible(native[childAt+0x1a]),
                    source+": state-zero accessory position/draw priority differs under text and object freeze.");
            }
            _dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;Step(3);
            FailIf(_entities.EntityAdapters<TokayHeldAccessoryRoomEntity>().Count()!=initFree ||
                Enumerable.Range(2,14).Count(s=>native[0xd040+s*256]!=0&&native[0xd041+s*256]==0x63)!=initFree,
                source+$": state0 must preserve checked accessory allocation, including failure (runtime={_entities.EntityAdapters<TokayHeldAccessoryRoomEntity>().Count()}, native="+
                string.Join(";",Enumerable.Range(2,14).Select(s=>0xd040+s*256).Where(a=>native[a]!=0)
                    .Select(a=>$"{a:x4}:{native[a+1]:x2}/{native[a+2]:x2} state{native[a+4]} signal{native[a+0x3b]}"))+").");
            if (free!=0)
            {
                var filler=InteractionAdmissionPool.First(pair=>pair.Key is InteractionSlotValidationEntity);
                int released=0xd040+filler.Value*256;
                for (int offset=0;offset<0x40;offset++) native[released+offset]=0;
                _entities.RetireEntity(filler.Key);
            }
            // The A-probe geometry is independently exercised by
            // ValidateTokayItemRecoveryGameplay. Here stage both queued bytes
            // at the original script-slot boundary to isolate native dispatch.
            FailIf(!slot.Script!.TryInteract(new(actor,null),_player),source+": failed to queue A.");
            native[at+0x31]=1;
            Step(1);
            FailIf(!_dialogue.IsOpen,source+": pressed A must open held-item text.");
            _dialogue.Close();native[0xcba0]=0;
            for (int update=0;update<80;update++)
            {
                if (_dialogue.IsOpen) { _dialogue.Close();native[0xcba0]=0; }
                Step(1);
            }
            bool reward=initFree!=0 || free!=0;
            FailIf(!_saveData.HasRoomFlag(group,room,0x40) || _inventory.HasTreasure(treasure)!=reward ||
                _runtimeState.ReadWramByte(0xcc8a)!=0,
                source+": full capacity must skip the reward while preserving the original script continuation and flag.");
            _entities.Clear();
        }
    }
}
