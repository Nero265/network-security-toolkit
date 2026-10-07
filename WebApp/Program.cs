using Core;
using Core.Jobs;
using Data;
using Microsoft.EntityFrameworkCore;
using WebApp.Background;
using WebApp.Components;
using WebApp.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        //for enum -> string in JSON response
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    }); //must for /api/scan routes

//builder.Services.AddSingleton<IScanJobStore, InMemoryScanJobStore>(); //data live through requests
builder.Services.AddSingleton<IScanJobStore, SqliteScanJobStore>();
//register waiting queue as Singleton ( so controller and worker share the same channel)
builder.Services.AddSingleton<IScanJobQueue, ChannelScanJobQueue>();

//register background worker which listens all the time channel and scans it
builder.Services.AddHostedService<ScanJobRecoveryService>();
builder.Services.AddHostedService<ScanBackgroundWorker>();

builder.Services.Configure<ScanApiOptions>(
    builder.Configuration.GetSection(ScanApiOptions.SectionName));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//register Scanner as Keyed Services using ScanType as base
builder.Services.AddKeyedSingleton<IPortScanner, TcpPortScanner>(ScanType.Tcp);
builder.Services.AddKeyedSingleton<IPortScanner, UdpPortScanner>(ScanType.Udp);

builder.Services.AddSingleton<IScannerFactory, ScannerFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

//app can find our ScanController and lay out its routes
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
public partial class Program { }