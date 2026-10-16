using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlackTowerCreditsTitleRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int ring in new[]{0xff,0x20})
        foreach(bool cancelEarly in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0x65);_entities.Clear();
            _inventory.SetScriptedEquippedItems(TreasureId.Sword,TreasureId.Shield);
            typeof(InventoryState).GetProperty(nameof(InventoryState.ActiveRing))!.SetValue(_inventory,ring);
            _saveData.WriteWramByte(WramAddress.wActiveRing,(byte)ring);
            int healthBefore=_inventory.HealthQuarters;var originalRoom=_currentRoom;var layoutBefore=(byte[])originalRoom.Layout.Clone();
            var palettesBefore=_world.BackgroundPalettes.Capture();
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.Begin();
            var ownerType=typeof(BlackTowerEndingEvent);
            ownerType.GetField("_initialize",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,false);
            ownerType.GetMethod("BeginScreen",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,null);
            ownerType.GetMethod("Profile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,["twinrova",0x9e]);
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0,0)
                {CompanionDispatchEnabled=true,NativeGameKeysEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom.EnableVramDma();var menu=rom.CreateMenuView();menu.LoadHudGraphics();menu.LoadRoomGraphics();
            rom.LoadCutsceneGraphics(0x2c,0x9e);
            for(int address=0xd000;address<0xd080;address++)rom[address]=0;
            rom[0xcc03]=1;rom[0xcd16]=0;rom[0xcd17]=0xd0;
            // Enter the actual last flash update, then follow the native09
            // closeup -> native0a handoff. These are fixture inputs only.
            foreach(var pair in new[]{(0xc2ef,9),(0xcbc1,1),(0xcbc2,8),(0xcbb3,14),(0xcbba,1),(0xcbc3,255),(0xcbc4,255),(0xcbc5,255)})
            {_runtimeState.SetWramByte(pair.Item1,(byte)pair.Item2);rom[pair.Item1]=(byte)pair.Item2;}
            var screen=_scene.EndingScreen;var sounds=_sound.AttachPlayRequestAudit();int update=0,titleStart=0,fadeStart=0,finished=0;
            Texture2D? previousBackground=null;bool clearedTerrain=false;
            void Compare()
            {
                bool creditsBefore=rom[0xc2ef]==0x0a;
                rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();
                if(creditsBefore)rom.UpdateCredits(++update);else rom.UpdateGameplay(0,0,255,++update);
                FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_runtimeState.ReadWramByte(0xc2ef)!=rom[0xc2ef]||
                    _transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||_player.Visible||_hud.Visible||!screen.Visible,
                    $"Credits title update{update}: state{owner.Stage}:{owner.State}/{rom[0xcbc1]}:{rom[0xcbc2]}, fade={_transitions.NativePaletteFadeActive}/{rom[0xc4ab]}.");
                foreach(int address in new[]{0xcbb3,0xcbb4,0xcbc3,0xcbc4,0xcbc5,0xcbc9,0xcde3,0xcde4,0xcfde,0xcfdf})
                    FailIf(_runtimeState.ReadWramByte(address)!=rom[address],$"Credits title byte${address:x4} update{update}: port=${_runtimeState.ReadWramByte(address):x2}, ROM=${rom[address]:x2}.");
                FailIf(_inventory.HealthQuarters!=rom[0xc6aa]||_inventory.ActiveRing!=rom[0xc6cb]||
                    _inventory.EquippedB!=rom[0xc688]||_inventory.EquippedA!=rom[0xc689]||
                    _saveData.ReadWramByte(WramAddress.wLinkHealth)!=rom[0xc6aa]||_saveData.ReadWramByte(WramAddress.wActiveRing)!=rom[0xc6cb],
                    $"Credits title inventory update{update}: health{_inventory.HealthQuarters}/{rom[0xc6aa]}, ring${_inventory.ActiveRing:x2}/${rom[0xc6cb]:x2}.");
                if(owner.CreditsPending&&owner.Stage==0&&owner.State==1&&titleStart==0)
                {
                    titleStart=update;
                    FailIf(rom.Word(0xcbb3)!=180||rom[0xc6aa]!=4||rom[0xc6cb]!=(ring|0x40)||rom[0xcde3]!=TreasureId.Sword||rom[0xcde4]!=TreasureId.Shield,
                        "Native credits title must save both button bytes, disable the equipped ring, install one heart and start a 180-update hold.");
                }
                if(owner.CreditsPending&&owner.Stage==0&&owner.State==2&&fadeStart==0)
                {
                    fadeStart=update;
                    FailIf(fadeStart-titleStart!=180||rom.Word(0xcbb3)!=840||rom[0xc4ab]!=12,
                        "Credits title must start its 840-update counter and delayed black fade-in exactly after 180 updates.");
                }
                if(owner.CreditsPending&&owner.Stage==1)
                {
                    finished=update;
                    FailIf(finished-fadeStart!=840||rom[0xcbc2]!=0||rom[0xcfde]!=0||rom[0xcfdf]!=0||!_transitions.NativePaletteFadeActive,
                        "Credits title must select normal vignette $00 and white fade-out exactly 840 updates after its fade starts.");
                }
                if(!ReferenceEquals(previousBackground,screen.BackgroundTexture))
                {
                    previousBackground=screen.BackgroundTexture;using var image=previousBackground!.GetImage();
                    int mapBase=titleStart==0?0x9800:0x9c00;
                    for(int y=0;y<144;y++)for(int x=0;x<160;x++)
                    {
                        int map=mapBase+(y/8)*32+x/8,tile=rom.VramByte(0,map),flags=rom.VramByte(1,map);
                        int source=0x9000+unchecked((sbyte)tile)*16,bank=(flags>>3)&1;
                        int sx=(flags&0x20)==0?x%8:7-x%8,sy=(flags&0x40)==0?y%8:7-y%8;
                        int shade=((rom.VramByte(bank,source+sy*2)>>(7-sx))&1)|(((rom.VramByte(bank,source+sy*2+1)>>(7-sx))&1)<<1);
                        var color=image.GetPixel(x,y);int rgb=Mathf.RoundToInt(color.R*31)|(Mathf.RoundToInt(color.G*31)<<5)|(Mathf.RoundToInt(color.B*31)<<10);
                        FailIf(color.A!=1||rgb!=menu.HudColor(false,flags&7,shade),$"Credits title pixel({x},{y}) update{update} differs from native LCDC map ${mapBase:x4}.");
                    }
                }
                clearedTerrain|=owner.CreditsPending;
                FailIf(clearedTerrain&&(_currentRoom.Layout.Any(value=>value!=0)||_currentRoom.Collisions.Any(value=>value!=0)),
                    "The credits title must retain the cleared gameplay terrain until the next native room load.");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=rom[0xff94]||rng.Rng2!=rom[0xff95]||rng.Calls-seed.Calls!=rom.RandomCalls||
                    !sounds.Requests.SequenceEqual(rom.Sounds),$"Credits title sound/RNG update{update} differs.");
            }
            while(update<1400&&finished==0&&!(cancelEarly&&fadeStart!=0&&update-fadeStart>=60))
            {
                int remaining=_runtimeState.ReadWramByte(0xcbb3)|(_runtimeState.ReadWramByte(0xcbb4)<<8);
                StepGameplayUpdates(update==0||remaining<=4?1:4,Vector2.Zero,batched:batched,afterUpdate:Compare);
            }
            FailIf(titleStart==0||fadeStart==0||!cancelEarly&&finished==0||sounds.Requests.Count(sound=>sound==0x2a)!=1,
                "The normal credits title must hold, fade, and request MUS_CREDITS_2 exactly once before the vignette handoff.");
            if(!cancelEarly)
            {
                void CompareHandoff()
                {
                    rom.AdvanceWarpPalette();rom.AdvanceBlackTowerEnding();rom.UpdateCredits(++update);
                    FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_transitions.NativePaletteFadeActive!=(rom[0xc4ab]!=0)||
                        _inventory.HealthQuarters!=4||_inventory.ActiveRing!=(ring|0x40)||
                        _inventory.EquippedB!=rom[0xc688]||_inventory.EquippedA!=rom[0xc689]||
                        _runtimeState.ReadWramByte(0xcde3)!=rom[0xcde3]||_runtimeState.ReadWramByte(0xcde4)!=rom[0xcde4],
                        $"Credits title/vignette handoff update{update} changed source state, fade, saved equips or disabled-ring ownership.");
                    if(owner.State==1)
                    {
                        FailIf(_rooms.ActiveGroup!=0||_rooms.CurrentRoom.Id!=0x38||rom[0xcc2d]!=0||rom[0xcc30]!=0x38||screen.Visible||
                            _player.CreditsLinkState!=rom[0xd004]||rom[0xd000]!=3||rom[0xd001]!=8||rom[0xd002]!=9||
                            _entities.EntityAdapters<ImpaCreditsRoomEntity>().Count()!=1||_entities.EntityAdapters<NayruRescueActorRoomEntity>().Count()!=3||
                            !originalRoom.Layout.SequenceEqual(layoutBefore),
                            "The credits title must restore its previous terrain and load the real Maku vignette, including Link's deferred special-object initialization.");
                    }
                    var rng=_random.CaptureState();FailIf(rng.Rng1!=rom[0xff94]||rng.Rng2!=rom[0xff95]||rng.Calls-seed.Calls!=rom.RandomCalls||
                        !sounds.Requests.SequenceEqual(rom.Sounds),"Credits title/vignette handoff must retain source music selection, sound order and shared RNG.");
                }
                while(owner.State==0&&update-finished<60)StepGameplayUpdates(1,Vector2.Zero,batched:batched,afterUpdate:CompareHandoff);
                FailIf(owner.State!=1,"Credits title must finish its white fade and load the Maku vignette.");
                StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:CompareHandoff);
            }
            owner.Cancel();
            FailIf(_inventory.HealthQuarters!=healthBefore||_inventory.ActiveRing!=ring||_inventory.EquippedB!=TreasureId.Sword||_inventory.EquippedA!=TreasureId.Shield||
                screen.Visible||!_hud.Visible||!originalRoom.Layout.SequenceEqual(layoutBefore)||
                !palettesBefore.Cast<Color>().SequenceEqual(_world.BackgroundPalettes.Capture().Cast<Color>()),
                "Cancelling the credits title must restore shared inventory, screen, HUD, palette and terrain ownership.");
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
        }
    }
}
