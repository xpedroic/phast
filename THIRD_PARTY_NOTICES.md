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

## Mozilla Readability

- Source: https://github.com/mozilla/readability
- License: Apache License 2.0 (https://www.apache.org/licenses/LICENSE-2.0)
- Copyright (c) 2010 Arc90 Inc and Mozilla contributors.
- Used by Reader mode. The full Apache 2.0 license text is shipped next to the bundled file (`Readability.LICENSE.md`).

---

## Ad-block filter lists (EasyList, EasyPrivacy)

- Source: https://easylist.to/
- License: dual-licensed GPLv3 / Creative Commons Attribution-ShareAlike 3.0 Unported (https://easylist.to/pages/licence.html)
- Copyright: The EasyList authors (https://easylist.to/)

These lists are **not bundled** with Phast's source code or binaries: they are downloaded by the user's browser at runtime from their official URLs and used unmodified as data. Users can add or remove filter lists in Settings.

---

_This file is updated whenever a new third-party component is added._
