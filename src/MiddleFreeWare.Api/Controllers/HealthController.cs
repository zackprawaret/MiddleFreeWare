using MiddleFreeWare.Api.Models;
using MiddleFreeWare.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace MiddleFreeWare.Api.Controllers;

/// <summary>
/// Health checks : API, PostgreSQL, Docker COBOL.
/// Base URL : /api/health
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ICobolRunner _cobolRunner;
    private readonly IConfiguration _config;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ICobolRunner cobolRunner,
        IConfiguration config,
        ILogger<HealthController> logger)
    {
        _cobolRunner = cobolRunner;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Vérifie l'état de tous les composants du système.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthStatus>), 200)]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var status = new HealthStatus
        {
            Api       = true,
            CheckedAt = DateTime.UtcNow
        };

        // Check PostgreSQL
        status.PostgreSQL = await CheckPostgreSQLAsync(ct);

        // Check Docker + COBOL container
        status.CobolDocker = await CheckCobolDockerAsync(ct);

        status.AllHealthy = status.Api && status.PostgreSQL && status.CobolDocker;

        var httpStatus = status.AllHealthy ? 200 : 503;
        return StatusCode(httpStatus, ApiResponse<HealthStatus>.Ok(status,
            status.AllHealthy ? "Tous les services sont opérationnels." : "Certains services sont dégradés."));
    }

    private async Task<bool> CheckPostgreSQLAsync(CancellationToken ct)
    {
        try
        {
            var cs = _config.GetConnectionString("PostgreSQL")!;
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PostgreSQL indisponible : {Msg}", ex.Message);
            return false;
        }
    }

    private async Task<bool> CheckCobolDockerAsync(CancellationToken ct)
    {
        try
        {
            // Exécute un programme COBOL minimaliste pour tester le conteneur
            var result = await _cobolRunner.ExecuteAsync("ex01_hello", null, ct);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Conteneur COBOL indisponible : {Msg}", ex.Message);
            return false;
        }
    }
}

public class HealthStatus
{
    public bool AllHealthy    { get; set; }
    public bool Api           { get; set; }
    public bool PostgreSQL    { get; set; }
    public bool CobolDocker   { get; set; }
    public DateTime CheckedAt { get; set; }
}
