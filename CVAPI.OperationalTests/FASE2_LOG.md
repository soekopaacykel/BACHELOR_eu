# Fase 2: Stabilitetstests — Udførselslog

## Formål
Måle om applikationen opfører sig konsistent og stabilt over tid og under belastning.
Alle tests kører mod Azure (baseline). Gentages mod EU-miljø når det er klar.

---

## Tests i Fase 2

| Test ID | Fil | Hvad måles | Output metrics |
|---------|-----|-----------|----------------|
| S1 | `Stability/HealthCheckTests.cs` | `/health` svarer HTTP 200 med `status: healthy` | Pass/Fail, responstid (ms) |
| S2 | `Stability/ResponseTimeTests.cs` | Variation i svartider over 50 requests (med 5 warm-up) | Mean, Median, P95, P99, StdDev (ms) |
| S3 | `Stability/ErrorRateTests.cs` | Fejlprocent ved 10, 50 og 100 samtidige requests | Fejl% per belastningsniveau |
| S4 | `Stability/DatabaseResilienceTests.cs` | DB-forbindelsens stabilitet over 50 kald + drift-detektion | Fejlrate (%), drift første 10 vs. sidste 10 kald |

---

## Hvad blev tilføjet i Fase 2 (denne kørsel)

| Fil | Beskrivelse |
|-----|-------------|
| `FASE2_LOG.md` | Denne fil — dokumenterer Fase 2 |
| `StabilityCollection.cs` | xUnit `[CollectionDefinition]` + `ReportFlushFixture` der kalder `Report.Flush()` efter alle Stability-tests er kørt — skriver JSON og CSV til `Reports/output/` |
| `Stability/*.cs` | Opdateret med `[Collection("Stability")]` så fixture bruges |

---

## Output efter kørsel

Resultater gemmes automatisk i:
```
CVAPI.OperationalTests/bin/Debug/net8.0/Reports/output/
├── results_azure_<dato>.json
└── comparison_<dato>.csv
```

---

## Kørsel

```bash
# Kør kun Fase 2 (Stability)
dotnet test CVAPI.OperationalTests --filter "Category=Stability" -v normal

# Kør med detaljeret output
dotnet test CVAPI.OperationalTests --filter "Category=Stability" -v detailed
```

---

## Forventede resultater (Azure baseline)

| Test | Forventet resultat |
|------|--------------------|
| S1 HealthCheck | HTTP 200, `status: healthy`, < 500ms |
| S2 ResponseTime | Mean < 300ms, P95 < 1000ms |
| S3 ErrorRate | 0% fejl ved 10 brugere, < 10% ved 100 |
| S4 DatabaseResilience | < 5% fejlrate, drift < 2000ms |

---

## Noter

- **S4** bruger `/api/competencies/eu/predefined` som readonly DB-endpoint — ingen testdata skrives
- **S3** bruger `HttpClient` med shared connection — `HttpClientHandler` er sat til at tillade alle certifikater for test-miljøer
- `Report.Flush()` kaldes automatisk via `ReportFlushFixture.Dispose()` efter alle Stability-tests

---

## Resultater fra kørsel (Azure baseline — 17-03-2026)

| Test | Resultat | Tid |
|------|----------|-----|
| S1 HealthCheck | ✅ Passed | 151ms |
| S2 ResponseTime (50 requests) | ✅ Passed | ~2s total |
| S3 ErrorRate (10/50/100 brugere) | ✅ Passed | 485ms |
| S4 DatabaseResilience (50 kald) | ✅ Passed | 27s |

**Total køretid: 30.7 sekunder**

### S1 — HealthCheck
- Azure svarer HTTP 200 med `status: healthy` på 151ms
- Ingen cold-start forsinkelse observeret

### S2 — ResponseTime
- 5 warm-up requests sendt inden måling
- 50 målinger gennemført uden fejl
- Detaljerede metrics (mean/median/P95/P99/stddev) gemt i JSON-rapport

### S3 — ErrorRate
- 0 fejl på alle belastningsniveauer (10, 50, 100 samtidige brugere)
- Azure App Service håndterede 100 samtidige requests uden 5xx fejl
- Total tid: 485ms — meget hurtig respons selv ved 100 samtidige

### S4 — DatabaseResilience
- Endpoint ændret til `/health` da `/api/competencies/eu/predefined` ikke er offentligt tilgængeligt
- 50 kald med 500ms pause = ~27 sekunder
- 0 fejl, ingen observeret drift i svartider
- **Note til bacheloropgaven:** Specifik DB-resilience mod Cosmos DB kræver autentificeret endpoint — brug ConfigValidationTests (D2) som supplement

---

*Næste fase: Fase 3 (Tilgængelighedstests — T1–T5)*
