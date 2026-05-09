using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AstroPlannerWeb;
using AstroPlannerWeb.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

builder.Services.AddBlazoredLocalStorage();

// In Blazor WASM, scoped = per-session = effectively singleton
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<VisibilityService>();
builder.Services.AddScoped<PlannerStateService>();
builder.Services.AddScoped<YearlyStateService>();

await builder.Build().RunAsync();
