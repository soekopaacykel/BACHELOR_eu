# Feedback: Operationel Testplan

---

## Hvad planen dækker godt ✅

- **Stabilitet** — S1–S4 giver kvantitative målinger der kan sammenlignes direkte
- **Tilgængelighed** — T1 (uptime), T3 (SSL), T5 (endpoints) er alle direkte målbare
- **Drift** — D1 (deployment tid) og D2 (config) er relevante og automatiserbare

---

## Mangler og svagheder ⚠️

### 1. Enkelt øjebliksbillede er ikke nok
Planen måler ét snapshot. For troværdigt at konkludere på *forskelle* kræves mindst **2–3 gentagelser på forskellige tidspunkter** (morgen/aften/weekend). Ellers kan én dårlig dag på Azure se ud som en strukturel forskel.

### 2. Cold Start (T4) er halvt manuel og svær at reproducere
At slå "Always On" fra manuelt i Azure gør testen næsten umulig at gentage på samme vilkår. Det er den eneste test der kræver ændringer i selve produktionsmiljøet.

### 3. D3 (Log tilgængelighed) hænger som lav-prioritet
Logs er et kernepunkt i *drift*-dimensionen, men testen er sat til "Manuel/Lav". Den burde enten automatiseres ordentligt eller **droppes** og erstattes af en manuel observation i rapporten.

### 4. S3 (Error Rate under Load) tester ikke auth-beskyttede endpoints
S3 sender requests til offentlige endpoints. Hvis de vigtigste endpoints kræver JWT, tester man i praksis ikke det **reelle system** under belastning.

### 5. T6 og D4 fra reviewet er ikke implementeret endnu
Begge var med i reviewet og er vigtige:
- **T6 (Auth Flow)** — JWT-skiftet fra Azure Key Vault til et EU-alternativ er kernen i migrationen. Hvis auth ikke virker i EU-miljøet, er alt andet irrelevant.
- **D4 (Data Residency)** — Verificerer at serveren faktisk er i EU. Direkte svar på compliance-spørgsmålet og afgørende for bachelorens konklusion.

---

## Anbefalinger

| Prioritet | Handling |
|-----------|----------|
| 🔴 Vigtig | Tilføj **T6 (Auth Flow)** til Availability |
| 🔴 Vigtig | Tilføj **D4 (Data Residency)** til Operations |
| 🟡 Overvej | Kør S2/S3 på **3 forskellige tidspunkter** og brug gennemsnittet |
| 🟡 Overvej | **Drop D3 (Log)** eller acceptér at den er manuel observation i rapporten |
| 🟢 Lille fix | Beskriv T4 (Cold Start) eksplicit som manuel observation — ikke "delvist automatiseret" |

---

*Feedback udarbejdet 17. marts 2026*
