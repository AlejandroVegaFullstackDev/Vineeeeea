using System.Globalization;
using Vineeeeea.Domain.Models;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Infrastructure.Utils;

/// <summary>
/// Every value placed in a filter graph is a number formatted here, never user text, so a graph cannot be altered by data.
/// </summary>
internal static class FfmpegExpressions
{
    public static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    public static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Seconds(long microseconds) =>
        ((decimal)microseconds / TimeUnits.MicrosecondsPerSecond).ToString(CultureInfo.InvariantCulture);

    /// <summary>A file path quoted for a filter option: forward slashes, an escaped drive colon and escaped quotes.</summary>
    public static string FilterPath(string path) =>
        string.Concat("'", path.Replace('\\', '/').Replace(":", "\\:").Replace("'", "'\\''"), "'");

    public static string Rate(FrameRate frameRate) =>
        string.Concat(Integer(frameRate.Numerator), "/", Integer(frameRate.Denominator));

    /// <summary>Piecewise-linear value of an effect parameter, where <paramref name="sourceSeconds"/> is the source media time.</summary>
    public static string ParameterOverTime(EffectInstance effect, string parameter, string sourceSeconds)
    {
        if (!effect.Keyframes.TryGetValue(parameter, out var keyframes))
        {
            return Number(effect.Parameters[parameter]);
        }

        var expression = Number(keyframes[^1].Value);
        for (var index = keyframes.Count - 1; index >= 1; index--)
        {
            var (previous, next) = (keyframes[index - 1], keyframes[index]);
            var (start, end) = (Seconds(previous.SourceUs), Seconds(next.SourceUs));
            var interpolation = string.Concat(
                Number(previous.Value), "+(", Number(next.Value - previous.Value), ")*(", sourceSeconds, "-", start, ")/(", end, "-", start, ")");
            expression = string.Concat("if(lt(", sourceSeconds, ",", end, "),", interpolation, ",", expression, ")");
        }

        return string.Concat(
            "if(lt(", sourceSeconds, ",", Seconds(keyframes[0].SourceUs), "),", Number(keyframes[0].Value), ",", expression, ")");
    }
}
