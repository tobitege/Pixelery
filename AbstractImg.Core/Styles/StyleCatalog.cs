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
        new NeonGlowStyle(),

        new OpArtStyle(),
        new RotatingSnakesStyle(),
        new CafeWallStyle(),
        new ScintillatingGridStyle(),
        new FraserSpiralStyle(),
        new ZoellnerStyle(),
        new OuchiStyle(),
        new KanizsaStyle(),
        new ImpossibleFiguresStyle()
    ];

    public static ArtStyle? Find(string id) => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
}
