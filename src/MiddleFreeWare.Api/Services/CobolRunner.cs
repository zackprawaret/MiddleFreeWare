using System.Diagnostics;
using System.Text;
using MiddleFreeWare.Api.Models;
using Microsoft.Extensions.Options;

namespace MiddleFreeWare.Api.Services;

/// <summary>
/// Service central du middleware : compile et exécute les programmes COBOL
/// via docker exec sur le conteneur MainFreem.
/// </summary>
public interface ICobolRunner
{
    Task<CobolExecutionResult> ExecuteAsync(string programName, string? stdinInput = null, CancellationToken ct = default);
    Task<CobolExecutionResult> CompileAndExecuteAsync(string cobolSource, string programName, string? stdinInput = null, CancellationToken ct = default);
    Task<bool> IsProgramCompiledAsync(string programName, CancellationToken ct = default);
}

public class CobolRunner : ICobolRunner
{
    private readonly CobolOptions _options;
    private readonly ILogger<CobolRunner> _logger;

    public CobolRunner(IOptions<CobolOptions> options, ILogger<CobolRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Exécute un programme COBOL déjà compilé dans le conteneur Docker MainFreem.
    /// Utilise "docker exec" pour lancer le binaire directement.
    /// </summary>
    public async Task<CobolExecutionResult> ExecuteAsync(
        string programName,
        string? stdinInput = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var programPath = $"{_options.ProgramsPath}/{programName}";

        _logger.LogInformation("Exécution COBOL : {Program}", programName);

        // Commande : docker exec <container> <binary>
        var dockerArgs = $"exec -i {_options.DockerContainerName} {programPath}";

        var result = await RunProcessAsync("docker", dockerArgs, stdinInput, ct);
        result.ProgramName = programName;
        result.ExecutionTimeMs = sw.ElapsedMilliseconds;

        _logger.LogInformation(
            "COBOL {Program} terminé en {Ms}ms — ExitCode: {Code}",
            programName, result.ExecutionTimeMs, result.ExitCode);

        return result;
    }

    /// <summary>
    /// Compile un source COBOL à la volée dans le conteneur, puis l'exécute.
    /// Utile pour les programmes dynamiques envoyés via l'API.
    /// </summary>
    public async Task<CobolExecutionResult> CompileAndExecuteAsync(
        string cobolSource,
        string programName,
        string? stdinInput = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Compilation + exécution COBOL : {Program}", programName);

        // 1. Écrire le source dans un fichier temporaire dans le conteneur
        var tempSrc  = $"/tmp/{programName}.cobol";
        var tempBin  = $"/tmp/{programName}";

        var writeCmd = $"exec {_options.DockerContainerName} bash -c \"cat > {tempSrc}\"";
        var writeResult = await RunProcessAsync("docker", writeCmd, cobolSource, ct);
        if (writeResult.ExitCode != 0)
            return new CobolExecutionResult
            {
                Success = false,
                ProgramName = programName,
                ErrorOutput = "Impossible d'écrire le source COBOL dans le conteneur.",
                ExitCode = -1,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };

        // 2. Compiler
        var compileCmd = $"exec {_options.DockerContainerName} cobc -x {tempSrc} -o {tempBin}";
        var compileResult = await RunProcessAsync("docker", compileCmd, null, ct);
        if (compileResult.ExitCode != 0)
            return new CobolExecutionResult
            {
                Success = false,
                ProgramName = programName,
                ErrorOutput = $"Erreur de compilation :\n{compileResult.ErrorOutput}",
                Output = compileResult.Output,
                ExitCode = compileResult.ExitCode,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };

        // 3. Exécuter
        var execCmd = $"exec -i {_options.DockerContainerName} {tempBin}";
        var execResult = await RunProcessAsync("docker", execCmd, stdinInput, ct);
        execResult.ProgramName = programName;
        execResult.ExecutionTimeMs = sw.ElapsedMilliseconds;

        _logger.LogInformation(
            "COBOL {Program} compile+exécuté en {Ms}ms", programName, sw.ElapsedMilliseconds);

        return execResult;
    }

    /// <summary>
    /// Vérifie si un binaire COBOL existe déjà dans le conteneur.
    /// </summary>
    public async Task<bool> IsProgramCompiledAsync(string programName, CancellationToken ct = default)
    {
        var checkCmd = $"exec {_options.DockerContainerName} test -f {_options.ProgramsPath}/{programName}";
        var result = await RunProcessAsync("docker", checkCmd, null, ct);
        return result.ExitCode == 0;
    }

    // ── Utilitaire interne : lance un process et capture stdout/stderr ──
    private async Task<CobolExecutionResult> RunProcessAsync(
        string command,
        string arguments,
        string? stdinInput,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
            CreateNoWindow  = true
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Envoyer stdin si besoin
        if (!string.IsNullOrEmpty(stdinInput))
        {
            await process.StandardInput.WriteAsync(stdinInput);
        }
        process.StandardInput.Close();

        // Attendre avec timeout
        var timeout = TimeSpan.FromSeconds(_options.ExecutionTimeoutSeconds);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            return new CobolExecutionResult
            {
                Success = false,
                ErrorOutput = $"Timeout : le programme n'a pas répondu en {timeout.TotalSeconds}s.",
                ExitCode = -1
            };
        }

        var exitCode = process.ExitCode;
        return new CobolExecutionResult
        {
            Success     = exitCode == 0,
            Output      = stdout.ToString(),
            ErrorOutput = stderr.ToString(),
            ExitCode    = exitCode
        };
    }
}
