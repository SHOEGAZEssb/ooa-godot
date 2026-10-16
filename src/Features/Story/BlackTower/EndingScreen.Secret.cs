using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class EndingScreen
{
    private Image? _secretFont;
    private IntroOamPart[]? _decorations;
    internal void UploadSecretText(ReadOnlySpan<byte> text)
    {
        byte[] font=OracleTileRenderer.CopyTextCharacterGraphics(text,0xff);
        foreach(var upload in _data!.SecretRamUploads)
        {
            switch(upload.Buffer)
            {
                case "w7d800":
                    if(upload.Length!=font.Length||upload.Target<0x8800||upload.Target+upload.Length>0x9000)
                        throw new InvalidOperationException("UNCMP_GFXH_2b: secret font must fit the resident BG graphics range.");
                    _secretFont=OracleGraphicsCache.DecodeTwoBitSpriteSheet(font,spriteEncoding:false);
                    int first=0x80+(upload.Target-0x8800)/16;
                    for(int tile=0;tile<font.Length/16;tile++)
                    {
                        var origin=OracleTileRenderer.SourceTileOrigin(_secretFont,tile,interleaved:true);
                        _tiles!.MapTile(_secretFont,origin.Y/8*(_secretFont.GetWidth()/8)+origin.X/8,(first+tile)&255,upload.Bank);
                    }
                    break;
                case "w3VramTiles":CheckMapUpload(upload,0);break;
                case "w3TileMappingIndices":CheckMapUpload(upload,1);break;
                default:throw new NotSupportedException($"UNCMP_GFXH_2b: unsupported ending RAM buffer {upload.Buffer}.");
            }
        }
        RebuildBackground();
    }
    private static void CheckMapUpload(EndingScreenRamUpload upload,int bank)
    {
        if(upload.Target!=0x9800||upload.Bank!=bank||upload.Length!=0x2c0)
            throw new NotSupportedException("UNCMP_GFXH_2b: expected the source $2c0-byte file-menu tilemap/attribute upload.");
    }
    internal void DrawFileDecorations(object owner)
    {
        _decorations??=MenuPresentationDatabase.Shared.FileOam("decorations")
            .Select(part=>new IntroOamPart(part.Y,part.X,part.Tile,part.Attributes)).ToArray();
        SetObjectOam(owner,_decorations,0,0,0);
    }
    private void ReleaseSecretGraphics(){_secretFont?.Dispose();_secretFont=null;}
}
