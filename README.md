# Phast

**Phast: a fast, simple and hackable web browser for Windows.**

[Español](#español) · [English](#english)

---

## Español

### ¿Qué es Phast?

Phast es un navegador web para Windows 10/11 pensado para ser **rápido, ligero y fácil de usar**, con el aspecto y el orden de Google Chrome. Usa el motor WebView2 que ya viene con Windows (el mismo motor que Chromium/Edge), así que las páginas se ven igual que en Chrome, pero la "carcasa" es mucho más pequeña.

Lo que lo hace especial:

- **Consume poco**: las pestañas en segundo plano se duermen solas y las pestañas restauradas no se cargan hasta que las abres.
- **Bloqueador de anuncios integrado**, activado desde el primer momento.
- **Modo lectura** y **modo oscuro instantáneo** para cualquier web.
- **Grupos de páginas**: abre "Trabajo" u "Ocio" con un clic.
- **Importa tus marcadores de Chrome** con un clic.
- **Modo desarrollador** en los ajustes: atajos, rendimiento, filtros, CSS/JS por sitio, flags del motor… todo desde la interfaz, sin tocar archivos de configuración.

### Capturas

_Próximamente._

### Descargar

1. Ve a la página de [Releases](https://github.com/xpedroic/phast/releases).
2. Descarga el archivo `Phast-vX.Y.Z-win-x64.zip` de la última versión.
3. Descomprímelo donde quieras y abre `Phast.exe`. No hace falta instalar nada.

Necesitas el **WebView2 Runtime**, que ya viene incluido en Windows 11 (y en casi todos los Windows 10 actualizados).

### Compilar desde el código

Requisitos: Windows 10/11 y el [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bat
git clone https://github.com/xpedroic/phast.git
cd phast
build.bat
```

El ejecutable listo para usar queda en la carpeta `dist`. Para desarrollo también puedes usar `dotnet run --project src/Phast`.

### Contribuir

¡Las mejoras son bienvenidas! Lee [CONTRIBUTING.md](CONTRIBUTING.md).

### Licencia

Phast usa la licencia [PolyForm Noncommercial 1.0.0](LICENSE). Explicado de forma sencilla:

- ✅ Phast es **gratis para todo el mundo**.
- ✅ Cualquiera puede **usarlo, compilarlo, modificarlo y hacer forks**.
- ❌ **Nadie puede venderlo ni usarlo con fines comerciales.**
- ℹ️ Los forks deben usar **un nombre distinto de "Phast"**, para que nadie los confunda con el proyecto original.

Phast incluye componentes de terceros con sus propias licencias: consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## English

### What is Phast?

Phast is a web browser for Windows 10/11 designed to be **fast, lightweight and easy to use**, with the look and layout of Google Chrome. It uses the WebView2 engine that already ships with Windows (the same Chromium engine used by Edge), so pages render just like in Chrome, while the browser "shell" stays small.

Highlights:

- **Low resource usage**: background tabs go to sleep automatically and restored tabs are not loaded until you open them.
- **Built-in ad blocker**, on by default.
- **Reader mode** and **instant dark mode** for any website.
- **Page groups**: open "Work" or "Fun" with a single click.
- **One-click import of your Chrome bookmarks.**
- **Developer mode** in Settings: shortcuts, performance, filter lists, per-site CSS/JS, engine flags… all from the UI, no config files to edit.

### Screenshots

_Coming soon._

### Download

1. Go to the [Releases](https://github.com/xpedroic/phast/releases) page.
2. Download `Phast-vX.Y.Z-win-x64.zip` from the latest release.
3. Unzip it anywhere and run `Phast.exe`. No installation needed.

You need the **WebView2 Runtime**, which is already included in Windows 11 (and in almost every up-to-date Windows 10).

### Build from source

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bat
git clone https://github.com/xpedroic/phast.git
cd phast
build.bat
```

The ready-to-use executable ends up in the `dist` folder. For development you can also run `dotnet run --project src/Phast`.

### Contributing

Improvements are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md).

### License

Phast is licensed under [PolyForm Noncommercial 1.0.0](LICENSE). In plain words:

- ✅ Phast is **free for everyone**.
- ✅ Anyone can **use, build, modify and fork it**.
- ❌ **Nobody may sell it or use it for commercial purposes.**
- ℹ️ Forks must use **a name other than "Phast"**, so nobody confuses them with the original project.

Phast includes third-party components under their own licenses: see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
