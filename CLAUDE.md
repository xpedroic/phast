# CLAUDE.md — Phast

Este archivo guarda el encargo completo del proyecto para que no se pierda entre sesiones.
El usuario no programa: Claude hace todo (código, compilar, probar, arreglar). **Hablar siempre en español** y explicar los avances en lenguaje sencillo, sin tecnicismos.

- Cuenta de GitHub: `xpedroic` · repo: https://github.com/xpedroic/phast (público)
- Aviso de licencia: `Required Notice: Copyright (c) 2026 xpedroic`

## Estado / progreso

- [x] Paso 0: GitHub (repo, licencia, README, CONTRIBUTING, CLAUDE.md, .gitignore, THIRD_PARTY_NOTICES, workflow)
- [x] Fase 1: base (ventana tipo Chrome, pestañas, navegación, barra de direcciones)
- [ ] Funciones v1 (1–11)
  - [x] 1 Pestañas (hecho en la Fase 1)
  - [ ] 4 Historial y descargas → el usuario pidió dejarla PARA EL FINAL (después de la 11)
  - [ ] 7 Modo lectura → SALTADA por petición del usuario (preguntarle al final si la quiere)
  - [x] 8 Modo oscuro instantáneo: Dark Reader (MIT) en Pages/scripts/darkreader.js, solo se carga en webs con modo oscuro; luna en la barra + Ctrl+Mayús+D; se recuerda por sitio (dominio); "todas las webs" con excepciones en Apariencia; proxy de CSS/imágenes limitado (Dark/CssProxy.cs) para hojas de estilo de otros servidores.
  - [x] 6 CAMBIO DECIDIDO POR EL USUARIO: Phast NO tendrá soporte de extensiones (ni gestor de extensiones en ajustes). En su lugar, lo que hacen uBlock Origin Lite y TwiBlocker va INTEGRADO de serie y debe funcionar incluso mejor. Hecho:
    - Twitch: vaft (TwitchAdSolutions, MIT, fork mantenido ryanbr) en Pages/scripts/twitch-vaft.js, se registra solo al entrar en twitch.tv, se actualiza cada 2 días (Adblock/SiteScripts.cs).
    - YouTube: script propio Pages/scripts/youtube-adblock.js (quita adPlacements/playerAds/adSlots, salta anuncios, oculta anuncios del feed).
    - Ocultación genérica tipo uBlock: Pages/scripts/cosmetic.js informa de clases/ids y el motor devuelve solo el CSS que encaja; respeta $generichide/$elemhide.
  - [x] 5 Adblock: motor propio (src/Phast/Adblock) con sintaxis ABP; listas EasyList, EasyPrivacy, EasyList Spanish (+ Cookie List opcional) descargadas y actualizadas cada 4 días; ~3 µs por petición en Release; escudo con contador y burbuja para desactivar por sitio; sección Privacidad en ajustes. PENDIENTE: comprobar visualmente la burbuja del escudo (el PC estaba bloqueado).
  - [x] 3 Marcadores: barra (carpetas, », clic derecho), estrella/Ctrl+D con burbuja, gestor phast://bookmarks (Ctrl+Mayús+O, arrastrar), importar de Chrome/Edge/Brave/Vivaldi (todos los perfiles + iconos) y HTML, exportar HTML. Pruebas automáticas en tests/Phast.Tests (se ejecutan en CI).
  - [x] 2 Barra de direcciones con sugerencias (historial SQLite, autocompletado, sugerencias del buscador) + buscador configurable en phast://settings (primera versión de ajustes: Apariencia, Buscador, Al iniciar)

## Notas para pruebas (Claude)

- Arrancar en modo pruebas: variables `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` (permite ejecutar JS en las páginas vía DevTools) y `PHAST_LOG=1` (escribe %LOCALAPPDATA%\Phast\debug.log).
- Al enviar teclas con SendKeys, comprobar SIEMPRE antes que la ventana de Phast está en primer plano; si no, abortar (una vez se escribió en otra ventana del usuario).
- Para probar sin molestar al usuario: manejar las páginas por DevTools (CDP) y los botones WPF por UI Automation (InvokePattern por x:Name); capturar con PrintWindow (no necesita primer plano). Los Popup con transparencia (burbujas, desplegables) solo salen con captura de pantalla y con Phast delante.
- No editar archivos de texto con `Get-Content | Set-Content` de Windows PowerShell 5.1: rompe las tildes (UTF-8). Usar las herramientas de edición.
- [ ] Medición de rendimiento frente a Chrome
- [ ] Al terminar todo: subir a GitHub (README) capturas del navegador y pruebas de rendimiento: tiempos de apertura, RAM y CPU de Phast frente a otros navegadores instalados (Chrome, Edge, Brave...), mismas páginas, para demostrar que consume menos.
- [ ] Release v0.1.0 (preguntar antes de publicar)

---

# Encargo original

Vamos a construir "Phast", un navegador web para Windows 11, desde cero, en esta carpeta (E:\Proyectos\Phast). Yo no voy a programar: lo haces tú entero, compilas, pruebas y arreglas los errores. Háblame siempre en español.

## PASO 0: GitHub (lo primero de todo, antes de escribir código)
1. Comprueba que tengo instalados git, gh (GitHub CLI) y el .NET 8 SDK (git --version, gh --version, dotnet --list-sdks). Si falta algo, dímelo y espera.
2. Comprueba que estoy conectado a GitHub con "gh auth status". Si no lo estoy, pídeme que ejecute "gh auth login" yo mismo; tú no inicies sesión ni manejes contraseñas o tokens.
3. Configura git con mi nombre de usuario y email de GitHub (pregúntamelos si no los puedes sacar de gh). Si ya hay una configuración global de git, respétala.
4. Inicializa el repositorio en esta carpeta con rama principal "main".
5. Crea estos archivos:
   - LICENSE: licencia PolyForm Noncommercial 1.0.0 (texto oficial completo de polyformproject.org), con la línea "Required Notice: Copyright (c) [año actual] [mi nombre]".
   - README.md en español e inglés: qué es Phast, capturas (cuando las haya), cómo descargarlo, cómo compilarlo, cómo contribuir y la licencia. Explica de forma sencilla que Phast es gratis para todo el mundo, que cualquiera puede usarlo, compilarlo, modificarlo y hacer forks, pero que nadie puede venderlo ni usarlo con fines comerciales. Añade que los forks deben usar otro nombre distinto de "Phast".
   - CONTRIBUTING.md breve, explicando cómo proponer mejoras.
   - CLAUDE.md: este encargo completo, para que no se pierda entre sesiones.
   - .gitignore para proyectos .NET/Visual Studio (bin, obj, .vs, dist, etc.).
   - THIRD_PARTY_NOTICES.md con las licencias de todo lo que usemos de terceros (por ejemplo Mozilla Readability, Apache 2.0, y las listas de filtros de adblock).
6. Crea con gh un repositorio PÚBLICO llamado "phast" en mi cuenta, con la descripción "Phast: a fast, simple and hackable web browser for Windows", conéctalo como origin y sube el primer commit.
7. Crea un workflow de GitHub Actions (.github/workflows/build.yml) que compile Phast en windows-latest en cada push y, cuando cree una etiqueta de versión (v0.1.0, etc.), publique una Release con el Phast.exe en un .zip para que la gente lo descargue sin compilar.
8. A partir de aquí: haz commit cada vez que algo funcione, con mensajes claros, y sube a GitHub al terminar cada fase. Nunca subas archivos con datos personales, contraseñas o tokens.

## Idea
Un navegador más sencillo de usar que Chrome o Brave, que consuma lo menos posible, con la apariencia y el orden de Chrome (es la única que me gusta), y que un desarrollador pueda tunear a fondo desde los AJUSTES del propio navegador (nada de editar archivos de configuración a mano).

## Tecnología (decidido)
- C# + .NET 8 + WPF + WebView2 (paquete NuGet Microsoft.Web.WebView2). No hacemos fork de Chromium: usamos el motor WebView2 que ya trae Windows y solo programamos la "carcasa".
- Un único CoreWebView2Environment compartido por todas las pestañas.
- Las páginas internas (nueva pestaña, ajustes, modo lectura) en HTML/CSS/JS servidas con SetVirtualHostNameToFolderMapping y comunicadas con la app por WebMessage. En la barra de direcciones se muestran como phast://newtab, phast://settings, etc.
- Código limpio y bien organizado (un archivo por responsabilidad) y comentado en inglés, para que otros desarrolladores puedan entenderlo, compilarlo y hacer forks fácilmente.
- Antes de usar cualquier código o librería de terceros, comprueba que su licencia es compatible con la de Phast (MIT, Apache 2.0, BSD: sí; GPL: no) y apúntala en THIRD_PARTY_NOTICES.md.

## Apariencia
- Clon visual de Chrome: pestañas arriba integradas en la barra de título (WindowChrome), botón + de nueva pestaña, botones de ventana a la derecha. Debajo: atrás, adelante, recargar, barra de direcciones (URL o búsqueda), iconos de extensiones/acciones y menú de tres puntos. Barra de marcadores debajo.
- Tema claro y oscuro de la interfaz siguiendo a Windows, con opción de forzarlo en ajustes.
- Diseña también un icono propio para Phast.

## Funciones v1
1. Pestañas: abrir, cerrar, reordenar arrastrando, Ctrl+T, Ctrl+W, Ctrl+Shift+T (reabrir cerrada), Ctrl+Tab, Ctrl+1..9, favicons y títulos. Restaurar la sesión al abrir.
2. Barra de direcciones con sugerencias de historial y marcadores. Buscador configurable (por defecto Google, con opciones DuckDuckGo, Brave Search, Bing, Startpage).
3. Marcadores: barra de marcadores, Ctrl+D, gestor de marcadores. IMPORTAR MARCADORES DE CHROME con un clic, leyendo el archivo JSON "Bookmarks" de %LOCALAPPDATA%\Google\Chrome\User Data\Default (y detectando otros perfiles). También importar desde un HTML exportado.
4. Historial y descargas básicos.
5. Adblock integrado activado por defecto: bloquear peticiones a dominios de anuncios y rastreadores (WebResourceRequested) usando una lista de filtros tipo EasyList/EasyPrivacy que se descarga y se actualiza sola, compilada en memoria para que sea rápida. Desactivable por sitio. Contador de bloqueados por página.
6. Soporte de extensiones de Chrome en lo que permita WebView2 (AreBrowserExtensionsEnabled + AddBrowserExtensionAsync), con un gestor en ajustes. Quiero usar uBlock Origin Lite y TwiBlocker (bloqueo de anuncios de Twitch). Si no funcionan en WebView2, avísame y busca la alternativa: por ejemplo integrar el bloqueo de anuncios de Twitch con el método de TwitchAdSolutions (vaft), respetando su licencia.
7. Modo lectura (F9 y botón en la barra): muestra solo el artículo e imágenes con tipografía cómoda, tamaño de letra ajustable y tema claro/sepia/oscuro. Usa Mozilla Readability (licencia Apache 2.0, incluye su aviso de licencia).
8. Modo oscuro instantáneo (Ctrl+Shift+D y botón): fuerza modo oscuro en cualquier web al instante, sin recargar, respetando imágenes y vídeos. Recordar la preferencia por sitio.
9. Grupos de páginas: en ajustes defino grupos con nombre (p. ej. "Trabajo", "Ocio") y sus URLs. Un botón en la barra despliega los grupos y abre todas sus páginas de golpe, en un grupo de pestañas con color.
10. Ajustes (phast://settings) con diseño limpio tipo Chrome. Secciones normales: Apariencia, Buscador, Al iniciar, Marcadores e importar, Privacidad y adblock, Grupos de páginas, Extensiones, Descargas.
11. Sección "Avanzado / Modo desarrollador", oculta hasta activarla con un interruptor:
   - Rendimiento: minutos hasta dormir pestañas, límite de pestañas activas, aceleración por GPU on/off.
   - Monitor de RAM y CPU por pestaña en tiempo real, con botón para dormir o cerrar.
   - Atajos de teclado: cambiar cualquiera pulsando la combinación nueva.
   - Adblock: añadir listas de filtros, bloquear o permitir dominios a mano, ver lo bloqueado.
   - Por sitio: forzar oscuro, desactivar JavaScript, cambiar user agent, CSS y JS propios.
   - Flags de WebView2 (argumentos extra del navegador) con explicación de cada uno.
   - Herramientas de desarrollo (F12), consola, exportar/importar toda la configuración.

## Rendimiento (prioridad máxima)
- Pestañas perezosas: una pestaña restaurada o en segundo plano no crea su WebView hasta que la abro.
- Pestañas en segundo plano con MemoryUsageTargetLevel = Low, y TrySuspendAsync tras X minutos sin usarlas (configurable). Dormir del todo (liberar el WebView y conservar URL y título) tras más tiempo.
- Nada de servicios en segundo plano innecesarios. Arranque en frío lo más rápido posible.
- Cuando tengamos la v1, mide RAM y tiempo de arranque frente a Chrome con las mismas 5 páginas, enséñame los números y ponlos en el README.

## Forma de trabajar
- Por fases: primero el Paso 0 (GitHub). Luego la base (ventana tipo Chrome, pestañas, navegación, barra de direcciones): compílala, ábremela para que la vea y súbela. Después el resto de funciones por orden.
- Después de cada cambio: compila (dotnet build), arregla todos los errores y avisos importantes, y ejecuta para comprobar que funciona.
- Crea un build.bat que genere un Phast.exe listo para usar en una carpeta "dist".
- Cuando la v1 esté completa y probada, crea la etiqueta v0.1.0 para que se publique la primera Release en GitHub. Pregúntame antes de publicarla.
- Explícame cada avance en lenguaje sencillo, sin tecnicismos.
