# AstroPlannerWeb — Blazor WASM Bootstrap Guide

This document is a Claude Code bootstrap guide for rewriting **AstroPlanner** (an Avalonia desktop astrophotography planner) as a **Blazor WebAssembly** app targeting **.NET 10**. The original source is at https://github.com/tankhardrive/AstroPlanner.

---

## Project Creation

The project has already been created with:

```bash
dotnet new blazorwasm -n AstroPlannerWeb --framework net10.0 --pwa
cd AstroPlannerWeb
```

This produces a standalone Blazor WASM app (no backend server) with PWA/offline support via a service worker.

---

## NuGet Packages to Add

```bash
dotnet add package Blazored.LocalStorage          # Browser localStorage for settings persistence
dotnet add package AASharp                         # Astronomy math (Jean Meeus algorithms) — same as desktop app
dotnet add package CsvHelper                       # Parse the OpenNGC catalog CSV
dotnet add package CommunityToolkit.Mvvm           # Optional: ObservableObject helpers if desired
dotnet add package Microsoft.AspNetCore.Components.Web  # Already included in template
```

---

## Solution Structure to Create

```
AstroPlannerWeb/
├── wwwroot/
│   ├── data/
│   │   └── NGC.csv                  # Copy from AstroPlanner/Assets/Data/NGC.csv
│   ├── css/
│   │   └── app.css
│   └── index.html
├── Models/                          # Plain C# model classes (port from desktop)
│   ├── DsoObject.cs
│   ├── ObservationSite.cs
│   ├── ImagingSetup.cs
│   ├── HorizonProfile.cs
│   ├── ObservingSession.cs
│   └── AppSettings.cs               # Root settings object stored in localStorage
├── Services/                        # Business logic and data access
│   ├── CatalogService.cs            # Load + filter NGC/IC/Messier/Caldwell from CSV
│   ├── AstronomyService.cs          # AASharp wrappers: altitude, visibility score, rise/set
│   ├── HorizonService.cs            # Custom horizon interpolation and intersection
│   ├── WeatherService.cs            # Open-Meteo + 7timer HTTP calls
│   ├── LightPollutionService.cs     # lightpollutionmap.info Bortle lookup
│   ├── CometService.cs              # MPC comet data fetch (with CORS caveat — see below)
│   ├── SettingsService.cs           # Read/write AppSettings to localStorage
│   └── VisibilityScorer.cs          # Score each object for the selected night
├── Components/                      # Reusable Blazor components
│   ├── AltitudePlot.razor           # Full-night altitude arc chart
│   ├── YearlyHeatmap.razor          # Month-by-month best-time heatmap
│   ├── FovPreview.razor             # Aladin Lite iframe/JS interop wrapper
│   ├── MoonInfo.razor               # Phase + separation display
│   ├── ObjectCard.razor             # Summary row in the main list
│   ├── ObjectDetail.razor           # Expanded detail panel
│   ├── HorizonEditor.razor          # Import horizon CSV + visual preview
│   └── WeatherPanel.razor           # Hourly cloud/seeing/wind display
├── Pages/
│   ├── Planner.razor                # Main page — object list, filters, date/location picker
│   ├── Settings.razor               # Sites, imaging setups, horizon profiles, import/export
│   └── ObjectPage.razor             # Full detail view for a single object
├── Layout/
│   ├── MainLayout.razor
│   └── NavMenu.razor
├── Interop/
│   └── AladinInterop.cs             # JS interop wrapper for Aladin Lite v3
├── Program.cs
└── _Imports.razor
```

---

## Models to Port

Port these model classes directly from the desktop app. They are plain C# with no Avalonia dependencies.

### AppSettings.cs
Top-level object serialized to localStorage as `"astroplanner-settings"`.

```csharp
public class AppSettings
{
    public List<ObservationSite> Sites { get; set; } = new();
    public int ActiveSiteIndex { get; set; } = 0;
    public List<ImagingSetup> ImagingSetups { get; set; } = new();
    public List<string> Favorites { get; set; } = new();         // NGC/IC IDs
    public List<ImagingLogEntry> ImagingLog { get; set; } = new();
    public HorizonProfile CustomHorizon { get; set; } = new();
    public AppPreferences Preferences { get; set; } = new();
}
```

### HorizonProfile.cs
```csharp
public class HorizonProfile
{
    public string Name { get; set; } = "Default";
    // List of (azimuth degrees, altitude degrees) pairs, sorted by azimuth
    public List<HorizonPoint> Points { get; set; } = new();
}

public class HorizonPoint
{
    public double Azimuth { get; set; }
    public double Altitude { get; set; }
}
```

---

## Services Implementation Notes

### SettingsService.cs
Uses `Blazored.LocalStorage`. Inject `ILocalStorageService`, serialize/deserialize `AppSettings` as JSON.

```csharp
public class SettingsService
{
    private const string Key = "astroplanner-settings";
    private readonly ILocalStorageService _storage;

    public async Task<AppSettings> LoadAsync() =>
        await _storage.GetItemAsync<AppSettings>(Key) ?? new AppSettings();

    public async Task SaveAsync(AppSettings settings) =>
        await _storage.SetItemAsync(Key, settings);
}
```

Add an **Export** button that triggers a JSON file download via JS interop, and an **Import** `<InputFile>` that reads a JSON file and saves it. This is the equivalent of the desktop settings file — users can back up and transfer settings between browsers/devices.

### CatalogService.cs
On startup, fetch `NGC.csv` from `wwwroot/data/` using `HttpClient`:

```csharp
var csv = await _http.GetStringAsync("data/NGC.csv");
// Parse with CsvHelper into List<DsoObject>
```

Filter methods match the desktop app: by type (galaxy/nebula/cluster/etc.), favorites, visibility.

### AstronomyService.cs
Port the AASharp wrappers directly. Key functions needed:
- `GetAltitudeAzimuth(DsoObject obj, DateTime utc, ObservationSite site)` → `(alt, az)`
- `GetRiseSetTimes(DsoObject obj, DateTime date, ObservationSite site, HorizonProfile horizon)`
- `ComputeNightWindow(ObservationSite site, DateTime date)` → astronomical twilight start/end
- `GetMoonPhaseAndSeparation(DsoObject obj, DateTime utc, ObservationSite site)`

### HorizonService.cs
Interpolate horizon altitude at a given azimuth using linear interpolation between the two nearest `HorizonPoint` entries. Wrap to 0–360. Used by visibility scorer to determine whether each altitude sample clears the custom horizon.

### VisibilityScorer.cs
For each DSO, sample altitude at N-minute intervals across the night window. Count samples where:
- `altitude > horizonAltitudeAtAzimuth(azimuth)` (clears custom horizon)
- `altitude > minimumUsefulAltitude` (e.g. 20°)

Score = weighted minutes above horizon, adjusted by Bortle class sky quality factor. Port this logic directly from the desktop `VisibilityScorer`.

### WeatherService.cs
- **Open-Meteo**: free, no API key, no CORS issues. Fetch `https://api.open-meteo.com/v1/forecast?...` directly from WASM.
- **7timer ASTRO**: `http://www.7timer.info/bin/api.pl?...` — note this is HTTP not HTTPS. Browsers block mixed content. Either skip 7timer or proxy it. Open-Meteo alone may be sufficient.

### CometService.cs — CORS Caveat
The Minor Planet Center (`https://www.minorplanetcenter.net`) does **not** send CORS headers, so direct browser fetches will be blocked.

**Solution**: Create a free **Cloudflare Worker** as a CORS proxy. The worker fetches MPC on the server side and returns the response with `Access-Control-Allow-Origin: *`. Deploy it to a route like `https://mpc-proxy.natejames.cc/comets`. Call that URL from `CometService` instead of MPC directly.

Same applies to **lightpollutionmap.info** if it blocks CORS — verify at runtime; if blocked, use the same Cloudflare Worker approach.

---

## Aladin Lite Integration

Aladin Lite v3 is a JavaScript library. Integration approach:

1. Add to `wwwroot/index.html`:
```html
<script type="module" src="https://aladin.cds.unistra.fr/AladinLite/api/v3/latest/aladin.js"></script>
```

2. Create `wwwroot/js/aladin-interop.js`:
```javascript
let aladinInstance = null;

window.initAladin = function(divId, ra, dec, fovDeg) {
    aladinInstance = A.aladin(`#${divId}`, {
        survey: 'P/DSS2/color',
        fov: fovDeg,
        target: `${ra} ${dec}`,
        showReticle: false,
        showZoomControl: false,
    });
};

window.aladinGoto = function(ra, dec) {
    if (aladinInstance) aladinInstance.gotoRaDec(ra, dec);
};

window.aladinSetFov = function(fovDeg) {
    if (aladinInstance) aladinInstance.setFoV(fovDeg);
};

window.aladinAddFovOverlay = function(widthDeg, heightDeg, angleDeg) {
    // Draw FOV rectangle overlay — use Aladin's overlay API
};
```

3. `AladinInterop.cs` wraps `IJSRuntime` calls to the above functions.

4. `FovPreview.razor` calls `AladinInterop.InitAsync(divId, ra, dec, fovDeg)` in `OnAfterRenderAsync`.

---

## Pages

### Planner.razor (main page, route: `/`)
- Date picker (defaults to tonight)
- Active site selector dropdown
- Filter bar: object type chips, favorites toggle, visible-only toggle
- Scrollable object list using `<Virtualize>` for performance (catalog is large)
- Clicking a row opens `ObjectDetail` in a side panel or navigates to `/object/{id}`
- Visibility scores computed async on page load / when date or site changes

### Settings.razor (route: `/settings`)
- **Sites tab**: add/edit/delete `ObservationSite` entries (name, lat, lon, elevation, timezone)
- **Horizon tab**: `HorizonEditor` component — import CSV via `<InputFile>`, preview the profile as a simple SVG polar or cartesian chart
- **Imaging Setups tab**: add/edit telescope+camera combinations (focal length, sensor width/height mm, pixel scale computed automatically)
- **Import/Export**: download settings as JSON, upload JSON to restore

### ObjectPage.razor (route: `/object/{CatalogId}`)
- Object name, type, constellation, magnitude, size
- `AltitudePlot` — full-night altitude curve as an SVG or Chart.js chart
- `MoonInfo` — phase percentage, separation angle, rise/set
- `YearlyHeatmap` — 12-column month grid showing best visibility windows
- `FovPreview` — Aladin Lite with imaging FOV overlay
- Favorite toggle, imaging log entries (date + notes)
- "Open in Stellarium" link: `stellarium://open?...` URL scheme (works from browser on desktop)
- "AstroBin" button: opens `https://www.astrobin.com/search/?q={name}`

---

## Stellar Altitude Plot Component

Use **Chart.js** via JS interop, or render as a pure SVG in Blazor (simpler, no JS dependency).

SVG approach — compute in C#, render declaratively:
```razor
@* Compute altitudes every 5 minutes across the night window *@
@* Map time → x, altitude → y, draw polyline *@
@* Draw horizontal line for horizon minimum *@
@* Draw custom horizon profile as a filled area *@
@* Shade twilight periods *@
```

This keeps it pure Blazor with no additional JS chart library.

---

## Yearly Heatmap Component

For each of the 12 months, compute the object's peak altitude and minutes-above-horizon for the 15th of that month. Render as a grid of colored cells (green = great, yellow = marginal, dark = below horizon/below threshold). Pure Blazor SVG or HTML table with inline styles.

---

## Static Asset: NGC Catalog

Copy `AstroPlanner/Assets/Data/NGC.csv` to `AstroPlannerWeb/wwwroot/data/NGC.csv`.

In `CatalogService`, fetch once on startup and cache in memory:
```csharp
protected override async Task OnInitializedAsync()
{
    if (!_catalogLoaded)
        await _catalogService.LoadAsync();
}
```

Register `CatalogService` as a singleton in `Program.cs` so the catalog is only loaded once per app session.

---

## Program.cs Setup

```csharp
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddBlazoredLocalStorage();

// Register services
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<AstronomyService>();
builder.Services.AddSingleton<HorizonService>();
builder.Services.AddSingleton<VisibilityScorer>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<LightPollutionService>();
builder.Services.AddScoped<CometService>();

await builder.Build().RunAsync();
```

---

## Hosting: Cloudflare Pages

1. Push `AstroPlannerWeb` to a GitHub repo (can be the same repo as AstroPlanner in a subfolder, or a new one)
2. Connect to **Cloudflare Pages** → New Project → Connect GitHub
3. Build settings:
   - **Build command**: `dotnet publish -c Release -o publish`
   - **Build output directory**: `publish/wwwroot`
   - **Environment variable**: `DOTNET_VERSION = 10`  
   *(Cloudflare Pages has .NET build support via their build image)*
4. Set custom domain: `astroplanner.natejames.cc` (already in your Cloudflare account)

Alternatively, use **GitHub Actions** to publish to `gh-pages` branch and serve via GitHub Pages — slightly simpler if you want to keep everything in GitHub.

---

## CORS Proxy Cloudflare Worker (for MPC comets + lightpollution)

Create `cors-proxy/worker.js`:
```javascript
export default {
  async fetch(request) {
    const url = new URL(request.url);
    const target = url.searchParams.get('url');
    if (!target) return new Response('Missing url param', { status: 400 });

    const upstream = await fetch(target);
    const body = await upstream.text();

    return new Response(body, {
      headers: {
        'Content-Type': upstream.headers.get('Content-Type') ?? 'text/plain',
        'Access-Control-Allow-Origin': '*',
      },
    });
  }
};
```

Deploy as a Cloudflare Worker at `https://astro-proxy.natejames.cc`. In `CometService` and `LightPollutionService`, prefix MPC/lightpollution URLs with `https://astro-proxy.natejames.cc/?url=`.

---

## Key Differences from Desktop App

| Desktop (Avalonia) | Web (Blazor WASM) |
|---|---|
| Settings file on disk | `localStorage` via Blazored.LocalStorage |
| Horizon CSV opened via file dialog | `<InputFile>` component |
| Aladin Lite via WebView | Aladin Lite via JS interop in a `<div>` |
| `Process.Start` for Stellarium | `JSRuntime.InvokeVoidAsync("open", url)` |
| MVVM ViewModels | Blazor component `@code` blocks with services injected |
| Avalonia data binding | `@bind`, `EventCallback`, `StateHasChanged()` |
| MPC fetch direct | MPC fetch via Cloudflare Worker CORS proxy |
| Bundled catalog in app binary | Catalog fetched from `wwwroot/data/NGC.csv` on startup |

---

## Suggested Build Order

1. **Scaffold + wiring**: project created, packages added, `Program.cs` configured, nav shell with placeholder pages
2. **Models**: port all model classes, `AppSettings`, `SettingsService` with localStorage read/write + import/export
3. **Catalog**: `CatalogService` loading NGC CSV, basic object list rendering on Planner page with type filters
4. **Astronomy core**: `AstronomyService` + `HorizonService` + `VisibilityScorer` — get scores showing up in the list
5. **Altitude plot**: SVG-based `AltitudePlot` component on the detail page
6. **Aladin Lite**: `FovPreview` component with FOV overlay
7. **Yearly heatmap**: `YearlyHeatmap` component
8. **Weather**: `WeatherService` pulling Open-Meteo data
9. **Comets + light pollution**: CORS proxy worker, then `CometService` + `LightPollutionService`
10. **Settings page**: site manager, horizon editor, imaging setups
11. **PWA + polish**: verify service worker caches catalog, mobile layout, dark theme
