using Vineeeeea.Domain.Utils;

namespace Vineeeeea.App.Utils;

public static class EffectLabels
{
    public static string ForEffect(string effectType) => effectType switch
    {
        EffectCatalog.Transform => "Posición y escala",
        EffectCatalog.Crop => "Recortar encuadre",
        EffectCatalog.Opacity => "Opacidad",
        EffectCatalog.Brightness => "Brillo",
        EffectCatalog.Contrast => "Contraste",
        EffectCatalog.Saturation => "Saturación",
        EffectCatalog.Temperature => "Temperatura",
        EffectCatalog.Grayscale => "Blanco y negro",
        EffectCatalog.Blur => "Desenfoque",
        EffectCatalog.Sharpen => "Nitidez",
        EffectCatalog.Vignette => "Viñeta",
        EffectCatalog.Rotate => "Girar",
        EffectCatalog.Flip => "Voltear (espejo)",
        EffectCatalog.Reverse => "Reversa",
        EffectCatalog.Fill => "Fondo desenfocado",
        EffectCatalog.Grain => "Grano de película",
        EffectCatalog.Pixelate => "Pixelado",
        EffectCatalog.ChromaKey => "Pantalla verde",
        EffectCatalog.Denoise => "Quitar ruido",
        EffectCatalog.Normalize => "Normalizar voz",
        EffectCatalog.Fade => "Animación: aparecer y desaparecer",
        EffectCatalog.Dissolve => "Transición: disolver",
        EffectCatalog.Volume => "Volumen",
        EffectCatalog.AudioFade => "Fundido de audio",
        _ => effectType,
    };

    public static string For(string effectType, string parameter) => (effectType, parameter) switch
    {
        (EffectCatalog.Transform, EffectCatalog.PositionX) => "Horizontal",
        (EffectCatalog.Transform, EffectCatalog.PositionY) => "Vertical",
        (EffectCatalog.Transform, EffectCatalog.Scale) => "Escala",
        (EffectCatalog.Crop, EffectCatalog.Left) => "Izquierda",
        (EffectCatalog.Crop, EffectCatalog.Top) => "Arriba",
        (EffectCatalog.Crop, EffectCatalog.Right) => "Derecha",
        (EffectCatalog.Crop, EffectCatalog.Bottom) => "Abajo",
        (EffectCatalog.Temperature, _) => "Kelvin (bajo = cálido, alto = frío)",
        (_, EffectCatalog.InSeconds) => "Entrada (segundos)",
        (_, EffectCatalog.OutSeconds) => "Salida (segundos)",
        (_, EffectCatalog.Seconds) => "Duración (segundos)",
        (EffectCatalog.Rotate, _) => "Grados",
        (EffectCatalog.Flip, EffectCatalog.Horizontal) => "Horizontal (0 no, 1 sí)",
        (EffectCatalog.Flip, EffectCatalog.Vertical) => "Vertical (0 no, 1 sí)",
        (EffectCatalog.Reverse, EffectCatalog.Amount) => "Al revés (0 no, 1 sí)",
        (EffectCatalog.Fill, EffectCatalog.Amount) => "Cuánto desenfoque",
        (EffectCatalog.Grain, EffectCatalog.Amount) => "Cuánto grano",
        (EffectCatalog.Pixelate, EffectCatalog.Amount) => "Tamaño del bloque",
        (EffectCatalog.ChromaKey, EffectCatalog.Similarity) => "Cuánto verde quita",
        (EffectCatalog.ChromaKey, EffectCatalog.Blend) => "Suavizar bordes",
        (EffectCatalog.Normalize, EffectCatalog.Target) => "Volumen objetivo (LUFS)",
        _ => "Intensidad",
    };
}
