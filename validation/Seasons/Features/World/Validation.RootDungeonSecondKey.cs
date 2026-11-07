namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsRootDungeonSecondKey()
    {
        foreach(bool batched in new[]{false,true})
        {
            var root=StartSeasonsAfterImpa(batched);
            try
            {
                var s=root._seasonsOpening!;var app=new ApplicationValidationFixture(root);var entities=s.Entities;
                s.Inventory.EquipA(0);s.Inventory.EquipB(0);
                void Walk(Vector2 input,Func<bool> arrived,int limit=120)
                {
                    for(int tick=0;tick<limit&&!arrived();tick++)app.Step(1,input,batched:batched);
                    FailIf(!arrived(),$"The source key-room corridor must remain reachable: room${s.RoomId:x2}, Link{s.Link.Position}, input{input}.");
                }
                // The switch's actual Sword producer has native coverage.
                // Declare its retained bit and a mounted cart on the source rail.
                s.Memory.SetField("wSwitchState",0x81);s.DebugTravel(4,0x0f);
                Vector2 start=new(56,120);
                MinecartValidationView.PrepareMountedBody(s.Memory,start,1);s.Memory.SetWramByte(0xd109,8);
                entities.BindNativeSpecialObjectPresentation(SpecialObjectId.Minecart,new MinecartBodySpawn());
                s.Link.WarpTo(start);s.Link.FinishMinecartMount(start,1,0);
                bool sawBombRoom=false,sawKeyRoom=false;
                void FreezeHostiles()
                {
                    s.Memory.SetField("wDisabledObjects",(byte)(s.Memory.ReadField("wDisabledObjects")|4));
                    if(s.RoomId==0x10)
                    {
                        sawBombRoom=true;
                        FailIf(entities.Entities<KeeseCharacter>().Count!=5 || entities.RoomEnemyCount!=5,
                            "The switched $4:0f rail must lead through $4:10's original five-Keese stream.");
                    }
                    if(s.RoomId==0x11)
                    {
                        sawKeyRoom=true;
                        FailIf(entities.Entities<KeeseCharacter>().Count!=3 || entities.Entities<BladeTrapCharacter>().Count!=1 ||
                            entities.RoomEnemyCount!=4 || entities.EntityAdapters<GroundButtonRoomEntity>().Count()!=1 ||
                            entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count!=1,
                            "$4:11 must construct three Keese, the flags$05 counted red trap, PART$09:$00 and INTERAC$20:$02.");
                    }
                }
                FreezeHostiles();
                for(int tick=0;tick<1000&&(!sawKeyRoom || s.Scrolling || s.Link.MinecartRideActive || s.Link.MinecartJumpActive);tick++)
                    app.Step(1,Vector2.Zero,batched:batched,afterUpdate:FreezeHostiles);
                app.Step(3,Vector2.Zero,batched:batched);
                FailIf(!sawBombRoom || !sawKeyRoom || s.Scrolling || s.Link.MinecartRideActive || s.Link.MinecartJumpActive ||
                    s.Room.GetPackedPosition(s.Link.Position)!=0x73 ||
                    entities.Entities<MinecartInteractionRoomEntity>().Single().Position!=new Vector2(40,120),
                    $"The switched rail must dismount on $4:11's original $73 platform: room${s.RoomId:x2}, Link{s.Link.Position}, ride{s.Link.MinecartRideActive}.");
                FailIf(s.Collision.Collides(s.Link.Position),"The key-room approach must begin on the original cart landing floor.");
                // Cross the stone corridor through both original stairways;
                // cutting the raised floor's corner instead starts a ledge hop.
                Walk(Vector2.Up,()=>s.Link.Position.Y<=104);
                Walk(Vector2.Left,()=>s.Link.Position.X<=40);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=24,180);
                Walk(Vector2.Right,()=>s.Link.Position.X>=120);
                Walk(Vector2.Down,()=>s.Link.Position.Y>=152,240);
                Walk(Vector2.Right,()=>s.Link.Position.X>=216);
                Walk(Vector2.Up,()=>s.Link.Position.Y<=104);
                Walk(Vector2.Left,()=>s.Link.Position.X<=200);
                var script=entities.Entities<DungeonTriggerChestScriptRoomEntity>().Single();
                FailIf(entities.ActiveTriggers!=0 || s.Room.Layout[0x2c]==0xf1,
                    "The $2c key chest must wait for actual button contact.");
                for(int tick=0;tick<24&&entities.ActiveTriggers==0;tick++)app.Step(1,Vector2.Up,batched:batched);
                FailIf(entities.ActiveTriggers!=1 || script.Counter!=-1,
                    "checkmemoryeq must accept the real one-shot button and yield before the puff sequence.");
                app.Step(2,Vector2.Zero,batched:batched);
                FailIf(script.Counter!=-1 || s.Room.Layout[0x2c]==0xf1,"Sound and puff each yield before wait15.");
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(script.Counter!=15,"The original wait command must install all fifteen updates.");
                app.Step(14,Vector2.Zero,batched:batched);
                FailIf(script.Counter!=1 || s.Room.Layout[0x2c]==0xf1,"The source key chest must remain absent through wait update14.");
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(entities.ActiveTriggers!=1 || s.Room.Layout[0x5c]!=0x0d || s.Room.Layout[0x2c]!=0xf1 ||
                    entities.RoomEnemyCount!=4,
                    "The one-shot button must reveal the $2c small-key chest while all four counted enemies remain alive.");
                Walk(Vector2.Up,()=>s.Link.Position.Y<=56);
                app.Step(8,Vector2.Up,batched:batched);
                s.Link.Face(Vector2I.Up);
                FailIf(!s.Link.FacesTileWallForInteraction || s.Collision.Collides(s.Link.Position),
                    "Opening $2c must use actual southern chest contact on clear floor.");
                app.Step(1,Vector2.Zero,held:["attack"],pressed:["attack"],batched:batched);
                app.Step(40,Vector2.Zero,batched:batched);
                FailIf(s.Inventory.GetDungeonSmallKeys(1)!=1 || s.Inventory.GetDungeonSmallKeys(0)!=0 ||
                    !s.Save.HasRoomFlag(4,0x11,0x20) || s.Room.Layout[0x2c]!=0xf0,
                    $"The source SMALL_KEY_03 chest must grant one dungeon$01 key and ROOMFLAG_ITEM: Link{s.Link.Position}, key1={s.Inventory.GetDungeonSmallKeys(1)}, key0={s.Inventory.GetDungeonSmallKeys(0)}, flag{s.Save.HasRoomFlag(4,0x11,0x20)}, tile${s.Room.Layout[0x2c]:x2}, text${s.LastText:x4}.");
                s.Dialogue.Close();app.Step(80,Vector2.Zero,batched:batched);
                s.DebugTravel(4,0x10);s.DebugTravel(4,0x11);s.Memory.SetField("wDisabledObjects",4);
                s.Link.WarpTo(new(216,88));app.Step(3,Vector2.Zero,batched:batched);
                FailIf(s.Collision.Collides(s.Link.Position) || entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count!=0 ||
                    s.Room.Layout[0x2c]!=0xf0,"A collected key watcher must retire and preserve its opened chest on re-entry.");
                Walk(Vector2.Left,()=>s.Link.Position.X<=200);app.Step(24,Vector2.Zero,batched:batched);
                FailIf(entities.ActiveTriggers!=1 || s.Room.Layout[0x2c]!=0xf0 || s.Inventory.GetDungeonSmallKeys(1)!=1,
                    "Pressing the reset button again must retain the collected chest and key count.");
            }
            finally {root.Free();}
        }
    }
}
