# Prompts para implementar el rediseño con Claude Code

Úsalos en orden, **una fase por sesión** o por rama. Cada fase termina con la app compilando, las pruebas en verde y una captura.

---

## Fase 0 — Contexto y plan (no escribe código)

```
Lee CLAUDE.md, @docs/design/ui-redesign.md y @src/FrameForge.App/Styles/Tokens.axaml.
Luego revisa cómo está hoy FrameForge.App (MainWindow, ViewModels, estilos).

No escribas código todavía. Dame:
1. Diferencias entre la UI actual y la spec, por pantalla.
2. Plan por fases (cambios pequeños y reversibles), con los archivos que tocarías en cada una.
3. Riesgos y dudas que necesites que yo resuelva.
```

## Fase 1 — Tema, tokens, fuentes e ícono

```
Implementa la fase 1 del plan:
- Integra Styles/Tokens.axaml en App.axaml sobre FluentTheme.
- Registra las fuentes de Assets/Fonts como AvaloniaResource (los .ttf ya están en esa carpeta).
- Crea estilos base con clases: Panel, Button (estándar y "primary"), Toggle seleccionado, foco visible y deshabilitado, según §3 de la spec. Las sombras duras van con Setter BoxShadow.
- Pon vineeeeea.ico como Window.Icon y ApplicationIcon.
No cambies layouts todavía. Al final reporta según la sección 12 de CLAUDE.md.
```

## Fase 2 — Pantalla de inicio

```
Implementa la pantalla Inicio de §4.1 de la spec, con su ViewModel, sin tocar dominio.
"¡Empezar!" crea el proyecto con la forma y los fps elegidos usando el caso de uso existente.
Incluye estado vacío de recientes y la zona de soltar archivos.
Prueba headless: elegir Vertical + Empezar crea un proyecto 1080x1920.
```

## Fase 3 — Shell del modo fácil (estático)

```
Crea EasyShellView con el layout de §4.2: barra superior, columna Agregar, visor y panel "¿Qué le hacemos a este clip?".
Conecta Deshacer/Rehacer, Exportar y las fichas Cortar y Quitar a los comandos existentes.
El timeline puede seguir siendo el actual en esta fase.
Agrega Ver › Modo avanzado para alternar con la vista actual, conservando el estado.
```

## Fase 4 — Timeline simplificado

```
Implementa el timeline de §4.2 como control propio con Render(DrawingContext): filas Tu video/Encima/Música/Texto, miniaturas, forma de onda, cabezal arrastrable, selección, manijas de recorte e imán.
Arrastrar y recortar generan un solo paso de undo al soltar.
Mide el render con 50 clips antes y después; no optimices sin datos.
```

## Fase 5 — Estilos, luz, velocidad y volumen

```
Implementa Estilo (Original/Vivo/Cálido/B y N), Luz, Velocidad, Volumen y Girar de §4.2 como presets de EffectInstance del dominio, con su traducción en Rendering al grafo de filtros de FFmpeg.
Pruebas de dominio para cada preset: aplicar + deshacer.
```

## Fase 6 — Exportar

```
Implementa la pantalla Exportar de §4.3 con los cuatro destinos como presets de ExportSettings.
Exportación asíncrona, cancelable, con progreso en la barra superior y errores en lenguaje humano.
Prueba de integración: exportar el proyecto demo para WhatsApp y validar 1280x720 con ffprobe.
```

## Fase 7 — Modo avanzado con el tema nuevo

```
Ajusta la vista avanzada a §4.4 usando solo tokens: barra del timeline con herramientas, cabeceras de pista, inspector por secciones y timecode con fotogramas.
Verifica los criterios de aceptación de §7 y repórtalos uno por uno.
```
