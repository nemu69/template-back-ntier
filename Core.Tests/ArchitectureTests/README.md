# Tests d’architecture

Ces tests (ArchUnitNET + xUnit) **figent les règles du N-tier généré** : noms, héritage, direction des dépendances. Ils tournent en millisecondes, sans base ni HTTP. Si quelqu’un casse une convention, le build échoue au lieu de laisser l’architecture dériver.

Ici **Core** contient entités, DTOs, services, repositories et Unit of Work. Les projets **`Api*`** (Carter) ne font qu’exposer HTTP et injecter les services.

## Lancer les tests

```bash
dotnet test Core.Tests/Core.Tests.csproj --filter "FullyQualifiedName~ArchitectureTests"
```

Préférer une build **Debug** : ArchUnitNET lit les binaires.

## Comment ça sélectionne les types

`GeneratedArchitecture.cs` charge l’assembly **Core** et les hosts référencés (`Api*`). Les groupes ne se basent pas sur le nom de fichier, mais sur les **contrats** :

| Groupe | Sélection |
|--------|-----------|
| Entités | `IBaseEntity<,>` (exclut les owned comme `Address`) |
| DTOs | `IDTO<,>` |
| Services | `IBaseEntityService<,>` hors kernel |
| Repositories | `IBaseEntityRepository<,>` hors kernel |
| Endpoints | `ICarterModule` |

Renommer `DTOMovie` en `MovieDAO` **fait échouer** le test de nommage : la classe implémente encore `IDTO<,>` mais ne commence plus par `DTO`. Renommer seulement le `.cs` sans changer la classe ne change rien (ArchUnitNET voit le type IL).

Les règles marquées « no-op si vide » (`WithoutRequiringPositiveResults`) restent vertes quand le powerpack n’est pas là (ex. SignalR).

---

## Pourquoi chaque fichier

### `LayerDependencyTests` — direction des dépendances

Sans ça, un endpoint finit par appeler un repository, ou Core référence un `ApiMovie`. Les `csproj` bloquent une partie des cycles, pas les fuites **à l’intérieur** de Core (entité → service, DTO → repo).

| Test | Pourquoi |
|------|----------|
| Core ↛ hosts | Core reste réutilisable ; pas de référence inverse vers une API |
| Hosts isolés entre eux | Chaque `Api*` est une slice ; pas de couplage Movie ↔ Cinema au niveau HTTP |
| Entités / DTOs ↛ services / repos | Le modèle reste du data, pas de l’orchestration |
| Services ↛ Carter | La logique métier ne dépend pas de HTTP |
| Repos ↛ services / endpoints | Accès données uniquement |
| Hosts ↛ repos / `AppDbContext` | Les APIs passent par les **services** |

### `NamingConventionTests` — recherche et codegen

Le générateur et les reviews comptent sur des suffixes/préfixes stables. Un `MovieDAO` ou un `CreateMovieHandler` au milieu des `*Service` rend le grep et le scaffolding inutiles.

| Test | Convention |
|------|------------|
| Services / repos / endpoints | `*Service`, `*Repository`, `*Endpoint` |
| DTOs | **préfixe** `DTO` → `DTOMovie` (pas `MovieDTO`) |
| Exceptions | `*Exception` dans `Core.Shared.Exceptions` |
| Interfaces | `I…` ; `IMovieService`, `IMovieRepository` |
| Hubs SignalR | `*Hub` si le pack est présent |

### `InheritanceConventionTests` — CRUD générique

`BaseEntityService`, `BaseEntityRepository` et `BaseEntityEndpoint` portent le CRUD. Sans héritage, on duplique le plumbing et on casse Mapster / UoW / Carter.

| Test | Pourquoi |
|------|----------|
| Entité → `BaseEntity` | ID, TS, concurrence |
| DTO → `DTOBaseEntity` | `ToModel` / `ToDTO` |
| Service / repo → bases génériques | Même contrat CRUD |
| Carter `Api*` → `BaseEndpoint` | `GenericEndpoint` + Problem Details |
| CRUD généré → `BaseEntityEndpoint` | flags Create/Read/Update/Delete |

Les owned (`[Owned] Address`) n’implémentent pas `IBaseEntity` : ils sont hors de ces règles. Un `ApplicationUser` Identity non plus.

### `DependencyGuardTests` — pas d’infra dans le mauvais slice

Même sans référence de projet, un `using Microsoft.EntityFrameworkCore` dans un service ou Carter dans une entité **compile**. Ces tests ferment ce trou.

| Test | Fuite bloquée |
|------|----------------|
| Entités ↛ Carter | HTTP dans le modèle |
| Services ↛ `DbContext` / Carter | EF et HTTP hors service |
| Repos ↛ Carter | HTTP dans la persistence |
| Hosts ↛ `DbContext` | EF reste dans Core |
| Services ↛ `*Repository` concret | résolution via `IAppUOW` |
| Carter ↛ `AppDbContext` | pas de DbContext dans l’endpoint |

### `EndpointConventionTests` — surface HTTP

Tous les modules Carter doivent vivre dans `*.Endpoints`, injecter des **services**, et s’appuyer sur `BaseEndpoint` / `BaseEntityEndpoint` pour le format de réponse.

| Test | Pourquoi |
|------|----------|
| Namespace `*.Endpoints.*` | Même arborescence que le générateur |
| Endpoints ↛ repositories | Pas de shortcut data-access |
| CRUD → `ICarterModule` + `BaseEntityEndpoint` | Contrat généré |
| Endpoint custom → `BaseEndpoint` | Scheduler, fichiers, etc. gardent `GenericEndpoint` |

### `ColocationTests` — paires que ArchUnitNET n’exprime pas

Réflexion + `[Theory]` : « ce service et **son** interface », « cette entité et **son** DTO ».

| Test | Pourquoi |
|------|----------|
| `MovieService` / `IMovieService` même namespace | Un dossier = un contrat |
| Idem repositories | Pareil pour `IMovieRepository` |
| `Movie` ↔ `DTOMovie` dans `*.Models.DTO` | Nom exact `DTO` + nom d’entité |
| `MovieService` → `MovieRepository` à côté | Pas de service orphelin |

Chaque theory s’exécute **une fois par type** (Movie, Cinema, HorrorMovie, ComedyMovie, …).

---

## Ce qui n’est volontairement pas testé

- **Fichiers** : seul le nom du **type** compte.
- **Kernel** (`BaseEntityService` dans `Core.Shared.Services.Kernel`) : exclu des règles « entity service ».
- **Services shared** (`ISchedulerService` si JobScheduler) : pas des `IBaseEntityService`.
- **Powerpacks absents** : règles SignalR / hosts extra = no-op.

## Fichiers

| Fichier | Rôle |
|---------|------|
| `GeneratedArchitecture.cs` | Loader + groupes de types |
| `LayerDependencyTests.cs` | Qui a le droit de dépendre de qui |
| `NamingConventionTests.cs` | Préfixes / suffixes |
| `InheritanceConventionTests.cs` | Bases du CRUD |
| `DependencyGuardTests.cs` | Carter / EF / repos concrets |
| `EndpointConventionTests.cs` | Modules Carter |
| `ColocationTests.cs` | Paires service/interface/repo/DTO |
