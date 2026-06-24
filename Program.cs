using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DndCompanion;
using DndCompanion.Services;
using DndCompanion.Services.Repositories;

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

// Repository per aggregato (sotto-fase A): l'accesso dati, prima nel god-object SupabaseService,
// è ora dietro interfacce. Singleton perché dipendono dal Singleton SupabaseService (provider del client).
builder.Services.AddSingleton<ICharacterRepository, CharacterRepository>();
builder.Services.AddSingleton<ISpellRepository, SpellRepository>();
builder.Services.AddSingleton<IMonsterRepository, MonsterRepository>();
builder.Services.AddSingleton<INoteRepository, NoteRepository>();
builder.Services.AddSingleton<ICombatStateRepository, CombatStateRepository>();
builder.Services.AddSingleton<IProfileRepository, ProfileRepository>();
builder.Services.AddSingleton<IRaceRepository, RaceRepository>();
builder.Services.AddSingleton<IClassRepository, ClassRepository>();
builder.Services.AddSingleton<IInventoryRepository, InventoryRepository>();
builder.Services.AddSingleton<ICharacterSpellRepository, CharacterSpellRepository>();
builder.Services.AddSingleton<ICampaignRepository, CampaignRepository>();

await builder.Build().RunAsync();
