using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCreditsSwordRom()
    {
        // One bounded vignette, its batched replay, and cancellation during B.
        foreach(int fixture in new[]{0,1,2})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            _inventory.GiveTreasure(TreasureId.Sword,1);
            _inventory.SetScriptedHealth(4);_inventory.SetActiveRingEnabled(false);
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            typeof(BlackTowerEndingEvent).GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            typeof(BlackTowerEndingEvent).GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0);
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            for(int address=0xd000;address<0xe000;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            foreach(var pair in new[]{(0xc2ef,10),(0xcbc1,1),(0xcbc2,0),(0xcfde,2),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            var sounds=_sound.AttachPlayRequestAudit();int update=0;bool sawSword=false,sawSpin=false,sawLinkedSword=false;
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                FailIf(owner.State!=rom[0xcbc2]||_rooms.ActiveGroup!=0||_currentRoom.Id!=0x4a||_hud.Visible,
                    $"Credits sword update{update}: scene state {owner.State}/{rom[0xcbc2]}.");
                foreach(int address in new[]{0xcfde,0xcfdf,0xcbc3,0xcbc4,0xcbc5,0xcbc9,WramAddress.wDisabledObjects,WramAddress.wTilesetAnimation})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Credits sword byte${address:x4} update{update}: port${_runtimeState.ReadWramByte(address):x2}/ROM${rom[address]:x2}.");
                var ralph=_entities.EntityAdapters<NayruRescueActorRoomEntity>().Single();
                const int at=0xd240;
                FailIf(rom[at+1]!=0x37||rom[at+2]!=8||ralph.State!=rom[at+4]||ralph.Substate!=rom[at+5]||
                    ralph.PrecisePosition!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                    ralph.Actor.CurrentAnimationCounter!=rom[at+0x20]||ralph.Actor.CurrentAnimationParameter!=rom[at+0x21],
                    $"Credits Ralph update{update}: port{ralph.State}:{ralph.Substate}/{ralph.PrecisePosition}/anim{ralph.Actor.CurrentAnimationCounter},{ralph.Actor.CurrentAnimationParameter}; ROM{rom[at+4]}:{rom[at+5]}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f}/anim{rom[at+0x20]},{rom[at+0x21]}.");
                var sword=_entities.EntityAdapters<LinkedSwordRoomEntity>().Single();var actor=(NpcCharacter)sword.Node;
                int swordAt=0xd040+_entities.InteractionSlot(sword.Node)*256;
                FailIf(swordAt!=0xd440||rom[swordAt+1]!=0x5e||actor.Visible!=((rom[swordAt+0x1a]&128)!=0)||
                    actor.Visible&&(actor.Position!=new Vector2(rom[swordAt+13],rom[swordAt+11])||actor.ZIndex!=ObjectDrawPriority.FromVisible(rom[swordAt+0x1a])),
                    $"Credits linked sword update{update}: slot${swordAt:x4}, visible{actor.Visible}/{rom[swordAt+0x1a]:x2}.");
                sawLinkedSword|=actor.Visible;
                if(update>1)
                {
                    FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                        _player.FacingVector!=(Vector2I)OracleObjectMath.StrictCardinalVector(rom[0xd008]*8)||_player.Visible!=((rom[0xd01a]&128)!=0)||
                        _inventory.EquippedB!=rom[WramAddress.wInventoryB]||_inventory.EquippedA!=rom[WramAddress.wInventoryA],$"Credits Link update{update}: pos{_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}, facing{_player.FacingVector}/{rom[0xd008]}, visible{_player.Visible}/{rom[0xd01a]:x2}, equip{_inventory.EquippedB},{_inventory.EquippedA}/{rom[WramAddress.wInventoryB]},{rom[WramAddress.wInventoryA]}.");
                    var expected=rom[0xd200]==0?SwordActionState.None:rom[0xd204] switch
                    {1=>SwordActionState.Swing,2=>SwordActionState.Held,3=>SwordActionState.Charged,4=>SwordActionState.Spin,5 or 6=>SwordActionState.Poke,
                        _=>throw new System.InvalidOperationException($"Credits sword state ${rom[0xd204]:x2}.")};
                    FailIf(_player.SwordState!=expected,$"Credits sword update{update}: port{_player.SwordState}/ROM{expected}.");
                }
                sawSword|=_player.IsAttacking;sawSpin|=_player.SwordState==SwordActionState.Spin;
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),$"Credits sword sounds update{update}: port{string.Join(',',sounds.Requests)}/ROM{string.Join(',',rom.Sounds)}.");
            }
            while(update<1100&&owner.State!=2&&!(fixture==2&&_player.IsAttacking))
                StepGameplayUpdates(update<2?1:4,Vector2.Zero,batched:fixture==1,afterUpdate:Compare);
            FailIf(update>=1100||!sawSword||fixture!=2&&(!sawSpin||!sawLinkedSword),"Credits sword demonstration must exercise Link's swing/spin and Ralph's linked sword before its completion signal.");
            if(fixture!=2)
            {
                while(owner.State==2)StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>
                {rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);FailIf(owner.State!=rom[0xcbc2],"Credits sword caption must wait for white fade completion.");});
                FailIf(owner.State!=3||_runtimeState.ReadWramByte(0xcbb3)!=90||rom[0xcbb3]!=90||!_scene.EndingScreen.Visible||
                    _entities.EntityAdapters<LinkedSwordRoomEntity>().Any()||_entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||_player.Visible||_player.IsAttacking,
                    "Credits caption $02 must retire actors, Link and item parents before its 90-update hold.");
            }
            owner.Cancel();
            StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_player.IsAttacking||_runtimeState.ReadWramByte(0xcbc3)!=0||_entities.EntityAdapters<LinkedSwordRoomEntity>().Any()||
                _entities.EntityAdapters<NayruRescueActorRoomEntity>().Any()||!_hud.Visible,"Credits sword cancellation must retire actors, input and active sword.");
        }
    }
}
