# Third-Party Notices

Phast is licensed under PolyForm Noncommercial 1.0.0 (see `LICENSE`).
It uses or downloads the following third-party components, each under its own license.

---

## Microsoft Edge WebView2 SDK (`Microsoft.Web.WebView2` NuGet package)

- Source: https://www.nuget.org/packages/Microsoft.Web.WebView2
- License: BSD-style license by Microsoft Corporation (https://www.nuget.org/packages/Microsoft.Web.WebView2/License)
- Copyright (C) Microsoft Corporation. All rights reserved.

The WebView2 Runtime itself is part of Windows and is not redistributed by Phast.

---

## Microsoft.Data.Sqlite

- Source: https://github.com/dotnet/efcore (package `Microsoft.Data.Sqlite`)
- License: MIT (https://licenses.nuget.org/MIT)
- Copyright (c) .NET Foundation and Contributors.
- Used to store the browsing history.

## SQLitePCLRaw (`SQLitePCLRaw.bundle_e_sqlite3`, `SQLitePCLRaw.core`)

- Source: https://github.com/ericsink/SQLitePCL.raw
- License: Apache License 2.0 (https://www.apache.org/licenses/LICENSE-2.0)
- Copyright (c) Eric Sink and contributors.

## SQLite

- Source: https://www.sqlite.org/
- License: Public Domain (https://www.sqlite.org/copyright.html)

---

## TwitchAdSolutions — "vaft" (Twitch ad blocking)

- Source: https://github.com/ryanbr/TwitchAdSolutions (maintained fork of https://github.com/pixeltris/TwitchAdSolutions)
- License: MIT — full text in `src/Phast/Pages/scripts/twitch-vaft.LICENSE.txt`
- Copyright (c) 2020-present TwitchAdSolutions Contributors. Credits: https://github.com/cleanlock/VideoAdBlockForTwitch#credits
- Bundled unmodified as `src/Phast/Pages/scripts/twitch-vaft.js` and injected only into twitch.tv pages. If enabled in Settings, Phast downloads newer versions from the URL above.

---

## Dark Reader (instant dark mode)

- Source: https://github.com/darkreader/darkreader (npm package `darkreader` 4.9.133)
- License: MIT — full text in `src/Phast/Pages/scripts/darkreader.LICENSE.txt`
- Copyright (c) 2026 Dark Reader Ltd.
- Bundled unmodified as `src/Phast/Pages/scripts/darkreader.js`; loaded only on sites where the user turns dark mode on.

---

## Mozilla Readability

- Source: https://github.com/mozilla/readability
- License: Apache License 2.0 (https://www.apache.org/licenses/LICENSE-2.0)
- Copyright (c) 2010 Arc90 Inc and Mozilla contributors.
- Used by Reader mode. The full Apache 2.0 license text is shipped next to the bundled file (`Readability.LICENSE.md`).

---

## Ad-block filter lists (EasyList, EasyPrivacy, EasyList Spanish, EasyList Cookie List)

- EasyList, EasyPrivacy, EasyList Spanish — https://easylist.to/ — dual-licensed GPLv3 / Creative Commons Attribution-ShareAlike 3.0 Unported (https://easylist.to/pages/licence.html). Copyright: The EasyList authors.
- EasyList Cookie List (optional, off by default) — https://secure.fanboy.co.nz/fanboy-cookiemonster.txt — Creative Commons Attribution 3.0 (http://creativecommons.org/licenses/by/3.0/). Copyright: Fanboy and the EasyList authors.

These lists are **not bundled** with Phast's source code or binaries: they are downloaded by the user's browser at runtime from their official URLs and used unmodified as data. Phast's own filter engine (`src/Phast/Adblock`) is original code that reads the public Adblock Plus filter syntax. Users can enable or disable lists in Settings.

---

_This file is updated whenever a new third-party component is added._
