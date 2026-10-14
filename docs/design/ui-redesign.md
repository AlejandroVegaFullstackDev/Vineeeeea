# Vineeeeea — Especificación del rediseño de la UI

Documento de traspaso del diseño a implementación en Avalonia. Fuente visual: el canvas de diseño (tableros Logo, Modo fácil, Exportar, Inicio y Modo avanzado). Los HTML de `docs/design/referencia/` son la fuente de **valores exactos** (tamaños, márgenes, textos); no se abren en el navegador porque dependen del editor de diseño, y sus imágenes `/_blob/...` corresponden a los archivos de `Assets/Brand/`.

Tokens: `src/FrameForge.App/Styles/Tokens.axaml`. Ninguna vista usa colores, fuentes o tamaños literales: solo las claves de ese archivo.

---

## 1. Principios del producto

1. **Para alguien que nunca ha editado.** Lenguaje humano ("Cortar aquí", "Tu video", "Música"), nunca jerga ("pista V1", "keyframe", "fps") en el modo fácil.
2. **Modo fácil por defecto, modo avanzado opcional** (`Ver › Modo avanzado`). Ambos modos editan el mismo proyecto y comparten dominio, casos de uso y ViewModels de estado; solo cambia el *shell* de vistas.
3. **Manipulación directa:** arrastrar medios al timeline, arrastrar clips, arrastrar el cabezal. Los botones existen como alternativa, no como único camino.
4. **Undo antes que confirmaciones.** "Quitar" no pregunta; Deshacer está siempre visible con texto.
5. **Estilo visual:** claro, llamativo, caricatura punk. Trazo negro grueso (2.5 px), esquinas redondeadas, sombras duras sin desenfoque y colores sacados del logo. Nada de degradados suaves ni minimalismo gris.

## 2. Marca

| Pieza | Archivo | Uso |
|---|---|---|
| Emblema (rata completa, sin texto) | `Assets/Brand/logo-emblema.png` | Pantalla de inicio grande, "Acerca de", splash |
| Ícono (cara) | `Assets/Brand/icono.png` | Barra superior (32–44 px) |
| Ícono Windows | `Assets/Brand/vineeeeea.ico` (16–256 px) | `Window.Icon` y `ApplicationIcon` del `.csproj` |
| Nombre | Texto "Vineeeeea" (5 e) con `Font.Brand` | Junto al ícono en la barra superior y en el lockup |

Sin eslogan ni texto "Digital piracy" en ninguna parte de la UI.

### Fuentes (OFL, descargar de Google Fonts a `Assets/Fonts/` como `AvaloniaResource`)

| Token | Familia | Uso |
|---|---|---|
| `Font.Brand` | UnifrakturMaguntia | Solo el nombre "Vineeeeea" |
| `Font.Heading` | Bowlby One | Títulos de sección y botones protagonistas ("¡Empezar!", "¡Exportar!") |
| `Font.UI` | Bricolage Grotesque (400–700) | Todo lo demás |
| `Font.Mono` | JetBrains Mono (500/700) | Tiempos (`0:02`, `00:00:02:18`) y especificaciones (`1920×1080 · MP4`) |

## 3. Lenguaje visual

- **Superficie de panel:** `Border` con `Background=Brush.Bg.Surface`, `BorderBrush=Brush.Border.Strong`, `BorderThickness=Border.Width`, `CornerRadius=Radius.L`, `BoxShadow="4 4 0 0 #111111"`.
- **Botón estándar:** alto `Size.Control`, `Radius.M`, borde fuerte, sombra `3 3 0 0`. Hover: desplazar −1,−1 y sombra `4 4`. Presionado: desplazar +2,+2 y sombra `1 1`. Usar `RenderTransform` con transición corta (80 ms) y respetar "reducir movimiento" del sistema.
- **Botón primario:** igual que el estándar con fondo `Brush.Accent.Primary`. **Uno solo por pantalla.**
- **Seleccionado / activo (toggle):** fondo `Brush.Selection` (o `Brush.Accent.Primary` en tarjetas de opción), borde fuerte, sombra `3 3`.
- **Foco de teclado:** contorno de 3 px `Brush.Focus` separado 3 px. Nunca quitarlo.
- **Deshabilitado:** opacidad 0.45, sin hover ni desplazamiento, con tooltip que explique por qué.
- **Fondo de la app:** `Brush.Bg.App`. El patrón de puntos (`Brush.Bg.AppDots`, 18 px) es opcional; empezar con color plano (YAGNI).
- **Visor:** `Brush.Bg.Stage` con el video en *letterbox* a la relación de aspecto del proyecto.
- **Iconos:** una sola familia de trazo (2.6 px, extremos redondeados). Todo botón solo-ícono lleva `ToolTip` y `AutomationProperties.Name`.

## 4. Pantallas

### 4.1 Inicio (sin proyecto abierto)

- Encabezado: ícono 88 px y el título "¿Qué vamos a editar hoy?" (`Font.Heading`, 44), con el subtítulo "Escoge la forma de tu video. Lo demás lo ajustamos por ti."
- Tarjeta **Nuevo video** con tres formas: **Horizontal** (1920×1080, "YouTube, computador"), **Vertical** (1080×1920, "TikTok, Reels, Shorts") y **Cuadrado** (1080×1080, "Publicaciones de feed"). La selección es única y por defecto Horizontal.
- `Expander` "Opciones avanzadas" cerrado por defecto, con fotogramas por segundo (24/25/30/60, por defecto 30) y audio (44,1 / 48 kHz, por defecto 48).
- Primario: **¡Empezar!**. Secundario: "Abrir un proyecto… Ctrl+O".
- Zona de soltar: "O suelta aquí tus videos y empezamos de una con ellos." Crea el proyecto con la forma del primer video y los pone en "Tu video".
- Tarjeta **Seguir editando** con los recientes (miniatura, nombre, duración y ruta). Estado vacío: "Tus proyectos recientes aparecen aquí."

### 4.2 Editor — modo fácil (pantalla principal)

Grid de 3 filas (barra 64 · contenido · timeline 300) con 12 px de separación y 12 px de margen.

**Barra superior**
- Izquierda: ícono 44 px, "Vineeeeea" en `Font.Brand` 32 y una píldora de estado "Demo · se guarda solo" (punto `Brush.Success`).
- Centro: **Deshacer** y **Rehacer** con ícono y texto. Rehacer está deshabilitado si no hay nada que rehacer.
- Derecha: **Ayuda** y el primario **Exportar video**, que abre la pantalla Exportar.

**Columna izquierda "Agregar" (272 px)**
- Tres categorías: Videos y fotos · Música y sonido · Texto. La activa va en `Brush.Selection`.
- Lista de medios de la categoría. Cada fila tiene miniatura 68×40, nombre, una línea humana ("8 s · ya está en tu video") y un botón verde **+** (`Brush.Success`) que agrega el medio al final de "Tu video" (o de Música/Texto según el tipo).
- Abajo: "Buscar en mi computador" (abre el selector de archivos), con el texto "o suelta los archivos aquí".

**Centro: visor**
- Fondo `Brush.Bg.Stage` y video en *letterbox*.
- Transporte simple: tiempo actual `0:02`, botón play/pausa redondo de 56 px en `Brush.Accent.Primary` y "de 0:08". El tiempo va en segundos (`m:ss`); los fotogramas solo aparecen en el modo avanzado.

**Columna derecha "¿Qué le hacemos a este clip?" (340 px)**
- Encabezado en `Brush.Accent.Primary` con la miniatura y el nombre del clip seleccionado.
- Grid de 2 columnas con 6 fichas. Cada ficha lleva ícono, título y una línea de explicación:

| Ficha | Token de fondo | Explicación | Comando de aplicación |
|---|---|---|---|
| Cortar aquí | `Brush.Tile.Cut` | Parte el clip en dos donde está la línea naranja | `SplitClipCommand` en el cabezal (atajo S) |
| Acortar | `Brush.Tile.Trim` | Quita pedazos del inicio o del final | Activa el modo de recorte guiado (ver §5) |
| Velocidad | `Brush.Tile.Speed` | Cámara lenta o más rápido | Panel de velocidad: 0.5× · 1× · 1.5× · 2× |
| Volumen | `Brush.Tile.Volume` | Sube, baja o silencia este clip | Panel de volumen: deslizador + silenciar |
| Girar | `Brush.Tile.Rotate` | Endereza un video que quedó de lado | Rota 90° por clic (transform) |
| Quitar | `Brush.Tile.Remove` (ícono `Brush.Danger`) | Lo saca del video (puedes deshacer) | `RemoveClipCommand` (atajo Supr) |

- **Estilo:** 4 tarjetas con vista previa. Cada una es un *preset* de efectos del dominio, no un filtro de UI:
  - Original: sin efectos.
  - Vivo: `saturation 1.5`, `contrast 1.1`.
  - Cálido: `colorbalance` hacia rojos y amarillos más `saturation 1.2`.
  - B y N: `saturation 0`, `contrast 1.1`.
- **Luz:** deslizador de −100 a 100 con las etiquetas "Más oscuro" y "Más claro". Se mapea a `brightness` y genera un solo paso de undo por gesto.
- `Expander` **Ajustes avanzados** cerrado: posición, escala, contraste, opacidad y keyframes. Reutiliza el inspector del modo avanzado.
- **Sin selección:** el panel muestra "Toca un clip de abajo para editarlo" y deshabilita las fichas.

**Timeline simplificado (abajo)**
- Consejo que se puede cerrar (fondo `Brush.Tile.Speed`): "Arrastra la línea naranja para moverte por el video. Arrastra un clip para cambiarlo de lugar." Recordar que se cerró en las preferencias del usuario.
- Filas con nombre humano e ícono de color:
  - **Tu video** (82 px, `Brush.Timeline.Video`).
  - **Encima** (44 px, `Brush.Timeline.Overlay`).
  - **Música** (44 px, `Brush.Timeline.Audio`).
  - **Texto** (44 px, `Brush.Timeline.Text`).
- Internamente siguen siendo pistas del dominio. "Tu video" es la pista base y "Encima" agrupa las pistas de overlay.
- Clips con miniaturas (video) o forma de onda (audio) y una etiqueta blanca con borde ("paisaje.mp4 · 4 s").
- **Clip seleccionado:** borde de 4 px tinta, contorno de 4 px `Brush.Timeline.ClipSelected` y manijas de recorte de 8 px a cada lado.
- **Cabezal:** línea de 4 px `Brush.Timeline.Playhead` con borde tinta y una bola de 20 px arriba para agarrarla.
- Botón **+** punteado al final de "Tu video" y botón "Agregar texto" punteado en la fila Texto.
- Regla en segundos (`0:00`, `0:01`…). El zoom solo se hace con Ctrl+rueda.

### 4.3 Exportar

- Enlace "‹ Volver a editar", la miniatura del proyecto y el título "¿Para dónde va tu video?" con la línea "Demo Vineeeeea · 0:08 · horizontal".
- Cuatro tarjetas de destino (selección única, la seleccionada en `Brush.Accent.Primary` con sombra `6 6`):

| Destino | Descripción | Salida |
|---|---|---|
| YouTube o computador | Horizontal, se ve bien en pantalla grande. | 1920×1080 · MP4 (H.264 + AAC) |
| TikTok, Reels o Shorts | Vertical, para ver en el celular. | 1080×1920 · MP4 |
| WhatsApp | Archivo liviano que se envía rápido. | 1280×720 · MP4 a menor bitrate |
| Máxima calidad | Para guardar o seguir editando en otro lado. Pesa más. | 1920×1080 · bitrate alto |

- Aviso contextual (fondo `Brush.Selection`) cuando la forma del proyecto no coincide con el destino: "Tu video es horizontal: quedará centrado con franjas arriba y abajo."
- Campos "Nombre del archivo" y "Guardar en" (botón "Cambiar" que abre el selector de carpeta).
- Primario **¡Exportar!** con "Puedes seguir editando mientras se exporta." Al pulsarlo, el progreso se muestra en la barra superior y se puede cancelar. Al terminar: "Listo · Abrir carpeta". Si falla, el mensaje es humano, nunca el stderr de FFmpeg.

### 4.4 Editor — modo avanzado

Misma paleta y lenguaje visual. Layout: Medios (272) · Visor · Inspector (320) arriba, y timeline (330) abajo. Lo que cambia frente al modo fácil:
- Barra superior con menús (Archivo/Editar/Ver/Ayuda), el nombre del proyecto con su estado y el primario Exportar.
- Barra del timeline con Seleccionar (V), Dividir (S), Eliminar (Supr), Deshacer y Rehacer, Imán (N) y zoom. La herramienta activa va en fondo tinta con ícono ácido.
- Cabeceras de pista con ocultar, silenciar y bloquear.
- Inspector por secciones plegables (Transformar, Ajustes de color). Cada propiedad lleva campo numérico, deslizador, diamante de keyframe (relleno si hay keyframe en el cabezal) y restablecer.
- Transporte con timecode `HH:MM:SS:FF` y botones para ir al inicio, ir al final y avanzar o retroceder un fotograma.

## 5. Interacciones clave

| Acción | Gesto | Atajo | Resultado |
|---|---|---|---|
| Reproducir/pausar | Botón del visor | Espacio | — |
| Mover el cabezal | Arrastrar la bola o clic en la regla | ← / → (1 frame), Shift+← / → (10 frames) | — |
| Agregar medio | Arrastrar al timeline · botón + | — | `InsertClipCommand` |
| Mover clip | Arrastrar; imán a bordes y cabezal | — | `MoveClipCommand` (1 undo al soltar) |
| Cortar | Ficha "Cortar aquí" | S | `SplitClipCommand` |
| Acortar | Arrastrar las manijas del clip seleccionado | — | `TrimClipCommand` (1 undo al soltar) |
| Quitar | Ficha "Quitar" | Supr | `RemoveClipCommand` |
| Deshacer / Rehacer | Botones de la barra | Ctrl+Z / Ctrl+Shift+Z | — |
| Exportar | Botón primario | Ctrl+E | Navega a Exportar |

Todos los atajos se registran en **un único registro** que alimenta los `KeyBinding`, los tooltips y los menús.

## 6. Accesibilidad

- Contraste verificado:
  - Tinta sobre ácido: 13.6:1.
  - Blanco sobre morado: 6.9:1.
  - Tinta sobre verde cresta: 7.4:1.
  - El texto secundario `#4F4D42` sobre papel supera 4.5:1.
- Video, audio y texto se distinguen por **ícono y contenido** (miniaturas u onda), no solo por color.
- Orden de tabulación: barra superior → Agregar → visor → panel del clip → timeline.
- `AutomationProperties.Name` en todos los controles sin texto visible.

## 7. Criterios de aceptación de la UI

1. Ninguna vista contiene colores, tamaños de fuente ni radios literales; todo sale de `Tokens.axaml`.
2. Un usuario nuevo puede, sin leer nada, importar un video, cortarlo, quitar un pedazo, ponerle música y exportarlo para WhatsApp.
3. Todo lo de la tabla §5 se puede hacer con teclado, y cada acción se deshace con Ctrl+Z.
4. Pasar entre modo fácil y avanzado conserva la selección, la posición del cabezal y el historial de undo.
5. La ventana mínima de 1280×720 no recorta controles; los paneles laterales se pueden colapsar.
6. Importar o exportar nunca congela la ventana.
