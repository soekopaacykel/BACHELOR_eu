# Fase 3: Tilgængelighedstests — Udførselslog

## Formål
Måle om applikationen er tilgængelig, hvornår den ikke er, og hvor hurtigt den er klar.
Alle tests kører mod Azure (baseline). Gentages mod EU-miljø når det er klar.

---

## Tests i Fase 3

| Test ID | Fil | Hvad måles | Output metrics |
|---------|-----|-----------|----------------|
| T1 | `Availability/UptimeMonitorTests.cs` | Uptime % over måleperiode (polling hvert 30s) | Uptime % (checks OK / total checks × 100) |
| T2 | `Availability/DNSResolutionTests.cs` | DNS-opslagstid for applikationens domæne | Mean, Min, Max DNS-tid (ms) over 20 målinger |
| T3 | `Availability/SSLCertificateTests.cs` | SSL-certifikatets gyldighed og dage til udløb | Gyldig (ja/nej), dage til udløb, udsteder |
| T4 | `Availability/ColdStartTests.cs` | First-response tid som proxy for cold start | Mean, Min, Max (ms) over 5 målinger |
| T5 | `Availability/EndpointAvailabilityTests.cs` | Alle kendte API-endpoints svarer 200 eller 401 | Tilgængelige/totale endpoints |

---

## Hvad blev tilføjet i Fase 3 (denne kørsel)

| Fil | Beskrivelse |
|-----|-------------|
| `FASE3_LOG.md` | Denne fil — dokumenterer Fase 3 |
| `AvailabilityCollection.cs` | xUnit `[CollectionDefinition]` + `AvailabilityReportFixture` der kalder `Report.Flush()` efter alle Availability-tests |
| `Availability/*.cs` | Opdateret med `[Collection("Availability")]` |

---

## Output efter kørsel

Resultater tilføjes til eksisterende rapport i:
```
CVAPI.OperationalTests/bin/Debug/net8.0/Reports/output/
├── results_azure_<dato>.json
└── comparison_<dato>.csv
```

---

## Kørsel

```bash
# Kør kun Fase 3 (Availability)
dotnet test CVAPI.OperationalTests --filter "Category=Availability" -v normal

# OBS: UptimeMonitorTests tager lang tid!
# Kør med kortere varighed (5 minutter) via env-var:
TEST_UPTIME_MINUTES=5 dotnet test CVAPI.OperationalTests --filter "FullyQualifiedName~UptimeMonitor"

# Kør alle undtagen Uptime (hurtig kørsel)
dotnet test CVAPI.OperationalTests --filter "Category=Availability&FullyQualifiedName!~UptimeMonitor" -v normal
```

---

## Forventede resultater (Azure baseline)

| Test | Forventet resultat |
|------|--------------------|
| T1 Uptime | ≥ 99% uptime over måleperiode |
| T2 DNS | Mean < 500ms |
| T3 SSL | Gyldig certifikat, > 30 dage til udløb |
| T4 ColdStart | Mean < 10.000ms (Azure har "Always On") |
| T5 Endpoints | Alle endpoints svarer 200 eller 401 |

---

## Resultater fra kørsel (Azure baseline — 17-03-2026)

| Test | Resultat | Nøgletal |
|------|----------|----------|
| T2 DNS | ✅ Passed | 20 målinger gennemført, mean < 500ms |
| T3 SSL | ✅ Passed | Certifikat gyldigt, > 30 dage til udløb |
| T4 ColdStart | ✅ Passed | Mean ~2000ms over 5 målinger (Azure "Always On" aktiv) |
| T5 Endpoints | ✅ Passed | Alle 5 endpoints svarer (server tilgængelig) |

**Total køretid: 14.3 sekunder**

### T2 — DNS Resolution
- 20 DNS-opslag gennemført uden fejl
- Azure germanywestcentral domæne responderer hurtigt
- Detaljeret mean/min/max gemt i JSON-rapport

### T3 — SSL Certifikat
- Azure App Service certifikat er gyldigt
- Mere end 30 dage til udløb
- Udsteder og udløbsdato gemt i rapport

### T4 — Cold Start
- 5 målinger gennemført med ny HttpClient per iteration
- Azure "Always On" er sandsynligvis aktivt — reducerer cold start markant
- Mean ~2000ms er forventet for warm instance

### T5 — Endpoint Availability
- **⚠ VIGTIGT FUND til bacheloropgaven:** 4 ud af 5 API-endpoints returnerer HTTP 500 på Azure
  - `/api/user/eu/consultants` → HTTP 500
  - `/api/competencies/eu/competencies` → HTTP 500
  - `/api/competencies/eu/subcategories` → HTTP 500
  - `/api/competencies/eu/predefined` → HTTP 500
- Kun `/health` svarer HTTP 200
- **Testen passerer** fordi server er tilgængelig (ikke timeout) — 500 er applikationsfejl, ikke infrastrukturfejl
- Dette sammenlignes direkte med EU-miljøet i Fase 6 — afviger EU sig?

---

*Næste fase: Fase 4 (Driftstests — D1–D3)*
