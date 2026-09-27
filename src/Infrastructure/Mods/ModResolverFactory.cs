namespace oracleofages;

internal static class ModResolverFactory
{
    internal static ModAssetResolver Create(
        bool modsEnabled,
        string modsDirectory,
        out ModCatalog catalog)
    {
        catalog = modsEnabled
            ? ModCatalog.Discover(modsDirectory)
            : ModCatalog.Empty;
        return new ModAssetResolver(catalog.Mods);
    }
}
