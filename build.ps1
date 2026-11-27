<#
.SYNOPSIS
    Script de build automatizado do Costa Livre 2 para Android, Windows, Linux e Xbox (UWP .appx / GDK).
.EXAMPLE
    .\build.ps1 -Target windows
    .\build.ps1 -Target xbox
    .\build.ps1 -Target all
#>
param(
    [ValidateSet("menu", "android", "windows", "linux", "xbox", "all")]
    [string]$Target = "menu",
    [string]$UnityPath = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$BuildsDir = Join-Path $ProjectRoot "Builds"
New-Item -ItemType Directory -Force -Path $BuildsDir | Out-Null

function Resolve-UnityExe {
    if ($UnityPath -and (Test-Path $UnityPath)) { return $UnityPath }
    $versionFile = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
    $editorVersion = "6000.3.29f1"
    if (Test-Path $versionFile) {
        $match = Select-String -Path $versionFile -Pattern "m_EditorVersion:\s*(.+)" | Select-Object -First 1
        if ($match) { $editorVersion = $match.Matches.Groups[1].Value.Trim() }
    }
    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe",
        "D:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe",
        "$env:LOCALAPPDATA\Programs\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
    }
    throw "Unity $editorVersion nao encontrado. Informe -UnityPath 'C:\...\Unity.exe'."
}

function Resolve-MSBuild {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }
    throw "MSBuild nao encontrado. Instale o Visual Studio com suporte a UWP/C++."
}

function Invoke-UnityBuild([string]$BuildTarget, [string]$Method, [string]$LogName) {
    $unity = Resolve-UnityExe
    $log = Join-Path $BuildsDir $LogName
    Write-Host "`n==> Iniciando Unity ($BuildTarget -> $Method)..." -ForegroundColor Cyan
    Write-Host "    Log: $log"
    $args = @(
        "-batchmode",
        "-quit",
        "-projectPath", "`"$ProjectRoot`"",
        "-buildTarget", $BuildTarget,
        "-executeMethod", $Method,
        "-logFile", "`"$log`""
    )
    $proc = Start-Process -FilePath $unity -ArgumentList $args -Wait -PassThru
    if ($proc.ExitCode -ne 0) {
        Write-Host "ERRO: Unity falhou (codigo $($proc.ExitCode)). Ultimas linhas do log:" -ForegroundColor Red
        if (Test-Path $log) { Get-Content $log -Tail 40 }
        exit $proc.ExitCode
    }
    Write-Host "OK: Unity concluiu $BuildTarget." -ForegroundColor Green
}

function Build-Android {
    Invoke-UnityBuild "Android" "CostaLivre.Editor.CostaPlatformBuild.Android" "build_android.log"
    Write-Host "APK gerado em: $(Join-Path $BuildsDir 'Android\CostaLivre2.apk')" -ForegroundColor Green
}

function Build-Windows {
    Invoke-UnityBuild "Win64" "CostaLivre.Editor.CostaPlatformBuild.Windows" "build_windows.log"
    Write-Host "Build Windows gerado em: $(Join-Path $BuildsDir 'Windows\CostaLivre2.exe')" -ForegroundColor Green
}

function Build-Linux {
    Invoke-UnityBuild "Linux64" "CostaLivre.Editor.CostaPlatformBuild.Linux" "build_linux.log"
    Write-Host "Build Linux gerado em: $(Join-Path $BuildsDir 'Linux\CostaLivre2.x86_64')" -ForegroundColor Green
}

function Build-XboxUwp {
    Invoke-UnityBuild "WSAPlayer" "CostaLivre.Editor.CostaUwpBuild.BuildUwp" "build_xbox_uwp.log"
    $msbuild = Resolve-MSBuild
    $sln = Join-Path $BuildsDir "UWP\Costa Livre 2.sln"
    $pkgDir = (Join-Path $BuildsDir "UWP\AppPackagesAppx") + "\"
    Write-Host "`n==> Empacotando .appx via MSBuild..." -ForegroundColor Cyan
    & $msbuild $sln /t:Build /m `
        /p:Configuration=Master `
        /p:Platform=x64 `
        /p:UapAppxPackageBuildMode=SideloadOnly `
        /p:AppxBundle=Never `
        /p:AppxPackageExtension=.appx `
        /p:GenerateAppxPackageOnBuild=true `
        /p:AppxPackageDir="$pkgDir" `
        /p:AppxPackageSigningEnabled=true `
        /v:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERRO: MSBuild falhou ao gerar o .appx." -ForegroundColor Red
        exit $LASTEXITCODE
    }
    Write-Host "APPX gerado em: $pkgDir" -ForegroundColor Green
}

if ($Target -eq "menu") {
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  COSTA LIVRE 2 - GERADOR DE BUILDS" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "1) Windows (x64)"
    Write-Host "2) Linux (x64)"
    Write-Host "3) Android (APK)"
    Write-Host "4) Xbox UWP Dev Mode (.appx)"
    Write-Host "5) Todas as plataformas (Windows, Linux, Android, Xbox UWP)"
    $choice = Read-Host "Escolha uma opcao [1-5]"
    $Target = switch ($choice) {
        "1" { "windows" }
        "2" { "linux" }
        "3" { "android" }
        "4" { "xbox" }
        "5" { "all" }
        default { Write-Host "Opcao invalida."; exit 1 }
    }
}

switch ($Target) {
    "android"     { Build-Android }
    "windows"     { Build-Windows }
    "linux"       { Build-Linux }
    "xbox"        { Build-XboxUwp }
    "all" {
        Build-Windows
        Build-Linux
        Build-Android
        Build-XboxUwp
    }
}
