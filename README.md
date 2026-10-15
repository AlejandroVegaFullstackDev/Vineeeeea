# Vineeeeea
<img width="864" height="536" alt="logo-emblema" src="https://github.com/user-attachments/assets/4365be0e-07bb-4720-b90d-413af5512ac9" />

Editor de video de escritorio para Windows en C#/.NET 10 y Avalonia 12.

- Multipista: video, fotos, audio y textos, con superposiciones y PNG con transparencia como stickers.
- Edición: mover, recortar, dividir, duplicar, eliminar, velocidad (0,1× a 10×), deshacer y rehacer.
- Arrastrar y soltar desde el Explorador o desde la biblioteca a la línea de tiempo o al visor.
- Formato del lienzo (16:9, 9:16, 1:1, 4:5, 4:3, 21:9) y fotogramas por segundo.
- Efectos con keyframes: posición, escala, recorte, opacidad, brillo, contraste, saturación, temperatura,
  blanco y negro, desenfoque, nitidez, viñeta, volumen; filtros rápidos (Cálido, Frío, Vívido, Vintage, Cine, B/N).
- Animaciones de entrada y salida, fundido de audio y transición «disolver» entre clips.
- Textos y títulos con tamaño, color, fondo y negrita.
- Subtítulos automáticos con whisper.cpp, en local.
- Previsualización con sonido y exportación a MP4 (H.264 + AAC). Visor, sonido y exportación usan el mismo
  grafo de ffmpeg, así que lo que se ve es lo que se exporta.

## Requisitos

- .NET SDK 10.0.402 (fijado en `global.json`)
- `ffmpeg`, `ffprobe` y `ffplay` en el `PATH`
- Para subtítulos: `whisper-cli.exe` y un modelo `ggml-*.bin` de [whisper.cpp](https://github.com/ggml-org/whisper.cpp)
  en `%LOCALAPPDATA%\Vineeeeea\tools` (ver configuración)

## Uso

```bash
dotnet build Vineeeeea.slnx
dotnet test Vineeeeea.slnx
dotnet run --project src/Vineeeeea.Desktop
dotnet run --project src/Vineeeeea.Desktop -- "C:\ruta\proyecto.vine"
```

Atajos: `Espacio` reproducir/pausa, `Ctrl+Z`/`Ctrl+Y` deshacer/rehacer, `Ctrl+B` dividir en el cabezal,
`Supr` eliminar el clip, `Ctrl+T` añadir texto, `Ctrl+S` guardar, `Ctrl+I` importar, `Ctrl+E` exportar.
Arrastra un clip para moverlo y sus bordes para recortarlo; haz clic en la regla para mover el cabezal;
clic derecho en clips, pistas y medios para más opciones (filtros, efectos, subtítulos automáticos).

Las pruebas de integración generan medios con `ffmpeg`. La prueba de subtítulos reales (`Category=Whisper`)
necesita whisper.cpp y una voz de Windows en español; la CI la excluye.

Capturas de la ventana para revisiones de diseño, sin abrirla en pantalla:

```bash
VINEEEEEA_SCREENSHOT_DIR="C:\ruta\capturas" dotnet test tests/Vineeeeea.App.Tests --filter "Category=Screenshots"
```

## Configuración

| Variable | Valor por defecto | Uso |
| --- | --- | --- |
| `VINEEEEEA_FFMPEG_PATH` | `ffmpeg` | Ejecutable de ffmpeg |
| `VINEEEEEA_FFPROBE_PATH` | `ffprobe` | Ejecutable de ffprobe |
| `VINEEEEEA_FFPLAY_PATH` | `ffplay` | Ejecutable de ffplay (sonido al reproducir) |
| `VINEEEEEA_PROBE_TIMEOUT_SECONDS` | `30` | Tiempo máximo para analizar un archivo |
| `VINEEEEEA_CACHE_DIR` | `%LOCALAPPDATA%\Vineeeeea\Cache` | Miniaturas, formas de onda y textos; se puede borrar |
| `VINEEEEEA_FONT_PATH` | `%WINDIR%\Fonts\segoeui.ttf` | Tipografía de los textos |
| `VINEEEEEA_BOLD_FONT_PATH` | `%WINDIR%\Fonts\segoeuib.ttf` | Tipografía de los textos en negrita |
| `VINEEEEEA_WHISPER_PATH` | `%LOCALAPPDATA%\Vineeeeea\tools\whisper\Release\whisper-cli.exe` | whisper.cpp |
| `VINEEEEEA_WHISPER_MODEL` | `%LOCALAPPDATA%\Vineeeeea\tools\models\ggml-base.bin` | Modelo de whisper.cpp |
| `VINEEEEEA_CAPTION_LANGUAGE` | `auto` | Idioma de los subtítulos (`auto`, `es`, `en`…) |

## Estructura

| Proyecto | Responsabilidad |
| --- | --- |
| `Vineeeeea.Domain` | `Models` (proyecto, pistas, clips, medios, textos, efectos), `Errors`, `Utils`. Sin dependencias externas |
| `Vineeeeea.Application` | `Controllers` (casos de uso), `Services` (contratos, sesión con historial, plan de render), `Dtos`, `Config`, `Utils` |
| `Vineeeeea.Infrastructure` | `Implements` (ffprobe, ffmpeg, ffplay, whisper.cpp, proyecto JSON), `Services` (procesos, textos), `Dtos`, `Utils` |
| `Vineeeeea.App` | Biblioteca de UI: vistas, ViewModels y composición de dependencias (`CompositionRoot`) |
| `Vineeeeea.Desktop` | Arranque de escritorio (Windows, Linux, macOS) |
| `Vineeeeea.Android` | Arranque Android (fuera de `Vineeeeea.slnx`; necesita el workload `android`, JDK 17 y Android SDK) |

Los proyectos se guardan como `.vine` (JSON UTF-8, `SchemaVersion` 3; abre también las versiones 1 y 2). El guardado
es atómico y deja una copia `.bak`. Las reglas de trabajo del repositorio están en `CLAUDE.md`.

## Android

```
dotnet workload install android
dotnet build src/Vineeeeea.Android -c Release -t:InstallAndroidDependencies -p:AndroidSdkDirectory=$HOME/Android/Sdk -p:JavaSdkDirectory=<jdk17> -p:AcceptAndroidSDKLicenses=True
dotnet build src/Vineeeeea.Android -c Release -p:AndroidSdkDirectory=$HOME/Android/Sdk -p:JavaSdkDirectory=<jdk17>
```

El APK queda en `src/Vineeeeea.Android/bin/Release/net10.0-android/`. El motor de video móvil aún no existe: `UnsupportedToolRunner` avisa en lugar de fallar con un error de ejecutable.
