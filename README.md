<div align="center">
  <h1>AstroPlanner Web</h1>
  <p>A horizon-aware deep sky object planner for visual observers and astrophotographers — now in the browser.</p>

  [![Live App](https://img.shields.io/badge/live-planner.njastro.cc-blue)](https://planner.njastro.cc)
  [![Deploy](https://img.shields.io/github/actions/workflow/status/tankhardrive/AstroPlannerWeb/deploy.yml?label=deploy)](https://github.com/tankhardrive/AstroPlannerWeb/actions)
  [![.NET](https://img.shields.io/badge/.NET-10-purple)](https://dotnet.microsoft.com)
  [![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
  [![Catalog: CC BY-SA 4.0](https://img.shields.io/badge/catalog-CC%20BY--SA%204.0-orange)](#credits)
</div>

---

A browser-based port of [AstroPlanner](https://github.com/tankhardrive/AstroPlanner), the cross-platform desktop app. Runs entirely client-side with no server required — install it as a PWA and it works offline.

**[→ Open the app](https://planner.njastro.cc)**

---

## What it does

AstroPlanner Web tells you **what's worth observing tonight** and exactly how long you have to observe it. It loads the full NGC/IC catalog, computes each object's altitude throughout the night against your custom horizon profile, and scores every object based on how long it clears your horizon. Pick a night, set your location, and get a ranked list you can actually trust — no account, no server, no install required.

## Features

### Planning
- **Horizon-aware visibility** — define a custom horizon profile (azimuth/altitude pairs) to account for your tree line, rooftop, or observatory walls
- **Visibility scoring** — each object is scored by how long it clears your horizon at a useful altitude, weighted for object size and sky conditions
- **Yearly best-time heatmap** — month-by-month strip showing the best observing windows across the full year
- **Observation date picker** — plan ahead for any night

### Catalog
- **NGC and IC objects** from the embedded [OpenNGC](https://github.com/mattiaverga/OpenNGC) catalog
- **Filters** — narrow by object type (galaxies, nebulae, clusters, double stars), favorites, or visible-only
- **Search** — find any object by name or catalog ID

### Telescope & FOV
- **Imaging setup manager** — define telescope + camera combinations with focal length and sensor dimensions
- **FOV overlay** — interactive sky view powered by [Aladin Lite](https://aladin.cds.unistra.fr/AladinLite/) with your field of view overlaid on real DSS imagery
- **Best setup suggestion** — highlights the setup whose FOV best fits the target object
- **Fill percentage** — shows how much of the frame the object fills for each setup

### Object detail
- **Sky survey image** — DSS2 color thumbnail via [CDS HiPS2FITS](https://alasky.cds.unistra.fr/hips-image-services/hips2fits)
- **Altitude plot** — full-night arc showing rise, transit, and set against your horizon
- **Moon interference** — phase and angular separation at peak visibility
- **Favorites & imaging log** — mark objects as favorites or record when you imaged them
- **Simbad / AstroBin links** — one-click access to reference data and community images

### Offline / PWA
- **Progressive Web App** — install to your home screen on mobile or desktop
- **Full offline support** — the catalog, app code, and assets are cached at install time; planning works without a network connection
- **Sky survey images** require an internet connection (fetched live from CDS)

---

## Built with

| | |
|---|---|
| **Language** | C# / .NET 10 |
| **UI framework** | [Blazor WebAssembly](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) — runs entirely in the browser via WASM |
| **Astronomy math** | [AASharp](https://github.com/jsauve/AASharp) — port of Jean Meeus' *Astronomical Algorithms* |
| **Sky viewer** | [Aladin Lite v3](https://aladin.cds.unistra.fr/AladinLite/) embedded via iframe |
| **Survey images** | [CDS HiPS2FITS](https://alasky.cds.unistra.fr/hips-image-services/hips2fits) — DSS2 Color |
| **Catalog** | [OpenNGC](https://github.com/mattiaverga/OpenNGC) by Mattia Verga (bundled, [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)) |
| **Local storage** | [Blazored.LocalStorage](https://github.com/Blazored/LocalStorage) |
| **CSV parsing** | [CsvHelper](https://joshclose.github.io/CsvHelper/) by Josh Close |
| **Hosting** | [Cloudflare Pages](https://pages.cloudflare.com) |

---

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/tankhardrive/AstroPlannerWeb.git
cd AstroPlannerWeb
dotnet run --project AstroPlannerWeb/AstroPlannerWeb.csproj
```

Then open `https://localhost:5001` in your browser.

---

## Deployment

Every push to `main` triggers a GitHub Actions workflow that builds the app with `dotnet publish` and deploys the output to Cloudflare Pages automatically. See [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml).

---

## Desktop version

Looking for the full desktop app with weather forecasting, comet tracking, Stellarium integration, and more?

**[→ AstroPlanner (desktop)](https://github.com/tankhardrive/AstroPlanner)** — Windows, macOS, and Linux

---

## Credits

Built by [tankhardrive](https://github.com/tankhardrive) with the help of [Claude Code](https://claude.ai/code) by Anthropic.

### Third-party data

The bundled NGC/IC catalog (`AstroPlannerWeb/wwwroot/data/NGC.csv`) is **OpenNGC**,
created by [Mattia Verga](https://github.com/mattiaverga) and contributors, and
licensed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/).
This data is not covered by the MIT license that applies to the AstroPlanner Web source code.

---

## License

MIT — see [LICENSE](LICENSE) for details.
