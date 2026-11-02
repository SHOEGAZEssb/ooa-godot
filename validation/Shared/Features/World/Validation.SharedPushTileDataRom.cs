
namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateSharedPushTileDataRom()
    {
        foreach(var game in new[]{OracleGame.Ages,OracleGame.Seasons})
        {
            bool ages=game==OracleGame.Ages;
            var image=ValidationRom.LoadCleanUs(game);
            int Byte(int bank,int address)=>image.Span[bank*0x4000+(address&0x3fff)];
            int Word(int bank,int address)=>Byte(bank,address)|(Byte(bank,address+1)<<8);
            int parameterTable=ages?0x43b2:0x43a3,propertyTable=ages?0x452d:0x43f5;
            var push=new PushableTileDatabase(OracleAssetPack.ForGame(game));
            var dispatch=new InteractableTileDatabase(OracleAssetPack.ForGame(game));
            int entries=0,pushes=0;
            for(int mode=0;mode<6;mode++)
            for(int tile=0;tile<256;tile++)
            {
                int cursor=Word(6,parameterTable+mode*2),parameter=-1;
                while(Byte(6,cursor)!=0)
                {
                    if(Byte(6,cursor)==tile){parameter=Byte(6,cursor+1);break;}
                    cursor+=2;
                }
                FailIf(dispatch.Parameter(mode,(byte)tile)!=unchecked((byte)parameter),
                    $"{game}: native input profile${mode:x2}/tile${tile:x2} differs.");
                if(parameter>=0)entries++;
                bool somaria=ages&&tile==0xda;
                FailIf(push.TryGetSomaria(mode,(byte)tile,out byte somariaParameter)!=somaria ||
                    somaria&&somariaParameter!=parameter,
                    $"{game}: ITEM$18 push dispatch must retain its actual source build guard.");
                bool expected=parameter>=0&&(parameter&15)==0&&!somaria;
                if(expected)pushes++;
                for(int group=0;group<8;group++)
                {
                    int profile=ages?mode:group;
                    cursor=propertyTable+profile+Byte(8,propertyTable+profile);
                    while(Byte(8,cursor)!=0&&Byte(8,cursor)!=tile)cursor+=4;
                    var properties=Byte(8,cursor)==0?default:
                        new PushableTileRecord(0,(byte)Byte(8,cursor+1),(byte)Byte(8,cursor+2),(byte)Byte(8,cursor+3));
                    FailIf(push.GetNativeProperties(mode,(byte)tile,group)!=properties,
                        $"{game}: native actor property mode${mode:x2}/group${group:x2}/tile${tile:x2} differs, including script-only IDs and misses.");
                    bool found=push.TryGet(mode,(byte)tile,out var actual,group);
                    var record=properties with{InteractionParameter=unchecked((byte)parameter)};
                    FailIf(found!=expected||found&&actual!=record,
                        $"{game}: native push producer/actor handoff differs for mode${mode:x2}/group${group:x2}/tile${tile:x2}.");
                }
            }
            FailIf(entries!=(ages?115:74)||pushes!=(ages?51:29),
                $"{game}: native push/input profile counts differ: {pushes}/{entries}.");
            string root=OracleAssetPack.ForGame(game);
            var doors=new DungeonKeyDoorDatabase(root);
            var keyBlock=new DungeonKeyBlockDatabase(root);
            for(int mode=0;mode<6;mode++)
            for(int tile=0;tile<256;tile++)
            {
                int parameter=dispatch.Parameter(mode,(byte)tile);
                bool expected=parameter!=255 && (parameter&15)==2;
                FailIf(doors.TryGet(mode,(byte)tile,out var door)!=expected,
                    $"{game}: key-door contact eligibility differs for profile${mode:x2}/tile${tile:x2}.");
                if(!expected)continue;
                int direction=(parameter>>4)&3;
                FailIf(door.RoomFlag!=Byte(0,(ages?0x122e:0x11ec)+direction*4) ||
                    door.OppositeRoomFlag!=Byte(0,(ages?0x122e:0x11ec)+direction*4+2) ||
                    door.OpenTile!=0xa0 || door.PushCounter!=20 || door.DoorFrameWait!=6 ||
                    door.UsesBossKey!=((parameter&0x40)!=0) || door.KeyGraphic!=((parameter&0x40)!=0?0x43:0x42),
                    $"{game}: key-door native flags, speed or key presentation differ.");
            }
            for(int mode=0;mode<6;mode++)
                FailIf(keyBlock.SupportsActiveCollisions(mode)!=((dispatch.Parameter(mode,0x1e)&15)==1),
                    $"{game}: key-block contact eligibility differs for profile${mode:x2}.");
            var hints=new TileInfoTextDatabase(root);
            var keyholes=new OverworldKeyholeDatabase(root);
            int keyholeCount=0;
            for(int group=0;group<(ages?6:1);group++)
            {
                int cursor=Word(6,(ages?0x4267:0x4238)+group*2);
                var expected=new bool[256];
                while(Byte(6,cursor)!=0)
                {
                    int room=Byte(6,cursor),treasure=Byte(6,cursor+1),subid=treasure-0x42;
                    expected[room]=true;cursor+=2;keyholeCount++;
                    FailIf(!keyholes.TryGet(group,room,out var key) || key.Treasure!=treasure || key.SubId!=subid ||
                        key.TileBase!=(Byte(0x3f,(ages?0x67a7:0x67a8)+subid*3+1)&0x7f) ||
                        key.Palette!=((Byte(0x3f,(ages?0x67a7:0x67a8)+subid*3+2)>>4)&7),
                        $"{game}: native named-key table or selected graphics differ in room${group:x1}:${room:x2}.");
                }
                for(int room=0;room<256;room++)
                    FailIf(keyholes.TryGet(group,room,out _)!=expected[room],$"{game}: named-key room eligibility differs.");
            }
            FailIf(keyholes.Count!=keyholeCount || keyholeCount!=(ages?6:3),$"{game}: named-key table aliases or terminators differ.");
            for(int mode=0;mode<6;mode++)
            for(int tile=0;tile<256;tile++)
                FailIf(keyholes.IsKeyholeTile(mode,(byte)tile)!=((dispatch.Parameter(mode,(byte)tile)&15)==6),
                    $"{game}: named-key contact profile differs for mode${mode:x2}/tile${tile:x2}.");
            for(int index=0;index<10;index++)
            {
                int at=(ages?0x4315:0x4300)+index*2,text=0x5100|Byte(6,at+1);
                FailIf(hints.Get(text).Mask!=Byte(6,at),$"{game}: source hint row${index:x2} lost its shared suppression mask.");
            }
            if(!ages)
                FailIf(hints.Get(0x5105).Message!="This block has\ncracks in it.\nIt looks like it\ncould be broken!" ||
                    hints.Get(0x5106).Message!="This wall is\ncracked.\\nIt looks like it\ncould be broken!" ||
                    hints.Get(0x510a).Message!="Oof! It's too\nheavy to move!",
                    "Seasons: hint jump, missing-terminator fallthrough and roller text must retain source content.");
            var shops=new RoomShopDatabase(root);
            int shopTable=ages?0x7aaa:0x671a,shopBank=ages?2:4,shopCount=0;
            for(int group=0;group<8;group++)
            {
                int cursor=Word(shopBank,shopTable+group*2);
                var expected=new byte[256];
                while(Byte(shopBank,cursor)!=0)
                {
                    int room=Byte(shopBank,cursor),handler=Byte(shopBank,cursor+1);cursor+=2;
                    if(handler==4){expected[room]=2;shopCount++;}
                }
                for(int room=0;room<256;room++)
                    FailIf(shops.InitialFlags(group,room)!=expected[room],$"{game}: source graphics initialization lost shop flags for room${group:x1}:${room:x2}.");
            }
            FailIf(shopCount!=(ages?4:12),$"{game}: native shop alias counts differ.");
            var substitutions=GeneratedTable.Load(root+"metadata/standard_tile_substitutions.tsv",
                new GeneratedTableSchema("native standard substitutions",GeneratedTableKeySemantics.Grouped,
                    ["room-flag","profile","replacement","original","source"],["room-flag","profile"],headerRequired:true));
            int substitutionTable=ages?0x60f5:0x5e26,profiles=ages?6:8,total=0;
            for(int bitIndex=0;bitIndex<5;bitIndex++)
            for(int profile=0;profile<profiles;profile++)
            {
                int flag=1<<(bitIndex==4?7:bitIndex);
                var rows=substitutions.Rows.Where(row=>row.HexByte(0)==flag && row.Decimal(1)==profile).ToArray();
                int cursor=Word(4,substitutionTable+(bitIndex*profiles+profile)*2),index=0;
                while(Byte(4,cursor)!=0)
                {
                    FailIf(index>=rows.Length || rows[index].HexByte(2)!=Byte(4,cursor) ||
                        rows[index].HexByte(3)!=Byte(4,cursor+1),$"{game}: native standard substitution order differs for flag${flag:x2}/profile${profile:x2}.");
                    index++;cursor+=2;total++;
                }
                FailIf(index!=rows.Length,$"{game}: extra substitution rows beyond the native terminator.");
            }
            FailIf(total!=substitutions.Rows.Count || total!=(ages?50:67),$"{game}: selected native standard substitution profiles lost aliases or rows.");
            var single=GeneratedTable.Load(root+"metadata/single_tile_changes.tsv",
                new GeneratedTableSchema("native single tile changes",GeneratedTableKeySemantics.Grouped,
                    ["group","room","mask","position","tile","source"],["group","room"],headerRequired:true));
            int singleTable=ages?0x632e:0x6005,singleIndex=0;
            for(int group=0;group<8;group++)
            {
                int cursor=Word(4,singleTable+group*2);
                while(Byte(4,cursor)!=0 || Byte(4,cursor+1)!=0)
                {
                    FailIf(singleIndex>=single.Rows.Count,$"{game}: omitted native single tile changes.");
                    var row=single.Rows[singleIndex++];
                    FailIf(row.Decimal(0)!=group || row.HexByte(1)!=Byte(4,cursor) ||
                        row.HexByte(2)!=Byte(4,cursor+1) || row.HexByte(3)!=Byte(4,cursor+2) || row.HexByte(4)!=Byte(4,cursor+3),
                        $"{game}: native single-tile group${group:x1} order, masks or aliases differ.");
                    cursor+=4;
                }
            }
            FailIf(singleIndex!=single.Rows.Count || singleIndex!=(ages?56:41),$"{game}: native single-tile row count differs.");
        }
    }
}
