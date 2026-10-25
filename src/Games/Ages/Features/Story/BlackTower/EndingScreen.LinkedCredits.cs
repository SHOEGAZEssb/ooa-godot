using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public partial class EndingScreen
{
    internal void InstallObjectGraphics(NpcRecord record)
    {
        // Ordinary NPC rendering uses a resident image independently of the
        // ROM's replaceable OBJ cache slots. Use that same canonical slot here.
        var upload=new EndingScreenUpload(record.SpriteName+".png","8000",0,0,true,true,0,512);
        var image=OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{record.SpriteName}.png");
        var bytes=EncodedSpriteBytes(upload,image);
        Array.Copy(bytes,_creditsSpriteBytes,bytes.Length);Array.Fill(_creditsSpriteDefined,true,0,bytes.Length);RebuildCreditsSprites();RebuildObjectOam();
    }
    internal void ApplyLinkedRamHeader(int header)
    {
        foreach(var upload in _data!.LinkedRamUploads(header))
        {
            if(upload.Target==0x8600&&upload.Bank==1&&upload.Buffer=="w4TileMap")
            {
                if(_maskedCreditsFont.Length<upload.Length||_creditsSpriteBytes.Length!=0x2000)throw new InvalidOperationException("UNCMP_GFXH_01 has no saved Triforce graphics.");
                Array.Copy(_maskedCreditsFont,0,_creditsSpriteBytes,0x1600,upload.Length);
                Array.Copy(_maskedFontDefined,0,_creditsSpriteDefined,0x1600,upload.Length);
                RebuildCreditsSprites();
            }
            else if(upload.Target==0x9800)
            {
                byte[] source=upload.Buffer switch{"w4TileMap"=>_secondMap,"w4AttributeMap"=>_secondAttributes,_=>throw new NotSupportedException($"UNCMP_GFXH_${header:x2}: {upload.Buffer}.")};
                if(source.Length<upload.Length)throw new InvalidOperationException("UNCMP_GFXH_2c has no saved waving tilemap.");
                Array.Copy(source,upload.Bank==0?_map:_attributes,upload.Length);RebuildBackground();
            }
            else throw new NotSupportedException($"UNCMP_GFXH_${header:x2}: ${upload.Target:x4}/bank{upload.Bank}.");
        }
        RebuildObjectOam();
    }
    internal void SetOamBatches(params (string Name,int Y,int X)[] batches)
    {
        var parts=new List<IntroOamPart>();
        foreach(var batch in batches)foreach(var part in _data!.Oam(batch.Name))
        {
            int y=(part.Y+batch.Y)&255,x=(part.X+batch.X)&255;
            if(y>=160||x>=168||parts.Count==40)continue;
            parts.Add(new(y,x,part.Tile,part.Flags));
        }
        _oamY=_oamX=0;
        if(!_parts.SequenceEqual(parts))
        {
            _parts=parts.ToArray();_cells=new Texture2D[_parts.Length];
            var colors=new Color[8,4];foreach(var palette in _entities!.NativeObjectPalettes!)for(int shade=0;shade<4;shade++)colors[palette.Key,shade]=palette.Value[shade];
            for(int index=0;index<_parts.Length;index++)
            {
                var part=_parts[index];var key=(part.Tile,part.Flags);
                if(!_objectCells.TryGetValue(key,out var cell))
                {
                    int tile=part.Tile+((part.Flags&8)!=0?256:0);
                    _objectCells.Add(key,cell=OracleTileRenderer.GetOamCellTexture(_sprites!,tile,(byte)part.Flags,colors));
                }
                _cells[index]=cell;
            }
        }
        RebuildObjectOam();QueueRedraw();
    }
}
