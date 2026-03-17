# Guide til at køre Operationelle Tests

Dette dokument beskriver hvordan du kører de operationelle tests i `CVAPI.OperationalTests`.  
Testene er opdelt i **4 faser** og tester det live Azure-miljø — de kræver ingen lokal server.

---

## Forudsætninger

- [.NET 8 SDK](https://dotnet.microsoft.com/download) installeret
- Adgang til internet (testene rammer live Azure-endpointet)
- Terminal åben i roden af repositoriet (hvor `VEXA.sln` ligger)

---

## Miljøvariabler

| Variabel | Påkrævet | Bruges til |
|---|---|---|
| `TEST_ENV` | Nej (default: `Azure`) | Vælger miljø — `Azure` eller `EU` |
| `TEST_USER_EMAIL` | Kun T6 | Login-email til auth flow-test |
| `TEST_USER_PASSWORD` | Kun T6 | Login-password til auth flow-test |
| `DEPLOY_START_UTC` | Kun D1 | ISO-timestamp for hvornår deployment startede |

Sæt variabler i terminalen inden du kører tests:

```bash
export TEST_ENV=Azure
export TEST_USER_EMAIL="amelia@bepa.dk"
export TEST_USER_PASSWORD="HEJ1234"
```

---

## Kør alle tests

```bash
TEST_ENV=Azure dotnet test CVAPI.OperationalTests
```

---

## Kør én fase ad gangen

### Fase 2 — Stabilitet (S1–S5)

```bash
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "Category=Stability"
```

| Test ID | Navn | Beskrivelse |
|---|---|---|
| S1 | HealthCheckTests | Verificerer at `/health` svarer 200 med korrekt JSON-struktur |
| S2 | ResponseTimeTests | 50 kald — beregner mean, median, P95, P99, standardafvigelse |
| S3 | ErrorRateTests | Concurrent load på 10 / 50 / 100 brugere — fejlrate ≤ 10% |
| S4 | DatabaseResilienceTests | 50 DB-kald — analyserer drift (første vs. sidste 25%) |
| S5 | NetworkLatencyTests | TCP-latens port 443 — 20 gentagelser, avg + jitter |

---

### Fase 3 — Tilgængelighed (T1–T6)

```bash
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "Category=Availability"
```

| Test ID | Navn | Beskrivelse |
|---|---|---|
| T1 | UptimeMonitorTests | Poller `/health` i 1440 min (24 timer) — uptime ≥ 99% |
| T2 | DNSResolutionTests | 20 DNS-opslag med op til 3 retry — måler opløsningstid |
| T3 | SSLCertificateTests | Verificerer SSL-kæde og at certifikat ikke udløber inden for 30 dage |
| T4 | ColdStartTests | Poller til HTTP 200 efter kold start — max 120 sekunder |
| T5 | EndpointAvailabilityTests | Tester alle endpoints fra config — 200/401 = tilgængelig |
| T6 | AuthFlowTests | Login → JWT → kald beskyttet endpoint (kræver env vars) |

> **T1 kører i 24 timer** — kør den separat og lad den stå:
> ```bash
> TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "TestId=T1"
> ```

> **T4 kræver manuel handling** — slå "Always On" fra i Azure App Service inden testen startes,  
> så applikationen faktisk er kold.

---

### Fase 4 — Drift (D1–D4)

```bash
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "Category=Operations"
```

| Test ID | Navn | Beskrivelse |
|---|---|---|
| D1 | DeploymentTimeTests | Poller `/health` til ny version er live — måler deployment-tid |
| D2 | ConfigValidationTests | Verificerer JWT-config og CosmosDB-config (ingen 500-fejl) |
| D3 | LoggingVerificationTests | Trigger requests og noterer tidspunkt — kræver manuel opfølgning i log-platform |
| D4 | DataResidencyTests | GeoIP-opslag på server-IP — verificerer at data er i EU |

#### D1 — Deployment-tid (særlig procedure)

Kør dette **inden** du pusher til git:

```bash
export DEPLOY_START_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
git push
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "TestId=D1"
```

Testen poller automatisk til ny version er live (max 15 minutter).

#### D3 — Logging (manuel opfølgning)

Testen skriver et unikt `testRunId` til rapporten. Åbn Azure Monitor efter testen og søg efter dette ID for at verificere at logs er modtaget.

---

## Kør én enkelt test

```bash
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "TestId=S2"
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "TestId=D4"
```

---

## Sammenlign Azure og EU

Kør testene mod begge miljøer og saml resultaterne:

```bash
TEST_ENV=Azure dotnet test CVAPI.OperationalTests
TEST_ENV=EU dotnet test CVAPI.OperationalTests
```

> Husk at sætte EU-URL'en i `CVAPI.OperationalTests/Config/appsettings.test.json`  
> under `Environments.EU.BaseUrl` inden du kører EU-tests.

---

## Testrapporter

Resultater gemmes automatisk i:

```
CVAPI.OperationalTests/Reports/output/
├── results_azure_<dato>.json
├── results_eu_<dato>.json
└── comparison_<dato>.csv
```

CSV-filen indeholder en samlet sammenligning af begge miljøer og kan åbnes i Excel.

---

## Hurtig reference

```bash
# Alle tests mod Azure
TEST_ENV=Azure dotnet test CVAPI.OperationalTests

# Kun stabilitet
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "Category=Stability"

# Kun tilgængelighed (uden T1 uptime)
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "Category=Availability&TestId!=T1"

# Kun drift
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "Category=Operations"

# Data residency (vigtig for konklusion)
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --filter "TestId=D4"

# Verbose output
TEST_ENV=Azure dotnet test CVAPI.OperationalTests --logger "console;verbosity=detailed"
```
