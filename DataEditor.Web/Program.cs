using DataEditor.Core.Services;
using DataEditor.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SqliteWasmBlazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddDbContextFactory<DataEditorDataContext>(options =>
{
    var connection = new SqliteWasmConnection("Data Source=data-editor.db");
    options.UseSqliteWasm(connection);
});
builder.Services.AddSingleton<IDBInitializationService, DBInitializationService>();

builder.Services.AddTransient<DataEditorDataContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<DataEditorDataContext>>()
      .CreateDbContext());

builder.Services.AddMudServices();
builder.Services.AddScoped<GitService>();
builder.Services.AddTransient<GitEntityService>();
builder.Services.AddSingleton<CoreSettingsModel>(i => new CoreSettingsModel()
{
    Branch = "drafts",
    Folder = "csv",
    Owner = "International-Fairy-Tale-Filmography",
    RepoName = "data"
});
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var app = builder.Build();
await app.Services.InitializeSqliteWasmDatabaseAsync<DataEditorDataContext>();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataEditorDataContext>>();
    await using var dbContext = await dbFactory.CreateDbContextAsync();
    await dbContext.Database.EnsureCreatedAsync();
}

await app.RunAsync();
