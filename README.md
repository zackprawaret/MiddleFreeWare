# MiddleFreeWare

> API REST middleware entre C# (ASP.NET Core 8) et les programmes COBOL du projet [MainFreem](../MainFreem).

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-REST_API-blue)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791?logo=postgresql)

---

## Architecture

```
┌─────────────┐     HTTP/JSON      ┌──────────────────────┐     docker exec     ┌─────────────────┐
│  Client C#  │ ──────────────────▶│  Middleware API       │ ───────────────────▶│  COBOL          │
│  (ou autre) │ ◀────────────────── │  ASP.NET Core 8      │ ◀─────────────────── │  GNU COBOL 4.0  │
└─────────────┘                    │  :5000               │                     │  MainFreem      │
                                   └──────────┬───────────┘                     └─────────────────┘
                                              │ SQL
                                              ▼
                                   ┌──────────────────────┐
                                   │  PostgreSQL 16       │
                                   │  (partagée COBOL/C#) │
                                   └──────────────────────┘
```

### Comment ça fonctionne

1. Un **client C#** (ou n'importe quel client HTTP) appelle l'API REST.
2. L'**API middleware** traite la requête :
   - Pour les données : lit/écrit dans **PostgreSQL** (base partagée avec COBOL).
   - Pour les traitements métier COBOL : exécute un **`docker exec`** sur le conteneur MainFreem.
3. Le **programme COBOL** reçoit les données via stdin, traite, renvoie via stdout.
4. L'API parse la réponse COBOL et retourne du **JSON** au client.

---

## Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker + Docker Compose](https://docs.docker.com/get-docker/)
- Image `mainfreem-cobol:latest` construite depuis le projet [MainFreem](../MainFreem)

---

## Démarrage rapide

### 1. Construire l'image COBOL (projet MainFreem)

```bash
cd ../MainFreem
docker build -t mainfreem-cobol .
```

### 2. Lancer tout l'environnement

```bash
cd MiddleFreeWare
docker-compose up -d
```

Cela démarre :
- `middlefreleware` — API C# sur le port **5000**
- `mainfreem-cobol` — conteneur COBOL
- `mainfreem-postgres` — PostgreSQL sur le port **5432**

### 3. Ouvrir Swagger

```
http://localhost:5000
```

### 4. Lancer en développement local (sans Docker)

```bash
cd src/MiddleFreeWare.Api
dotnet run
# Swagger disponible sur http://localhost:5000
```

---

## Endpoints REST

### Employés — `/api/employes`

| Méthode | URL | Description |
|---------|-----|-------------|
| `GET` | `/api/employes` | Liste tous les employés |
| `GET` | `/api/employes/{matricule}` | Récupère un employé |
| `POST` | `/api/employes` | Crée un employé |
| `PATCH` | `/api/employes/{matricule}/salaire` | Met à jour le salaire |
| `DELETE` | `/api/employes/{matricule}` | Supprime un employé |
| `POST` | `/api/employes/{matricule}/paie` | **Calcul de paie via COBOL** |

### COBOL — `/api/cobol`

| Méthode | URL | Description |
|---------|-----|-------------|
| `GET` | `/api/cobol/programs` | Liste les programmes disponibles |
| `POST` | `/api/cobol/run` | **Exécute un programme COBOL** |
| `POST` | `/api/cobol/compile-run` | **Compile et exécute du source COBOL** |

### Health — `/api/health`

| Méthode | URL | Description |
|---------|-----|-------------|
| `GET` | `/api/health` | Statut API + PostgreSQL + Docker COBOL |

---

## Exemples d'utilisation

### Lister les employés

```bash
curl http://localhost:5000/api/employes
```

### Calculer la paie (délégué au COBOL)

```bash
curl -X POST http://localhost:5000/api/employes/EMP001/paie \
  -H "Content-Type: application/json" \
  -d '{ "matricule": "EMP001", "anciennete": 5 }'
```

Réponse :
```json
{
  "success": true,
  "data": {
    "matricule": "EMP001",
    "nomComplet": "Sophie MARTIN",
    "anciennete": 5,
    "salaireBrut": 3675.00,
    "prime": 175.00,
    "charges": 808.50,
    "salaireNet": 2866.50,
    "tauxPrime": 0.05
  }
}
```

### Exécuter un programme COBOL

```bash
curl -X POST http://localhost:5000/api/cobol/run \
  -H "Content-Type: application/json" \
  -d '{ "programName": "ex01_hello", "inputData": {} }'
```

### Compiler et exécuter du source COBOL à la volée

```bash
curl -X POST http://localhost:5000/api/cobol/compile-run \
  -H "Content-Type: application/json" \
  -d '{
    "programName": "test_dynamic",
    "source": "       IDENTIFICATION DIVISION.\n       PROGRAM-ID. TEST.\n       PROCEDURE DIVISION.\n           DISPLAY \"Hello depuis C# !\"\n           STOP RUN."
  }'
```

---

## Structure du projet

```
MiddleFreeWare/
├── docker-compose.yml
├── docker/
│   └── Dockerfile.api                    # Image Docker de l'API
├── src/
│   └── MiddleFreeWare.Api/
│       ├── Controllers/
│       │   ├── CobolController.cs        # Exécution COBOL directe
│       │   ├── EmployesController.cs     # CRUD + calcul paie
│       │   └── HealthController.cs       # Health checks
│       ├── Services/
│       │   ├── CobolRunner.cs            # Cœur : docker exec vers COBOL
│       │   ├── CobolProgramService.cs    # Gestion programmes COBOL
│       │   └── EmployeService.cs         # Logique métier + DB
│       ├── Models/
│       │   └── Models.cs                 # DTOs et modèles
│       ├── Cobol/
│       │   └── ex15_calcsal_api.cobol    # Programme COBOL dédié à l'API
│       ├── Program.cs                    # Configuration et DI
│       └── appsettings.json
└── tests/
    └── MiddleFreeWare.Api.Tests/
        └── CobolProgramServiceTests.cs   # Tests unitaires
```

---

## Configuration

`appsettings.json` :

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=postgres;Port=5432;Database=coboldb;Username=cobol;Password=cobol123"
  },
  "Cobol": {
    "ProgramsPath": "/workspace/exercises",
    "ExecutionTimeoutSeconds": 30,
    "DockerContainerName": "mainfreem-cobol"
  }
}
```

---

## Lancer les tests

```bash
cd tests/MiddleFreeWare.Api.Tests
dotnet test
```

---

## Exercices

Le dossier `exercises/` contient 13 exercices progressifs C# avec corrigés.

📄 **[Exercices](./exercises/MiddleFreeWare_Exercices.pdf)** — Du niveau Novice à Expert

📄 **[Corrigés](./exercises/MiddleFreeWare_Corriges.pdf)** — Solutions complètes et commentées

| Niveau | Ex. | Thèmes |
|--------|-----|--------|
| 🟢 Novice | 1–3 | Premier appel REST, liste employés, POST |
| 🟡 Débutant | 4–6 | CRUD complet, calcul paie COBOL, health check |
| 🟠 Intermédiaire | 7–9 | IHttpClientFactory, COBOL dynamique, batch async |
| 🔴 Avancé | 10–11 | Polly retry/circuit breaker, webhook |
| ⚫ Expert | 12–13 | SDK NuGet, pipeline ETL complet |

## Liens

- Projet COBOL : [../MainFreem](../MainFreem)
- Documentation GNU COBOL : https://gnucobol.sourceforge.io/
