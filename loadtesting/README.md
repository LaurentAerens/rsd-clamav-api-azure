# Azure Load Testing for ClamAV JSON endpoint

This guide adds a standalone Azure Load Testing resource and two JMeter scenarios focused on `POST /scan/json`.

## What was added
- Standalone Bicep deployment for Azure Load Testing:
  - `loadtesting/infra/loadtesting.bicep`
  - `loadtesting/infra/modules/load-testing.bicep` (AVM: `br/public:avm/res/load-test-service/load-test:0.4.3`)
  - `loadtesting/infra/loadtesting.bicepparam` (source-controlled template)
- Test assets:
  - `loadtesting/assets/payloads`
  - `loadtesting/assets/jmeter/json-baseline.jmx`
  - `loadtesting/assets/jmeter/json-capacity.jmx`
  - `loadtesting/assets/jmeter/datasets/*.csv`

## 1) Deploy Azure Load Testing resource (standalone)

Run the interactive script and choose **Deploy or update Azure Load Testing resource now?**.

Optional local override file (ignored by git):

```powershell
Copy-Item loadtesting/infra/loadtesting.bicepparam loadtesting/infra/loadtesting.dev.bicepparam
```

The script can read the deployed resource name automatically from deployment outputs.

## 2) Create and run tests from CLI

Run the interactive script and follow prompts for:
- resource group and load testing resource
- optional infra deployment/what-if
- automatic scan + selection of `.bicepparam` files in `loadtesting/infra`
- baseline and/or capacity scenario selection
- upload of all JMeter + CSV + payload assets
- run parameters (`HOST`, thread/loop/ramp/threshold values)

```powershell
pwsh ./loadtesting/run-loadtests.ps1
```

Optional convenience defaults:

```powershell
pwsh ./loadtesting/run-loadtests.ps1 `
  -DefaultResourceGroup "<rg-name>" `
  -DefaultLoadTestResourceName "<load-test-resource-name>" `
  -DefaultHost "<container-app-fqdn>"
```

## 3) Portal workflow
- Open Azure Load Testing resource in portal.
- Create test from JMeter script.
- Upload `.jmx`, dataset `.csv`, and payload `.json` files.
- Set environment variables (`HOST`, `PROTOCOL`, thread/loop values).
- Run baseline and capacity scenarios.

## Cold-start handling
Both plans include a **Warm-up** thread group before measured load so startup effects don't skew measured results.

## Tune thresholds later
You can tune later by updating:
- `MAX_RESPONSE_MS` when starting runs
- Thread/loop/ramp values per scenario
- Dataset composition in `loadtesting/assets/jmeter/datasets/*.csv`
