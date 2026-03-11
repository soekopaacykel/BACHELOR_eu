# Operationel Testplan: Azure vs. EU-alternativ

**Formål:** Kvantitativt sammenligne drift, stabilitet og tilgængelighed mellem det nuværende Azure-setup og en fremtidig europæisk cloudarkitektur.

**Problemformulering (operationalisering 2):**
> Hvilke forskelle i drift, stabilitet og tilgængelighed kan observeres efter implementeringen af alternativerne?

---

## Oversigt over faser

| Fase | Navn | Indhold |
|------|------|---------|
| 1 | Forberedelse | Opret testprojekt, infrastruktur og /health endpoint |
| 2 | Stabilitetstests | Response time, error rate, database, load |
| 3 | Tilgængelighedstests | Uptime, SSL, DNS, cold start, endpoints |
| 4 | Driftstests | Deployment tid, config-validering, logging |
| 5 | Rapportering | Sammenligning og output til analyse |
| 6 | Kørsel og sammenligning | Baseline på Azure → kør mod EU-alternativ |

---

## Fase 1: Forberedelse og projektstruktur

### 1.1 Opret testprojekt

Opret et nyt xUnit-testprojekt i solution'en:

```bash
cd BACHELOR-main
dotnet new xunit -n CVAPI.OperationalTests
dotnet sln VEXA.sln add CVAPI.OperationalTests/CVAPI.OperationalTests.csproj
```

Tilføj dependencies til `CVAPI.OperationalTests.csproj`:

```xml
<PackageReference Include="xunit" Version="2.9.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

### 1.2 Mappestruktur

```
CVAPI.OperationalTests/
├── Config/
│   ├── TestEnvironment.cs          ← Enum: Azure, EU
│   ├── TestConfig.cs               ← Loader konfiguration per miljø
│   └── appsettings.test.json       ← URLs, endpoints per miljø
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
└── Reports/
    ├── TestResult.cs               ← Fælles result-model
    └── TestReportGenerator.cs      ← Skriver JSON/CSV output
```

### 1.3 Konfigurationsfil: `appsettings.test.json`

```json
{
  "Environments": {
    "Azure": {
      "BaseUrl": "https://bachelor-ete0e0e5d4cphjg7.germanywestcentral-01.azurewebsites.net",
      "HealthEndpoint": "/health",
      "TestEndpoints": [
        "/api/user",
        "/api/competence"
      ]
    },
    "EU": {
      "BaseUrl": "https://<EU-ALTERNATIV-URL>",
      "HealthEndpoint": "/health",
      "TestEndpoints": [
        "/api/user",
        "/api/competence"
      ]
    }
  },
  "TestSettings": {
    "RepeatCount": 50,
    "ConcurrentUsers": [1, 10, 50, 100],
    "UptimeDurationMinutes": 60,
    "UptimeIntervalSeconds": 30,
    "TimeoutMs": 10000
  }
}
```

### 1.4 Tilføj `/health` endpoint til CVAPI

I `Program.cs`, tilføj inden `app.Run()`:

```csharp
app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "2.3.1"
}));
```

> **Vigtigt:** Dette endpoint skal eksistere i begge miljøer for at health check-tests kan køre.

---

## Fase 2: Stabilitetstests

### Mål
Måle om applikationen opfører sig konsistent og stabilt over tid og under belastning.

---

### S1 — Health Check Test

**Fil:** `Stability/HealthCheckTests.cs`

**Hvad testes:** Om applikationen svarer korrekt på `/health`

**Fremgangsmåde:**
1. Send HTTP GET til `/health`
2. Verificér HTTP 200 og korrekt JSON-response
3. Mål responstid

**Output metric:** Pass/Fail, responstid (ms)

```csharp
// Pseudokode
[Theory]
[InlineData(TestEnvironment.Azure)]
[InlineData(TestEnvironment.EU)]
public async Task HealthCheck_ShouldReturn200(TestEnvironment env)
{
    var url = config.GetBaseUrl(env) + "/health";
    var sw = Stopwatch.StartNew();
    var response = await httpClient.GetAsync(url);
    sw.Stop();

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    Report.Record(env, "HealthCheck", sw.ElapsedMilliseconds);
}
```

---

### S2 — Response Time Consistency Test

**Fil:** `Stability/ResponseTimeTests.cs`

**Hvad testes:** Variation og konsistens i svartider

**Fremgangsmåde:**
1. Send N requests (default: 50) til samme endpoint
2. Mål responstid per request
3. Beregn: mean, median, P95, P99, standardafvigelse

**Output metrics:** Mean (ms), Median (ms), P95 (ms), P99 (ms), Std.dev (ms)

```
Kør 50 requests → [45ms, 52ms, 48ms, ...]
Mean: 49ms | Median: 48ms | P95: 67ms | P99: 89ms | Std.dev: 8ms
```

---

### S3 — Error Rate under Load

**Fil:** `Stability/ErrorRateTests.cs`

**Hvad testes:** Fejlprocent ved stigende antal samtidige brugere

**Fremgangsmåde:**
1. Send X samtidige requests for X ∈ {10, 50, 100}
2. Tæl HTTP 5xx og timeouts
3. Beregn fejlprocent

**Output metrics:** Fejl% ved 10, 50 og 100 samtidige requests

```
10 samtidige:  0/10  fejl  → 0%
50 samtidige:  2/50  fejl  → 4%
100 samtidige: 8/100 fejl  → 8%
```

---

### S4 — Database Resilience Test

**Fil:** `Stability/DatabaseResilienceTests.cs`

**Hvad testes:** Stabiliteten af databaseforbindelsen (Cosmos DB)

**Fremgangsmåde:**
1. Kald et endpoint der læser fra databasen N gange over tid
2. Registrér fejl, timeouts og langsomme responses
3. Observer om svartider forværres over tid (memory/connection leak)

**Output metrics:** Fejlrate (%), gennemsnitlig DB-responstid (ms), drift over tid

> **Note:** Vælg et readonly endpoint (f.eks. hent kompetencer) for ikke at skrive testdata.

---

## Fase 3: Tilgængelighedstests

### Mål
Måle om applikationen er tilgængelig, hvornår den ikke er, og hvor hurtigt den er klar.

---

### T1 — Uptime Monitoring

**Fil:** `Availability/UptimeMonitorTests.cs`

**Hvad testes:** Samlet oppetid over en måleperiode

**Fremgangsmåde:**
1. Kald `/health` hvert 30. sekund i 60 minutter (120 checks)
2. Tæl succesfulde og fejlede checks
3. Beregn uptime-procent

**Output metrics:** Uptime % (succesfulde checks / totale checks × 100)

```
Azure:       118/120 checks OK → 98.3% uptime
EU-alternativ: 120/120 checks OK → 100% uptime
```

> **Bemærkning:** Kør denne test på samme tidspunkt i begge miljøer for fair sammenligning. Overvej at køre den natten over.

---

### T2 — DNS Resolution Time

**Fil:** `Availability/DNSResolutionTests.cs`

**Hvad testes:** Hvor hurtigt DNS-opslag sker for applikationens domæne

**Fremgangsmåde:**
1. Lav DNS-opslag for begge miljøers domænenavne
2. Mål opslag-tid 20 gange
3. Beregn gennemsnit

**Output metrics:** Gennemsnitlig DNS-opslag tid (ms)

---

### T3 — SSL/TLS Certifikat Validering

**Fil:** `Availability/SSLCertificateTests.cs`

**Hvad testes:** Certifikatets gyldighed, kæde og udløbsdato

**Fremgangsmåde:**
1. Opret HTTPS-forbindelse til begge miljøer
2. Hent certifikatinfo programmatisk
3. Verificér: gyldig kæde, ikke udløbet, udløbsdato > 30 dage

**Output metrics:** Gyldig (ja/nej), dage til udløb, certifikatsudsteder

---

### T4 — Cold Start Time

**Fil:** `Availability/ColdStartTests.cs`

**Hvad testes:** Tid fra applikation er "kold" (idle) til første succesfulde response

**Fremgangsmåde:**
1. Vent til applikationen er idled (eller trigger genstart via deployment)
2. Send request og mål tid til første HTTP 200
3. Gentag N gange for statistik

**Output metrics:** Cold start tid (ms), varians

> **Note:** Azure App Service har "Always On" indstilling — noter om dette er aktivt for fair sammenligning.

---

### T5 — Endpoint Availability

**Fil:** `Availability/EndpointAvailabilityTests.cs`

**Hvad testes:** At alle kendte API-endpoints er tilgængelige og svarer korrekt

**Fremgangsmåde:**
1. Test alle endpoints fra konfigurationen systematisk
2. Verificér HTTP 200 eller 401 (auth-krævet endpoints er OK hvis de svarer)
3. Rapportér hvilke endpoints er nede

**Output metrics:** Tilgængelige endpoints / Totale endpoints, liste over fejlende

```
Azure:        8/8 endpoints tilgængelige
EU-alternativ: 7/8 endpoints tilgængelige (mangler /api/competence)
```

---

## Fase 4: Driftstests

### Mål
Måle de operationelle aspekter der påvirker arbejdet med systemet dagligt.

> **Note:** Disse tests er mere svære at automatisere fuldt ud. Nogle kræver manuel observation.

---

### D1 — Deployment Tid

**Fil:** `Operations/DeploymentTimeTests.cs`

**Hvad testes:** Tid fra git push til applikationen er live med ny version

**Fremgangsmåde:**
1. Notér tidspunkt for git push / pipeline start
2. Poll `/health` endpoint indtil ny version-header/timestamp er synlig
3. Beregn total deployment-tid

**Output metrics:** Deployment tid (sekunder), pipeline-tid vs. live-tid

> **Alternativ (manuelt):** Tag tid fra GitHub Actions pipeline log — "Workflow started" til "Deploy completed".

---

### D2 — Konfigurationsvalidering

**Fil:** `Operations/ConfigValidationTests.cs`

**Hvad testes:** At alle nødvendige konfigurationer og hemmeligheder er tilgængelige i miljøet

**Fremgangsmåde:**
1. Kald et endpoint der kræver JWT (secret fra Key Vault)
2. Kald et endpoint der rammer databasen
3. Verificér at ingen "missing configuration" fejl opstår

**Output metrics:** Manglende configs (antal), konfig-læsetid (ms)

---

### D3 — Log Tilgængelighed

**Fil:** `Operations/LoggingVerificationTests.cs`

**Hvad testes:** Om logs er strukturerede og tilgængelige

**Fremgangsmåde:**
1. Trigger et kendt request der bør logges
2. Verificér at log-infrastrukturen modtager events (Azure Monitor vs. EU-alternativ)
3. Mål tid fra event til log er synlig

**Output metrics:** Log-latens (sekunder), struktureret format (ja/nej)

> **Note:** Dette kræver adgang til log-platformen i begge miljøer. Kan delvist gøres manuelt.

---

## Fase 5: Rapportering

### 5.1 Fælles resultatmodel

Alle tests skriver til en fælles `TestResult`-model:

```csharp
public class TestResult
{
    public string TestName { get; set; }
    public TestEnvironment Environment { get; set; }
    public DateTime RunAt { get; set; }
    public bool Passed { get; set; }
    public double ValueMs { get; set; }       // Primær måling i ms
    public double ValuePercent { get; set; }  // Procentmåling
    public string Notes { get; set; }
}
```

### 5.2 Output format

Resultater gemmes i `/Reports/output/`:

- `results_azure_<dato>.json` — Alle Azure-resultater
- `results_eu_<dato>.json` — Alle EU-resultater
- `comparison_<dato>.csv` — Side-om-side sammenligning

### 5.3 Eksempel på sammenligningsoutput (CSV)

```
TestName,Azure_Mean_ms,EU_Mean_ms,Difference_ms,Azure_Pass,EU_Pass
HealthCheck,45,62,+17,true,true
ResponseTime_P95,210,180,-30,true,true
ErrorRate_50users,4%,1%,-3pp,true,true
UptimePercent,98.3%,100%,+1.7pp,true,true
ColdStartTime,3200,1100,-2100,true,true
DeploymentTime,95s,45s,-50s,true,true
```

---

## Fase 6: Kørsel og sammenligning

### 6.1 Rækkefølge

```
1. Konfigurér begge miljøers URLs i appsettings.test.json
2. Kør ALLE tests mod Azure  → gem resultater (baseline)
3. Deploy applikation til EU-alternativ
4. Kør ALLE tests mod EU-alternativ → gem resultater
5. Kør TestReportGenerator → generer comparison CSV
6. Analyser og dokumentér forskelle i bacheloropgaven
```

### 6.2 Kørselsbetingelser (vigtigt for fair sammenligning)

For at sikre sammenlignelige resultater:

- [ ] Kør tests fra **samme maskine/netværk** for begge miljøer
- [ ] Kør tests på **samme tidspunkt** (helst samme time på dagen)
- [ ] Sørg for applikationen har **"warmed up"** inden response time-tests (send 5 warm-up requests først)
- [ ] Brug **samme testdata** i begge miljøer (ingen eksisterende brugerdata i EU-miljøet kan påvirke)
- [ ] Kør uptime-tests **parallelt** i begge miljøer hvis muligt

### 6.3 Kørsel via kommandolinje

```bash
# Kør alle tests
dotnet test CVAPI.OperationalTests

# Kør kun stabilitetstests
dotnet test CVAPI.OperationalTests --filter "Category=Stability"

# Kør kun mod Azure
dotnet test CVAPI.OperationalTests --filter "Environment=Azure"

# Kør med detaljeret output
dotnet test CVAPI.OperationalTests -v detailed
```

---

## Checkliste: Forudsætninger

Inden testene kan køres, skal følgende være på plads:

### Azure (nuværende)
- [x] App Service kørende
- [x] Cosmos DB forbundet
- [x] Key Vault konfigureret
- [ ] `/health` endpoint tilføjet til koden

### EU-alternativ (fremtidigt)
- [ ] Beslut hvilken EU-cloud platform der bruges
- [ ] Deploy applikationen til EU-miljøet
- [ ] Konfigurér database-alternativ (f.eks. Scaleway, OVHcloud, Hetzner)
- [ ] Konfigurér secret management-alternativ
- [ ] Tilgængeligt domæne med SSL
- [ ] Udfyld `EU.BaseUrl` i `appsettings.test.json`

---

## Testmatrix: Hvad testes hvornår

| Test ID | Beskrivelse | Fase | Automatiseret | Prioritet |
|---------|-------------|------|---------------|-----------|
| S1 | Health Check | 2 | Ja | Høj |
| S2 | Response Time | 2 | Ja | Høj |
| S3 | Error Rate under load | 2 | Ja | Høj |
| S4 | Database Resilience | 2 | Delvist | Høj |
| T1 | Uptime Monitoring | 3 | Ja | Høj |
| T2 | DNS Resolution | 3 | Ja | Medium |
| T3 | SSL Validering | 3 | Ja | Medium |
| T4 | Cold Start | 3 | Delvist | Medium |
| T5 | Endpoint Availability | 3 | Ja | Høj |
| D1 | Deployment Tid | 4 | Delvist | Medium |
| D2 | Config Validering | 4 | Ja | Medium |
| D3 | Log Tilgængelighed | 4 | Manuel | Lav |

---

*Testplan udarbejdet som del af bacheloropgave om migration fra Azure til europæisk cloudarkitektur.*
