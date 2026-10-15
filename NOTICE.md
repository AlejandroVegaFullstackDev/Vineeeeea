# Avisos de terceros

Vineeeeea se distribuye bajo la **GNU GPL v3** (ver `LICENSE`). Las versiones para móvil incluyen
programas de terceros que se distribuyen junto con la app:

| Componente | Licencia | Uso |
| --- | --- | --- |
| [FFmpeg](https://ffmpeg.org) (compilado con `--enable-gpl --enable-version3`) | GPL-3.0 | Render, exportación, miniaturas y audio |
| [x264](https://www.videolan.org/developers/x264.html) | GPL-2.0-o-posterior | Codificación de video H.264 |
| [FreeType](https://freetype.org) | FreeType License / GPL-2.0 | Texto en pantalla (`drawtext`) |
| [HarfBuzz](https://harfbuzz.github.io) | MIT | Forma del texto en pantalla (`drawtext`) |
| [whisper.cpp](https://github.com/ggml-org/whisper.cpp) | MIT | Subtítulos automáticos |
| [Avalonia](https://avaloniaui.net) | MIT | Interfaz |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | MVVM |

Las versiones exactas de FFmpeg, x264 y FreeType de cada compilación están fijadas en `build/versions.env`
junto con los scripts para reconstruir los binarios, de modo que cualquiera pueda obtener el código fuente
correspondiente.
