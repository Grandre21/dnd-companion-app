using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DndCompanion;
using DndCompanion.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<SupabaseService>();
builder.Services.AddSingleton<AuthStateService>();
builder.Services.AddSingleton<CampaignStateService>();
builder.Services.AddSingleton<PwaUpdateService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<ConfirmService>();

await builder.Build().RunAsync();
