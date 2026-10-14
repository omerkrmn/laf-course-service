# LAF Course Service - Windows Server & IIS Publish Script
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  LAF Course Service - Production Publish for IIS" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "src\Laf.CourseService"
$publishDir = Join-Path $scriptDir "publish_output"

if (Test-Path $publishDir) {
    Write-Host "Temizleniyor: $publishDir" -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}

$appcmd = "$env:SystemRoot\system32\inetsrv\appcmd.exe"
$poolStopped = $false

if (Test-Path $appcmd) {
    Write-Host "IIS LafCoursePool durduruluyor (Dosya kilidini acmak icin)..." -ForegroundColor Yellow
    & $appcmd stop apppool /apppool.name:"LafCoursePool" 2>$null
    $poolStopped = $true
    Start-Sleep -Seconds 1
}

Write-Host "Publish islemi baslatiliyor..." -ForegroundColor Green
dotnet publish "$projectDir\Laf.CourseService.csproj" -c Release -o "$publishDir"
$publishExitCode = $LASTEXITCODE

if ($poolStopped) {
    Write-Host "IIS LafCoursePool baslatiliyor..." -ForegroundColor Yellow
    & $appcmd start apppool /apppool.name:"LafCoursePool" 2>$null
}

if ($publishExitCode -eq 0) {
    Write-Host "`n[BASARILI] Proje basariyla derlendi ve yayinlandi!" -ForegroundColor Green
    Write-Host "Yayinlanan Dizin: $publishDir" -ForegroundColor White
    Write-Host "web.config dogrulaniyor..." -ForegroundColor White
    if (Test-Path "$publishDir\web.config") {
        Write-Host "  -> web.config mevcut (IIS In-Process + 500MB + HLS MIME Types)" -ForegroundColor Green
    } else {
        Write-Host "  -> [UYARI] web.config bulunamadi!" -ForegroundColor Red
    }
    Write-Host "`nWindows Server IIS'e aktarilacak dosyalar '$publishDir' klasorundedir." -ForegroundColor Cyan
} else {
    Write-Host "`n[HATA] Derleme sirasinda hata olustu!" -ForegroundColor Red
}
