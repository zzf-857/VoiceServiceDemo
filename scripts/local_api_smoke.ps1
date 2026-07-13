[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$BaseUrl = "http://127.0.0.1:5055",

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$Token,

    [Parameter()]
    [string]$RequestFile
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd("/")
$authorizedHeaders = @{ Authorization = "Bearer $Token" }

function Assert-StatusCode {
    param(
        [Parameter(Mandatory)]$Response,
        [Parameter(Mandatory)][int]$Expected,
        [Parameter(Mandatory)][string]$CheckName
    )

    if ([int]$Response.StatusCode -ne $Expected) {
        throw "$CheckName failed: expected HTTP $Expected, got $([int]$Response.StatusCode)."
    }
}

function Write-Pass {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host "[PASS] $Message"
}

function ConvertFrom-WebResponseJson {
    param([Parameter(Mandatory)]$Response)

    $content = $Response.Content
    if ($content -is [byte[]]) {
        $content = [Text.Encoding]::UTF8.GetString($content)
    }
    return $content | ConvertFrom-Json
}

$health = Invoke-WebRequest -Uri "$base/health" -Method Get -SkipHttpErrorCheck
Assert-StatusCode -Response $health -Expected 200 -CheckName "Health"
$healthJson = ConvertFrom-WebResponseJson -Response $health
if ($healthJson.status -ne "healthy") {
    throw "Health failed: response status is not healthy."
}
Write-Pass "anonymous health endpoint"

$unauthorized = Invoke-WebRequest -Uri "$base/api/v1/vendors" -Method Get -SkipHttpErrorCheck
Assert-StatusCode -Response $unauthorized -Expected 401 -CheckName "Unauthorized access"
$unauthorizedJson = ConvertFrom-WebResponseJson -Response $unauthorized
if ($unauthorizedJson.code -ne "unauthorized") {
    throw "Unauthorized access failed: stable problem code is missing."
}
Write-Pass "missing Bearer token is rejected"

$vendors = Invoke-WebRequest -Uri "$base/api/v1/vendors" -Method Get -Headers $authorizedHeaders -SkipHttpErrorCheck
Assert-StatusCode -Response $vendors -Expected 200 -CheckName "Authorized vendors"
$vendorJson = @(ConvertFrom-WebResponseJson -Response $vendors)
if ($vendorJson.Count -lt 1) {
    throw "Authorized vendors failed: vendor list is empty."
}
Write-Pass "authorized vendor list"

$openApi = Invoke-WebRequest -Uri "$base/openapi/v1.json" -Method Get -SkipHttpErrorCheck
Assert-StatusCode -Response $openApi -Expected 200 -CheckName "OpenAPI"
$openApiJson = ConvertFrom-WebResponseJson -Response $openApi
if ($openApiJson.openapi -notlike "3.*" -or -not $openApiJson.paths.'/api/v1/tts') {
    throw "OpenAPI failed: expected version or TTS route is missing."
}
Write-Pass "OpenAPI document"

if ($RequestFile) {
    $resolvedRequest = Resolve-Path -LiteralPath $RequestFile -ErrorAction Stop
    $requestJson = Get-Content -Raw -LiteralPath $resolvedRequest
    $null = $requestJson | ConvertFrom-Json
    $audioPath = Join-Path ([System.IO.Path]::GetTempPath()) ("voiceops-smoke-{0}.audio" -f [guid]::NewGuid().ToString("N"))

    try {
        Invoke-WebRequest `
            -Uri "$base/api/v1/tts/audio" `
            -Method Post `
            -Headers $authorizedHeaders `
            -ContentType "application/json" `
            -Body $requestJson `
            -OutFile $audioPath

        $bytes = [System.IO.File]::ReadAllBytes($audioPath)
        if ($bytes.Length -lt 4) {
            throw "Generation failed: audio response is empty."
        }

        $isKnownAudio =
            ($bytes[0] -eq 0x49 -and $bytes[1] -eq 0x44 -and $bytes[2] -eq 0x33) -or
            ($bytes[0] -eq 0xFF -and (($bytes[1] -band 0xE0) -eq 0xE0)) -or
            ([Text.Encoding]::ASCII.GetString($bytes, 0, 4) -in @("RIFF", "OggS", "fLaC"))
        if (-not $isKnownAudio) {
            throw "Generation failed: response does not have a recognized MP3/WAV/Ogg/FLAC signature."
        }

        Write-Pass "optional paid TTS generation returned audio"
    }
    finally {
        Remove-Item -LiteralPath $audioPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Local API smoke checks passed"
