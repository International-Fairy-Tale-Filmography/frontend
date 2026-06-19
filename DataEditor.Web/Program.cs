using DataEditor.Core.Services;
using DataEditor.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SqliteWasmHelper;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSqliteWasmDbContextFactory<DataEditorDataContext>(options =>
    options.UseSqlite("Data Source=data-editor.db"));

builder.Services.AddScoped<DataEditorDataContext>(sp =>
    sp.GetRequiredService<ISqliteWasmDbContextFactory<DataEditorDataContext>>()
      .CreateDbContextAsync()
      .GetAwaiter()
      .GetResult());

builder.Services.AddMudServices();
builder.Services.AddScoped<GitService>();
builder.Services.AddScoped<GitEntityService>();
builder.Services.AddSingleton<CoreSettingsModel>(i => new CoreSettingsModel()
{
    Branch = "drafts",
    Folder = "csv",
    Owner = "International-Fairy-Tale-Filmography",
    RepoName = "data"
});
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var app = builder.Build();
await app.RunAsync();
