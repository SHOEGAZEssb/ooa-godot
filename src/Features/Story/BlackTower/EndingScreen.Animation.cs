using Godot;

namespace oracleofages;

public partial class EndingScreen
{
    private OracleAnimationData? _nativeAnimations;
    private long? _nativeAnimationStart;
    private int _nativeAnimationSignature;
    internal void BeginNativeAnimation(long tick)
    {_nativeAnimations??=new();_nativeAnimationStart=tick;_nativeAnimationSignature=0;}
    internal void UpdateNativeAnimation(long tick)
    {
        if(_nativeAnimationStart is not long start||_tiles is null)return;
        var headers=_nativeAnimations!.GetActiveHeadersAfterDirectLoad(1,tick-start+1);
        int signature=17;foreach(int header in headers)signature=unchecked(signature*31+header);
        if(signature==_nativeAnimationSignature)return;_nativeAnimationSignature=signature;
        for(int tile=0;tile<256;tile++)if(_nativeAnimations.TryGetOverride(headers,tile,out Image source,out int sourceTile))
            _tiles.MapTile(source,sourceTile,(tile+0x80)&255,1);
        RebuildBackground();
    }
}
