using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public partial class EndingScreen
{
    private byte[] _creditsSpriteBytes=[],_savedCreditsFont=[],_maskedCreditsFont=[];
    private bool[] _creditsSpriteDefined=[],_savedFontDefined=[],_maskedFontDefined=[];
    private Image? _ownedSprites;
    private int _scrollX,_scrollY;
    private readonly Dictionary<object,(IntroOamPart[] Parts,int Y,int X,int Slot,int Priority,Func<Vector2,Vector2>? Project)> _objects=[];
    private readonly List<IntroOamPart> _objectOam=[];
    private readonly Dictionary<(int Tile,int Flags),Texture2D> _objectCells=[];
    internal IReadOnlyList<IntroOamPart> ObjectOam=>_objectOam;
    internal void SetScrollX(int value){_scrollX=value&255;QueueRedraw();}
    internal void SetScrollY(int value){_scrollY=value&255;QueueRedraw();}
    private void ResetCreditsGraphics()
    {
        _ownedSprites?.Dispose();_ownedSprites=null;_creditsSpriteBytes=[];_savedCreditsFont=[];_maskedCreditsFont=[];
        _creditsSpriteDefined=[];_savedFontDefined=[];_maskedFontDefined=[];
        _objects.Clear();_objectOam.Clear();_objectCells.Clear();_scrollX=_scrollY=0;
    }
    private static byte[] EncodedSpriteBytes(EndingScreenUpload upload,Image? source=null)
    {
        var image=source??OracleGraphicsCache.LoadImage(EndingScreenDatabase.Root+"ending/"+upload.Asset);
        int available=image.GetWidth()/8*(image.GetHeight()/8)-upload.Padding;
        int tiles=upload.Length==0?available:upload.Length/16;
        if(tiles>available)throw new InvalidOperationException($"Ending upload {upload.Asset} exceeds its encoded source bytes.");
        var bytes=new byte[tiles*16];
        for(int tile=0;tile<tiles;tile++)
        {
            var origin=OracleTileRenderer.SourceTileOrigin(image,tile,upload.Interleave);
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            {
                int shade=OracleGraphicsData.TwoBitShade(image.GetPixel(origin.X+x,origin.Y+y));
                if(upload.Invert)shade=3-shade;
                bytes[tile*16+y*2]|=(byte)((shade&1)<<(7-x));
                bytes[tile*16+y*2+1]|=(byte)(((shade>>1)&1)<<(7-x));
            }
        }
        return bytes;
    }
    private void UploadCreditsSprites(EndingScreenUpload upload,int offset)
    {
        if(_creditsSpriteBytes.Length==0){_creditsSpriteBytes=new byte[0x2000];_creditsSpriteDefined=new bool[0x2000];}
        offset+=upload.Bank*0x1000;
        var bytes=EncodedSpriteBytes(upload);Overlay(_creditsSpriteBytes,bytes,offset);Array.Fill(_creditsSpriteDefined,true,offset,bytes.Length);
    }
    private void UploadCreditsBuffer(EndingScreenUpload upload,bool masked,int offset)
    {
        // w4AttributeMap is $400 bytes after w4TileMap, and the corresponding
        // bank-3 buffers have the same spacing. UNCMP_GFXH_09 copies all $600
        // bytes as font graphics, not tile/attribute map pairs.
        var bytes=EncodedSpriteBytes(upload);
        ref byte[] buffer=ref(masked?ref _maskedCreditsFont:ref _savedCreditsFont);
        ref bool[] defined=ref(masked?ref _maskedFontDefined:ref _savedFontDefined);
        int size=Math.Max(0x600,offset+bytes.Length);
        if(buffer.Length<size){Array.Resize(ref buffer,size);Array.Resize(ref defined,size);}
        Overlay(buffer,bytes,offset);Array.Fill(defined,true,offset,bytes.Length);
    }
    internal void MaskCreditsFontColumn(int column,bool restore)
    {
        if((uint)column>=16||_maskedCreditsFont.Length!=0x600||_savedCreditsFont.Length!=0x600)
            throw new InvalidOperationException($"creditsTextHorizontal.s: UNCMP_GFXH_09 font buffers/column ${column:x2} are unavailable.");
        for(int row=0;row<48;row++)for(int plane=0;plane<2;plane++)
        {
            int offset=row*32+column*2+plane;_maskedCreditsFont[offset]=restore?_savedCreditsFont[offset]:(byte)0;
            _maskedFontDefined[offset]=!restore||_savedFontDefined[offset];
        }
        _maskedCreditsFont.CopyTo(_creditsSpriteBytes,0);_maskedFontDefined.CopyTo(_creditsSpriteDefined,0);RebuildCreditsSprites();RebuildObjectOam();
    }
    private void RebuildCreditsSprites()
    {
        var image=OracleGraphicsCache.DecodeTwoBitSpriteSheet(_creditsSpriteBytes);
        _ownedSprites?.Dispose();_ownedSprites=image;_sprites=image;_objectCells.Clear();
    }
    internal void SetObjectOam(object owner,IntroOamPart[] parts,int y,int x,int slot,int priority=2,Func<Vector2,Vector2>? project=null)
    {_objects[owner]=(parts,y,x,slot,priority,project);RebuildObjectOam();}
    internal void RefreshObjectOam()=>RebuildObjectOam();
    internal void RemoveObjectOam(object owner){_objects.Remove(owner);RebuildObjectOam();}
    private void RebuildObjectOam()
    {
        _objectOam.Clear();
        int prefix=0;foreach(var part in _parts)if(((part.Y+_oamY)&255)<160&&((part.X+_oamX)&255)<168)prefix++;
        foreach(var obj in _objects.Values.OrderBy(value=>value.Priority).ThenBy(value=>value.Slot))foreach(var part in obj.Parts)
        {
            Vector2 position=obj.Project?.Invoke(new(obj.X,obj.Y))??new Vector2(obj.X,obj.Y);
            int y=(part.Y+(int)position.Y)&255,x=(part.X+(int)position.X)&255;
            if(y>=160||x>=168||_objectOam.Count+prefix>=40)continue;
            // Padding between font uploads aliases saved OAM/ring scratch
            // memory in the ROM. It is not a glyph. Keep it explicitly unknown
            // rather than inventing those unrelated bytes or rendering them.
            int tile=part.Tile+((part.Flags&8)!=0?0x100:0);
            int first=(tile&~1)*16;
            for(int offset=first;offset<first+32;offset++)if(!_creditsSpriteDefined[offset])
                throw new NotSupportedException($"Credits OAM tile ${part.Tile:x2}, bank{(part.Flags>>3)&1} reads uninitialized font byte ${0x8000+(offset&0xfff):x4}.");
            var key=(part.Tile,part.Flags);
            if(!_objectCells.ContainsKey(key))
            {
                var live=_entities!.NativeObjectPalettes??throw new InvalidOperationException("Credits OAM requires resident OBJ palettes.");
                var palettes=new Color[8,4];foreach(var entry in live)for(int shade=0;shade<4;shade++)palettes[entry.Key,shade]=entry.Value[shade];
                _objectCells.Add(key,OracleTileRenderer.GetOamCellTexture(_sprites!,tile,(byte)part.Flags,palettes));
            }
            _objectOam.Add(new(y,x,part.Tile,part.Flags));
        }
        QueueRedraw();
    }
}
