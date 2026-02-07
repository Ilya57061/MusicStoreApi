using MusicStore.Application.Abstractions;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Linq;

namespace MusicStore.Infrastructure.Covers;

public sealed class ImageSharpCoverGenerator : ICoverGenerator
{
    private const int Size = 512;

    public byte[] GenerateCoverPng(ulong seed, string title, string artist, bool isSingle)
    {
        var rng = CreateRng(seed);
        var palette = CreatePalette(rng);

        using var img = new Image<Rgba32>(Size, Size);

        img.Mutate(ctx =>
        {
            DrawBackground(ctx, rng, palette);
            DrawPattern(ctx, rng, palette);

            var panel = GetPanelRect();
            DrawPanel(ctx, panel);

            var fonts = CreateFonts();
            DrawTitle(ctx, fonts.Title, title, GetTitleRect());
            DrawArtist(ctx, fonts.Artist, artist, GetArtistRect());
            DrawTag(ctx, fonts.Tag, isSingle);
        });

        return SaveAsPng(img);
    }

    private static Random CreateRng(ulong seed)
    {
        return new Random(unchecked((int)(seed ^ (seed >> 32))));
    }
    private static Palette CreatePalette(Random rng)
    {
        var bg1 = RandomColor(rng);
        var bg2 = RandomColor(rng);
        var accent = RandomColor(rng);

        return new Palette(bg1, bg2, accent);
    }

    private static Color RandomColor(Random rng)
    {
        return Color.FromRgb(
               (byte)rng.Next(20, 236),
               (byte)rng.Next(20, 236),
               (byte)rng.Next(20, 236));
    }

    private static RectangleF GetPanelRect()
    {
        return new RectangleF(32, 300, Size - 64, 180);
    }
    private static RectangleF GetTitleRect()
    {
        return new RectangleF(48, 320, Size - 96, 90);
    }

    private static RectangleF GetArtistRect()
    {
        return new RectangleF(48, 410, Size - 96, 40);
    }

    private static void DrawBackground(IImageProcessingContext ctx, Random rng, Palette palette)
    {
        ctx.Fill(palette.Bg1);
        DrawLayeredRectangles(ctx, rng, palette);
    }

    private static void DrawLayeredRectangles(IImageProcessingContext ctx, Random rng, Palette palette)
    {
        for (var i = 0; i < 12; i++)
        {
            var alpha = 0.05f + (float)rng.NextDouble() * 0.12f;
            var c = palette.Bg2.WithAlpha(alpha);

            var x = rng.Next(-Size / 2, Size);
            var y = rng.Next(-Size / 2, Size);
            var w = rng.Next(Size / 3, Size);
            var h = rng.Next(Size / 3, Size);

            ctx.Fill(c, new RectangleF(x, y, w, h));
        }
    }

    private static void DrawPattern(IImageProcessingContext ctx, Random rng, Palette palette)
    {
        for (var i = 0; i < 30; i++)
        {
            var r = rng.Next(10, 120);
            var x = rng.Next(-50, Size + 50);
            var y = rng.Next(-50, Size + 50);

            var a = 0.03f + (float)rng.NextDouble() * 0.08f;
            ctx.Fill(palette.Accent.WithAlpha(a), new EllipsePolygon(x, y, r));
        }
    }

    private static void DrawPanel(IImageProcessingContext ctx, RectangleF panel)
    {
        ctx.Fill(Color.Black.WithAlpha(0.35f), panel);
        ctx.Draw(Color.White.WithAlpha(0.18f), 2, panel);
    }

    private static Fonts CreateFonts()
    {
        var baseDir = AppContext.BaseDirectory;

        var regularPath = System.IO.Path.Combine(baseDir, "Resources", "Fonts", "InterDisplay-Regular.ttf");
        var boldPath = System.IO.Path.Combine(baseDir, "Resources", "Fonts", "Inter-Bold.ttf");

        if (File.Exists(regularPath) && File.Exists(boldPath))
        {
            var fc = new FontCollection();
            var regularFamily = fc.Add(regularPath);
            var boldFamily = fc.Add(boldPath);

            return new Fonts(
                Title: boldFamily.CreateFont(38, FontStyle.Bold),
                Artist: regularFamily.CreateFont(24, FontStyle.Regular),
                Tag: regularFamily.CreateFont(18, FontStyle.Italic));
        }

        if (SystemFonts.Families.Any())
        {
            var family = SystemFonts.Families.First();
            return new Fonts(
                Title: family.CreateFont(38, FontStyle.Bold),
                Artist: family.CreateFont(24, FontStyle.Regular),
                Tag: family.CreateFont(18, FontStyle.Italic));
        }

        throw new InvalidOperationException(
            "No fonts available. Add TTF fonts to MusicStore.Infrastructure/Resources/Fonts and set CopyToOutputDirectory.");
    }

    private static void DrawTitle(IImageProcessingContext ctx, Font font, string title, RectangleF rect)
    {
        ctx.DrawText(
            new RichTextOptions(font)
            {
                Origin = new PointF(rect.Left, rect.Top),
                WrappingLength = rect.Width
            },
            title,
            Color.White);
    }

    private static void DrawArtist(IImageProcessingContext ctx, Font font, string artist, RectangleF rect)
    {
        ctx.DrawText(
            new RichTextOptions(font)
            {
                Origin = new PointF(rect.Left, rect.Top),
                WrappingLength = rect.Width
            },
            artist,
            Color.White.WithAlpha(0.95f));
    }

    private static void DrawTag(IImageProcessingContext ctx, Font font, bool isSingle)
    {
        var tag = isSingle ? "Single" : "Album";

        ctx.DrawText(
            new RichTextOptions(font)
            {
                Origin = new PointF(48, 462)
            },
            tag,
            Color.White.WithAlpha(0.75f));
    }

    private static byte[] SaveAsPng(Image<Rgba32> img)
    {
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        return ms.ToArray();
    }

    private readonly record struct Palette(Color Bg1, Color Bg2, Color Accent);

    private readonly record struct Fonts(Font Title, Font Artist, Font Tag);
}
