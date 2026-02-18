param(
    [string]$MetricsFile = "docs/release-metrics.json",
    [string]$AuditBasePath = "$env:APPDATA\SegmentApp",
    [int]$AuditCheckpointInterval = 100,
    [double]$LatencyP95TargetMs = 700,
    [double]$CrashFreeSessionTarget = 0.995,
    [double]$GlossaryDeterminismTarget = 0.995,
    [double]$MigrationSuccessTarget = 0.995,
    [string]$AuditSigningKey = $env:SEGMENT_AUDIT_CHECKPOINT_KEY
)

$ErrorActionPreference = "Stop"

function Compute-Sha256Hex([string]$value) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash($bytes)
    } finally {
        $sha.Dispose()
    }
    return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
}

function Compute-HmacHex([string]$key, [string]$value) {
    $hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($key))
    try {
        $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($value))
    } finally {
        $hmac.Dispose()
    }
    return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
}

function Test-AuditIntegrity {
    param(
        [string]$BasePath,
        [int]$Interval,
        [string]$SigningKey
    )

    $auditPath = Join-Path $BasePath "compliance_audit.jsonl"
    $checkpointPath = Join-Path $BasePath "compliance_audit.checkpoints.json"

    if (!(Test-Path $auditPath)) {
        Write-Host "[FAIL] Audit log not found at $auditPath."
        return $false
    }

    $lines = Get-Content -Path $auditPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($lines.Count -eq 0) {
        Write-Host "[INFO] Audit log is empty; integrity gate passed."
        return $true
    }

    if (!(Test-Path $checkpointPath)) {
        Write-Host "[FAIL] Checkpoint file missing: $checkpointPath"
        return $false
    }

    $stored = Get-Content -Raw -Path $checkpointPath | ConvertFrom-Json
    if ($null -eq $stored -or $stored.Count -eq 0) {
        Write-Host "[FAIL] Checkpoint file is empty for non-empty audit log."
        return $false
    }

    $expected = @()
    $prev = "GENESIS"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $current = Compute-Sha256Hex "$prev|$($lines[$i].Trim())"
        $prev = $current
        $isInterval = (($i + 1) % [Math]::Max(1, $Interval)) -eq 0
        $isLast = $i -eq ($lines.Count - 1)
        if ($isInterval -or $isLast) {
            $sig = ""
            if (-not [string]::IsNullOrWhiteSpace($SigningKey)) {
                $sig = Compute-HmacHex $SigningKey "$i`:$current"
            }
            $expected += [PSCustomObject]@{
                RecordIndex = $i
                ChainHash = $current
                Signature = $sig
            }
        }
    }

    if ($stored.Count -ne $expected.Count) {
        Write-Host "[FAIL] Checkpoint count mismatch. expected=$($expected.Count) actual=$($stored.Count)"
        return $false
    }

    for ($i = 0; $i -lt $expected.Count; $i++) {
        if (($stored[$i].RecordIndex -ne $expected[$i].RecordIndex) -or ($stored[$i].ChainHash -ne $expected[$i].ChainHash)) {
            Write-Host "[FAIL] Hash chain mismatch at checkpoint $i."
            return $false
        }

        if (-not [string]::IsNullOrWhiteSpace($SigningKey)) {
            if ($stored[$i].Signature -ne $expected[$i].Signature) {
                Write-Host "[FAIL] Signature mismatch at checkpoint $i."
                return $false
            }
        }
    }

    Write-Host "[PASS] Audit hash chain/signature checkpoints verified."
    return $true
}

function Assert-Gate([string]$name, [double]$actual, [double]$target, [bool]$higherIsBetter = $true) {
    if ($higherIsBetter) {
        if ($actual -lt $target) {
            throw "[FAIL] $name gate failed. actual=$actual target>=$target"
        }
    } else {
        if ($actual -gt $target) {
            throw "[FAIL] $name gate failed. actual=$actual target<=$target"
        }
    }
    Write-Host "[PASS] $name gate passed. actual=$actual target=$target"
}

Write-Host "[INFO] Running release-gate test suite..."
dotnet test Segment.Tests/Segment.Tests.csproj --filter "ReleaseGate=Must|FullyQualifiedName~GlossaryResolverServiceTests|FullyQualifiedName~GlossarySqliteStoreMigrationTests|FullyQualifiedName~ReflexLatencyMetricsServiceTests" --nologo

if (!(Test-AuditIntegrity -BasePath $AuditBasePath -Interval $AuditCheckpointInterval -SigningKey $AuditSigningKey)) {
    throw "[FAIL] Audit integrity gate failed."
}

if (!(Test-Path $MetricsFile)) {
    throw "[FAIL] Metrics file not found: $MetricsFile"
}

$metrics = Get-Content -Raw -Path $MetricsFile | ConvertFrom-Json

Assert-Gate -name "Latency p95 (ms)" -actual ([double]$metrics.latency_p95_ms) -target $LatencyP95TargetMs -higherIsBetter $false
Assert-Gate -name "Crash-free sessions" -actual ([double]$metrics.crash_free_session_rate) -target $CrashFreeSessionTarget
Assert-Gate -name "Glossary determinism" -actual ([double]$metrics.glossary_determinism_pass_rate) -target $GlossaryDeterminismTarget
Assert-Gate -name "Migration success" -actual ([double]$metrics.migration_success_rate) -target $MigrationSuccessTarget

Write-Host "[PASS] Release readiness gates satisfied."
