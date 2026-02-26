using Dapper;
using MiddleFreeWare.Api.Models;
using Npgsql;

namespace MiddleFreeWare.Api.Services;

/// <summary>
/// Service métier Employé.
/// — Les opérations CRUD passent par PostgreSQL (partagée avec COBOL).
/// — Le calcul de paie délègue au programme COBOL ex15.
/// </summary>
public interface IEmployeService
{
    Task<IEnumerable<Employe>> GetAllAsync(CancellationToken ct = default);
    Task<Employe?> GetByMatriculeAsync(string matricule, CancellationToken ct = default);
    Task<Employe> CreateAsync(CreateEmployeRequest request, CancellationToken ct = default);
    Task<Employe?> UpdateSalaireAsync(string matricule, UpdateSalaireRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string matricule, CancellationToken ct = default);
    Task<ResultatPaie?> CalculerPaieAsync(CalculPaieRequest request, CancellationToken ct = default);
}

public class EmployeService : IEmployeService
{
    private readonly string _connectionString;
    private readonly ICobolRunner _cobolRunner;
    private readonly ILogger<EmployeService> _logger;

    public EmployeService(
        IConfiguration config,
        ICobolRunner cobolRunner,
        ILogger<EmployeService> logger)
    {
        _connectionString = config.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("ConnectionString PostgreSQL manquante.");
        _cobolRunner = cobolRunner;
        _logger = logger;
    }

    // ── READ : tous les employés ───────────────────────────────
    public async Task<IEnumerable<Employe>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT matricule, nom, prenom, code_svc AS CodeService,
                   salaire, statut
            FROM employes
            ORDER BY nom, prenom
            """;
        return await conn.QueryAsync<Employe>(new CommandDefinition(sql, cancellationToken: ct));
    }

    // ── READ : un employé par matricule ───────────────────────
    public async Task<Employe?> GetByMatriculeAsync(string matricule, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            SELECT matricule, nom, prenom, code_svc AS CodeService,
                   salaire, statut
            FROM employes
            WHERE matricule = @matricule
            """;
        return await conn.QueryFirstOrDefaultAsync<Employe>(
            new CommandDefinition(sql, new { matricule }, cancellationToken: ct));
    }

    // ── CREATE ─────────────────────────────────────────────────
    public async Task<Employe> CreateAsync(CreateEmployeRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            INSERT INTO employes (matricule, nom, prenom, code_svc, salaire, statut)
            VALUES (@Matricule, @Nom, @Prenom, @CodeService, @Salaire, 'A')
            RETURNING matricule, nom, prenom, code_svc AS CodeService, salaire, statut
            """;
        var created = await conn.QuerySingleAsync<Employe>(
            new CommandDefinition(sql, req, cancellationToken: ct));

        _logger.LogInformation("Employé créé : {Matricule}", created.Matricule);
        return created;
    }

    // ── UPDATE salaire ─────────────────────────────────────────
    public async Task<Employe?> UpdateSalaireAsync(string matricule, UpdateSalaireRequest req, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = """
            UPDATE employes
            SET salaire = @NouveauSalaire
            WHERE matricule = @matricule
            RETURNING matricule, nom, prenom, code_svc AS CodeService, salaire, statut
            """;
        var updated = await conn.QueryFirstOrDefaultAsync<Employe>(
            new CommandDefinition(sql, new { matricule, req.NouveauSalaire }, cancellationToken: ct));

        if (updated != null)
            _logger.LogInformation("Salaire mis à jour : {Matricule} → {Salaire}", matricule, req.NouveauSalaire);

        return updated;
    }

    // ── DELETE ─────────────────────────────────────────────────
    public async Task<bool> DeleteAsync(string matricule, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM employes WHERE matricule = @matricule";
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { matricule }, cancellationToken: ct));

        _logger.LogInformation("Employé supprimé : {Matricule}", matricule);
        return rows > 0;
    }

    // ── CALCUL PAIE via COBOL ──────────────────────────────────
    /// <summary>
    /// Délègue le calcul de paie au programme COBOL ex15_calcsal.
    /// Envoie les données en stdin, parse le stdout structuré.
    /// </summary>
    public async Task<ResultatPaie?> CalculerPaieAsync(CalculPaieRequest req, CancellationToken ct = default)
    {
        // 1. Récupérer l'employé
        var employe = await GetByMatriculeAsync(req.Matricule, ct);
        if (employe == null) return null;

        _logger.LogInformation("Calcul paie COBOL pour {Matricule}", req.Matricule);

        // 2. Appeler le programme COBOL dédié au calcul de paie
        //    On lui passe les données en stdin au format CSV simple
        var stdinData = $"{employe.Matricule},{employe.Salaire},{req.Anciennete}\n";
        var cobolResult = await _cobolRunner.ExecuteAsync("ex15_calcsal_api", stdinData, ct);

        if (!cobolResult.Success)
        {
            _logger.LogWarning("COBOL calcul paie échoué : {Error}", cobolResult.ErrorOutput);
            // Fallback : calcul C# si COBOL indisponible
            return CalculerPaieLocal(employe, req.Anciennete);
        }

        // 3. Parser la sortie COBOL (format: BRUT|PRIME|CHARGES|NET)
        return ParseResultatPaieCobol(cobolResult.Output, employe, req.Anciennete)
               ?? CalculerPaieLocal(employe, req.Anciennete);
    }

    // ── Fallback C# si COBOL indisponible ──────────────────────
    private static ResultatPaie CalculerPaieLocal(Employe employe, int anciennete)
    {
        var tauxPrime = Math.Min(anciennete * 0.01m, 0.15m);
        var prime     = employe.Salaire * tauxPrime;
        var brut      = employe.Salaire + prime;
        var charges   = brut * 0.22m;
        var net       = brut - charges;

        return new ResultatPaie
        {
            Matricule   = employe.Matricule,
            NomComplet  = $"{employe.Prenom} {employe.Nom}",
            Anciennete  = anciennete,
            SalaireBrut = Math.Round(brut, 2),
            Prime       = Math.Round(prime, 2),
            Charges     = Math.Round(charges, 2),
            SalaireNet  = Math.Round(net, 2),
            TauxPrime   = tauxPrime
        };
    }

    // ── Parser sortie COBOL ─────────────────────────────────────
    private static ResultatPaie? ParseResultatPaieCobol(string output, Employe employe, int anciennete)
    {
        // Format attendu du COBOL : "BRUT=3850.00|PRIME=350.00|CHARGES=847.00|NET=3003.00"
        try
        {
            var parts = output.Trim().Split('|');
            if (parts.Length < 4) return null;

            decimal ParseVal(string part) =>
                decimal.Parse(part.Split('=')[1].Trim());

            return new ResultatPaie
            {
                Matricule   = employe.Matricule,
                NomComplet  = $"{employe.Prenom} {employe.Nom}",
                Anciennete  = anciennete,
                SalaireBrut = ParseVal(parts[0]),
                Prime       = ParseVal(parts[1]),
                Charges     = ParseVal(parts[2]),
                SalaireNet  = ParseVal(parts[3]),
                TauxPrime   = Math.Min(anciennete * 0.01m, 0.15m)
            };
        }
        catch { return null; }
    }
}
