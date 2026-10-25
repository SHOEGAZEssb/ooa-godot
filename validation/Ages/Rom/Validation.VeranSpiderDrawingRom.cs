using Godot;
using System.Reflection;
using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private async Task ValidateVeranSpiderDrawingRom()
    {
        ReinitializeGameplayForValidation();_saveData.SetRoomFlag(4,0xfc,0x80);LoadValidationRoom(4,0xfc);_entities.Clear();
        _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,152));
        _scene.ProcessMode=ProcessModeEnum.Disabled;
        _entities.WorldToScreen=static position=>position;
        var spider=(VeranSpiderRoomEntity)_entities.TryCreateVeranSpider(new(120,88))!;var actor=spider.Actor;
        var initial=_random.CaptureState();var rom=new SomariaRom(_saveData,initial,_currentRoom,0,120,152)
            {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
        rom.InitializeLinkGameplay();rom[0xd080]=1;rom[0xd081]=0x0f;rom[0xd082]=1;rom[0xd08b]=88;rom[0xd08d]=120;rom[0xcdd1]=1;
        int update=0,draws=0,drawnZ=0;Vector2 drawnOffset=default;
        var drawOffset=typeof(VeranSpiderCharacter).GetProperty("AnimationDrawOffset",BindingFlags.Instance|BindingFlags.NonPublic)!;
        void CaptureDraw(){draws++;drawnZ=actor.ZFixed>>8;drawnOffset=(Vector2)drawOffset.GetValue(actor)!;}
        actor.Draw+=CaptureDraw;
        void Compare()
        {
            rom.UpdateGameplay(0,0,255,++update);
            bool dead=rom[0xd080]==0;
            FailIf(spider.Finished!=dead||!dead&&(actor.State!=rom[0xd084]||actor.ZFixed!=unchecked((short)rom.Word(0xd08e))||
                actor.SpeedZ!=unchecked((short)rom.Word(0xd094))||actor.Position!=new Vector2(rom.Word(0xd08c)/256f,rom.Word(0xd08a)/256f)||
                SomariaPrivate<int>(actor.Animation,"_frameCounter")!=rom[0xd0a0]||actor.AnimationParameter!=rom[0xd0a1])||
                _random.CaptureState().Calls!=initial.Calls+rom.RandomCalls,
                $"Spider falling draw update{update}: native state/ground XY/Z/speedZ/animation/RNG differs.");
        }
        void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:count>1,afterUpdate:Compare);
        async Task FlushDraws()
        {
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        }
        try
        {
            for(int bound=0;bound<12&&actor.State==0;bound++)Step(1);
            FailIf(actor.State!=8||actor.ZFixed>=0,"Spider must initialize above the screen in native falling state8.");
            await FlushDraws();
            FailIf(draws==0,"Spider fixture must observe an actual deferred _Draw notification.");
            Vector2 ground=actor.Position;
            foreach(int count in new[]{12,4})
            {
                int before=draws,previousZ=drawnZ;
                if(count==4)for(int i=0;i<count;i++)Step(1);else Step(count);
                await FlushDraws();
                int expectedZ=unchecked((sbyte)rom[0xd08f]);
                FailIf(actor.State!=8||draws<=before||drawnZ!=expectedZ||drawnZ<=previousZ||actor.Position!=ground||
                    drawnOffset!=actor.Animation.CurrentOffset+Vector2.Down*expectedZ,
                    $"Spider state8 must redraw falling Z without advancing animation or ground XY: draws{before}->{draws}, Z{previousZ}->{drawnZ}/{expectedZ}.");
            }
            for(int bound=0;bound<96&&actor.State==8;bound++)Step(1);
            await FlushDraws();
            FailIf(actor.State!=9||drawnZ!=0||drawnOffset!=actor.Animation.CurrentOffset,
                "Spider landing must draw at its source ground position with no stale airborne offset.");
            _runtimeState.SetWramByte(0xcfd0,1);rom[0xcfd0]=1;Step(1);
            FailIf(!spider.Finished||rom[0xd080]!=0,"The defeat cutscene must still remove the rendered spider.");
        }
        finally
        {
            if(GodotObject.IsInstanceValid(actor))actor.Draw-=CaptureDraw;
            _scene.ProcessMode=ProcessModeEnum.Inherit;
            _entities.WorldToScreen=_transitions.WorldToGameplayScreen;
        }
    }
}
