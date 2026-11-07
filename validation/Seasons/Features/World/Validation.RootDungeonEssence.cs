namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonEssence()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                s.Save.SetRoomFlag(0,0x96,0x80);s.Save.SetRoomFlag(4,0x12,0xa0);s.DebugTravel(4,0x12);s.Link.WarpTo(new(216,88));
                FailIf(s.Collision.Collides(s.Link.Position),"The Essence route must start on $4:12's original east-door approach.");
                for(int tick=0;tick<240&&(s.RoomId!=0x13||s.Scrolling);tick++)app.Step(1,Vector2.Right,batched:batched);
                FailIf(s.RoomId!=0x13||s.Scrolling,"The defeated boss's east shutter must lead to Essence room $4:13.");
                var essence=s.Entities.Entities<DungeonEssence>().Single();
                FailIf(essence.Position!=new Vector2(120,40)||essence.EssenceIndex!=0||
                    !essence.Message.Contains("Fertile Soil",StringComparison.Ordinal)||essence.ExitWarp is not
                    {DestinationGroup:0,DestinationRoom:0x96,DestinationPosition:0x44,DestinationTransition:1},
                    "Native Seasons Essence row0 must select Fertile Soil, TX_000e and exit $0:96/$44/$01.");
                for(int tick=0;tick<60&&s.Link.Position.X<40;tick++)app.Step(1,Vector2.Right,batched:batched);
                for(int tick=0;tick<80&&s.Link.Position.Y<136;tick++)app.Step(1,Vector2.Down,batched:batched);
                for(int tick=0;tick<120&&s.Link.Position.X<120;tick++)app.Step(1,Vector2.Right,batched:batched);
                FailIf(s.Collision.Collides(s.Link.Position)||s.Link.Position.X!=120,
                    $"The pedestal approach must go around the solid $ed columns on the original south floor: Link{s.Link.Position}.");
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                for(int tick=0;tick<160&&s.DisabledObjects!=0x81;tick++)app.Step(1,Vector2.Up,batched:batched);
                FailIf(s.DisabledObjects!=0x81||s.Dialogue.IsOpen||s.Inventory.Essences!=0,"The Essence must own input before committing its collection.");
                s.DebugTravel(4,0x12);s.DebugTravel(4,0x13);s.Link.WarpTo(new(120,136));
                FailIf(s.DisabledObjects!=0||s.Entities.PlayerMenusDisabled||s.Link.IsHoldingItemTwoHands||
                    s.Save.HasRoomFlag(4,0x13,0x20)||s.Inventory.Essences!=0,
                    "Cancelling the uncollected Essence must release its controls and pose without granting a treasure or room flag.");
                essence=s.Entities.Entities<DungeonEssence>().Single();
                s.Sound.AttachPlayRequestAudit();s.Sound.ClearPlayRequestAudit();
                bool sawGlow=false,sawHeld=false,sawSwirl=false;
                for(int tick=0;tick<200&&!s.Dialogue.IsOpen;tick++)
                {
                    app.Step(1,Vector2.Up,batched:batched);sawGlow|=essence.GlowVisible;
                }
                app.Step(3,Vector2.Zero,batched:batched);sawHeld=s.Link.IsHoldingItemTwoHands;
                FailIf(!s.Dialogue.IsOpen||s.Dialogue.SourceTextId!=0x000e||s.Inventory.Essences!=1||
                    !s.Save.HasRoomFlag(4,0x13,0x20)||!sawGlow||!sawHeld||s.DisabledObjects!=0x81||
                    s.Sound.PlayRequestsFor(SoundId.MusGetEssence)!=1,
                    $"The real pedestal approach must fall/hold, grant only essence bit0 and lock native input: Link{s.Link.Position}, text{s.Dialogue.SourceTextId}, essences${s.Inventory.Essences:x2}, held{sawHeld}.");
                s.Dialogue.Close();
                for(int tick=0;tick<800&&(s.Rooms.ActiveGroup!=0||s.RoomId!=0x96||s.Transitions.IsTransitioning);tick++)
                {app.Step(1,Vector2.Zero,batched:batched);if(s.RoomId==0x13)sawSwirl|=essence.SwirlActive;}
                FailIf(!sawSwirl||s.Rooms.ActiveGroup!=0||s.RoomId!=0x96||s.Transitions.IsTransitioning||
                    s.Inventory.Essences!=1||s.Link.Position!=new Vector2(72,72)||s.Link.IsHoldingItemTwoHands||
                    s.Sound.PlayRequestsFor(SoundId.MusEssence)!=1||s.Sound.PlayRequestsFor(SoundId.SndFadeOut)!=4,
                    $"Fertile Soil must finish its shared swirl/four fades/30-update tail and warp to $0:96/$44: room${s.Rooms.ActiveGroup:x1}:{s.RoomId:x2}, Link{s.Link.Position}, transitioning{s.Transitions.IsTransitioning}.");
                for(int tick=0;tick<700&&!s.Dialogue.IsOpen;tick++)app.Step(1,Vector2.Zero,batched:batched);
                FailIf(s.Dialogue.SourceTextId!=0x1704||!s.Save.HasRoomFlag(0,0x96,0x40)||
                    s.Save.ReadField("wMakuMapTextPresent")!=5||s.DisabledObjects!=0x81,
                    "The actual Essence exit must run remote Maku $22:$00, set room bit$40/map TX_1705 and show TX_1704.");
                s.Dialogue.Close();app.Step(180,Vector2.Zero,batched:batched);
                FailIf(s.DisabledObjects!=0||s.Memory.ReadField("wMenuDisabled")!=0||s.Fading||s.Hud.StatusBarHidden||
                    s.Entities.EntityAdapters<SeasonsRemoteMakuRoomEntity>().Any(),
                    "Remote Maku must restore Link, HUD, room music and palette ownership after its message.");
                s.DebugTravel(0,0x97);s.DebugTravel(0,0x96);app.Step(4,Vector2.Zero,batched:batched);
                FailIf(s.Dialogue.IsOpen||s.Entities.EntityAdapters<SeasonsRemoteMakuRoomEntity>().Any(),"Maku's completed room flag must suppress repeat advice.");
                s.DebugTravel(4,0x13);app.Step(4,Vector2.Zero,batched:batched);
                var collected=s.Entities.Entities<DungeonEssence>().Single();
                FailIf(!collected.Collected||collected.Visible||!collected.BlocksLink(new(120,40)),"Collected Essence re-entry must retain only its solid pedestal.");
                s.Link.WarpTo(new(120,88));app.Step(70,Vector2.Up,batched:batched);
                FailIf(s.Dialogue.IsOpen||s.Inventory.Essences!=1,"Repeating the real pedestal approach must not replay or regrant Fertile Soil.");
                s.Save.SetRoomFlag(0,0x96,0x40,false);s.DebugTravel(0,0x96);s.Link.WarpTo(new(72,88));
                app.Step(50,Vector2.Zero,batched:batched);
                FailIf(!s.Entities.EntityAdapters<SeasonsRemoteMakuRoomEntity>().Any()||!s.Fading||!s.Hud.StatusBarHidden,
                    "The cancellation fixture must interrupt remote Maku during its native black fade.");
                s.DebugTravel(4,0x13);app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.DisabledObjects!=0||s.Memory.ReadField("wMenuDisabled")!=0||s.Fading||s.Hud.StatusBarHidden,
                    "Cancelling remote Maku must release its input, HUD and palette ownership in the next room.");
            }
            finally {root.Free();}
        }
    }
}
