using MiddleFreeWare.Api.Models;
using MiddleFreeWare.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MiddleFreeWare.Api.Controllers;

/// <summary>
/// CRUD Employés + calcul de paie via COBOL.
/// Base URL : /api/employes
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EmployesController : ControllerBase
{
    private readonly IEmployeService _service;
    private readonly ILogger<EmployesController> _logger;

    public EmployesController(IEmployeService service, ILogger<EmployesController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    /// <summary>
    /// Liste tous les employés.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<Employe>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var employes = await _service.GetAllAsync(ct);
        return Ok(ApiResponse<IEnumerable<Employe>>.Ok(employes));
    }

    /// <summary>
    /// Récupère un employé par son matricule.
    /// </summary>
    [HttpGet("{matricule}")]
    [ProducesResponseType(typeof(ApiResponse<Employe>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOne(string matricule, CancellationToken ct)
    {
        var employe = await _service.GetByMatriculeAsync(matricule.ToUpper(), ct);
        if (employe == null)
            return NotFound(ApiResponse<Employe>.Fail($"Employé '{matricule}' non trouvé."));

        return Ok(ApiResponse<Employe>.Ok(employe));
    }

    /// <summary>
    /// Crée un nouvel employé.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Employe>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Matricule) || string.IsNullOrWhiteSpace(request.Nom))
            return BadRequest(ApiResponse<Employe>.Fail("Matricule et Nom sont obligatoires."));

        request.Matricule = request.Matricule.ToUpper();

        try
        {
            var created = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetOne), new { matricule = created.Matricule },
                ApiResponse<Employe>.Ok(created, "Employé créé avec succès."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur création employé {Matricule}", request.Matricule);
            return BadRequest(ApiResponse<Employe>.Fail($"Erreur : {ex.Message}"));
        }
    }

    /// <summary>
    /// Met à jour le salaire d'un employé.
    /// </summary>
    [HttpPatch("{matricule}/salaire")]
    [ProducesResponseType(typeof(ApiResponse<Employe>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateSalaire(
        string matricule,
        [FromBody] UpdateSalaireRequest request,
        CancellationToken ct)
    {
        if (request.NouveauSalaire <= 0)
            return BadRequest(ApiResponse<Employe>.Fail("Le salaire doit être positif."));

        var updated = await _service.UpdateSalaireAsync(matricule.ToUpper(), request, ct);
        if (updated == null)
            return NotFound(ApiResponse<Employe>.Fail($"Employé '{matricule}' non trouvé."));

        return Ok(ApiResponse<Employe>.Ok(updated, "Salaire mis à jour."));
    }

    /// <summary>
    /// Supprime un employé.
    /// </summary>
    [HttpDelete("{matricule}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(string matricule, CancellationToken ct)
    {
        var deleted = await _service.DeleteAsync(matricule.ToUpper(), ct);
        if (!deleted)
            return NotFound(ApiResponse<bool>.Fail($"Employé '{matricule}' non trouvé."));

        return Ok(ApiResponse<bool>.Ok(true, "Employé supprimé."));
    }

    /// <summary>
    /// Calcule la fiche de paie via le programme COBOL ex15_calcsal.
    /// Inclut un fallback C# si le conteneur COBOL est indisponible.
    /// </summary>
    [HttpPost("{matricule}/paie")]
    [ProducesResponseType(typeof(ApiResponse<ResultatPaie>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> CalculerPaie(
        string matricule,
        [FromBody] CalculPaieRequest request,
        CancellationToken ct)
    {
        request.Matricule = matricule.ToUpper();

        var resultat = await _service.CalculerPaieAsync(request, ct);
        if (resultat == null)
            return NotFound(ApiResponse<ResultatPaie>.Fail($"Employé '{matricule}' non trouvé."));

        return Ok(ApiResponse<ResultatPaie>.Ok(resultat,
            "Calcul de paie effectué (COBOL ou fallback C#)."));
    }
}
