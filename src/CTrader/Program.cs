using CTrader.Components;
using CTrader.Data;
using CTrader.Services.Analysis;
using CTrader.Services.Analysis.LlmClients;
using CTrader.Services.Configuration;
using CTrader.Services.News;
using CTrader.Services.Logging;
using CTrader.Services.Risk;
using CTrader.Services.Trading;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=data/ctrader.db";
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// HTTP Clients
builder.Services.AddHttpClient<FinnhubClient>();
builder.Services.AddHttpClient<AlphaVantageClient>();
builder.Services.AddHttpClient<RssFeedClient>();
builder.Services.AddHttpClient<AnthropicLlmClient>();

// Services
builder.Services.AddScoped<IParameterService, ParameterService>();
builder.Services.AddScoped<IActivityLogger, ActivityLogger>();
builder.Services.AddScoped<INewsAggregator, NewsAggregator>();
builder.Services.AddScoped<ILlmClient, AnthropicLlmClient>();
builder.Services.AddScoped<IMarketAnalyzer, MarketAnalyzer>();
builder.Services.AddScoped<CostCalculator>();
builder.Services.AddScoped<IRiskManager, RiskManager>();

// Trading (Singleton for background service)
builder.Services.AddSingleton<IBrokerConnector, IbGatewayConnector>();
builder.Services.AddSingleton<TradingService>();
builder.Services.AddSingleton<ITradingService>(sp => sp.GetRequiredService<TradingService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<TradingService>());

var app = builder.Build();

// Ensure database is created and migrations applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
