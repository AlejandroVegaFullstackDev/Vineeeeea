using Vineeeeea.Application.Dtos;
using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;
using Vineeeeea.Infrastructure.Dtos;
using Vineeeeea.Infrastructure.Utils;
using static Vineeeeea.Infrastructure.Utils.FfmpegExpressions;

namespace Vineeeeea.Infrastructure.Implements;

internal sealed record FfmpegRenderGraph(IReadOnlyList<string> InputArguments, string FilterComplex);

/// <summary>
/// Turns a render plan into one filter graph: a black canvas with each video layer overlaid in order, and a silent
/// bed with each audio layer mixed in. Preview, audio playback and export all use it, so what plays is what exports.
/// </summary>
internal static class FfmpegGraphBuilder
{
    public const string VideoOutput = "[vout]";
    public const string AudioOutput = "[aout]";

    private const double MinAtempoFactor = 0.5;
    private const double MinNoiseFloorDb = -50;
    private const double MaxNoiseFloorDb = -20;
    private const double MinFillSigma = 6;
    private const double MaxFillSigma = 40;
    private const double MinPixelBlock = 6;
    private const double MaxPixelBlock = 48;
    private const double MaxGrainStrength = 40;
    private const int MillisecondsPerSecond = 1000;
    private const int TextLineSpacingDivisor = 5;
    private const int TextBoxPaddingDivisor = 3;
    private const double TextBoxOpacity = 0.55;

    public static FfmpegRenderGraph Build(RenderPlan plan, bool includeVideo, bool includeAudio, TextRenderingContext? textRendering = null)
    {
        var inputs = new List<string>();
        var filters = new List<string>();
        var audioLabels = new List<string> { "[abase]" };
        var duration = Seconds(plan.DurationUs);
        var videoLayerCount = 0;
        var inputCount = 0;

        if (includeVideo)
        {
            filters.Add(string.Concat(
                "color=c=black:s=", Integer(plan.OutputSize.Width), "x", Integer(plan.OutputSize.Height),
                ":r=", Rate(plan.FrameRate), ":d=", duration, "[base0]"));
        }

        if (includeAudio)
        {
            filters.Add(string.Concat("anullsrc=r=", Integer(plan.AudioSampleRate), ":cl=stereo,atrim=duration=", duration, "[abase]"));
        }

        foreach (var layer in plan.Layers)
        {
            var usesVideo = includeVideo && layer.ProducesVideo;
            var usesAudio = includeAudio && layer.ProducesAudio;
            if (!usesVideo && !usesAudio)
            {
                continue;
            }

            if (layer.Text is { } text)
            {
                var source = TextSource(plan, layer, text, textRendering ?? throw new BusinessRuleException("Falta la configuración para dibujar textos."));
                videoLayerCount += AddVideoLayer(filters, plan, layer, source, videoLayerCount);
                continue;
            }

            if (!File.Exists(layer.MediaPath))
            {
                throw new NotFoundException($"Falta el medio {Path.GetFileName(layer.MediaPath)}.");
            }

            var input = inputCount++;
            inputs.AddRange(layer.IsStillImage
                ? ["-loop", "1", "-framerate", Rate(plan.FrameRate), "-t", Seconds(layer.SourceDurationUs)]
                : ["-ss", Seconds(layer.SourceStartUs), "-t", Seconds(layer.SourceDurationUs)]);
            inputs.AddRange(["-i", FfmpegConventions.LocalFileProtocol + layer.MediaPath]);

            if (usesVideo)
            {
                var source = string.Concat(
                    "[", Integer(input), ":v]setpts=(PTS-STARTPTS)/", Number(layer.Speed), ",fps=", Rate(plan.FrameRate));
                videoLayerCount += AddVideoLayer(filters, plan, layer, source, videoLayerCount);
            }

            if (usesAudio)
            {
                var label = string.Concat("[a", Integer(input), "]");
                filters.Add(AudioLayerChain(plan, layer, input, label));
                audioLabels.Add(label);
            }
        }

        if (includeVideo)
        {
            filters.Add(string.Concat("[base", Integer(videoLayerCount), "]null", VideoOutput));
        }

        if (includeAudio)
        {
            filters.Add(audioLabels.Count == 1
                ? string.Concat("[abase]anull", AudioOutput)
                : string.Concat(string.Concat(audioLabels), "amix=inputs=", Integer(audioLabels.Count), ":duration=first:normalize=0", AudioOutput));
        }

        return new FfmpegRenderGraph(inputs, string.Join(';', filters));
    }

    private static string TextSource(RenderPlan plan, RenderLayer layer, TextStyle text, TextRenderingContext textRendering)
    {
        var fontPath = text.IsBold ? textRendering.BoldFontPath : textRendering.FontPath;
        if (!File.Exists(fontPath))
        {
            throw new NotFoundException($"No se encontró la tipografía {Path.GetFileName(fontPath)}.");
        }

        var fontSize = Math.Max(1, (int)Math.Round(plan.OutputSize.Height * text.Size));
        var source = string.Concat(
            "color=c=black:s=", Integer(plan.OutputSize.Width), "x", Integer(plan.OutputSize.Height),
            ":r=", Rate(plan.FrameRate), ":d=", Seconds(plan.FrameRate.FramesToMicroseconds(layer.DurationFrames)),
            ",format=rgba,colorchannelmixer=aa=0",
            ",drawtext=fontfile=", FilterPath(fontPath),
            ":textfile=", FilterPath(textRendering.TextFileFor(text.Content)),
            ":expansion=none",
            ":fontsize=", Integer(fontSize),
            ":fontcolor=0x", text.Color[1..],
            ":line_spacing=", Integer(fontSize / TextLineSpacingDivisor),
            ":x=(w-text_w)/2:y=(h-text_h)/2");

        return text.HasBackground
            ? string.Concat(source, ":box=1:boxcolor=black@", Number(TextBoxOpacity), ":boxborderw=", Integer(fontSize / TextBoxPaddingDivisor))
            : source;
    }

    /// <summary>
    /// Adds the filters that turn a layer into its final overlaid picture and returns how many base layers it used
    /// (one normally, two when the blurred-background fill draws a cover copy behind the sharp picture).
    /// </summary>
    private static int AddVideoLayer(List<string> filters, RenderPlan plan, RenderLayer layer, string source, int videoLayer)
    {
        if (FindEffect(layer, EffectCatalog.Fill) is { } fill)
        {
            // split hands the clip to two chains; each label feeds a null so the chain starts "[label]null,filter…"
            // (a bare "[label],filter" would read as an empty filter).
            filters.Add(string.Concat(source, ",split=2[fillbg", Integer(videoLayer), "][fillfg", Integer(videoLayer), "]"));
            filters.Add(BlurBackgroundChain(plan, layer, string.Concat("[fillbg", Integer(videoLayer), "]null"), fill, videoLayer));
            filters.Add(BackgroundOverlay(plan, layer, videoLayer));
            AddSharpLayer(filters, plan, layer, string.Concat("[fillfg", Integer(videoLayer), "]null"), videoLayer + 1);
            return 2;
        }

        AddSharpLayer(filters, plan, layer, source, videoLayer);
        return 1;
    }

    private static void AddSharpLayer(List<string> filters, RenderPlan plan, RenderLayer layer, string source, int videoLayer)
    {
        var transform = FindEffect(layer, EffectCatalog.Transform);
        if (transform is null || !transform.Keyframes.ContainsKey(EffectCatalog.Scale))
        {
            filters.Add(VideoLayerChain(plan, layer, source, videoLayer, kenBurnsBase: null));
            filters.Add(Overlay(plan, layer, videoLayer, panInChain: false));
            return;
        }

        var baseScale = KenBurnsBaseScale(transform);
        filters.Add(VideoLayerChain(plan, layer, source, videoLayer, baseScale));
        filters.AddRange(KenBurnsFilters(plan, layer, videoLayer, transform, baseScale));
        filters.Add(Overlay(plan, layer, videoLayer, panInChain: true));
    }

    /// <summary>A blurred copy of the clip scaled to cover the whole canvas, to fill the bars instead of black.</summary>
    private static string BlurBackgroundChain(RenderPlan plan, RenderLayer layer, string source, EffectInstance fill, int videoLayer)
    {
        var width = Integer(plan.OutputSize.Width);
        var height = Integer(plan.OutputSize.Height);
        var sigma = Number(MinFillSigma + (MaxFillSigma - MinFillSigma) * fill.Parameters[EffectCatalog.Amount]);
        return string.Join(',',
            source,
            string.Concat("scale=", width, ":", height, ":force_original_aspect_ratio=increase"),
            string.Concat("crop=", width, ":", height),
            string.Concat("gblur=sigma=", sigma),
            "format=yuv420p",
            string.Concat("setpts=PTS+", Seconds(plan.FrameRate.FramesToMicroseconds(layer.StartFrame)), "/TB[v", Integer(videoLayer), "]"));
    }

    private static string BackgroundOverlay(RenderPlan plan, RenderLayer layer, int videoLayer)
    {
        var start = Seconds(plan.FrameRate.FramesToMicroseconds(layer.StartFrame));
        var end = Seconds(plan.FrameRate.FramesToMicroseconds(layer.StartFrame + layer.DurationFrames));
        return string.Concat(
            "[base", Integer(videoLayer), "][v", Integer(videoLayer), "]overlay=0:0:",
            "eval=frame:eof_action=pass:enable='between(t,", start, ",", end, ")'",
            "[base", Integer(videoLayer + 1), "]");
    }

    private static string VideoLayerChain(RenderPlan plan, RenderLayer layer, string source, int videoLayer, double? kenBurnsBase)
    {
        var width = Integer(plan.OutputSize.Width);
        var height = Integer(plan.OutputSize.Height);
        var transform = FindEffect(layer, EffectCatalog.Transform);
        var scale = Number(kenBurnsBase ?? transform?.Parameters[EffectCatalog.Scale] ?? 1);
        var fit = string.Concat("min(", width, "/iw,", height, "/ih)*", scale);
        var sourceSeconds = string.Concat("(", Seconds(layer.SourceStartUs), "+t*", Number(layer.Speed), ")");

        var chain = new List<string> { source };
        if (ReverseOn(layer))
        {
            chain.Add("reverse");
        }

        chain.AddRange(RotationFilters(layer));
        chain.AddRange(FlipFilters(layer));

        if (FindEffect(layer, EffectCatalog.Crop) is { } crop)
        {
            chain.Add(CropFilter(crop));
        }

        chain.Add(string.Concat("scale=w='max(2,2*trunc(iw*", fit, "/2))':h='max(2,2*trunc(ih*", fit, "/2))'"));
        chain.AddRange(ColorFilters(layer, sourceSeconds));
        chain.Add("format=yuva420p");
        if (FindEffect(layer, EffectCatalog.ChromaKey) is { } chroma)
        {
            chain.Add(ChromaKeyFilter(chroma));
        }

        var opacity = FindEffect(layer, EffectCatalog.Opacity)?.Parameters[EffectCatalog.Amount] ?? 1;
        if (layer.VideoRamp is { } ramp)
        {
            chain.Add(string.Concat(
                "geq=lum='lum(X,Y)':cb='cb(X,Y)':cr='cr(X,Y)':a='alpha(X,Y)*", Number(opacity), "*", RampFactor(ramp, "T"), "'"));
        }
        else if (opacity < 1)
        {
            chain.Add(string.Concat("colorchannelmixer=aa=", Number(opacity)));
        }

        if (kenBurnsBase is null)
        {
            chain.Add(string.Concat("setpts=PTS+", Seconds(plan.FrameRate.FramesToMicroseconds(layer.StartFrame)), "/TB[v", Integer(videoLayer), "]"));
        }
        else
        {
            chain[^1] = string.Concat(chain[^1], "[layer", Integer(videoLayer), "]");
        }

        return string.Join(',', chain);
    }

    /// <summary>
    /// Composites a fitted layer at its smallest scale onto a transparent canvas and magnifies it with zoompan, which
    /// also pans. FFmpeg cannot change the scale filter's size per frame, and zoompan only magnifies, so the base
    /// scale is the smallest keyframe (never above 1) and everything above it is zoom.
    /// </summary>
    private static IEnumerable<string> KenBurnsFilters(RenderPlan plan, RenderLayer layer, int videoLayer, EffectInstance transform, double baseScale)
    {
        var width = Integer(plan.OutputSize.Width);
        var height = Integer(plan.OutputSize.Height);
        var rate = Rate(plan.FrameRate);
        var duration = Seconds(plan.FrameRate.FramesToMicroseconds(layer.DurationFrames));
        var sourceSeconds = string.Concat("(", Seconds(layer.SourceStartUs), "+time*", Number(layer.Speed), ")");
        var zoom = string.Concat("max(1,(", ParameterOverTime(transform, EffectCatalog.Scale, sourceSeconds), ")/", Number(baseScale), ")");
        var panX = ParameterOverTime(transform, EffectCatalog.PositionX, sourceSeconds);
        var panY = ParameterOverTime(transform, EffectCatalog.PositionY, sourceSeconds);

        yield return string.Concat(
            "color=c=black@0.0:s=", width, "x", height, ":r=", rate, ":d=", duration,
            ",format=yuva420p[canvas", Integer(videoLayer), "]");
        yield return string.Concat(
            "[canvas", Integer(videoLayer), "][layer", Integer(videoLayer), "]overlay=",
            "x='(main_w-overlay_w)/2':y='(main_h-overlay_h)/2':eof_action=pass[comp", Integer(videoLayer), "]");
        yield return string.Concat(
            "[comp", Integer(videoLayer), "]zoompan=",
            "z='", zoom, "':x='(iw-iw/zoom)*(1+(", panX, "))/2':y='(ih-ih/zoom)*(1+(", panY, "))/2':",
            "d=1:s=", width, "x", height, ":fps=", rate,
            ",setpts=PTS+", Seconds(plan.FrameRate.FramesToMicroseconds(layer.StartFrame)), "/TB[v", Integer(videoLayer), "]");
    }

    /// <summary>The scale a zoompan layer is built at: the smallest keyframe, kept at or below the fit so zoompan's zoom is at least 1.</summary>
    private static double KenBurnsBaseScale(EffectInstance transform) =>
        Math.Min(1, transform.Keyframes[EffectCatalog.Scale].Min(keyframe => keyframe.Value));

    private static bool ReverseOn(RenderLayer layer) =>
        FindEffect(layer, EffectCatalog.Reverse)?.Parameters[EffectCatalog.Amount] is >= 0.5;

    /// <summary>Quarter-turn rotation applied before scaling, so the letterbox fit uses the turned dimensions.</summary>
    private static IEnumerable<string> RotationFilters(RenderLayer layer)
    {
        var degrees = FindEffect(layer, EffectCatalog.Rotate)?.Parameters[EffectCatalog.Degrees] ?? 0;
        var quarterTurns = ((int)Math.Round(degrees / EffectCatalog.QuarterTurnDegrees) % 4 + 4) % 4;
        for (var turn = 0; turn < quarterTurns; turn++)
        {
            yield return "transpose=clock";
        }
    }

    /// <summary>Mirrors the picture horizontally and/or vertically, before scaling so it reads the turned dimensions.</summary>
    private static IEnumerable<string> FlipFilters(RenderLayer layer)
    {
        if (FindEffect(layer, EffectCatalog.Flip) is not { } flip)
        {
            yield break;
        }

        if (flip.Parameters[EffectCatalog.Horizontal] >= 0.5)
        {
            yield return "hflip";
        }

        if (flip.Parameters[EffectCatalog.Vertical] >= 0.5)
        {
            yield return "vflip";
        }
    }

    /// <summary>Turns a solid green background transparent so the layer below shows through (green-screen keying).</summary>
    private static string ChromaKeyFilter(EffectInstance chroma) => string.Concat(
        "chromakey=color=0x00D800:similarity=",
        Number(chroma.Parameters[EffectCatalog.Similarity]),
        ":blend=",
        Number(chroma.Parameters[EffectCatalog.Blend]));

    private static string CropFilter(EffectInstance crop)
    {
        var (left, top) = (Number(crop.Parameters[EffectCatalog.Left]), Number(crop.Parameters[EffectCatalog.Top]));
        var (right, bottom) = (Number(crop.Parameters[EffectCatalog.Right]), Number(crop.Parameters[EffectCatalog.Bottom]));
        return string.Concat(
            "crop=w='iw*(1-", left, "-", right, ")':h='ih*(1-", top, "-", bottom, ")':x='iw*", left, "':y='ih*", top, "'");
    }

    private static IEnumerable<string> ColorFilters(RenderLayer layer, string sourceSeconds)
    {
        var brightness = FindEffect(layer, EffectCatalog.Brightness);
        var contrast = FindEffect(layer, EffectCatalog.Contrast);
        var saturation = FindEffect(layer, EffectCatalog.Saturation);
        if (brightness is not null || contrast is not null || saturation is not null)
        {
            yield return string.Concat(
                "eq=brightness='", brightness is null ? "0" : ParameterOverTime(brightness, EffectCatalog.Amount, sourceSeconds),
                "':contrast='", contrast is null ? "1" : ParameterOverTime(contrast, EffectCatalog.Amount, sourceSeconds),
                "':saturation='", saturation is null ? "1" : ParameterOverTime(saturation, EffectCatalog.Amount, sourceSeconds),
                "':eval=frame");
        }

        if (FindEffect(layer, EffectCatalog.Temperature)?.Parameters[EffectCatalog.Kelvin] is { } kelvin && kelvin != EffectCatalog.NeutralKelvin)
        {
            yield return string.Concat("colortemperature=temperature=", Number(kelvin));
        }

        if (AmountOf(layer, EffectCatalog.Grayscale) is > 0 and var grayscale)
        {
            yield return string.Concat("hue=s=", Number(1 - grayscale));
        }

        if (AmountOf(layer, EffectCatalog.Blur) is > 0 and var blur)
        {
            yield return string.Concat("gblur=sigma=", Number(blur));
        }

        if (AmountOf(layer, EffectCatalog.Sharpen) is > 0 and var sharpen)
        {
            yield return string.Concat("unsharp=luma_msize_x=5:luma_msize_y=5:luma_amount=", Number(sharpen));
        }

        if (AmountOf(layer, EffectCatalog.Vignette) is > 0 and var vignette)
        {
            yield return string.Concat("vignette=angle=", Number(vignette * Math.PI / 2));
        }

        if (AmountOf(layer, EffectCatalog.Pixelate) is > 0 and var pixelate)
        {
            var block = (int)Math.Round(MinPixelBlock + (MaxPixelBlock - MinPixelBlock) * pixelate);
            yield return string.Concat("pixelize=width=", Integer(block), ":height=", Integer(block));
        }

        if (AmountOf(layer, EffectCatalog.Grain) is > 0 and var grain)
        {
            yield return string.Concat("noise=alls=", Integer((int)Math.Round(grain * MaxGrainStrength)), ":allf=t+u");
        }
    }

    /// <summary>Multiplier from 0 to 1 that rises at the start of the clip and falls at its end, as a function of <paramref name="localTime"/>.</summary>
    private static string RampFactor(FadeRamp ramp, string localTime)
    {
        var clipTime = string.Concat("(", localTime, "+", Seconds(ramp.OffsetUs), ")");
        var factors = new List<string>();
        if (ramp.InUs > 0)
        {
            factors.Add(string.Concat("clip(", clipTime, "/", Seconds(ramp.InUs), ",0,1)"));
        }

        if (ramp.OutUs > 0)
        {
            factors.Add(string.Concat("clip((", Seconds(ramp.ClipDurationUs), "-", clipTime, ")/", Seconds(ramp.OutUs), ",0,1)"));
        }

        return string.Join('*', factors);
    }

    private static string Overlay(RenderPlan plan, RenderLayer layer, int videoLayer, bool panInChain)
    {
        var start = Seconds(plan.FrameRate.FramesToMicroseconds(layer.StartFrame));
        var end = Seconds(plan.FrameRate.FramesToMicroseconds(layer.StartFrame + layer.DurationFrames));
        var sourceSeconds = string.Concat("(", Seconds(layer.SourceStartUs), "+(t-", start, ")*", Number(layer.Speed), ")");
        var transform = FindEffect(layer, EffectCatalog.Transform);
        var x = "0";
        var y = "0";
        if (transform is not null && !panInChain)
        {
            x = ParameterOverTime(transform, EffectCatalog.PositionX, sourceSeconds);
            y = ParameterOverTime(transform, EffectCatalog.PositionY, sourceSeconds);
        }

        return string.Concat(
            "[base", Integer(videoLayer), "][v", Integer(videoLayer), "]overlay=",
            "x='(main_w-overlay_w)/2+(", x, ")*main_w/2':",
            "y='(main_h-overlay_h)/2+(", y, ")*main_h/2':",
            "eval=frame:eof_action=pass:enable='between(t,", start, ",", end, ")'",
            "[base", Integer(videoLayer + 1), "]");
    }

    private static string AudioLayerChain(RenderPlan plan, RenderLayer layer, int input, string label)
    {
        var sourceSeconds = string.Concat("(", Seconds(layer.SourceStartUs), "+t*", Number(layer.Speed), ")");
        var volume = FindEffect(layer, EffectCatalog.Volume);
        var gain = volume is null ? "1" : ParameterOverTime(volume, EffectCatalog.Gain, sourceSeconds);
        if (layer.AudioRamp is { } ramp)
        {
            gain = string.Concat("(", gain, ")*", RampFactor(ramp, "t"));
        }
        var delayMs = Integer(plan.FrameRate.FramesToMicroseconds(layer.StartFrame) * MillisecondsPerSecond / TimeUnits.MicrosecondsPerSecond);

        var chain = new List<string> { string.Concat("[", Integer(input), ":a]asetpts=PTS-STARTPTS") };
        if (ReverseOn(layer))
        {
            chain.Add("areverse");
        }

        chain.AddRange(AtempoSteps(layer.Speed).Select(step => string.Concat("atempo=", Number(step))));
        if (FindEffect(layer, EffectCatalog.Denoise)?.Parameters[EffectCatalog.Amount] is > 0 and var denoise)
        {
            chain.Add(string.Concat("afftdn=nf=", Number(DenoiseNoiseFloorDb(denoise))));
        }

        if (FindEffect(layer, EffectCatalog.Normalize) is { } normalize)
        {
            chain.Add(string.Concat("loudnorm=I=", Number(normalize.Parameters[EffectCatalog.Target]), ":TP=-1.5:LRA=11"));
        }

        chain.Add(string.Concat("aformat=sample_rates=", Integer(plan.AudioSampleRate), ":channel_layouts=stereo"));
        chain.Add(string.Concat("volume='", gain, "':eval=frame"));
        chain.Add(string.Concat("adelay=", delayMs, ":all=1", label));
        return string.Join(',', chain);
    }

    /// <summary>Maps a 0..1 "quitar ruido" amount to an afftdn noise floor in dB: more reduction, lower floor.</summary>
    private static double DenoiseNoiseFloorDb(double amount) => MinNoiseFloorDb + (MaxNoiseFloorDb - MinNoiseFloorDb) * (1 - amount);

    private static IEnumerable<double> AtempoSteps(double speed)
    {
        var remaining = speed;
        while (remaining < MinAtempoFactor)
        {
            yield return MinAtempoFactor;
            remaining /= MinAtempoFactor;
        }

        if (Math.Abs(remaining - DomainLimits.NormalSpeed) > double.Epsilon)
        {
            yield return remaining;
        }
    }

    private static double? AmountOf(RenderLayer layer, string type) => FindEffect(layer, type)?.Parameters[EffectCatalog.Amount];

    private static EffectInstance? FindEffect(RenderLayer layer, string type) =>
        layer.Effects.FirstOrDefault(effect => effect.Type == type);
}
