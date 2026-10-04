using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateMapPresentationContract()
    {
        Vector2[] crowdedOam = Enumerable.Repeat(new Vector2(-8, 0), 11).ToArray();
        ushort[] selectedScanlines = MapScreen.SelectOamScanlines(crowdedOam);
        FailIf(selectedScanlines[0] != 0 || selectedScanlines.Skip(1).Any(mask => mask != 0xffff),
            "Map OAM did not retain only the first ten source sprites on a scanline, including offscreen X.");
        crowdedOam[0] = new Vector2(0, 8);
        selectedScanlines = MapScreen.SelectOamScanlines(crowdedOam);
        FailIf(selectedScanlines[0] != 0xff00,
            "Map OAM scanline limit discarded non-overlapping rows of its eleventh sprite.");
        FailIf(_scene.MenuFade.Material is not CanvasItemMaterial fadeMaterial ||
            fadeMaterial.BlendMode != CanvasItemMaterial.BlendModeEnum.Add,
            "Menu fade does not add and saturate source palette channels.");
    }
}
