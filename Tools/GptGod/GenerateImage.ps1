<#
.SYNOPSIS
  GPT-GOD 图片生成（gpt-image-2-vip），下载到项目 Assets。

.EXAMPLE
  .\GenerateImage.ps1 -Name browser -Prompt "卡通浏览器ICON..."

.EXAMPLE
  .\GenerateImage.ps1 -BatchFile .\batch-desktop.json -Parallel -ThrottleLimit 5
#>
param(
    [string]$Name,
    [string]$Prompt,
    [string]$PromptB64,
    [string]$Model,
    [string]$Size,
    [string]$OutDir,
    [string]$BatchFile,
    [switch]$Parallel,
    [int]$ThrottleLimit = 5,
    [string]$ConfigPath = "$PSScriptRoot\config.json"
)

if ($PromptB64) {
    $Prompt = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($PromptB64))
}

$ErrorActionPreference = "Stop"
$config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
if (-not $Model) { $Model = $config.defaultModel }
if (-not $Size) { $Size = $config.defaultSize }

$projectRoot = Split-Path $PSScriptRoot -Parent | Split-Path -Parent
function Resolve-OutDir([string]$RelativeOrAbsolute) {
    if (-not $RelativeOrAbsolute) {
        return Join-Path $projectRoot $config.outputDir
    }
    if ([IO.Path]::IsPathRooted($RelativeOrAbsolute)) {
        return $RelativeOrAbsolute
    }
    return Join-Path $projectRoot $RelativeOrAbsolute
}

function Invoke-GptGodImage {
    param(
        [string]$FileStem,
        [string]$PromptText,
        [string]$SizeOverride,
        [string]$OutDirOverride
    )

    $useSize = if ($SizeOverride) { $SizeOverride } else { $Size }
    $destDir = Resolve-OutDir $OutDirOverride
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null

    $bodyObj = @{
        model  = $Model
        prompt = $PromptText
        n      = 1
        size   = $useSize
    }
    $body = $bodyObj | ConvertTo-Json -Compress
    Write-Host "[GptGod] generating: $FileStem ($useSize)"
    Write-Host "  prompt: $PromptText"

    $resp = Invoke-RestMethod -Uri "$($config.baseUrl)/images/generations" -Method POST `
        -Headers @{
            Authorization  = "Bearer $($config.apiKey)"
            "Content-Type" = "application/json"
        } `
        -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) `
        -TimeoutSec 180

    $item = $resp.data[0]
    $outPath = Join-Path $destDir "$FileStem.png"

    if ($item.b64_json) {
        [IO.File]::WriteAllBytes($outPath, [Convert]::FromBase64String($item.b64_json))
    }
    elseif ($item.url) {
        Invoke-WebRequest -Uri $item.url -OutFile $outPath -UseBasicParsing -TimeoutSec 120
    }
    else {
        throw "No url/b64_json in response: $($resp | ConvertTo-Json -Depth 6)"
    }

    Write-Host "[GptGod] saved: $outPath"
    return $outPath
}

if ($BatchFile) {
    $jobs = Get-Content $BatchFile -Raw | ConvertFrom-Json
    if ($Parallel) {
        $running = @()
        $failed = 0
        foreach ($job in $jobs) {
            while (($running | Where-Object { -not $_.HasExited }).Count -ge $ThrottleLimit) {
                Start-Sleep -Seconds 1
            }
            # Prompt 含空格/逗号，用 Base64 避免 Start-Process 参数拆分
            $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$job.prompt))
            $argParts = @(
                "-NoProfile",
                "-ExecutionPolicy Bypass",
                "-File `"$PSCommandPath`"",
                "-Name `"$($job.name)`"",
                "-PromptB64 `"$b64`"",
                "-ConfigPath `"$ConfigPath`""
            )
            if ($job.size) { $argParts += "-Size `"$([string]$job.size)`"" }
            if ($job.outDir) { $argParts += "-OutDir `"$([string]$job.outDir)`"" }
            elseif ($OutDir) { $argParts += "-OutDir `"$OutDir`"" }
            if ($Model) { $argParts += "-Model `"$Model`"" }

            Write-Host "[GptGod] spawn: $($job.name)"
            $running += Start-Process -FilePath "powershell.exe" -ArgumentList ($argParts -join " ") `
                -WorkingDirectory $PSScriptRoot -PassThru -NoNewWindow
        }
        $running | Wait-Process
        foreach ($p in $running) {
            # ExitCode 可能为 $null（短暂竞态）；仅把明确非 0 当失败
            if ($null -ne $p.ExitCode -and $p.ExitCode -ne 0) { $failed++ }
        }
        if ($failed -gt 0) {
            throw "[GptGod] batch parallel finished with $failed failure(s)"
        }
        Write-Host "[GptGod] batch parallel done ($($jobs.Count))"
        return
    }

    foreach ($job in $jobs) {
        $od = if ($job.outDir) { [string]$job.outDir } elseif ($OutDir) { $OutDir } else { $null }
        $sz = if ($job.size) { [string]$job.size } else { $null }
        Invoke-GptGodImage -FileStem $job.name -PromptText $job.prompt -SizeOverride $sz -OutDirOverride $od | Out-Null
    }
    Write-Host "[GptGod] batch done ($($jobs.Count))"
    return
}

if (-not $Name -or -not $Prompt) {
    throw "需要 -Name 与 -Prompt，或使用 -BatchFile"
}

Invoke-GptGodImage -FileStem $Name -PromptText $Prompt -SizeOverride $Size -OutDirOverride $OutDir
