using MiddleFreeWare.Api.Models;

namespace MiddleFreeWare.Api.Services;

/// <summary>
/// Service de gestion des programmes COBOL.
/// Permet d'exécuter n'importe quel programme COBOL du dépôt MainFreem
/// en passant des données dynamiques.
/// </summary>
public interface ICobolProgramService
{
    Task<CobolExecutionResult> RunProgramAsync(CobolExecutionRequest request, CancellationToken ct = default);
    Task<CobolExecutionResult> RunSourceAsync(string cobolSource, string programName, CancellationToken ct = default);
    Task<IEnumerable<string>> ListProgramsAsync(CancellationToken ct = default);
}

public class CobolProgramService : ICobolProgramService
{
    private readonly ICobolRunner _runner;
    private readonly ILogger<CobolProgramService> _logger;

    // Programmes autorisés à être exécutés via l'API (whitelist sécurité)
    private static readonly HashSet<string> AllowedPrograms =
    [
        "ex01_hello",
        "ex02_variables",
        "ex03_arithmetique",
        "ex04_conditions",
        "ex05_boucles",
        "ex06_tableaux",
        "ex07_lecture_fichier",
        "ex08_ecriture_fichier",
        "ex09_tri_rupture",
        "ex14_debug",
        "ex15_principal"
    ];

    public CobolProgramService(ICobolRunner runner, ILogger<CobolProgramService> logger)
    {
        _runner = runner;
        _logger = logger;
    }

    // ── Exécuter un programme COBOL par nom ────────────────────
    public async Task<CobolExecutionResult> RunProgramAsync(
        CobolExecutionRequest request,
        CancellationToken ct = default)
    {
        if (!AllowedPrograms.Contains(request.ProgramName))
        {
            return new CobolExecutionResult
            {
                Success = false,
                ProgramName = request.ProgramName,
                ErrorOutput = $"Programme '{request.ProgramName}' non autorisé ou inexistant.",
                ExitCode = -1
            };
        }

        // Convertir le dictionnaire InputData en stdin
        var stdin = BuildStdin(request.InputData);

        _logger.LogInformation("Exécution programme COBOL : {Program}", request.ProgramName);
        return await _runner.ExecuteAsync(request.ProgramName, stdin, ct);
    }

    // ── Exécuter un source COBOL envoyé directement ────────────
    public async Task<CobolExecutionResult> RunSourceAsync(
        string cobolSource,
        string programName,
        CancellationToken ct = default)
    {
        // Sanitize : nom de programme alphanumérique uniquement
        if (!System.Text.RegularExpressions.Regex.IsMatch(programName, @"^[a-zA-Z0-9_\-]+$"))
        {
            return new CobolExecutionResult
            {
                Success = false,
                ProgramName = programName,
                ErrorOutput = "Nom de programme invalide.",
                ExitCode = -1
            };
        }

        _logger.LogInformation("Compilation + exécution source COBOL : {Program}", programName);
        return await _runner.CompileAndExecuteAsync(cobolSource, programName, null, ct);
    }

    // ── Lister les programmes disponibles ─────────────────────
    public Task<IEnumerable<string>> ListProgramsAsync(CancellationToken ct = default)
        => Task.FromResult<IEnumerable<string>>(AllowedPrograms);

    // ── Construire le stdin à partir du dictionnaire ───────────
    private static string? BuildStdin(Dictionary<string, string> inputData)
    {
        if (inputData.Count == 0) return null;
        return string.Join("\n", inputData.Values) + "\n";
    }
}
