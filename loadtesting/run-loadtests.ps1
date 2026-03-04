param(
    [string]$DefaultResourceGroup = '',
    [string]$DefaultLoadTestResourceName = '',
    [string]$DefaultHost = ''
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Read-YesNo {
    param(
        [string]$Prompt,
        [bool]$Default = $true
    )

    $hint = if ($Default) { '[Y/n]' } else { '[y/N]' }
    $inputValue = Read-Host "$Prompt $hint"
    if ([string]::IsNullOrWhiteSpace($inputValue)) {
        return $Default
    }

    switch -Regex ($inputValue.Trim().ToLowerInvariant()) {
        '^y(es)?$' { return $true }
        '^n(o)?$' { return $false }
        default {
            Write-Host 'Please answer y or n.' -ForegroundColor Yellow
            return Read-YesNo -Prompt $Prompt -Default $Default
        }
    }
}

function Read-Value {
    param(
        [string]$Prompt,
        [string]$Default = '',
        [bool]$Required = $true
    )

    $display = if ([string]::IsNullOrWhiteSpace($Default)) { $Prompt } else { "$Prompt [$Default]" }
    $value = Read-Host $display
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = $Default
    }

    if ($Required -and [string]::IsNullOrWhiteSpace($value)) {
        Write-Host 'Value is required.' -ForegroundColor Yellow
        return Read-Value -Prompt $Prompt -Default $Default -Required $Required
    }

    return $value
}

function Get-BicepParamFiles {
    param(
        [string]$RootPath
    )

    if (-not (Test-Path $RootPath)) {
        return @()
    }

    return @(Get-ChildItem -Path $RootPath -Filter '*.bicepparam' -File -Recurse | Sort-Object FullName)
}

function Select-BicepParamFile {
    param(
        [System.IO.FileInfo[]]$Files,
        [string]$DefaultFilePath = ''
    )

    if ($null -eq $Files -or $Files.Count -eq 0) {
        throw 'No .bicepparam files found for load testing deployment.'
    }

    $defaultIndex = 0
    if (-not [string]::IsNullOrWhiteSpace($DefaultFilePath)) {
        $matchIndex = -1
        for ($i = 0; $i -lt $Files.Count; $i++) {
            if ($Files[$i].FullName -ieq $DefaultFilePath) {
                $matchIndex = $i
                break
            }
        }

        if ($matchIndex -ge 0) {
            $defaultIndex = $matchIndex
        }
    }

    Write-Host 'Select a parameter file:' -ForegroundColor Cyan
    for ($i = 0; $i -lt $Files.Count; $i++) {
        $marker = if ($i -eq $defaultIndex) { ' (default)' } else { '' }
        Write-Host ("[{0}] {1}{2}" -f ($i + 1), $Files[$i].FullName, $marker)
    }

    while ($true) {
        $rawChoice = Read-Host ("Enter number 1-{0} (Enter for default)" -f $Files.Count)
        if ([string]::IsNullOrWhiteSpace($rawChoice)) {
            return $Files[$defaultIndex]
        }

        $index = 0
        if ([int]::TryParse($rawChoice, [ref]$index)) {
            if ($index -ge 1 -and $index -le $Files.Count) {
                return $Files[$index - 1]
            }
        }

        Write-Host 'Invalid selection. Please choose a valid number.' -ForegroundColor Yellow
    }
}

function Invoke-AzCli {
    param(
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $output = & az @Arguments --only-show-errors 2>&1
    $exitCode = $LASTEXITCODE
    $text = ($output | Out-String).Trim()

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Azure CLI command failed (exit $exitCode): az $($Arguments -join ' ')`n$text"
    }

    return [pscustomobject]@{
        Success = ($exitCode -eq 0)
        ExitCode = $exitCode
        Output = $output
        Text = $text
    }
}

function Resolve-TargetEndpoint {
    param(
        [string]$InputValue,
        [string]$DefaultProtocol = 'https'
    )

    $value = ($InputValue ?? '').Trim()
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw 'Target endpoint value is required.'
    }

    $protocol = $DefaultProtocol
    $endpointHost = $value

    if ($value -match '^https?://') {
        try {
            $uri = [System.Uri]$value
            $protocol = $uri.Scheme.ToLowerInvariant()
            $endpointHost = $uri.Host
        }
        catch {
            throw "Invalid URL format: '$value'"
        }
    }

    $endpointHost = ($endpointHost ?? '').Trim().TrimEnd('/')
    if ([string]::IsNullOrWhiteSpace($endpointHost)) {
        throw 'Could not determine host from target endpoint.'
    }

    if ($endpointHost.Contains('/')) {
        $endpointHost = ($endpointHost -split '/')[0]
    }

    return [pscustomobject]@{
        Protocol = $protocol
        Host = $endpointHost
    }
}

function Wait-ForHealthEndpoint {
    param(
        [string]$Protocol,
        [string]$TargetHost,
        [string]$Path = '/healthz',
        [int]$TimeoutSeconds = 300,
        [int]$PollIntervalSeconds = 5
    )

    $healthUrl = "${Protocol}://${TargetHost}${Path}"
    Write-Step "Waiting for health endpoint: $healthUrl"

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempt = 0

    while ((Get-Date) -lt $deadline) {
        $attempt++
        $statusCode = -1

        try {
            $response = Invoke-WebRequest -Uri $healthUrl -Method Get -TimeoutSec 15 -ErrorAction Stop
            $statusCode = [int]$response.StatusCode
        }
        catch {
            if ($null -ne $_.Exception.Response -and $null -ne $_.Exception.Response.StatusCode) {
                $statusCode = [int]$_.Exception.Response.StatusCode
            }
        }

        if ($statusCode -eq 200) {
            Write-Host "Health endpoint ready (HTTP 200) after $attempt attempt(s)." -ForegroundColor Green
            return
        }

        Write-Host "Health probe attempt $attempt returned status $statusCode. Retrying..." -ForegroundColor DarkGray
        Start-Sleep -Seconds $PollIntervalSeconds
    }

    throw "Health endpoint did not return HTTP 200 within $TimeoutSeconds seconds: $healthUrl"
}

function Test-AzCliInstalled {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw 'Azure CLI (az) is not installed or not on PATH.'
    }
}

function Test-AzureLoginContext {
    Write-Step 'Checking Azure login context'
    $result = Invoke-AzCli -Arguments @('account', 'show', '--output', 'none') -AllowFailure
    if (-not $result.Success) {
        throw "You are not logged in. Run: az login`n$($result.Text)"
    }
}

function Install-LoadExtensionIfNeeded {
    Write-Step 'Checking Azure Load Testing CLI extension'
    $extensionsResult = Invoke-AzCli -Arguments @('extension', 'list', '-o', 'json')
    $extensionsJson = $extensionsResult.Text | ConvertFrom-Json
    $hasLoadExtension = $extensionsJson | Where-Object { $_.name -eq 'load' }

    if (-not $hasLoadExtension) {
        Write-Host 'Azure CLI extension "load" is not installed.' -ForegroundColor Yellow
        if (Read-YesNo -Prompt 'Install extension now?' -Default $true) {
            Invoke-AzCli -Arguments @('extension', 'add', '--name', 'load', '--yes') | Out-Null
            Write-Host 'Installed extension: load' -ForegroundColor Green
        }
        else {
            throw 'Azure Load Testing extension is required. Install with: az extension add --name load'
        }
    }
}

function ConvertTo-YamlSingleQuoted {
    param([string]$Value)
    $safe = ($Value ?? '').Replace("'", "''")
    return "'$safe'"
}

function New-LoadTestConfigFile {
    param(
        [string]$TestId,
        [string]$DisplayName,
        [string]$Description,
        [string]$TestPlanPath,
        [int]$EngineInstances,
        [double]$AutostopErrorRate,
        [int]$AutostopTimeWindowSeconds,
        [string[]]$FailureCriteria
    )

    $lines = @(
        'version: v0.1',
        "testId: $(ConvertTo-YamlSingleQuoted -Value $TestId)",
        "displayName: $(ConvertTo-YamlSingleQuoted -Value $DisplayName)",
        "description: $(ConvertTo-YamlSingleQuoted -Value $Description)",
        "testPlan: $(ConvertTo-YamlSingleQuoted -Value $TestPlanPath)",
        "engineInstances: $EngineInstances",
        'autoStop:',
        "  errorPercentage: $AutostopErrorRate",
        "  timeWindow: $AutostopTimeWindowSeconds",
        'failureCriteria:'
    )

    foreach ($criterion in $FailureCriteria) {
        $lines += "  - $(ConvertTo-YamlSingleQuoted -Value $criterion)"
    }

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'rsd-clamav-loadtesting'
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

    $configFile = Join-Path $tempRoot "$TestId.config.yaml"
    Set-Content -Path $configFile -Value ($lines -join [Environment]::NewLine) -Encoding UTF8
    return $configFile
}

function Initialize-LoadTest {
    param(
        [string]$ResourceGroup,
        [string]$LoadTestResource,
        [string]$TestId,
        [string]$DisplayName,
        [string]$Description,
        [string]$TestPlanPath,
        [int]$EngineInstances,
        [double]$AutostopErrorRate,
        [int]$AutostopTimeWindowSeconds,
        [int]$AutostopEngineUsers,
        [string[]]$FailureCriteria
    )

    $configFile = New-LoadTestConfigFile -TestId $TestId -DisplayName $DisplayName -Description $Description -TestPlanPath $TestPlanPath -EngineInstances $EngineInstances -AutostopErrorRate $AutostopErrorRate -AutostopTimeWindowSeconds $AutostopTimeWindowSeconds -FailureCriteria $FailureCriteria

    $showResult = Invoke-AzCli -Arguments @(
        'load', 'test', 'show',
        '--resource-group', $ResourceGroup,
        '--load-test-resource', $LoadTestResource,
        '--test-id', $TestId,
        '--output', 'none'
    ) -AllowFailure

    if ($showResult.Success) {
        Write-Host "Test '$TestId' already exists." -ForegroundColor DarkGray
        Write-Host "Updating existing test '$TestId' with latest config, criteria, engine count, and metadata..." -ForegroundColor DarkGray
        Invoke-AzCli -Arguments @(
            'load', 'test', 'update',
            '--resource-group', $ResourceGroup,
            '--load-test-resource', $LoadTestResource,
            '--test-id', $TestId,
            '--load-test-config-file', $configFile
        ) | Out-Null
    }
    else {
        if ($showResult.Text -notmatch 'TestNotFound') {
            throw "Failed to check if test '$TestId' exists.`n$($showResult.Text)"
        }

        Write-Host "Creating test '$TestId'..." -ForegroundColor DarkGray
        Invoke-AzCli -Arguments @(
            'load', 'test', 'create',
            '--resource-group', $ResourceGroup,
            '--load-test-resource', $LoadTestResource,
            '--test-id', $TestId,
            '--load-test-config-file', $configFile
        ) | Out-Null

        Invoke-AzCli -Arguments @(
            'load', 'test', 'show',
            '--resource-group', $ResourceGroup,
            '--load-test-resource', $LoadTestResource,
            '--test-id', $TestId,
            '--output', 'none'
        ) | Out-Null
    }

    Write-Host "Applied Azure test criteria for '$TestId' via config file." -ForegroundColor DarkGray
}

function Publish-TestAssets {
    param(
        [string]$ResourceGroup,
        [string]$LoadTestResource,
        [string]$TestId,
        [string]$JmxPath,
        [string[]]$DatasetPaths,
        [string]$PayloadFolder
    )

    Write-Step "Uploading assets for $TestId"

    Invoke-AzCli -Arguments @(
        'load', 'test', 'file', 'upload',
        '--resource-group', $ResourceGroup,
        '--load-test-resource', $LoadTestResource,
        '--test-id', $TestId,
        '--path', $JmxPath
    ) | Out-Null

    foreach ($dataset in $DatasetPaths) {
        Invoke-AzCli -Arguments @(
            'load', 'test', 'file', 'upload',
            '--resource-group', $ResourceGroup,
            '--load-test-resource', $LoadTestResource,
            '--test-id', $TestId,
            '--path', $dataset
        ) | Out-Null
    }

    $payloadFiles = Get-ChildItem -Path $PayloadFolder -Filter '*.json' -File
    foreach ($payload in $payloadFiles) {
        Invoke-AzCli -Arguments @(
            'load', 'test', 'file', 'upload',
            '--resource-group', $ResourceGroup,
            '--load-test-resource', $LoadTestResource,
            '--test-id', $TestId,
            '--path', $payload.FullName
        ) | Out-Null
    }

    Write-Host "Uploaded $($payloadFiles.Count) payload files." -ForegroundColor Green
}

function New-ResolvedJmxFile {
    param(
        [string]$TemplatePath,
        [hashtable]$Variables,
        [string]$TargetFileName
    )

    if ([string]::IsNullOrWhiteSpace($TargetFileName)) {
        $TargetFileName = Split-Path -Path $TemplatePath -Leaf
    }

    $content = Get-Content -Path $TemplatePath -Raw
    foreach ($entry in $Variables.GetEnumerator()) {
        $token = '${' + $entry.Key + '}'
        $value = [System.Security.SecurityElement]::Escape([string]$entry.Value)
        $content = $content.Replace($token, $value)
    }

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'rsd-clamav-loadtesting'
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

    $resolvedPath = Join-Path $tempRoot $TargetFileName
    Set-Content -Path $resolvedPath -Value $content -Encoding UTF8
    return $resolvedPath
}

function Upload-TestPlanFile {
    param(
        [string]$ResourceGroup,
        [string]$LoadTestResource,
        [string]$TestId,
        [string]$TestPlanPath
    )

    Invoke-AzCli -Arguments @(
        'load', 'test', 'file', 'upload',
        '--resource-group', $ResourceGroup,
        '--load-test-resource', $LoadTestResource,
        '--test-id', $TestId,
        '--path', $TestPlanPath
    ) | Out-Null
}

function Start-LoadTestRun {
    param(
        [string]$ResourceGroup,
        [string]$LoadTestResource,
        [string]$TestId,
        [hashtable]$Environment
    )

    $timestamp = Get-Date -Format 'yyyyMMddHHmmss'
    $runId = "$TestId-$timestamp"

    $envList = @()
    foreach ($entry in $Environment.GetEnumerator()) {
        $envList += "$($entry.Key)=$($entry.Value)"
    }

    $targetRpmText = ''
    if ($Environment.ContainsKey('TARGET_RPM')) {
        $targetRpmText = [string]$Environment['TARGET_RPM']
    }

    $runDescription = "${TestId} host=$($Environment.HOST) rpm=$targetRpmText"
    if ($runDescription.Length -gt 100) {
        $runDescription = $runDescription.Substring(0, 100)
    }

    $runCreateArgs = @(
        'load', 'test-run', 'create',
        '--resource-group', $ResourceGroup,
        '--load-test-resource', $LoadTestResource,
        '--test-id', $TestId,
        '--test-run-id', $runId,
        '--display-name', "$TestId - $timestamp",
        '--description', $runDescription,
        '--env'
    )
    $runCreateArgs += $envList

    Write-Step "Starting run '$runId'"
    Invoke-AzCli -Arguments $runCreateArgs | Out-Null

    $runShow = Invoke-AzCli -Arguments @(
        'load', 'test-run', 'show',
        '--resource-group', $ResourceGroup,
        '--load-test-resource', $LoadTestResource,
        '--test-run-id', $runId,
        '--query', '{portalUrl:portalUrl,status:status}',
        '-o', 'json'
    ) -AllowFailure

    $runUrl = ''
    $runStatus = 'Unknown'

    if ($runShow.Success -and -not [string]::IsNullOrWhiteSpace($runShow.Text)) {
        try {
            $runMeta = $runShow.Text | ConvertFrom-Json
            if ($null -ne $runMeta.portalUrl) {
                $runUrl = [string]$runMeta.portalUrl
            }
            if ($null -ne $runMeta.status) {
                $runStatus = [string]$runMeta.status
            }
        }
        catch {
            $runStatus = 'Unknown'
        }
    }

    Write-Host "Run completed: $runId (status: $runStatus)" -ForegroundColor Green
    if (-not [string]::IsNullOrWhiteSpace($runUrl)) {
        Write-Host "Portal URL: $runUrl" -ForegroundColor DarkGray
    }

    return [pscustomobject]@{
        RunId = $runId
        Status = $runStatus
        PortalUrl = $runUrl
    }
}

$loadtestingRoot = $PSScriptRoot
$infraRoot = Join-Path $loadtestingRoot 'infra'
$assetsRoot = Join-Path $loadtestingRoot 'assets'
$jmeterRoot = Join-Path $assetsRoot 'jmeter'
$datasetsRoot = Join-Path $jmeterRoot 'datasets'
$payloadRoot = Join-Path $assetsRoot 'payloads'

$templateFile = Join-Path $infraRoot 'loadtesting.bicep'
$paramTemplateFile = Join-Path $infraRoot 'loadtesting.bicepparam'

$baselineJmx = Join-Path $jmeterRoot 'json-baseline.jmx'
$capacityJmx = Join-Path $jmeterRoot 'json-capacity.jmx'
$warmupCsv = Join-Path $datasetsRoot 'warmup-payloads.csv'
$baselineCsv = Join-Path $datasetsRoot 'baseline-payloads.csv'
$capacityCsv = Join-Path $datasetsRoot 'capacity-payloads.csv'

Test-AzCliInstalled
Test-AzureLoginContext
Install-LoadExtensionIfNeeded

Write-Step 'Collecting execution inputs'
$resourceGroup = Read-Value -Prompt 'Azure resource group' -Default $DefaultResourceGroup

$loadTestResourceName = $DefaultLoadTestResourceName

if (Read-YesNo -Prompt 'Deploy or update Azure Load Testing resource now?' -Default $true) {
    Write-Step 'Scanning parameter files'
    $parameterFiles = Get-BicepParamFiles -RootPath $infraRoot

    $defaultParamFilePath = ''
    $preferredDevFile = Join-Path $infraRoot 'loadtesting.dev.bicepparam'
    if (Test-Path $preferredDevFile) {
        $defaultParamFilePath = $preferredDevFile
    }
    elseif (Test-Path $paramTemplateFile) {
        $defaultParamFilePath = $paramTemplateFile
    }

    $selectedParamFile = Select-BicepParamFile -Files $parameterFiles -DefaultFilePath $defaultParamFilePath
    $paramFile = $selectedParamFile.FullName
    Write-Host "Using parameter file: $paramFile" -ForegroundColor DarkGray

    if (Read-YesNo -Prompt 'Run what-if before deployment?' -Default $true) {
        Write-Step 'Running what-if deployment'
        Invoke-AzCli -Arguments @(
            'deployment', 'group', 'what-if',
            '--resource-group', $resourceGroup,
            '--template-file', $templateFile,
            '--parameters', $paramFile
        ) | Out-Null
    }

    $deploymentNameDefault = "loadtesting-$(Get-Date -Format 'yyyyMMddHHmmss')"
    $deploymentName = Read-Value -Prompt 'Deployment name' -Default $deploymentNameDefault

    Write-Step 'Deploying Azure Load Testing infrastructure'
    Invoke-AzCli -Arguments @(
        'deployment', 'group', 'create',
        '--name', $deploymentName,
        '--resource-group', $resourceGroup,
        '--template-file', $templateFile,
        '--parameters', $paramFile
    ) | Out-Null

    try {
        $showDeployment = Invoke-AzCli -Arguments @(
            'deployment', 'group', 'show',
            '--name', $deploymentName,
            '--resource-group', $resourceGroup,
            '--query', 'properties.outputs.loadTestingResourceName.value',
            '-o', 'tsv'
        )
        $loadTestResourceName = $showDeployment.Text
    }
    catch {
        Write-Host 'Could not read load testing resource name from deployment outputs.' -ForegroundColor Yellow
    }
}

$loadTestResourceName = Read-Value -Prompt 'Azure Load Testing resource name' -Default $loadTestResourceName
$targetInput = Read-Value -Prompt 'Container app URL' -Default $DefaultHost
$endpoint = Resolve-TargetEndpoint -InputValue $targetInput -DefaultProtocol 'https'

Write-Host "Resolved endpoint: $($endpoint.Protocol)://$($endpoint.Host)" -ForegroundColor DarkGray

$targetHost = $endpoint.Host
$protocol = $endpoint.Protocol
$engineInstances = [int](Read-Value -Prompt 'Engine instances per test' -Default '3')
$healthPath = '/healthz'
$healthTimeoutSeconds = 300
$healthPollIntervalSeconds = 5

$runBaseline = Read-YesNo -Prompt 'Run baseline scenario?' -Default $true
$runCapacity = Read-YesNo -Prompt 'Run capacity scenario?' -Default $true

if (-not $runBaseline -and -not $runCapacity) {
    Write-Host 'No scenarios selected. Exiting.' -ForegroundColor Yellow
    exit 0
}

if ($runBaseline) {
    $baselineCriteria = @(
        'percentage(error) > 1',
        'avg(response_time_ms) > 15000',
        'avg(requests_per_sec) < 2'
    )
    Initialize-LoadTest -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-baseline' -DisplayName 'JSON baseline' -Description 'Baseline validation for /scan/json with mixed clean and expected-infected payloads.' -TestPlanPath $baselineJmx -EngineInstances $engineInstances -AutostopErrorRate 1 -AutostopTimeWindowSeconds 30 -AutostopEngineUsers 50 -FailureCriteria $baselineCriteria
    Publish-TestAssets -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-baseline' -JmxPath $baselineJmx -DatasetPaths @($baselineCsv, $warmupCsv) -PayloadFolder $payloadRoot

    if (Read-YesNo -Prompt 'Start baseline run now?' -Default $true) {
        Wait-ForHealthEndpoint -Protocol $protocol -TargetHost $targetHost -Path $healthPath -TimeoutSeconds $healthTimeoutSeconds -PollIntervalSeconds $healthPollIntervalSeconds

        $baselineEnv = @{
            HOST = $targetHost
            PROTOCOL = $protocol
            WARMUP_THREADS = (Read-Value -Prompt 'Baseline warm-up clients (threads per engine)' -Default '5')
            WARMUP_LOOPS = (Read-Value -Prompt 'Baseline WARMUP_LOOPS' -Default '5')
            BASELINE_THREADS = (Read-Value -Prompt 'Baseline concurrent clients (threads per engine)' -Default '30')
            BASELINE_RAMP_SECONDS = (Read-Value -Prompt 'BASELINE_RAMP_SECONDS' -Default '30')
            BASELINE_LOOPS = (Read-Value -Prompt 'BASELINE_LOOPS' -Default '50')
            TARGET_RPM = (Read-Value -Prompt 'Baseline TARGET_RPM (calls/min)' -Default '900')
            MAX_RESPONSE_MS = (Read-Value -Prompt 'Baseline MAX_RESPONSE_MS' -Default '15000')
        }

        $resolvedBaselineJmx = New-ResolvedJmxFile -TemplatePath $baselineJmx -Variables $baselineEnv -TargetFileName 'json-baseline.jmx'
        Upload-TestPlanFile -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-baseline' -TestPlanPath $resolvedBaselineJmx

        $baselineRun = Start-LoadTestRun -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-baseline' -Environment $baselineEnv
        if ($baselineRun.Status -match 'FAILED|ERROR|CANCELLED') {
            Write-Host "Baseline run failed fast as expected for mismatch conditions. RunId: $($baselineRun.RunId)" -ForegroundColor Yellow
        }
    }
}

if ($runCapacity) {
    $capacityCriteria = @(
        'percentage(error) > 5',
        'avg(response_time_ms) > 20000'
    )
    Initialize-LoadTest -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-capacity' -DisplayName 'JSON capacity' -Description 'Capacity ramp for /scan/json to identify first unstable throughput point.' -TestPlanPath $capacityJmx -EngineInstances $engineInstances -AutostopErrorRate 5 -AutostopTimeWindowSeconds 60 -AutostopEngineUsers 1000 -FailureCriteria $capacityCriteria
    Publish-TestAssets -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-capacity' -JmxPath $capacityJmx -DatasetPaths @($capacityCsv, $warmupCsv) -PayloadFolder $payloadRoot

    if (Read-YesNo -Prompt 'Start capacity run now?' -Default $true) {
        Wait-ForHealthEndpoint -Protocol $protocol -TargetHost $targetHost -Path $healthPath -TimeoutSeconds $healthTimeoutSeconds -PollIntervalSeconds $healthPollIntervalSeconds

        $capacityBaseEnv = @{
            HOST = $targetHost
            PROTOCOL = $protocol
            WARMUP_THREADS = (Read-Value -Prompt 'Capacity warm-up clients (threads per engine)' -Default '10')
            WARMUP_LOOPS = (Read-Value -Prompt 'Capacity WARMUP_LOOPS' -Default '8')
            CAPACITY_THREADS = (Read-Value -Prompt 'Capacity concurrent clients (threads per engine)' -Default '80')
            CAPACITY_RAMP_SECONDS = (Read-Value -Prompt 'CAPACITY_RAMP_SECONDS' -Default '120')
            CAPACITY_LOOPS = (Read-Value -Prompt 'CAPACITY_LOOPS' -Default '80')
            MAX_RESPONSE_MS = (Read-Value -Prompt 'Capacity MAX_RESPONSE_MS' -Default '20000')
        }

        if (Read-YesNo -Prompt 'Auto-ramp capacity until failure?' -Default $true) {
            $startRpm = [int](Read-Value -Prompt 'Capacity start TARGET_RPM (calls/min)' -Default '1200')
            $stepRpm = [int](Read-Value -Prompt 'Capacity step TARGET_RPM (calls/min)' -Default '300')
            $maxRpm = [int](Read-Value -Prompt 'Capacity max TARGET_RPM (calls/min)' -Default '6000')

            $maxStableRpm = 0
            $failureRunId = ''
            $failureStatus = ''

            for ($targetRpm = $startRpm; $targetRpm -le $maxRpm; $targetRpm += $stepRpm) {
                $capacityEnv = @{}
                foreach ($key in $capacityBaseEnv.Keys) {
                    $capacityEnv[$key] = $capacityBaseEnv[$key]
                }
                $capacityEnv['TARGET_RPM'] = $targetRpm

                Write-Step "Capacity attempt at TARGET_RPM=$targetRpm"
                $resolvedCapacityJmx = New-ResolvedJmxFile -TemplatePath $capacityJmx -Variables $capacityEnv -TargetFileName 'json-capacity.jmx'
                Upload-TestPlanFile -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-capacity' -TestPlanPath $resolvedCapacityJmx

                $capacityRun = Start-LoadTestRun -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-capacity' -Environment $capacityEnv
                if ($capacityRun.Status -match 'FAILED|ERROR|CANCELLED') {
                    $failureRunId = $capacityRun.RunId
                    $failureStatus = $capacityRun.Status
                    break
                }

                $maxStableRpm = $targetRpm
            }

            if (-not [string]::IsNullOrWhiteSpace($failureRunId)) {
                Write-Host "Capacity limit reached. Max stable TARGET_RPM: $maxStableRpm; first failing run: $failureRunId ($failureStatus)" -ForegroundColor Yellow
            }
            else {
                Write-Host "No failing run detected up to TARGET_RPM=$maxRpm. Increase max RPM to continue search." -ForegroundColor Green
            }
        }
        else {
            $capacityEnv = @{}
            foreach ($key in $capacityBaseEnv.Keys) {
                $capacityEnv[$key] = $capacityBaseEnv[$key]
            }
            $capacityEnv['TARGET_RPM'] = (Read-Value -Prompt 'Capacity TARGET_RPM (calls/min)' -Default '1800')

            $resolvedCapacityJmx = New-ResolvedJmxFile -TemplatePath $capacityJmx -Variables $capacityEnv -TargetFileName 'json-capacity.jmx'
            Upload-TestPlanFile -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-capacity' -TestPlanPath $resolvedCapacityJmx

            Start-LoadTestRun -ResourceGroup $resourceGroup -LoadTestResource $loadTestResourceName -TestId 'json-capacity' -Environment $capacityEnv | Out-Null
        }
    }
}

Write-Host "`nAll done." -ForegroundColor Green
