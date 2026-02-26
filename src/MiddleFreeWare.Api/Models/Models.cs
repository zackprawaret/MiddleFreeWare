namespace MiddleFreeWare.Api.Models;

// ── Réponse générique de l'API ─────────────────────────────────
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = [];

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Errors = [error] };

    public static ApiResponse<T> Fail(List<string> errors) =>
        new() { Success = false, Errors = errors };
}

// ── Résultat d'exécution COBOL ─────────────────────────────────
public class CobolExecutionResult
{
    public bool Success { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public string ErrorOutput { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public long ExecutionTimeMs { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

// ── Requête d'exécution COBOL ──────────────────────────────────
public class CobolExecutionRequest
{
    public string ProgramName { get; set; } = string.Empty;
    public Dictionary<string, string> InputData { get; set; } = [];
}

// ── Modèle Employé ─────────────────────────────────────────────
public class Employe
{
    public string Matricule { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string? CodeService { get; set; }
    public decimal Salaire { get; set; }
    public string Statut { get; set; } = "A";
}

public class CreateEmployeRequest
{
    public string Matricule { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string? CodeService { get; set; }
    public decimal Salaire { get; set; }
}

public class UpdateSalaireRequest
{
    public decimal NouveauSalaire { get; set; }
    public string? Motif { get; set; }
}

// ── Modèle Service ─────────────────────────────────────────────
public class Service
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public int NbEmployes { get; set; }
    public decimal MasseSalariale { get; set; }
}

// ── Résultat calcul de paie (depuis COBOL) ─────────────────────
public class ResultatPaie
{
    public string Matricule { get; set; } = string.Empty;
    public string NomComplet { get; set; } = string.Empty;
    public int Anciennete { get; set; }
    public decimal SalaireBrut { get; set; }
    public decimal Prime { get; set; }
    public decimal Charges { get; set; }
    public decimal SalaireNet { get; set; }
    public decimal TauxPrime { get; set; }
    public DateTime CalculeAt { get; set; } = DateTime.UtcNow;
}

public class CalculPaieRequest
{
    public string Matricule { get; set; } = string.Empty;
    public int Anciennete { get; set; }
}

// ── Résultat statistiques (depuis COBOL) ──────────────────────
public class StatistiquesNotes
{
    public List<int> Notes { get; set; } = [];
    public double Moyenne { get; set; }
    public int NoteMax { get; set; }
    public int NoteMin { get; set; }
    public string MentionMoyenne { get; set; } = string.Empty;
}

// ── Configuration COBOL ────────────────────────────────────────
public class CobolOptions
{
    public string ProgramsPath { get; set; } = "/workspace/exercises";
    public int ExecutionTimeoutSeconds { get; set; } = 30;
    public string DockerContainerName { get; set; } = "mainfreem-cobol";
}
