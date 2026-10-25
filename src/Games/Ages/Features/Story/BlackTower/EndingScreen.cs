using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Stable screen-space presentation. Native event state owns uploads/OAM;
// palette state and cell rasterization stay in the shared graphics owners.
public partial class EndingScreen : Control
{
    private EndingScreenDatabase? _data;
    private BackgroundPaletteState? _background;
    private RoomEntityManager? _entities;
    private OracleVramTileMap? _tiles;
    private Image? _sprites;
    private Image? _roomGraphics;
    private readonly List<Image> _ownedBackgroundImages=[];
    private RoomView? _roomView;
    private byte[] _map=[],_attributes=[],_secondMap=[],_secondAttributes=[];
    private byte[] _windowMap=[],_windowAttributes=[];
    private IntroOamPart[] _parts=[];
    private Texture2D[] _cells=[];
    private Texture2D? _texture;
    private int _oamY,_oamX;
    internal Texture2D? BackgroundTexture=>_texture;
    internal IReadOnlyList<IntroOamPart> Parts=>_parts;
    internal Vector2I OamOffset=>new(_oamX,_oamY);
    internal void Bind(EndingScreenDatabase data,BackgroundPaletteState background,RoomEntityManager entities)
    {
        if(_background is not null)_background.Changed-=RebuildBackground;
        _data=data;_background=background;_entities=entities;_background.Changed+=RebuildBackground;
    }
    internal void Load(string profile,OracleRoomData? room=null,RoomView? roomView=null,int roomMapHeader=0x2d,bool retainGraphics=false,string? extraProfile=null)
    {
        ReleaseSecretGraphics();
        var retainedBytes=_creditsSpriteBytes;var retainedDefined=_creditsSpriteDefined;
        ResetCreditsGraphics();if(retainGraphics){_creditsSpriteBytes=retainedBytes;_creditsSpriteDefined=retainedDefined;}
        bool creditsImage=profile.StartsWith("credits-image",StringComparison.Ordinal),creditsSprites=creditsImage||profile is "credits-scroll" or "credits-waving" or "credits-linked-end" or "ending-secret" or "ending-continued";
        _nativeAnimationStart=null;
        _roomGraphics?.Dispose();_roomGraphics=null;_roomView=roomView;
        if(!retainGraphics)
        {
            foreach(var owned in _ownedBackgroundImages)owned.Dispose();_ownedBackgroundImages.Clear();
            _tiles=new();_map=new byte[1024];_attributes=new byte[1024];
        }
        if(_tiles is null)throw new InvalidOperationException("Retained ending graphics require a preceding graphics profile.");
        _secondMap=[];_secondAttributes=[];
        _windowMap=new byte[1024];_windowAttributes=new byte[1024];_sprites=null;
        var zero=OracleGraphicsCache.LoadImage(EndingScreenDatabase.Root+"ending/zero_tile.png");
        if(room is not null)
        {
            Array.Fill(_attributes,(byte)0x80);
            _roomGraphics=room.CaptureLiveGraphics();_tiles.Map(_roomGraphics,0x8800,1);
            _tiles.Map(OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + "gfx/gfx_hud.png"),0x9000,0);
            byte[] pairs=room.CaptureBackgroundMappings(nativeStorage:true);
            int start=roomMapHeader switch {0x2d=>64,0x0f=>0,_=>throw new NotSupportedException($"Ending room UNCMP_GFXH_${roomMapHeader:x2}.")};
            for(int index=0;index<512;index++){_map[start+index]=pairs[index*2];_attributes[start+index]=pairs[index*2+1];}
        }
        // GFXH_TWINROVA_CLOSEUP clears VRAM bank 0 before loading its header.
        if(profile=="closeup")for(int index=0;index<256;index++)_tiles.MapTile(zero,0,index,0);
        var uploads=new List<EndingScreenUpload>(_data!.Uploads(profile));
        if(extraProfile is not null)uploads.AddRange(_data.Uploads(extraProfile));
        foreach(var upload in uploads)
        {
            string path=EndingScreenDatabase.Root+"ending/"+upload.Asset;
            switch(upload.Target)
            {
                case "8000":if(creditsSprites)UploadCreditsSprites(upload,0);else _sprites=OracleGraphicsCache.LoadImage(path);break;
                case "8200":case "8400":case "8600":UploadCreditsSprites(upload,Convert.ToInt32(upload.Target,16)-0x8000);break;
                case "8800":case "8a00":case "8e00":case "9000":case "9200":case "9300":
                    var image=OracleGraphicsCache.LoadImage(path);int address=Convert.ToInt32(upload.Target,16);
                    int first=(address-0x9000)/16&255;
                    // gfx.py removes tile_padding from the padded PNG on
                    // encoding; those pixels are not native VRAM uploads.
                    int available=image.GetWidth()/8*(image.GetHeight()/8)-upload.Padding;
                    int count=upload.Length==0?available:upload.Length/16;
                    if(count>available)throw new InvalidOperationException($"Ending upload {upload.Asset} exceeds its encoded source bytes.");
                    if(upload.Invert)
                    {
                        // Native encoding applies .properties inversion to BG
                        // uploads as well as OBJ uploads. Cached PNGs are immutable.
                        byte[] pixels=image.GetData();
                        for(int at=0;at<pixels.Length;at+=4)for(int channel=0;channel<3;channel++)pixels[at+channel]=(byte)(255-pixels[at+channel]);
                        image=Image.CreateFromData(image.GetWidth(),image.GetHeight(),false,Image.Format.Rgba8,pixels);
                        _ownedBackgroundImages.Add(image);
                    }
                    for(int index=0;index<count;index++)
                    {
                        var origin=OracleTileRenderer.SourceTileOrigin(image,index,upload.Interleave);
                        _tiles.MapTile(image,origin.Y/8*(image.GetWidth()/8)+origin.X/8,(first+index)&255,upload.Bank);
                    }
                    // $8800..$8fff are also OBJ tiles $80..$ff in that bank.
                    if(creditsSprites&&address==0x8800)UploadCreditsSprites(upload,0x800);
                    break;
                case "9800":if(upload.Bank==0)Overlay(_map,FileAccess.GetFileAsBytes(path),0);else Overlay(_attributes,FileAccess.GetFileAsBytes(path),0);break;
                case "9a00":if(upload.Bank==0)Overlay(_map,FileAccess.GetFileAsBytes(path),512);else Overlay(_attributes,FileAccess.GetFileAsBytes(path),512);break;
                case "9c40":if(upload.Bank==0)Overlay(_windowMap,FileAccess.GetFileAsBytes(path),64);else Overlay(_windowAttributes,FileAccess.GetFileAsBytes(path),64);break;
                // These are the original WRAM copies of the same native maps.
                case "w3VramTiles":if(creditsImage)UploadCreditsBuffer(upload,false,0);else Overlay(_map,FileAccess.GetFileAsBytes(path),upload.Offset);break;
                case "w3VramAttributes":if(creditsImage)UploadCreditsBuffer(upload,false,0x400);else Overlay(_attributes,FileAccess.GetFileAsBytes(path),upload.Offset);break;
                case "w4TileMap":if(creditsSprites&&profile!="credits-waving")UploadCreditsBuffer(upload,true,0);else _secondMap=Expand(FileAccess.GetFileAsBytes(path));break;
                case "w4AttributeMap":if(creditsImage)UploadCreditsBuffer(upload,true,0x400);else _secondAttributes=Expand(FileAccess.GetFileAsBytes(path));break;
                default:throw new NotSupportedException($"gfxHeaders.s ending {profile}: unsupported destination {upload.Target}.");
            }
        }
        if(creditsSprites)RebuildCreditsSprites();
        _parts=[];_cells=[];Visible=roomView is null;RebuildBackground();
    }
    internal void UseSecondMap()
    {
        if(_secondMap.Length!=1024||_secondAttributes.Length!=1024)throw new InvalidOperationException("UNCMP_GFXH_0a requires both saved ending tilemaps.");
        _map=_secondMap;_attributes=_secondAttributes;RebuildBackground();
    }
    internal void UseTitleMap()
    {
        // GFXH_TWINROVA_CLOSEUP retains this $9c00 map while the face
        // uses $9800. CUTSCENE $0a only changes LCDC bit 3.
        _map=_windowMap;_attributes=_windowAttributes;RebuildBackground();
    }
    internal void SetOam(string? name,int y=0,int x=0)
    {
        var parts=name is null?Array.Empty<IntroOamPart>():_data!.Oam(name);
        if(!ReferenceEquals(parts,_parts))
        {
            _parts=parts;_cells=new Texture2D[parts.Length];
            if(parts.Length>0&&_sprites is null)throw new InvalidOperationException("Ending OAM has no resident source sprite header.");
            var palettes=new Color[8,4];var live=_entities!.NativeObjectPalettes??throw new InvalidOperationException("Ending OAM has no native sprite palettes.");
            foreach(var entry in live)for(int shade=0;shade<4;shade++)palettes[entry.Key,shade]=entry.Value[shade];
            for(int index=0;index<parts.Length;index++)_cells[index]=OracleTileRenderer.GetOamCellTexture(_sprites!,parts[index].Tile,(byte)parts[index].Flags,palettes);
        }
        _oamY=y;_oamX=x;RebuildObjectOam();QueueRedraw();
    }
    private void RebuildBackground()
    {
        if(_tiles is null||_background is null)return;
        _texture=OracleTileRenderer.BuildTileMapTexture(_map,_attributes,_tiles,_background.Capture(),32,32);
        _roomView?.SetNativeTilemap(_texture);
        QueueRedraw();
    }
    internal void Release()
    {
        Visible=false;if(_background is not null)_background.Changed-=RebuildBackground;
        _background=null;_entities=null;_tiles=null;_sprites=null;_texture=null;_parts=[];_cells=[];
        _roomView=null;_roomGraphics?.Dispose();_roomGraphics=null;
        ReleaseSecretGraphics();ResetCreditsGraphics();
        foreach(var owned in _ownedBackgroundImages)owned.Dispose();_ownedBackgroundImages.Clear();
        _nativeAnimationStart=null;
    }
    public override void _ExitTree()=>Release();
    public override void _Draw()
    {
        if(_texture is null)return;DrawTexture(_texture,new(-_scrollX,-_scrollY));
        if(_scrollX!=0)DrawTexture(_texture,new(256-_scrollX,-_scrollY));
        if(_scrollY!=0)
        {
            DrawTexture(_texture,new(-_scrollX,256-_scrollY));
            if(_scrollX!=0)DrawTexture(_texture,new(256-_scrollX,256-_scrollY));
        }
        for(int index=_objectOam.Count-1;index>=0;index--)
        {
            var part=_objectOam[index];DrawTexture(_objectCells[(part.Tile,part.Flags)],new(part.X-8,part.Y-16));
        }
        for(int index=_parts.Length-1;index>=0;index--)
        {
            int y=(_parts[index].Y+_oamY)&255,x=(_parts[index].X+_oamX)&255;
            if(y<160&&x<168)DrawTexture(_cells[index],new(x-8,y-16));
        }
    }
    private static byte[] Expand(byte[] bytes){var output=new byte[1024];Overlay(output,bytes,0);return output;}
    private static void Overlay(byte[] target,byte[] bytes,int offset)
    {if(offset+bytes.Length>target.Length)throw new InvalidOperationException("Ending map upload exceeds $400 VRAM bytes.");bytes.CopyTo(target,offset);}
}
