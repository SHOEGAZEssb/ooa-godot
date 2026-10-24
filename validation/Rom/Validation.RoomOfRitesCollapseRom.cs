using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoomOfRitesCollapseRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(5,0xf1,0x80);LoadValidationRoom(5,0xf1);
            // Zelda's physically approached script handoff is covered by
            // ValidateRoom5f1ZeldaRom. Stage this controller's source entry
            // independently, retaining the actual room geometry and loop.
            _roomEvents.Get<RoomOfRitesZeldaEvent>().Cancel();_entities.Clear();
            _player.WarpTo(new(0x78,0x58));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            _inventory.GiveTreasure(TreasureId.RingBox,1);_inventory.GrantAppraisedRingForDebug(1);
            FailIf(!_inventory.SetRingBoxSlotFromList(0,1)||!_inventory.EquipRingAt(0)||_inventory.ActiveRing!=1,"Collapse ring fixture must equip its original nonempty ring.");
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0x11);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x58)
                {HostilePartsEnabled=true,PostObjectCollisionsEnabled=false,CompanionDispatchEnabled=true};
            rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom[0xcd17]=0xd0;
            rom[0xcc8a]=0x11;rom[0xc2ef]=0x0f;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var owner=_roomEvents.Get<BlackTowerEndingEvent>();owner.BeginRoomOfRitesCollapse();
            var sounds=_sound.AttachPlayRequestAudit();int update=0,generation=0,hold=0;bool flowers=false,swirl=false;
            while(owner.Stage==0&&update<1600)
                StepGameplayUpdates(owner.State==13?1:3,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(owner.Stage!=1||!flowers||!swirl,"Room of Rites collapse must finish source stage0, including the flowers and outgoing swirl.");
            owner.Cancel();
            FailIf(owner.HasState||_entities.EntityAdapters<RoomOfRitesBoulderRoomEntity>().Any()||
                _entities.EntityAdapters<MakuConfettiRoomEntity>().Any()||_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any(),
                "Cancelling collapse must retire its native controllers, flowers and indefinite swirl.");
            void Compare()
            {
                rom[0xcc00]=(byte)_entities.FrameCounter;rom.AdvanceWarpPalette();rom.AdvanceRoomOfRitesCollapse();
                rom.UpdateGameplay(0,0,255,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                flowers|=_entities.EntityAdapters<MakuConfettiRoomEntity>().Any();swirl|=_entities.EntityAdapters<BlueEnergyBeadRoomEntity>().Any();
                FailIf(owner.Stage!=rom[0xcbc1]||owner.State!=rom[0xcbc2]||_runtimeState.ReadWramByte(0xcbb3)!=rom[0xcbb3]||
                    _runtimeState.ReadWramByte(0xcbb4)!=rom[0xcbb4]||_inventory.ActiveRing!=rom[WramAddress.wActiveRing]||
                    _entities.ScreenShakeCounter!=rom[0xcd18]||_entities.HorizontalScreenShakeCounter!=rom[0xcd19]||
                    _transitions.NativePaletteThreadMode!=rom[0xc4ab],
                    $"Rites collapse update{update}: state={owner.Stage}:{owner.State}/{rom[0xcbc1]}:{rom[0xcbc2]},counter={_runtimeState.ReadWramByte(0xcbb3)}/{rom[0xcbb3]},shake={_entities.ScreenShakeCounter},{_entities.HorizontalScreenShakeCounter}/{rom[0xcd18]},{rom[0xcd19]},fade={_transitions.NativePaletteThreadMode}/{rom[0xc4ab]}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Rites collapse update{update}: RNG/audio differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                if(generation!=rom.TextGeneration){generation=rom.TextGeneration;hold=3;}
                if(hold!=0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
            }
        }
        GD.Print("Validated Room of Rites collapse stage0 counters, text gates, ring state, rumble, native boulders/confetti/swirl and fade handoffs in split/batched gameplay against the clean ROM.");
    }
}
