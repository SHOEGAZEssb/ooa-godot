using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private async Task ValidateSeasonsMakuBubbleRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            var viewport=new SubViewport {Size=new(160,144),Disable3D=true,
                RenderTargetUpdateMode=SubViewport.UpdateMode.Always,
                CanvasItemDefaultTextureFilter=Viewport.DefaultCanvasItemTextureFilter.Nearest};
            AddChild(viewport);
            var root=StartSeasonsAfterImpa(batched,viewport);
            try
            {
                var session=root._seasonsOpening!;
                session.Inventory.GiveTreasure(TreasureId.Sword,1);session.Inventory.EquipA(TreasureId.Sword);
                session.DebugTravel(2,0x0b);session.Link.WarpTo(new(80,112));
                var rom=new LinkCollisionRom(game:OracleGame.Seasons);
                var bytes=ValidationRom.LoadCleanUs(OracleGame.Seasons);
                SeasonsMakuBubbleRoomEntity? bubble=null;
                EnemyAnimationPlayer? animation=null;
                int address=0,mask=0,text=0,dropUpdates=0,checks=0,draws=0;
                Vector2 drawnOffset=default;
                var app=new ApplicationValidationFixture(root,Compare);
                void Step(int updates,Vector2 movement=default)=>app.Step(updates,movement,batched:batched);
                Step(4);
                WalkSeasons(app,session,point=>point.X==48&&point.Y==64);
                session.Link.Face(Vector2I.Right);
                for(int i=0;i<16&&(session.Memory.ReadWramByte(0xcfc0)&0x80)==0;i++)
                {
                    app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);
                    for(int update=0;update<25&&(session.Memory.ReadWramByte(0xcfc0)&0x80)==0;update++)Step(1);
                }
                FailIf((session.Memory.ReadWramByte(0xcfc0)&0x80)==0,"A right-facing sword arc must reach Maku's bubble from native floor (48,64).");
                for(int i=0;i<100&&animation!.CurrentParameter!=0x7f;i++)Step(1);
                FailIf(animation!.CurrentParameter!=0x7f || bubble!.Position!=new Vector2(48,255),
                    "The first falling sweat-drop update must retain native ground Y=$ff before wrapping to $00.");
                await FlushDraws();
                FailIf(draws==0 || drawnOffset!=bubble.DrawOffset,"The sweat drop must publish its initial deferred draw above the screen.");
                int previousDraws=draws;
                Step(17);
                await FlushDraws();
                FailIf(draws<=previousDraws || drawnOffset!=bubble.DrawOffset || bubble.SourceScreenPosition+drawnOffset!=new Vector2(44,0),
                    $"The sweat drop must redraw after Y wraps, entering the screen at (44,0); draws={previousDraws}->{draws}, cached={drawnOffset}, current={bubble.DrawOffset}.");
                if(DisplayServer.GetName()!="headless")
                {
                    using Image screen=viewport.GetTexture().GetImage();
                    using Image sprite=bubble.CurrentTexture.GetImage();
                    int pixels=0;
                    for(int y=0;y<sprite.GetHeight();y++)for(int x=0;x<sprite.GetWidth();x++)
                    {
                        Color expected=sprite.GetPixel(x,y);
                        if(expected.A<0.9f)continue;
                        Color actual=screen.GetPixel(44+x,16+y);
                        FailIf(actual.R8!=expected.R8 || actual.G8!=expected.G8 || actual.B8!=expected.B8,
                            $"The visible sweat-drop pixel ({44+x},{16+y}) must reach the rendered gameplay viewport.");
                        pixels++;
                    }
                    FailIf(pixels==0,"The sweat-drop texture must contain visible source pixels.");
                    await CaptureSeasonsOpeningScreen("maku-sweat-drop",viewport);
                }
                for(int i=0;i<250&&!session.Dialogue.IsOpen;i++)Step(1);
                FailIf(!session.Dialogue.IsOpen || session.Link.FacingVector!=Vector2I.Right,
                    "Popping ENEMY$56 must retain Link's right-facing direction through Maku's wake and TX_1700.");
                FailIf(dropUpdates!=40 || checks<150 || rom[address]!=0,
                    $"Maku native comparison must cover breathing, pop and all 40 sweat-drop updates before deletion; checks={checks}, drop={dropUpdates}.");

                async Task FlushDraws()
                {
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                }
                void Compare()
                {
                    if(bubble is null)
                    {
                        bubble=session.Entities.EntityAdapters<SeasonsMakuBubbleRoomEntity>().FirstOrDefault();
                        if(bubble is null)return;
                        address=((0xd0+bubble.Slot)<<8)|0x80;
                        animation=(EnemyAnimationPlayer)typeof(DungeonInteractionVisualEntity).GetField("_animation",
                            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(bubble)!;
                        bubble.Draw+=()=>{draws++;drawnOffset=bubble.DrawOffset;};
                        int parent=0xd040+session.Entities.NativeInteractionSlot(session.Entities.EntityAdapters<SeasonsMakuTreeRoomEntity>().Single())*0x100;
                        rom[address]=1;rom[address+1]=0x56;rom[address+0x29]=1;
                        rom[address+0x18]=0x40;rom[address+0x19]=(byte)(parent>>8);
                        rom[parent+0x0b]=0x26;rom[parent+0x0d]=0x50;
                        rom[0xffac]=0x80;rom[0xffad]=(byte)(address>>8);
                        rom.Call(0x2772,bank:0x0d,objectPage:address>>8); // enemySetAnimation 0.
                    }
                    if(bubble.State==0)return;
                    if(rom[address]==0)return;
                    rom[0xcca4]=(byte)mask;rom[0xcba0]=(byte)text;
                    if(rom[address+4]==0)
                        rom.Call(0x7559,bank:0x0d,objectPage:address>>8); // enemyCode56@normalStatus, after common initialization.
                    else rom.Call(0x2e86,bank:0,restoresBank:false); // updateEnemies includes status and eligibility.
                    mask=session.DisabledObjects;text=session.Dialogue.IsOpen?1:0;
                    if(rom[address]==0)
                    {
                        FailIf(session.Entities.EntityAdapters<SeasonsMakuBubbleRoomEntity>().Contains(bubble),"ENEMY$56 must retire on its native deletion update.");
                        return;
                    }
                    FailIf(bubble.Position!=new Vector2(rom[address+0x0d],rom[address+0x0b]) ||
                        bubble.State!=rom[address+4] || animation!.RemainingUpdates!=rom[address+0x20] ||
                        animation.CurrentParameter!=rom[address+0x21],
                        $"Maku bubble update{checks}: native position/state/animation differs; port={bubble.Position}/${bubble.State:x2}/{animation!.RemainingUpdates}/${animation.CurrentParameter:x2}, ROM={rom[address+0x0d]},{rom[address+0x0b]}/${rom[address+4]:x2}/{rom[address+0x20]}/${rom[address+0x21]:x2}.");
                    int pointer=rom.Word(address+0x1e);
                    int at=(0x12+(pointer>>14))*0x4000+(pointer&0x3fff);
                    int count=bytes.Span[at];
                    if(count!=0)
                    {
                        int x=int.MaxValue,y=int.MaxValue;
                        for(int cell=0;cell<count;cell++)
                        {
                            // bank0.@drawObject adds each OAM cell modulo 256 before hardware offsets and the HUD.
                            y=Math.Min(y,((rom[address+0x0b]+16+rom[address+0x0f]+bytes.Span[at+1+cell*4])&255)-32);
                            x=Math.Min(x,((rom[address+0x0d]+bytes.Span[at+2+cell*4])&255)-8);
                        }
                        FailIf(bubble.SourceScreenPosition+bubble.DrawOffset!=new Vector2(x,y),
                            $"Maku bubble update{checks}: native OAM top-left ({x},{y}) differs from {bubble.SourceScreenPosition+bubble.DrawOffset}.");
                    }
                    if(rom[address+0x21]==0x7f)dropUpdates++;
                    checks++;
                    // The real gameplay sword collision runs after the enemy phase.
                    if(session.Memory.ReadWramByte(address+0x29)==0)rom[address+0x29]=0;
                }
            }
            finally{root.Free();viewport.Free();}
        }
    }

    private void ValidateSeasonsMakuGate()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                session.DebugTravel(0,0xd9);session.Link.WarpTo(new(80,104));
                void Step(int count,Vector2 movement=default)=>app.Step(count,movement,batched:batched);
                Step(4);
                WalkSeasons(app,session,point=>point.X==80&&point.Y==54);
                Step(8,Vector2.Up);
                FailIf(!session.Collision.Collides(new(80,24)) || session.Link.Position.Y<40 || session.Save.HasRoomFlag(0,0xd9,0x80),
                    $"Maku gate must block a swordless approach through its original collision geometry; Link={session.Link.Position}.");
                session.Inventory.EquipA(TreasureId.Punch);
                app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);Step(20);
                FailIf(session.Save.HasRoomFlag(0,0xd9,0x80),"Punch/A cannot open INTERAC$22:$0a's sword gate.");
                session.Inventory.GiveTreasure(TreasureId.Sword,1);session.Inventory.EquipA(TreasureId.Sword);session.Link.Face(Vector2I.Up);
                app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);
                var gate=session.Entities.EntityAdapters<SeasonsMakuTreeRoomEntity>().Single(actor=>actor.Gate);
                Vector2 shake=Vector2.Zero;
                session.Entities.ScreenShakeChanged+=offset=>shake=offset;
                Vector2 BackgroundOffset()=>(Vector2)typeof(RoomView).GetField("_nativeBackgroundOffset",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(session.RoomView)!;
                for(int i=0;i<30&&gate.ReadByte(0x46)!=60;i++)Step(1);
                FailIf(gate.ReadByte(0x46)!=60,"Gate must arm its first source wait at exactly 60 updates.");
                Step(59);
                FailIf(gate.ReadByte(0x44)!=1 || gate.ReadByte(0x46)!=1,"Gate must remain closed through source wait update59.");
                Step(1);
                FailIf(gate.ReadByte(0x44)!=2 || session.Save.HasRoomFlag(0,0xd9,0x80),
                    "Gate must finish its source 60-update wait before entering native state2, without an early persistent flag.");
                Step(1);
                FailIf(gate.ReadByte(0x45)!=1 || gate.ReadByte(0x46)!=30 || session.Room.GetPackedStorageMetatile(0x24)==0xa9,
                    "Gate state2 must install interleaved graphics, preserve closed collision/layout and arm 30 native updates.");
                FailIf(shake==Vector2.Zero || BackgroundOffset()!=-shake || session.RoomCamera.Offset!=Vector2.Zero,
                    "Gate's native SCX/SCY shake must reach Seasons' background without moving the world camera or sprites.");
                Step(5);
                FailIf(BackgroundOffset()==Vector2.Zero,"The source six-update shake must retain its last active offset.");
                Step(1);
                FailIf(BackgroundOffset()!=Vector2.Zero,"The source background shake must clear on the update after its six active updates.");
                Step(23);
                FailIf(gate.ReadByte(0x46)!=1 || gate.ReadByte(0x44)!=2,"Gate must remain interleaved through opening update29.");
                Step(1);
                FailIf(gate.ReadByte(0x44)!=1 || session.Save.HasRoomFlag(0,0xd9,0x80),
                    "Gate update30 must install final tiles and return to script state1 before flag$80.");
                Step(80);
                FailIf(!session.Save.HasRoomFlag(0,0xd9,0x80) || session.DisabledObjects!=0 || session.Inventory.SwordLevel!=1 ||
                    session.Room.GetPackedStorageMetatile(0x14)!=0xbf || session.Room.GetPackedStorageMetatile(0x15)!=0xbf ||
                    session.Room.GetPackedStorageMetatile(0x24)!=0xa9 || session.Room.GetPackedStorageMetatile(0x25)!=0xaa,
                    "Gate sword_03 must retain sword level$01, open all four source tiles and release input.");
                session.DebugTravel(0,0xd9);session.Link.WarpTo(new(80,104));Step(4);
                FailIf(session.Entities.EntityAdapters<SeasonsMakuTreeRoomEntity>().Any(actor=>actor.Gate&&!actor.Finished) ||
                    session.Collision.Collides(new(80,24)),"Maku gate flag$80 must restore the open route without replaying its controller.");
                WalkSeasons(app,session,point=>point.X==80&&point.Y==8);
                Step(12,Vector2.Up);Step(160);
                FailIf(session.Rooms.ActiveGroup!=2 || session.RoomId!=0x0b || session.Transitions.IsTransitioning,
                    $"The opened gate must enter native Maku group$02 room$0b, got ${session.Rooms.ActiveGroup:x1}:${session.RoomId:x2} at {session.Link.Position}.");
                WalkSeasons(app,session,point=>point.X==80&&point.Y==120);
                Step(12,Vector2.Down);Step(160);
                FailIf(session.Rooms.ActiveGroup!=0 || session.RoomId!=0xd9 || session.Transitions.IsTransitioning,
                    "Maku's actual south-edge warp must return through the persisted open gate to room$0:d9.");
            }
            finally{root.Free();}
        }
    }

    private void ValidateSeasonsFirstMakuTree()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var session=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);
                byte[] disk=root.FileSlotSnapshot(0,OracleGame.Seasons);
                session.Inventory.GiveTreasure(TreasureId.Sword,1);
                session.Inventory.EquipA(TreasureId.Sword);
                session.DebugTravel(2,0x0b);session.Link.WarpTo(new(80,112));
                void Step(int count,Vector2 movement=default)=>app.Step(count,movement,batched:batched);
                var linkedActors=session.Entities.EntityAdapters<SeasonsUnlinkedInteractionRoomEntity>().ToArray();
                FailIf(linkedActors.Length!=2 || linkedActors.Any(actor=>actor.Finished),
                    "Maku's native object stream must reserve Impa/Zelda slots before their unlinked state-zero deletion.");
                Step(4);
                FailIf(linkedActors.Any(actor=>!actor.Finished),"Unlinked Maku Impa/Zelda must delete on their first eligible update.");
                var tree=session.Entities.EntityAdapters<SeasonsMakuTreeRoomEntity>().Single();
                FailIf(tree.ReadByte(0x66)!=0 || tree.ReadByte(0x67)!=0,"Stage0 Maku has no interaction hitbox; its enemy bubble owns sword collision.");
                FailIf(session.Save.MakuMapTextPresent!=3 || session.Dialogue.IsOpen,
                    "Maku stage0 must install map advice TX_1703 and sleep until its native bubble is hit.");
                PopBubble();WaitForText(0x1700);
                FailIf(!session.Save.HasGlobalFlag(0x18) || session.Save.HasTreasure(0x42),
                    "First wake sets GLOBALFLAG_GNARLED_KEY_GIVEN before TX_1700 without granting the key.");
                Vector2 held=session.Link.Position;Step(4,Vector2.Down);
                FailIf(session.Link.Position!=held,"Maku's first story textbox must retain control of Link.");
                session.Dialogue.Close();WaitForText(0x1701);
                Choose(0);WaitForText(0x1701);
                FailIf(session.Save.HasRoomFlag(2,0x0b,0x80),"Rejecting TX_1701 must repeat the explanation before dropping the key.");
                Choose(1);WaitForText(0x1702);session.Dialogue.Close();Step(180);
                FailIf(!session.Save.HasRoomFlag(2,0x0b,0x80) || session.Save.HasTreasure(0x42) || session.DisabledObjects!=0,
                    "Accepting Maku's quest must drop the key, remember flag$80 and release control after its sleep animation.");
                int keyX=session.Save.ReadWramByte(0xc6e0);
                FailIf(keyX!=0x60,"Link left of$50 in the central interval must select the native right-hand key drop X=$60.");
                var sleeping=session.Entities.EntityAdapters<SeasonsMakuBubbleRoomEntity>().Single();
                int departingEnemy=((0xd0+sleeping.Slot)<<8)|0x80;
                // Leave the dropped key, then exercise native state-zero restoration.
                session.DebugTravel(0,0xd8);Step(2);
                FailIf(session.Memory.ReadWramByte(departingEnemy)!=0,"Room retirement must release the sleeping bubble's live native allocation.");
                session.DebugTravel(2,0x0b);session.Link.WarpTo(new(80,112));Step(8);
                FailIf(session.Entities.Entities<GroundTreasurePickup>().Count!=1 || session.Save.HasTreasure(0x42),
                    "An uncollected Gnarled Key must respawn once at saved X and Y=$58 on Maku-room re-entry.");
                FailIf(session.Entities.NativeInteractionSlot(session.Entities.EntityAdapters<GroundTreasureRoomEntity>().Single())!=5,
                    "Maku must restore the key into slot$05 before linked-only slots$03/$04 run their native deletion.");
                WalkSeasons(app,session,point=>point.X==80&&point.Y==100);
                for(int i=0;i<40&&!session.Dialogue.IsOpen;i++)Step(1,new Vector2(1,-1));
                Step(8);
                FailIf(!session.Save.HasTreasure(0x42) || !session.Dialogue.IsOpen || !session.Dialogue.CurrentMessage.Contains("Gnarled Key",StringComparison.Ordinal),
                    $"The restored key must grant TREASURE$42 through shared pickup: obtained={session.Save.HasTreasure(0x42)}, text={session.Dialogue.CurrentMessage}, Link={session.Link.Position}.");
                session.Dialogue.Close();Step(8);
                FailIf(!session.Save.HasRoomFlag(2,0x0b,0x40) || !session.Save.HasRoomFlag(2,0x0b,0x20),
                    "Gnarled Key's source flags$09 must set ROOMFLAG_ITEM, and Maku state1 must then mark flag$40.");
                session.DebugTravel(0,0xd8);Step(2);session.DebugTravel(2,0x0b);session.Link.WarpTo(new(80,112));Step(5);
                FailIf(session.Entities.Entities<GroundTreasurePickup>().Count!=0,"Collected Gnarled Key must not respawn.");
                PopBubble();WaitForText(0x1703);session.Dialogue.Close();Step(180);
                FailIf(session.DisabledObjects!=0 || session.Memory.ReadField("wMenuDisabled")!=0 || session.Save.MakuMapTextPresent!=3,
                    "Repeat bubble wake must show TX_1703, retain map advice and release Link/menu control.");
                FailIf(!disk.SequenceEqual(root.FileSlotSnapshot(0,OracleGame.Seasons)),"Maku story writes must remain live until an explicit save.");
                PopBubble();WaitForText(0x1703);
                session.DebugTravel(0,0xd8);Step(3);
                FailIf(session.Dialogue.IsOpen || session.DisabledObjects!=0 || session.Memory.ReadField("wMenuDisabled")!=0 ||
                    (session.Memory.ReadField("wcc95")&0x80)!=0,
                    "Cancelling Maku's repeated advice on room retirement must close its text and release its control bytes.");

                void WaitForText(int id)
                {
                    for(int i=0;i<700&&(!session.Dialogue.IsOpen||session.Dialogue.SourceTextId!=id);i++)Step(1);
                    FailIf(!session.Dialogue.IsOpen||session.Dialogue.SourceTextId!=id,
                        $"Maku/batch{batched}: expected TX_{id:x4}, got {session.Dialogue.SourceTextId:x4}, mask${session.DisabledObjects:x2}, wcc95=${session.Memory.ReadField("wcc95"):x2}.");
                }
                void Choose(int choice)
                {
                    for(int i=0;i<40&&!session.Dialogue.ChoiceActive;i++)
                    {session.Dialogue.RevealCurrentPageForValidation();session.Dialogue.AdvanceOrClose();Step(8);}
                    session.Dialogue.SubmitChoiceForValidation(choice);Step(1);
                }
                void PopBubble()
                {
                    WalkSeasons(app,session,point=>point.X==64&&point.Y==64);
                    session.Link.Face(Vector2I.Up);
                    FailIf(session.Collision.Collides(session.Link.Position),"Maku bubble approach must be on its native floor.");
                    bool attacking=false;Rect2 sword=default;
                    for(int i=0;i<16&&!session.Dialogue.IsOpen&&(session.Memory.ReadWramByte(0xcfc0)&0x80)==0;i++)
                    {app.Step(1,Vector2.Zero,["attack"],["attack"],batched:batched);app.Step(25,Vector2.Zero,batched:batched,afterUpdate:()=>
                        {if(session.Link.IsAttacking){attacking=true;sword=session.Link.GetSwordHitbox();}});}
                    var bubble=session.Entities.EntityAdapters<SeasonsMakuBubbleRoomEntity>().FirstOrDefault();
                    int address=bubble is null?0:((0xd0+bubble.Slot)<<8)|0x80;
                    FailIf((session.Memory.ReadWramByte(0xcfc0)&0x80)==0,
                        $"Real Link sword collisions must pop ENEMY$56: attacking={attacking}, equipped=${session.Inventory.EquippedA:x2}/${session.Inventory.EquippedB:x2}, sword={sword}, bubble={bubble?.Position}, state={bubble?.State}, radius={session.Memory.ReadWramByte(address+0x26)}, health={session.Memory.ReadWramByte(address+0x29)}.");
                }
            }
            finally{root.Free();}
        }
    }
}
