using System.Collections.Concurrent;

namespace Oracles;

// specialObjectNextAnimationFrame and loadLinkAndCompanionAnimationFrame_body.
internal sealed class CompanionNativeAnimationDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<CompanionNativeAnimationDatabase>> Packs=new(StringComparer.Ordinal);
    internal static CompanionNativeAnimationDatabase For(string root)=>Packs.GetOrAdd(root,
        static pack=>new Lazy<CompanionNativeAnimationDatabase>(()=>new(pack))).Value;
    private readonly OracleGame _game;
    private readonly Dictionary<(int Id,int Animation,int Frame),(int Cursor,int Graphics)> _frames=[];
    private readonly Dictionary<(int Id,int Graphics),int> _graphics=[];
    private readonly Dictionary<(int Id,int Animation,int Cursor,int Graphics),int> _nativeFrames=[];

    private CompanionNativeAnimationDatabase(string root)
    {
        _game=OracleAssetPack.GameFor(root);
        foreach(var row in GeneratedTable.Load(root+"metadata/companion_native_graphics.tsv",
            new("native companion graphics",GeneratedTableKeySemantics.Unique,
                ["id","gfx","oam-address","source"],["id","gfx"],headerRequired:true)).Rows)
        {
            int id=row.HexByte(0),graphics=row.HexByte(1),address=row.HexWord(2);
            if(id is < 0x0b or > 0x0d || address>0x3fff)throw row.Invalid(0,"native animal graphics and bank-relative OAM address");
            _graphics.Add((id,graphics),address);_=row.RequiredString(3);
        }
        foreach(var row in GeneratedTable.Load(root+"metadata/companion_native_animation.tsv",
            new("native companion animation",GeneratedTableKeySemantics.Unique,
                ["id","animation","frame","cursor","gfx","source"],["id","animation","frame"],headerRequired:true)).Rows)
        {
            int id=row.HexByte(0),animation=row.HexByte(1),frame=row.Decimal(2,0,255),cursor=row.HexWord(3),graphics=row.HexByte(4);
            if(cursor is < 0x4000 or > 0x7fff || !_graphics.ContainsKey((id,graphics)))
                throw row.Invalid(3,"native ROM cursor and existing graphics row");
            _frames.Add((id,animation,frame),(cursor,graphics));_=row.RequiredString(5);
            var key=(id,animation,cursor,graphics);
            // Partial uploads can revisit one native cursor with different retained VRAM.
            if(!_nativeFrames.TryAdd(key,frame))_nativeFrames[key]=-1;
        }
    }

    internal void PublishAnimation(OracleRuntimeState memory,int id,int animation,int frame,int counter,int parameter)
    {
        RequireGame(memory);
        if(memory.ReadWramByte(0xd101)!=id)
            throw new ArgumentException($"specialObjectNextAnimationFrame: SPECIALOBJECT ${id:x2} cannot publish over native ${memory.ReadWramByte(0xd101):x2}.",nameof(id));
        if(!_frames.TryGetValue((id,animation,frame),out var native))
            throw new NotSupportedException($"specialObjectNextAnimationFrame: SPECIALOBJECT ${id:x2} animation${animation:x2}/frame${frame:x2} lacks native metadata.");
        memory.SetWramByte(0xd120,(byte)counter);memory.SetWramByte(0xd121,(byte)parameter);
        memory.SetWramByte(0xd122,(byte)native.Cursor);memory.SetWramByte(0xd123,(byte)(native.Cursor>>8));
        memory.SetWramByte(0xd130,(byte)animation);memory.SetWramByte(0xd131,(byte)native.Graphics);
    }

    internal void PublishGraphics(OracleRuntimeState memory)
    {
        RequireGame(memory);
        int id=memory.ReadWramByte(0xd101),frame=memory.ReadWramByte(0xd131);
        if(id is < 0x0b or > 0x0d || (memory.ReadWramByte(0xd11a)&0x80)==0 || frame==memory.ReadWramByte(0xd132))return;
        if(!_graphics.TryGetValue((id,frame),out int address))
            throw new NotSupportedException($"getSpecialObjectGraphicsFrame: SPECIALOBJECT ${id:x2} frame${frame:x2} lacks native metadata.");
        memory.SetWramByte(0xd132,(byte)frame);
        memory.SetWramByte(0xd11e,(byte)address);memory.SetWramByte(0xd11f,(byte)(address>>8));
    }

    internal bool CanRestorePresentation(OracleRuntimeState memory)
    {
        if(!TryResolveFrame(memory,out _))return false;
        int id=memory.ReadWramByte(0xd101),gfx=memory.ReadWramByte(0xd131);
        return (memory.ReadWramByte(0xd11a)&0x80)!=0 && memory.ReadWramByte(0xd132)==gfx &&
            _graphics.TryGetValue((id,gfx),out int oam) &&
            (memory.ReadWramByte(0xd11e)|memory.ReadWramByte(0xd11f)<<8)==oam;
    }

    internal bool TryRestoreAnimation(OracleRuntimeState memory,EnemyAnimationPlayer animation)
    {
        if(!TryResolveFrame(memory,out int frame))return false;
        animation.SetAnimation(memory.ReadWramByte(0xd130));
        animation.RestoreFrame(frame,memory.ReadWramByte(0xd120));
        animation.SetParameter(memory.ReadWramByte(0xd121));
        return true;
    }

    private bool TryResolveFrame(OracleRuntimeState memory,out int frame)
    {
        RequireGame(memory);
        return _nativeFrames.TryGetValue((memory.ReadWramByte(0xd101),memory.ReadWramByte(0xd130),
            memory.ReadWramByte(0xd122)|memory.ReadWramByte(0xd123)<<8,memory.ReadWramByte(0xd131)),out frame) && frame>=0;
    }

    private void RequireGame(OracleRuntimeState memory)
    {
        if(memory.Game!=_game)throw new ArgumentException("Native companion animation requires the selected game pack.",nameof(memory));
    }
}
