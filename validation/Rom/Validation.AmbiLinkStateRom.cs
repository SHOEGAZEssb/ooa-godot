using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAmbiLinkStateRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(4,0xfc,0x80);LoadValidationRoom(4,0xfc);_entities.Clear();
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x78,0x58));_player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"LINK_STATE $09 requires room $4:fc's real central floor.");
            var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,0x78,0x58) {CompanionDispatchEnabled=true};
            rom.InitializeLinkGameplay();
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);rom[WramAddress.wDisabledObjects]=1;
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled,1);rom[WramAddress.wMenuDisabled]=1;
            _roomEvents.Get<BlackTowerPossessionEvent>().RequestLinkState();rom[0xcc4f]=9;
            int update=0;bool jumped=false;
            var sounds=_sound.AttachPlayRequestAudit();
            void Compare()
            {
                rom.UpdateGameplay(0,0,0xff,++update);
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    (SomariaPrivate<int>(_player,"_topDownAirZFixed")&0xffff)!=rom.Word(0xd00e)||SomariaPrivate<int>(_player,"_topDownAirSpeedZ")!=unchecked((short)rom.Word(0xd014))||
                    CarriedObjectMotion.DirectionIndex(_player.FacingVector)!=rom[0xd008]||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"LINK_STATE $09 update {update}, batch={batched}: XY={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, " +
                    $"Z={_player.ScriptedZHigh:x2}/{rom[0xd00f]:x2}, speedZ={SomariaPrivate<int>(_player,"_topDownAirSpeedZ")}/{unchecked((short)rom.Word(0xd014))}, " +
                    $"direction={_player.FacingVector}/{rom[0xd008]}, nativeState={rom[0xd004]:x2}:{rom[0xd005]:x2}, sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                jumped|=_player.ScriptedZHigh!=0;
                if(rom[0xd004]==9&&rom[0xd005] is 1 or 2 or 5)
                    for(int address=0xcec0;address<0xcec4;address++)
                        FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Link $09 movement scratch ${address:x4} differs update {update}.");
                FailIf(SomariaPrivate<int>(_player,"_cutsceneDrawZFixed")!=(unchecked((sbyte)rom[0xd00f])<<8),
                    $"Link $09 must draw the source high-byte height without discarding its gameplay Z fraction at update {update}.");
                if(rom[0xd030]==0x18)
                {
                    int phase=rom[0xd031] switch {0xe4=>0,0xe8=>1,0xec=>2,0x80=>3,_=>throw new System.InvalidOperationException($"Link $09 unknown jump graphic ${rom[0xd031]:x2}.")};
                    FailIf(_player.ScriptedLinkAnimationMode!=0x18||SomariaPrivate<int>(_player,"_topDownAirAnimationPhase")!=phase||
                        phase<3&&SomariaPrivate<int>(_player,"_topDownAirAnimationCounter")!=rom[0xd020]||_player.ScriptedLinkAnimationPixelHash==0,
                        $"Link $09 jump animation/clock differs update {update}.");
                }
            }
            StepGameplayUpdates(170,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(!jumped||rom[0xd004]!=8||rom[0xd005]!=1||sounds.Requests.Count(sound=>sound==SoundId.SndJump)!=1,
                "Link $09 must back right/left, look down/up, exclaim, jump away and hold state08 while disabled.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);rom[WramAddress.wDisabledObjects]=0;
            _runtimeState.SetWramByte(WramAddress.wMenuDisabled,0);rom[WramAddress.wMenuDisabled]=0;
            StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(rom[0xd004]!=1||SomariaPrivate<int>(_player,"_ambiState")!=-1,"Source state08 must release Link when wDisabledObjects clears.");
            _roomEvents.Get<BlackTowerPossessionEvent>().RequestLinkState();
            for(int wait=0;wait<160&&_player.ScriptedZHigh==0;wait++)StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_player.ScriptedZHigh==0,"Link $09 cancellation must reach the actual jump first.");
            LoadValidationRoom(4,0xfd);StepGameplayUpdates(4,Vector2.Zero);
            FailIf(_player.ScriptedZHigh!=0||SomariaPrivate<int>(_player,"_ambiState")!=-1||_entities.PlayerMenusDisabled,
                "Leaving during Link's $09 jump must clear its native state, height, pose and masks.");
        }
    }
}
