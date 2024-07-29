using System.Globalization;
using System.Text.RegularExpressions;
using LVK.Core.App.Console;
using LVK.Core.App.Console.Parameters;
using Microsoft.Extensions.Configuration;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using SixLabors.ImageSharp.Processing;

namespace Watermarker;

public class MainEntrypoint : IMainEntrypoint
{
    private readonly IConfiguration _configuration;

    [PositionalArguments]
    public List<string> Filenames { get; } = new();

    public MainEntrypoint(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<int> RunAsync(CancellationToken stoppingToken)
    {
        if (Filenames.Count == 0)
        {
            Console.Error.WriteLine("error: no filenames specified");
            return 1;
        }

        foreach (string filename in Filenames)
        {
            await ProcessImage(filename, stoppingToken);
        }

        return 0;
    }

    private async Task ProcessImage(string inputFilename, CancellationToken cancellationToken)
    {
        Console.WriteLine($"processing {inputFilename}");
        var options = new DecoderOptions
        {
            MaxFrames = 1,
            SkipMetadata = false,
        };

        var image  = await SixLabors.ImageSharp.Image.LoadAsync(options, inputFilename, cancellationToken);
        Console.WriteLine($"  {image.Width} x {image.Height}");

        string outputFilename = Path.ChangeExtension(inputFilename, ".jpg");
        image.Metadata.ExifProfile = image.Frames[0].Metadata.ExifProfile;
        if ((TryGetReferenceValue(image, ExifTag.Copyright)?.Contains("Lasse") ?? false) && (TryGetReferenceValue(image, ExifTag.Copyright)?.Contains("Karlsen") ?? false))
        {
            image.Metadata.ExifProfile!.SetValue(ExifTag.Copyright, $"Copyright \x00A9 Lasse Vågsæther Karlsen {DateTime.Today.Year}, All rights reserved");
            image.Metadata.ExifProfile!.SetValue(ExifTag.OwnerName, "Lasse Vågsæther Karlsen");
            image.Metadata.ExifProfile!.SetValue(ExifTag.Artist, "Lasse Vågsæther Karlsen");
            image.Metadata.ExifProfile!.SetValue(ExifTag.Software, "LVK Watermarker");

            image.Metadata.ExifProfile!.RemoveValue(ExifTag.HostComputer);
            image.Metadata.ExifProfile!.RemoveValue(ExifTag.SerialNumber);
            image.Metadata.ExifProfile!.RemoveValue(ExifTag.LensSerialNumber);
        }

        ApplyMetadataBanner(image);
        
        await image.SaveAsJpegAsync(outputFilename, new JpegEncoder {
            Quality = 85,
            ColorType = JpegEncodingColor.Rgb,
            SkipMetadata = false,
        }, cancellationToken);

        File.SetCreationTimeUtc(outputFilename, File.GetCreationTimeUtc(inputFilename));
        Console.WriteLine("   Done!");

        File.Delete(inputFilename);
    }

    private void ApplyMetadataBanner(Image image)
    {
        if (!SystemFonts.TryGet("Arial", out var fontFamily))
            throw new InvalidOperationException("Unable to locate font");
        
        var bannerHeight = image.Height * 5 / 100;
        var bannerMargin = bannerHeight / 6;
        var bannerTop = image.Height - bannerHeight;
        var bannerArea = new Rectangle(0, bannerTop, image.Width, bannerHeight);
        var fontSize = bannerHeight / 3f;

        var line1Top = bannerTop + bannerMargin / 2f;
        var line2Top = line1Top + fontSize + bannerMargin;

        var font = fontFamily.CreateFont(fontSize, FontStyle.Regular);

        string leftLine1 = GetCopyright(image);
        string leftLine2 = GetCameraAndLensAndExposure(image);

        string rightLine1 = GetLocation(image);
        string rightLine2 = GetDateTime(image); 

        image.Mutate(img =>
        {
            img.GaussianBlur(50f, bannerArea);
            img.Brightness(0.5f, bannerArea);

            img.DrawLine(Color.White, 1f, new PointF(0, bannerTop), new PointF(image.Width, bannerTop));

            if (!string.IsNullOrWhiteSpace(leftLine1))
            {
                Console.WriteLine("  1< " + leftLine1);
                img.DrawText(leftLine1, font, Color.White, new PointF(bannerMargin, line1Top));
            }

            if (!string.IsNullOrWhiteSpace(leftLine2))
            {
                Console.WriteLine("  2< " + leftLine2);
                img.DrawText(leftLine2, font, Color.White, new PointF(bannerMargin, line2Top));
            }

            if (!string.IsNullOrWhiteSpace(rightLine1))
            {
                Console.WriteLine("  1> " + rightLine1);
                var measurement = TextMeasurer.MeasureSize(rightLine1, new TextOptions(font));
                img.DrawText(rightLine1, font, Color.White, new PointF(image.Width - measurement.Width - bannerMargin, line1Top));
            }

            if (!string.IsNullOrWhiteSpace(rightLine2))
            {
                Console.WriteLine("  2> " + rightLine2);
                var measurement = TextMeasurer.MeasureSize(rightLine2, new TextOptions(font));
                img.DrawText(rightLine2, font, Color.White, new PointF(image.Width - measurement.Width - bannerMargin, line2Top));
            }
        });
    }

    private string GetDateTime(Image image)
    {
        string? value = TryGetReferenceValue(image, ExifTag.DateTimeOriginal);
        if (value is null)
            return "";

        var ma = Regex.Match(value, @"^(?<yyyy>\d{4}):(?<mm>\d{2}):(?<dd>\d{2}) (?<t>\d{2}:\d{2}:\d{2})$");
        if (!ma.Success)
            return value;

        return $"{ma.Groups["yyyy"]}/{ma.Groups["mm"]}/{ma.Groups["dd"]} {ma.Groups["t"]}";
    }

    private string GetLocation(Image image)
    {
        Rational[]? latitudeValues = TryGetReferenceValue(image, ExifTag.GPSLatitude);
        Rational[]? longitudeValues = TryGetReferenceValue(image, ExifTag.GPSLongitude);

        if (latitudeValues is null || longitudeValues is null)
            return "";

        FormattableString latitude = $"{Math.Abs(latitudeValues[0].ToDouble()):0}\x00B0{latitudeValues[1].ToDouble():00}'{latitudeValues[2].ToDouble():00.0}\" {(latitudeValues[0].ToDouble() > 0 ? "N" : "S")}";
        FormattableString longitude = $"{Math.Abs(longitudeValues[0].ToDouble()):0}\x00B0{longitudeValues[1].ToDouble():00}'{longitudeValues[2].ToDouble():00.0}\" {(latitudeValues[0].ToDouble() > 0 ? "E" : "W")}";
        return latitude.ToString(CultureInfo.InvariantCulture) + ", " + longitude.ToString(CultureInfo.InvariantCulture);
    }

    private string GetCameraAndLensAndExposure(Image image)
    {
        string cameraAndLens = GetCameraAndLens(image);

        Rational? focalLength = TryGetValue(image, ExifTag.FocalLength);
        Rational? exposureTime = TryGetValue(image, ExifTag.ExposureTime);
        Rational? aperture = TryGetValue(image, ExifTag.ApertureValue);
        uint? iso1 = TryGetValue(image, ExifTag.ISOSpeed);
        uint? iso2 = TryGetValue(image, ExifTag.RecommendedExposureIndex);

        var iso = iso1 ?? iso2;

        var parts = new List<string?>
        {
            focalLength.HasValue ? focalLength.Value.ToDouble().ToString("0", CultureInfo.InvariantCulture) + "mm" : null,
            exposureTime.HasValue ? exposureTime + "s" : null,
            aperture.HasValue ? "f/" + aperture.Value.ToDouble().ToString("0.0", CultureInfo.InvariantCulture) : null,
            iso.HasValue ? "ISO " + iso.Value : null,
        };

        var exposure = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

        parts =
        [
            cameraAndLens,
            exposure
        ];

        return string.Join(" @ ", parts);
    }

    private string GetCameraAndLens(Image image)
    {
        string? camera = Replace(CoalesceMakeModel(image, ExifTag.Make, ExifTag.Model));
        string? lens = Replace(CoalesceMakeModel(image, ExifTag.LensMake, ExifTag.LensModel));
        
        string leftLine2 = camera != null && lens != null
            ? camera + " + " + lens
            : camera ?? "";
        return leftLine2;
    }

    private string GetCopyright(Image image)
    {
        return TryGetReferenceValue(image, ExifTag.Copyright) ?? $"Copyright {DateTime.Today.Year} Lasse Vågsæther Karlsen";
    }

    private string? CoalesceMakeModel(Image image, ExifTag<string> makeTag, ExifTag<string> modelTag)
    {
        string? name = null;
        string? make = TryGetReferenceValue(image, makeTag);
        string? model = TryGetReferenceValue(image, modelTag);
        if (make != null && model != null && model.ToUpperInvariant().Contains(make.ToUpperInvariant().Trim()))
            name = model;
        else if (make != null && model != null)
            name = make.Trim() + " " + model;
        else
            name = model;

        return name;
    }

    private TValueType? TryGetValue<TValueType>(Image image, ExifTag<TValueType> tag)
        where TValueType : struct
    {
        image.Metadata.ExifProfile!.TryGetValue(tag, out var value);
        return value?.Value;
    }

    private TValueType? TryGetReferenceValue<TValueType>(Image image, ExifTag<TValueType> tag)
        where TValueType : class
    {
        image.Metadata.ExifProfile!.TryGetValue(tag, out var value);
        return value?.Value;
    }

    private string? Replace(string? input)
    {
        if (input is null)
            return null;
        
        return _configuration["Replacements:" + input] ?? input;
    }
}