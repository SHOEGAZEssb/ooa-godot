
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedHoleWarp(FrontendValidationRoot root,bool batched)
    {
        var engine=root._gameplaySession!;var rooms=engine.Rooms;var memory=rooms.RuntimeState;
        var entities=root._seasonsOpening?.Entities??root._entities;var player=root._scene.Player;
        var transitions=engine.Transitions;var app=new ApplicationValidationFixture(root);
        OracleGame game=rooms.SaveData.Game;
        root._scene.Dialogue.Close();entities.Clear();player.Inventory.EquipA(0);player.Inventory.EquipB(0);
        player.WarpTo(new(72.25f,79.5f));player.Face(Vector2I.Down);
        var native=new BraceletRom(rooms.CurrentRoom,player.PrecisePosition,2,player.Inventory.ActiveRing,game:game);
        native.LoadSave(rooms.SaveData);native.InitializeWalkingAnimation();native[0xd01a]=0xc1;
        int room=rooms.CurrentRoom.Id,group=rooms.ActiveGroup,health=player.HealthQuarters;
        var sounds=root._sound.AttachPlayRequestAudit();
        // Declared live parent at source selection; state$0a owns its later clear.
        memory.SetWramByte(0xd500,1);memory.SetWramByte(0xd501,TreasureId.Shield);
        native[0xd500]=1;native[0xd501]=TreasureId.Shield;
        int loads=0;
        void Loaded(int changedGroup,OracleRoomData changedRoom)=>loads++;
        rooms.RoomChanged+=Loaded;
        try
        {
            transitions.ApplyWarp(player,new(group,room,0x44,0,9,group,room,0x44,0,0));
            native.SetNativeByte(memory.FieldAddress("wWarpTransition"),9);
            native[0xcc4f]=0x0a;native[0xcc8a]=0x1e;
            native.SetNativeByte(memory.FieldAddress("wScrollMode"),0);
            FailIf(player.PrecisePosition!=new Vector2(72.25f,79.5f) || memory.ReadWramByte(0xd500)!=1 || sounds.Requests.Contains(SoundId.SndLinkFall),
                "initiateWarp must defer the hole centering, animation and cue to Link's next native visit.");
            int visits=0;bool fadeSelected=false;
            void Compare(int count)=>app.Step(count,Vector2.Right,batched:batched,afterUpdate:()=>
            {
                if(fadeSelected)return;
                native.UpdateLinkItems(entities.FrameCounter,0x10);visits++;
                int request=native.ReadNativeByte(memory.FieldAddress("wWarpTransition2"));
                fadeSelected=request!=0;
                FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                    memory.ReadField("wLinkForceState")!=native[0xcc4f] || memory.ReadWramByte(0xd005)!=native[0xd005] ||
                    memory.ReadWramByte(0xd500)!=native[0xd500] ||
                    memory.ReadField("wWarpTransition2")!=0 || (fadeSelected && request!=3) ||
                    transitions.PhaseName!=(fadeSelected?"FadeOut":"HoleFall") ||
                    player.NativeNormalStateForInteraction!=(native[0xd004]==1),
                    $"{game}/hole/batch{batched}/visit{visits}: native request, centered position or state handoff differs.");
                for(int offset=0x20;offset<=0x23;offset++)
                    FailIf(memory.ReadWramByte(0xd000+offset)!=native[0xd000+offset],
                        $"{game}/hole/visit{visits}: native animation byte${offset:x2} differs.");
            });
            Compare(1);
            FailIf(player.PrecisePosition!=new Vector2(72,88) || memory.ReadWramByte(0xd500)!=0 || sounds.RequestsFor(SoundId.SndLinkFall)!=1,
                "warpTransition9 must add$08 to Y, center both coordinates with cleared fractions and play one fall cue.");
            memory.SetField("wDisabledObjects",0x9f);native[0xcc8a]=0x9f;Compare(3);
            memory.SetField("wDisabledObjects",0x1e);native[0xcc8a]=0x1e;
            root._scene.Dialogue.ShowMessage("Warp state ignores ordinary text gates.",104);native[0xcba0]=1;
            Compare(4);
            while(!fadeSelected && visits<80)Compare(1);
            root._scene.Dialogue.Close();
            FailIf(visits>=80 || sounds.RequestsFor(SoundId.SndLinkFall)!=1 || sounds.Requests.Contains(SoundId.SndEnterCave),
                "Hole source must request its fade on the update after the terminal animation marker, without a cave cue.");
            FailIf(transitions.PhaseName!="FadeOut" || loads!=0 || memory.ReadField("wWarpTransition2")!=0,
                "The main thread must consume the hole request after the terminal source object pass.");
            app.Step(31,Vector2.Zero,batched:batched);
            FailIf(loads!=0,"The source room must remain resident through fade update31.");
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(loads!=1,"Hole source fade update32 must load the destination exactly once.");
            app.Step(33,Vector2.Zero,batched:batched);
            FailIf(transitions.IsTransitioning || player.ScriptedLinkAnimationMode is not null || player.HealthQuarters!=health,
                "Hole warp completion must retire the fall presentation and restore control without ordinary-hole damage.");
        }
        finally { rooms.RoomChanged-=Loaded; }
    }
}
