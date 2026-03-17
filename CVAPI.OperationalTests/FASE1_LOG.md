# Fase 1: Forberedelse og projektstruktur — Udførselslog

## Formål
Dokumenterer hvad der er oprettet og verificeret i Fase 1 af den operationelle testplan.
Testmiljøet befinder sig i `CVAPI.OperationalTests`.

---

## Hvad var allerede på plads (fra tidligere arbejde)

| Fil | Beskrivelse |
|-----|-------------|
| `CVAPI.OperationalTests.csproj` | xUnit testprojekt med alle nødvendige dependencies (xunit, FluentAssertions, Microsoft.NET.Test.Sdk, Microsoft.Extensions.Configuration) |
| `Config/TestEnvironment.cs` | Enum med `Azure` og `EU` miljøer |
| `Config/TestConfig.cs` | Singleton der loader `appsettings.test.json`. Miljø styres via `TEST_ENV` environment variable (default: Azure) |
| `Config/appsettings.test.json` | URLs, endpoints og testindstillinger for begge miljøer |
| `Reports/TestResult.cs` | Fælles resultatmodel brugt af alle tests |
| `Reports/TestReportGenerator.cs` | Skriver JSON og CSV output til `Reports/output/` |
| `CVAPI/Program.cs` | `/health` endpoint tilføjet: returnerer status, timestamp, version og environment |

---

## Hvad blev oprettet i Fase 1 (denne kørsel)

### Stability/
| Fil | Test ID | Hvad testes |
|-----|---------|-------------|
| `Stability/HealthCheckTests.cs` | S1 | `/health` svarer HTTP 200 med korrekt JSON for begge miljøer |
| `Stability/ResponseTimeTests.cs` | S2 | 50 requests — beregner mean, median, P95, P99 og standardafvigelse |
| `Stability/ErrorRateTests.cs` | S3 | Fejlprocent ved 10, 50 og 100 samtidige requests |
| `Stability/DatabaseResilienceTests.cs` | S4 | Stabiliteten af DB-forbindelsen over tid via readonly endpoint |

### Availability/
| Fil | Test ID | Hvad testes |
|-----|---------|-------------|
| `Availability/UptimeMonitorTests.cs` | T1 | Uptime % over måleperiode (polling hvert 30. sekund) |
| `Availability/EndpointAvailabilityTests.cs` | T5 | Alle kendte API-endpoints svarer HTTP 200 eller 401 |
| `Availability/SSLCertificateTests.cs` | T3 | SSL-certifikatets gyldighed, kæde og dage til udløb |
| `Availability/DNSResolutionTests.cs` | T2 | DNS-opslagstid (gennemsnit over 20 målinger) |
| `Availability/ColdStartTests.cs` | T4 | Tid til første succesfulde response efter idle |

### Operations/
| Fil | Test ID | Hvad testes |
|-----|---------|-------------|
| `Operations/DeploymentTimeTests.cs` | D1 | Poller `/health` version-felt for at opdage ny deployment |
| `Operations/ConfigValidationTests.cs` | D2 | Endpoints der kræver JWT og DB er tilgængelige (ingen config-fejl) |
| `Operations/LoggingVerificationTests.cs` | D3 | Trigger request og verificér at API svarer (log-latens måles manuelt) |

---

## Mappestruktur efter Fase 1

```
CVAPI.OperationalTests/
├── Config/
│   ├── TestEnvironment.cs
│   ├── TestConfig.cs
│   └── appsettings.test.json
├── Stability/
│   ├── HealthCheckTests.cs
│   ├── ResponseTimeTests.cs
│   ├── ErrorRateTests.cs
│   └── DatabaseResilienceTests.cs
├── Availability/
│   ├── UptimeMonitorTests.cs
│   ├── EndpointAvailabilityTests.cs
│   ├── SSLCertificateTests.cs
│   ├── DNSResolutionTests.cs
│   └── ColdStartTests.cs
├── Operations/
│   ├── DeploymentTimeTests.cs
│   ├── ConfigValidationTests.cs
│   └── LoggingVerificationTests.cs
├── Reports/
│   ├── TestResult.cs
│   ├── TestReportGenerator.cs
│   └── output/           ← genereres ved kørsel
├── FASE1_LOG.md          ← denne fil
└── CVAPI.OperationalTests.csproj
```

---

## Kørsel

```bash
# Kør alle tests mod Azure (default)
dotnet test CVAPI.OperationalTests

# Kør mod EU-alternativ
TEST_ENV=EU dotnet test CVAPI.OperationalTests

# Kør kun stabilitetstests
dotnet test CVAPI.OperationalTests --filter "Category=Stability"

# Kør kun tilgængelighedstests
dotnet test CVAPI.OperationalTests --filter "Category=Availability"
```

---

## Forudsætninger inden fuld kørsel

- [x] `/health` endpoint tilføjet til CVAPI
- [x] Azure BaseUrl konfigureret i `appsettings.test.json`
- [ ] EU BaseUrl udfyldes når EU-miljø er klar (`<EU-ALTERNATIV-URL>`)
- [ ] `UptimeMonitorTests` kræver lang køretid (1440 min) — kør separat

---

*Næste fase: Fase 2 (Stabilitetstests) og Fase 3 (Tilgængelighedstests) implementeres med fulde assertions og Report.Record() kald.*
