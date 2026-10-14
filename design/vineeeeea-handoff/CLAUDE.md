# CLAUDE.md — Guía base para proyectos de interfaz (web · escritorio · móvil)

Este archivo define cómo debe trabajar Claude Code en un repositorio con interfaz de usuario. Es **agnóstico de empresa, marca y stack**: sirve para una SPA web, un microfrontend, una app de escritorio (Avalonia, WPF, WinUI, Electron, Tauri) o una app móvil (React Native, Flutter, nativa).

> **Cómo usarlo:** copia este archivo a la raíz del repo y completa la sección 0. Todo lo demás aplica tal cual; las reglas que solo valen para una plataforma están marcadas con **[Web]**, **[Escritorio]** o **[Móvil]**. Si una regla de una plataforma no corresponde al proyecto, ignórala; no la fuerces.

---

## 0 · Contexto del proyecto (completar por repo)

| Campo                      | Valor                                              |
| -------------------------- | -------------------------------------------------- |
| Producto                   | **Vineeeeea** (5 e) — editor de video de escritorio, muy fácil para quien nunca ha editado |
| Plataforma(s)              | Escritorio Windows (Avalonia permite otras más adelante) |
| Stack de UI                | Avalonia + .NET 10 LTS; FFmpeg/ffprobe como procesos externos |
| Patrón de presentación     | MVVM con CommunityToolkit.Mvvm, bindings compilados (`x:DataType`) |
| Lenguaje(s)                | C# (`Nullable` habilitado)                          |
| Sistema de diseño          | `src/FrameForge.App/Styles/Tokens.axaml` + `docs/design/ui-redesign.md` |
| Linter / formateador       | `dotnet format` + `.editorconfig`                   |
| Pruebas                    | xUnit · Avalonia.Headless.XUnit para UI             |
| Comandos                   | build: `dotnet build` · test: `dotnet test` · lint: `dotnet format --verify-no-changes` |
| Documentos de arquitectura | `docs/architecture.md`, `docs/project-format-v1.md`, `docs/decisions/ADR-*.md` |

Reglas propias de este proyecto:

- Proyectos `FrameForge.*` (App, Application, Domain, Contracts, Infrastructure, Playback, Rendering). `Domain` no depende de Avalonia, FFmpeg ni del disco.
- Tiempo de edición en enteros: fotogramas (`long`) en el timeline y microsegundos (`long`) para medios y audio; el frame rate como fracción, nunca `double`.
- Toda edición es un comando reversible de `Application`; los ViewModels no tocan el modelo directamente.
- **Modo fácil por defecto, modo avanzado opcional.** Comparten dominio, comandos y estado; solo cambia el shell de vistas. Antes de tocar UI, lee `docs/design/ui-redesign.md`.
- En el modo fácil no aparece jerga técnica (pista V1, keyframe, fps): el texto que ve el usuario es humano.

Si este archivo contradice un ADR o la documentación de arquitectura del repo, **manda el ADR**; señala la contradicción en lugar de resolverla en silencio.

---

## 1 · Prioridades (en este orden)

1. **Integridad de los datos del usuario** — nunca perder ni corromper lo que el usuario creó o escribió.
2. **Accesibilidad y usabilidad por diseño** — no es una fase posterior.
3. **Interfaz que no se bloquea** — nada pesado en el hilo de UI.
4. **Mantenibilidad** — límites claros, cambios pequeños y reversibles.
5. **Rendimiento medido**, no supuesto.
6. **Consistencia** con el sistema de diseño y los patrones existentes del repo.

Principios y **cuándo** aplican:

| Principio | Úsalo para… | Señal de abuso |
| --- | --- | --- |
| **KISS** | elegir la solución más directa que funcione | patrones o capas que nadie pidió |
| **YAGNI** | no construir “por si acaso” | interfaces con una sola implementación sin motivo de prueba o de límite |
| **DRY** | eliminar duplicación de **conocimiento** (reglas, cálculos) | extraer tras la primera repetición; acoplar cosas que solo se parecen. Regla de tres |
| **SOLID** | límites entre módulos y dependencias invertidas hacia el dominio | una clase/archivo por cada verbo |
| **Clean / Hexagonal** | que el dominio no conozca UI, red, disco ni frameworks | capas vacías de “pasamanos” |
| **Diseño atómico** | organizar el sistema de componentes (átomos → organismos) | forzar la taxonomía en pantallas únicas |
| **DDD ligero** | lenguaje de negocio en nombres y módulos por dominio | agregados y eventos en un CRUD |

Preferir siempre **el cambio seguro más pequeño** que resuelva la tarea correctamente.

---

## 2 · Arquitectura

### 2.1 Capas y regla de dependencia

```
Presentación  →  Aplicación  →  Dominio  ←  Infraestructura
(vistas, VM,      (casos de uso,  (modelo y     (HTTP, BD, disco,
 componentes)      comandos)       reglas puras)  SDKs, procesos)
```

- El **dominio** no importa frameworks de UI, clientes HTTP, ORMs, SDKs ni APIs de archivos.
- La **presentación** no contiene reglas de negocio: traduce interacción del usuario a casos de uso y muestra estado.
- La **infraestructura** implementa interfaces (puertos) definidas hacia adentro; se inyecta, no se instancia desde el dominio ni desde casos de uso.
- Prohibidas las dependencias circulares entre módulos.

Equivalencias por plataforma (orientativas):

| Capa | [Web] React | [Escritorio] .NET (Avalonia/WPF/WinUI) | [Móvil] Flutter / RN |
| --- | --- | --- | --- |
| Presentación | componentes, páginas, hooks de UI | Views (XAML/AXAML) + ViewModels | widgets/screens + BLoC/ViewModel |
| Aplicación | servicios/hooks de caso de uso | proyecto `*.Application`: comandos, casos de uso | use cases / repositories interfaces |
| Dominio | `types` + funciones puras | proyecto `*.Domain` | `domain/` puro |
| Infraestructura | `api/`, `storage/` | proyecto `*.Infrastructure` | `data/` (datasources) |

### 2.2 Organización por dominio (feature‑first)

Agrupa por **funcionalidad de negocio**, no por tipo técnico. Ejemplo genérico:

```
src/
├── app/            # arranque, composición raíz, DI, navegación, providers
├── config/         # configuración tipada, variables de entorno, feature flags
├── features/       # un módulo por dominio de negocio
│   └── <feature>/
│       ├── ui/          # vistas/componentes/pantallas del módulo
│       ├── application/ # casos de uso, estado del módulo
│       ├── domain/      # modelos, reglas, validaciones
│       ├── data/        # adaptadores: API, BD, archivos
│       └── index.*      # API pública del módulo (lo único importable desde fuera)
└── shared/         # sistema de diseño, utilidades técnicas, tipos globales
```

Flujo permitido: `app → features → shared → config`. Un `feature` **no importa internals de otro feature**: solo su API pública. Lo que dos features necesitan pasa a `shared` cuando hay una tercera necesidad, no antes.

En .NET u otros lenguajes con proyectos/paquetes, las capas pueden ser **proyectos separados** con referencias explícitas; no crees un proyecto por cada carpeta: un proyecto nuevo solo cuando hay un límite de dependencias real.

### 2.3 Estado

- Estado **local** primero; elevarlo solo cuando dos ramas lo comparten.
- Estado **del servidor** (caché de datos remotos) separado del estado de UI.
- Estado **global** solo con justificación escrita (sesión, tema, preferencias). No introducir un store global por costumbre.
- Transiciones complejas con reducers/máquinas de estado explícitas en lugar de múltiples setters sueltos.
- Si la app edita documentos del usuario: toda edición como **comando reversible** (undo/redo), gestos continuos fusionados en un solo paso de undo.

---

## 3 · Política de edición

Antes de escribir código:

1. **Lee los archivos relevantes** y el ADR aplicable; sigue el patrón existente salvo que viole accesibilidad, integridad de datos, mantenibilidad o rendimiento.
2. Cambios estructurales (límites de módulos, formato de datos persistidos, contrato de API, librería base): **propón primero, implementa después**, de forma incremental.
3. Cambios **pequeños, localizados y reversibles**; no reescribas módulos no relacionados.
4. No agregues dependencias externas salvo que sean **estrictamente necesarias**; justifica peso, licencia, mantenimiento y alternativa nativa.
5. Preserva compatibilidad hacia atrás salvo *breaking change* explícito y documentado.
6. Prohibido *force‑push* sobre ramas protegidas. No hagas commit, push ni PR si no se pidió.

---

## 4 · Reglas de código obligatorias

- No hardcodear secretos, claves, endpoints, rutas absolutas, feature flags ni valores de entorno: van en configuración tipada.
- No hardcodear colores, tipografías, espaciados ni radios en componentes: usar **tokens** (sección 6).
- Tipado estricto (`strict` en TS, `Nullable` en C#, null‑safety en Dart/Kotlin/Swift). Warnings tratados como errores cuando el repo lo permita.
- Sin logs de depuración (`console.*`, `Console.WriteLine`, `print`), código muerto, comentarios temporales ni TODO sin ticket.
- Comentarios solo para el **porqué**, nunca para repetir el qué.
- Operaciones asíncronas cancelables cuando pueden tardar (`AbortSignal`, `CancellationToken`, `Job`/`Task` cancelable).
- Errores manejados en el borde correcto: infraestructura traduce errores técnicos a errores de dominio; la UI los muestra en lenguaje humano.
- Linter y formateador limpios antes de dar una tarea por cerrada.

Convenciones de nombres (adapta al idioma del stack, pero sé consistente):

| Elemento | Convención |
| --- | --- |
| Componentes / vistas / clases | `PascalCase` |
| Funciones, variables | `camelCase` (o la del lenguaje) |
| Constantes | `UPPER_SNAKE_CASE` (o la del lenguaje) |
| Hooks [Web] | `use` + `PascalCase` |
| ViewModels [Escritorio/Móvil] | `XxxViewModel` |
| Servicios / adaptadores | `xxx.service.*`, `XxxRepository`, `XxxAdapter` |
| Pruebas | co‑ubicadas: `*.test.*` / `*Tests` |

Tamaño: un componente, vista o ViewModel que pasa de ~300–400 líneas o mezcla varias responsabilidades se divide.

---

## 5 · UX e interacción (todas las plataformas)

Cada cambio de UI se revisa contra esta lista.

### 5.1 Jerarquía y layout
- Una **acción primaria** visible por pantalla; el resto, secundaria o en menús.
- Agrupa controles junto a lo que modifican (proximidad), no en una barra genérica de botones del mismo peso.
- El contenido principal (documento, lienzo, lista, visor) ocupa el mayor espacio.
- Rejilla y espaciado consistentes a partir de la escala de tokens.

### 5.2 Manipulación directa y eficiencia
- Si el usuario puede **arrastrar, seleccionar o editar en sitio**, no lo obligues a un formulario o a un botón intermedio.
- Atajos de teclado para acciones frecuentes, mostrados en tooltips y menús, definidos en un **registro único**.
- Undo en lugar de diálogos de confirmación; confirmar solo lo destructivo e irreversible.

### 5.3 Estados (cada pantalla los diseña todos)
- **Vacío**: explica qué hacer y ofrece la acción (no un panel gris).
- **Cargando**: esqueletos o progreso en el elemento afectado; cancelable si es largo.
- **Error**: mensaje humano + acción sugerida; nunca stack traces ni mensajes crudos de librerías.
- **Parcial / sin conexión / sin permisos** cuando aplique.
- **Deshabilitado**: claramente distinguible de activo, y con explicación cuando no es obvio por qué.

### 5.4 Feedback
- Toda acción responde en < 100 ms (aunque sea visualmente); operaciones > 1 s muestran progreso.
- Cambios sin guardar visibles; guardado o autoguardado explícito según el producto.

---

## 6 · Sistema de diseño y tokens

- Fuente única de verdad para **color, tipografía, espaciado, radios, sombras, duraciones de animación**: tokens semánticos (`color.bg.surface`, `color.text.secondary`, `color.accent`, `space.3`), no literales (`#1E90FF`, `13px`).
- Los componentes consumen tokens semánticos; los valores concretos los decide cada producto o marca en su archivo de tema.
- Tema claro/oscuro (y alto contraste si la plataforma lo ofrece) cambiando solo el mapa de tokens.
- Antes de crear un componente, busca uno existente en el sistema de diseño; si falta, créalo **en** el sistema de diseño, no dentro del feature.
- Iconos de una sola familia; iconos solos siempre con etiqueta accesible y tooltip.

Ubicación típica de tokens: [Web] variables CSS / tema de Tailwind o del framework · [Escritorio] `ResourceDictionary`/estilos del tema · [Móvil] `ThemeData` / objeto de tema.

---

## 7 · Accesibilidad

- Objetivo mínimo: **WCAG 2.2 AA** (web) y las guías de accesibilidad de la plataforma (Windows/macOS, Android, iOS).
- Contraste ≥ 4.5:1 en texto normal, ≥ 3:1 en texto grande y en componentes/estados de foco.
- Nunca transmitir información **solo con color**.
- Navegación completa por teclado [Web/Escritorio]: orden lógico, foco visible, sin trampas de foco.
- Nombre accesible en todo control sin texto visible (`aria-label` · `AutomationProperties.Name` · `Semantics`/`contentDescription`/`accessibilityLabel`).
- Áreas táctiles ≥ 44×44 pt (iOS) / 48×48 dp (Android) [Móvil].
- Respeta preferencias del sistema: tamaño de texto, reducción de movimiento, tema.
- [Web] HTML semántico antes que ARIA; reglas de `eslint-plugin-jsx-a11y` si es React.

---

## 8 · Rendimiento

- Mide antes de optimizar (profiler del navegador, dotTrace/PerfView, Flutter DevTools, Android Studio/Instruments).
- Nunca bloquear el hilo de UI: I/O, red, parsing pesado y procesamiento de medios fuera de él; volver al hilo de UI solo para actualizar estado.
- Listas largas **virtualizadas**.
- Superficies de dibujo intensivo (lienzos, timelines, gráficos) con render propio y redibujo por región, no miles de componentes.
- [Web] code‑splitting y carga diferida por ruta/feature, imágenes optimizadas, presupuestos de bundle; Core Web Vitals en verde para páginas públicas.
- [Escritorio] arranque en frío medido; trabajo en segundo plano cancelable.
- [Móvil] evitar reconstrucciones innecesarias, imágenes al tamaño de pantalla, sin trabajo pesado en el arranque.

---

## 9 · Seguridad

- Toda entrada externa es **no confiable**: valida en el borde (esquemas tipados) y escapa al renderizar.
- [Web] sin `innerHTML`/`dangerouslySetInnerHTML` con datos de usuario; CSP estricta; cookies `HttpOnly`/`Secure`/`SameSite`; tokens fuera de `localStorage` cuando sea posible.
- [Escritorio/Móvil] secretos en el almacén seguro del sistema (Credential Manager, Keychain, Keystore), nunca en texto plano ni en el binario.
- Procesos externos con lista de argumentos, **nunca** concatenando cadenas ni pasando por un shell.
- Escritura de archivos del usuario **atómica**: temporal en la misma carpeta → flush → validar → reemplazar.
- Formatos persistidos versionados con migraciones explícitas; nunca sobrescribir un archivo de una versión futura que la app no entiende.
- Dependencias con versión fijada y auditadas (`npm audit`, `dotnet list package --vulnerable`, etc.).

---

## 10 · Pruebas

| Nivel | Qué cubre | Herramientas típicas |
| --- | --- | --- |
| Unitarias | dominio, casos de uso, reglas, comandos (aplicar + deshacer) | Vitest/Jest · xUnit/NUnit · flutter_test · JUnit/XCTest |
| Componentes / UI | comportamiento visible para el usuario, no detalles internos | Testing Library · Avalonia.Headless · widget tests · Compose/XCUITest |
| Integración | adaptadores reales: API, BD, archivos, procesos | MSW/Testcontainers · pruebas con binarios reales |
| E2E | 2–5 flujos críticos | Playwright · Appium/Maestro · WinAppDriver/FlaUI |
| Visual (opcional) | regresiones de UI | Storybook + capturas · golden tests |

- TDD para dominio y casos de uso: prueba roja primero.
- Las pruebas consultan la UI como un usuario (rol, etiqueta, texto), no por clases CSS ni IDs internos.
- Todo bug corregido llega con una prueba que lo reproduce.

---

## 11 · CI/CD y configuración

- Build reproducible: versión de SDK/runtime fijada (`.nvmrc`/`engines`, `global.json`, `fvm`), lockfile comprometido.
- Pipeline mínimo: instalar → lint/format → tipos → pruebas → build. Rojo = no se mergea.
- Configuración por entorno fuera del código (12‑Factor): variables de entorno [Web], archivos de configuración por entorno o build flavors [Escritorio/Móvil].
- Feature flags desde configuración, nunca `if (true)` en el código.
- [Web] despliegues independientes por microfrontend solo si existen equipos/ciclos independientes que lo justifiquen; si no, un monolito modular bien partido es más simple (KISS/YAGNI).

---

## 12 · Validación y entrega

Al terminar cada tarea, reporta:

1. **Resumen** breve del cambio y su porqué.
2. **Archivos tocados.**
3. **Pruebas** añadidas/ejecutadas y resultado.
4. Para cambios de UI: **evidencia visual** (captura antes/después o historia de Storybook) y qué punto de las secciones 5–7 resuelve.
5. **Supuestos, riesgos y pendientes** no verificados.

---

## 13 · Lo que se debe evitar

- Lógica de negocio en vistas, componentes, code‑behind o ViewModels.
- Bloquear el hilo de UI.
- Valores de diseño, secretos o endpoints hardcodeados.
- Componentes/clases “Dios” y módulos que importan internals de otros.
- Dependencias circulares.
- Estado global sin justificación.
- Abstracciones, capas o interfaces “por si acaso”.
- Refactors masivos sin requerimiento.
- Optimizar sin medir.
- UI que comunica estado solo con color, o controles sin nombre accesible.
- Copiar la interfaz, iconos o assets propietarios de otro producto: inspiración de flujos sí, clon visual no.

---

## 14 · Mindset de implementación

Elegir siempre la opción:

- **Simple** sobre ingeniosa.
- **Explícita** sobre mágica.
- **Medida** sobre supuesta.
- **Reversible** sobre definitiva.
- **Directa para el usuario** (ver, arrastrar, deshacer) sobre formularios y pasos extra.
- **Mínima** sobre especulativa.
