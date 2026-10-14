using Vineeeeea.Domain.Errors;
using Vineeeeea.Domain.Utils;

namespace Vineeeeea.Domain.Models;

public sealed record ProjectSettings
{
    public int Width { get; }
    public int Height { get; }
    public FrameRate FrameRate { get; }
    public int AudioSampleRate { get; }

    public ProjectSettings(int width, int height, FrameRate frameRate, int audioSampleRate)
    {
        if (width <= 0 || height <= 0 || width > DomainLimits.MaxCanvasSide || height > DomainLimits.MaxCanvasSide)
        {
            throw new ValidationException($"El lienzo debe medir entre 1 y {DomainLimits.MaxCanvasSide} px por lado.");
        }

        if (audioSampleRate <= 0 || audioSampleRate > DomainLimits.MaxAudioSampleRate)
        {
            throw new ValidationException("La frecuencia de muestreo de audio no es válida.");
        }

        Width = width;
        Height = height;
        FrameRate = frameRate ?? throw new ValidationException("Falta la frecuencia de fotogramas.");
        AudioSampleRate = audioSampleRate;
    }
}
