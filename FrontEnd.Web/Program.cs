using FrontEnd.Core.Configuration;
using FrontEnd.Core.Data;
using FrontEnd.Core.Services;
using FrontEnd.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using static System.Net.WebRequestMethods;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");


builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<FrontEndDataContext>();
builder.Services.AddScoped<CoreSettingsModel>(i => new CoreSettingsModel()
{
    CsvBaseUrl = "https://international-fairy-tale-filmography.github.io/data/csv/"
});
builder.Services.AddScoped<FrontEndDataInitializerService>();



await builder.Build().RunAsync();
