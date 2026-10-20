using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateWildTokayMeatRom()
    {
        foreach(bool batch in new[]{false,true})
        foreach(int free in new[]{0,1})
        foreach(int angle in new[]{0xff,8,24})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(TreasureId.Bracelet,1);_inventory.SetScriptedEquippedItems(0,TreasureId.Bracelet);
            LoadValidationRoom(2,0xde);_roomEvents.Get<WildTokayGameEvent>().Cancel();_entities.Clear();_player.WarpTo(new(80,80));_player.Face(Vector2I.Up);
            var meat=(WildTokayMeat)_entities.TryCreateInteraction(new WildTokayMeatSpawn())!;
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,80);
            native.InitializeLinkGameplay();native.InitializeLinkWalkingAnimation();native[0xcc39]=8;native[0xd240]=1;native[0xd241]=0x8c;
            int update=0;string source=$"Wild meat$8c/free{free}/angle${angle:x2}/batch{batch}";
            void Step(int n=1,int movement=0xff,bool press=false)=>StepGameplayUpdates(n,movement==0xff?Vector2.Zero:OracleObjectMath.StrictCardinalVector(movement),press?["attack"]:[],press?["attack"]:[],batch,()=>
            {
                int buttons=(press?1:0)|(movement switch{0=>0x40,8=>0x10,16=>0x80,24=>0x20,_=>0});
                native.UpdateGameplay(press?1:0,buttons,movement,_entities.FrameCounter);press=false;update++;
                CompareSomariaMotionRom(native,source+$" update{update}",allowReservedBracelet:true);
                FailIf(_runtimeState.ReadWramByte(0xcfda)!=native[0xcfda]||_runtimeState.ReadWramByte(0xcfdb)!=native[0xcfdb],source+$" update{update}: active meat pointer differs.");
                foreach(var actor in _entities.Entities<WildTokayMeat>())
                {
                    int at=0xd040+_entities.InteractionSlot(actor)*256;
                    FailIf(native[at+1]!=0x8c||actor.State!=native[at+4]||actor.Substate!=native[at+5]||actor.Counter!=native[at+6]||actor.Position!=new Vector2(native[at+13],native[at+11])||
                        actor.ZFixed!=(short)native.Word(at+14),source+$" update{update}: native meat state, counter, coordinate or Z differs (slot{at:x4}, state{actor.State}/{native[at+4]}, sub{actor.Substate}/{native[at+5]}, counter{actor.Counter}/{native[at+6]}, position{actor.Position}/{native[at+13]},{native[at+11]}, z{actor.ZFixed}/{(short)native.Word(at+14)}).");
                }
                bool item=native[0xdc00]!=0;FailIf(_entities.ReservedBraceletChildActive!=item,source+$" update{update}: reserved ITEM C lifetime differs.");
                if(item)
                {
                    var motion=meat.ReservedThrow!;
                    FailIf(motion.Position!=new Vector2(native.Word(0xdc0c)/256f,native.Word(0xdc0a)/256f)||unchecked((ushort)motion.ZFixed)!=native.Word(0xdc0e)||motion.SpeedZ!=(short)native.Word(0xdc14)||motion.Speed!=native[0xdc10]||motion.Angle!=native[0xdc09],source+$" update{update}: source weight0 reserved throw differs.");
                }
            });
            Step(70);Step(8,0);FailIf(_collision.Collides(_player.Position),"Meat approach must stay on room floor.");
            while(14-InteractionAdmissionPool.Count>free)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);_entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int at=0xd040+_entities.InteractionSlot(puff)*256;native[at]=1;native[at+1]=0x48;native[at+2]=0x1b;native[at+4]=1;
            }
            Step(1,press:true);Step(12);
            FailIf(!_player.IsCarryingObject||meat.Substate!=(free==0?0:1),source+": native checked replacement did not retain its retry/held substate.");
            if(free==0)
            {
                var filler=InteractionAdmissionPool.First(p=>p.Key.Node is PuzzlePuffEffect);int at=0xd040+filler.Value*256;
                _entities.RetireEntity(filler.Key);for(int i=0;i<0x40;i++)native[at+i]=0;Step();
                FailIf(meat.Substate!=1||_entities.Entities<WildTokayMeat>().Count!=2,source+": source replacement must retry on the first available physical slot.");
            }
            for(int i=0;i<20&&_bracelet.State!=BraceletState.Idle;i++)Step();
            FailIf(_bracelet.State!=BraceletState.Idle,source+": native lift sequence did not finish before release.");
            Step(1,angle,true);Step(70);
            FailIf(!meat.Finished||_player.IsCarryingObject||_entities.ReservedBraceletChildActive,source+": dropped meat must expire after source counter20 and release ITEM C.");
            _entities.Clear();
        }
    }
}
