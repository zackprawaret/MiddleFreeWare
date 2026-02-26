using MiddleFreeWare.Api.Models;
using MiddleFreeWare.Api.Services;
using Serilog;

// ── Bootstrap Serilog ──────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Démarrage MiddleFreeWare API...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services));

    // ── Configuration ──────────────────────────────────────────
    builder.Services.Configure<CobolOptions>(
        builder.Configuration.GetSection("Cobol"));

    // ── Services métier ────────────────────────────────────────
    builder.Services.AddSingleton<ICobolRunner, CobolRunner>();
    builder.Services.AddScoped<ICobolProgramService, CobolProgramService>();
    builder.Services.AddScoped<IEmployeService, EmployeService>();

    // ── Controllers ────────────────────────────────────────────
    builder.Services.AddControllers();

    // ── Swagger / OpenAPI ──────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title   = "MiddleFreeWare API",
            Version = "v1",
            Description = """
                API REST middleware entre C# et les programmes COBOL de MainFreem.

                ## Fonctionnalités
                - **CRUD Employés** via PostgreSQL (base partagée avec COBOL)
                - **Calcul de paie** délégué au programme COBOL ex15_calcsal
                - **Exécution directe** de programmes COBOL par nom
                - **Compilation + exécution** de source COBOL à la volée
                - **Health checks** API / PostgreSQL / Docker COBOL

                ## Architecture
                ```
                Client C#  →  REST API (ASP.NET Core)  →  docker exec  →  COBOL (MainFreem)
                                          ↕
                                    PostgreSQL (partagée)
                ```
                """
        });
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    });

    // ── CORS (dev permissif, restreindre en prod) ──────────────
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader()));

    var app = builder.Build();

    // ── Middleware pipeline ────────────────────────────────────
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "MainFreem API v1");
            c.RoutePrefix = string.Empty; // Swagger à la racine
        });
    }

    app.UseCors();
    app.UseAuthorization();
    app.MapControllers();

    // ── Redirect racine vers Swagger ───────────────────────────
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    Log.Information("MiddleFreeWare API prête sur {Urls}", string.Join(", ", builder.WebHost.GetSetting("urls") ?? "http://localhost:5000"));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Erreur fatale au démarrage.");
}
finally
{
    Log.CloseAndFlush();
}
