using MiddleFreeWare.Api.Models;
using MiddleFreeWare.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MiddleFreeWare.Api.Controllers;

/// <summary>
/// Exécution directe de programmes COBOL via l'API.
/// Base URL : /api/cobol
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CobolController : ControllerBase
{
    private readonly ICobolProgramService _programService;
    private readonly ILogger<CobolController> _logger;

    public CobolController(ICobolProgramService programService, ILogger<CobolController> logger)
    {
        _programService = programService;
        _logger = logger;
    }

    /// <summary>
    /// Liste tous les programmes COBOL disponibles.
    /// </summary>
    [HttpGet("programs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<string>>), 200)]
    public async Task<IActionResult> ListPrograms(CancellationToken ct)
    {
        var programs = await _programService.ListProgramsAsync(ct);
        return Ok(ApiResponse<IEnumerable<string>>.Ok(programs));
    }

    /// <summary>
    /// Exécute un programme COBOL existant dans MainFreem.
    /// Les inputData sont passés en stdin au programme COBOL.
    /// </summary>
    /// <remarks>
    /// Exemple de body :
    /// {
    ///   "programName": "ex04_conditions",
    ///   "inputData": { "note": "14" }
    /// }
    /// </remarks>
    [HttpPost("run")]
    [ProducesResponseType(typeof(ApiResponse<CobolExecutionResult>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> RunProgram(
        [FromBody] CobolExecutionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ProgramName))
            return BadRequest(ApiResponse<CobolExecutionResult>.Fail("ProgramName est obligatoire."));

        _logger.LogInformation("API RunProgram : {Program}", request.ProgramName);

        var result = await _programService.RunProgramAsync(request, ct);

        if (!result.Success && result.ExitCode == -1)
            return BadRequest(ApiResponse<CobolExecutionResult>.Fail(result.ErrorOutput));

        return Ok(ApiResponse<CobolExecutionResult>.Ok(result));
    }

    /// <summary>
    /// Compile et exécute un source COBOL envoyé directement dans le body.
    /// Utile pour tester du code COBOL à la volée depuis un client C#.
    /// </summary>
    /// <remarks>
    /// Exemple de body :
    /// {
    ///   "source": "       IDENTIFICATION DIVISION.\n       PROGRAM-ID. TEST.\n       PROCEDURE DIVISION.\n           DISPLAY 'Hello from API!'.\n           STOP RUN.",
    ///   "programName": "test_api"
    /// }
    /// </remarks>
    [HttpPost("compile-run")]
    [ProducesResponseType(typeof(ApiResponse<CobolExecutionResult>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CompileAndRun(
        [FromBody] CompileRunRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Source))
            return BadRequest(ApiResponse<CobolExecutionResult>.Fail("Source COBOL vide."));

        if (string.IsNullOrWhiteSpace(request.ProgramName))
            request.ProgramName = $"prog_{DateTime.UtcNow:yyyyMMddHHmmss}";

        _logger.LogInformation("API CompileRun : {Program}", request.ProgramName);

        var result = await _programService.RunSourceAsync(request.Source, request.ProgramName, ct);
        return Ok(ApiResponse<CobolExecutionResult>.Ok(result));
    }
}

// ── Request model pour compile-run ────────────────────────────
public class CompileRunRequest
{
    public string Source { get; set; } = string.Empty;
    public string? ProgramName { get; set; }
}
