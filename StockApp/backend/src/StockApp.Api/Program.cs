using StockApp.Api.ErrorHandling;
using StockApp.Api.Serialization;
using StockApp.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Service registration. Spring discovers beans by scanning; here every dependency is declared,
// so the full object graph of the application is visible in this one file.
builder.Services.AddControllers();

// How published prices are reduced: four decimal places rounded away from zero unless configured
// otherwise. Validated at startup so a bad setting fails the process rather than the first request.
builder.Services
    .AddOptions<PricePrecisionOptions>()
    .Bind(builder.Configuration.GetSection(PricePrecisionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.ConfigureOptions<ConfigurePriceSerialization>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// RFC 9457 problem responses, plus the handler that decides what each market data failure looks like.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<MarketDataExceptionHandler>();

// The market data stack: domain services plus the Yahoo-backed provider.
builder.Services.AddYahooFinanceMarketData(builder.Configuration);

// The frontend is served from its own origin during development, so it needs explicit permission
// to call this API from the browser. Allowed origins come from configuration, never hardcoded.
const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .WithHeaders("Content-Type")
    .WithMethods("GET")
    .WithExposedHeaders("X-Grouping-Timezone", "X-Grouping-Timezone-Fallback")));

var app = builder.Build();

// Middleware pipeline. Order matters: each call wraps the ones registered after it, so the
// exception handler goes first and therefore sees failures from everything below it.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(FrontendCorsPolicy);
app.MapControllers();

app.Run();

// Exposes the implicitly generated Program class to the integration test project,
// so WebApplicationFactory<Program> can boot this exact application.
public partial class Program;
