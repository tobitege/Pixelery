namespace AbstractImg.Core.Styles;

/// <summary>All available styles in display order.</summary>
public static class StyleCatalog
{
    public static IReadOnlyList<ArtStyle> All { get; } =
    [
        new MeshGradientStyle(),
        new AuroraStyle(),
        new GradientFieldStyle(),
        new BokehStyle(),
        new NebulaStyle(),

        new BauhausStyle(),
        new TruchetStyle(),
        new LowPolyStyle(),
        new SubdivisionStyle(),
        new CirclePackingStyle(),
        new MemphisStyle(),
        new IsometricStyle(),
        new HalftoneStyle(),
        new KaleidoscopeStyle(),
        new OpArtStyle(),

        new FlowFieldStyle(),
        new MarbleStyle(),
        new TopographicStyle(),
        new WatercolorStyle(),
        new PaperLayersStyle(),
        new RidgelinesStyle(),
        new StainedGlassStyle(),

        new PlasmaStyle(),
        new FractalStyle(),
        new HarmonographStyle(),

        new SynthwaveStyle(),
        new GlitchStyle(),
        new PixelMosaicStyle(),
        new NeonGlowStyle()
    ];

    public static ArtStyle? Find(string id) => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
}
