param([ValidateSet('mount','eject','status','help')][string]$Action = 'status', [string]$Pattern = '*JMicron*')
$ErrorActionPreference = 'Stop'
try {
    if ($Action -eq 'help') { Write-Host 'VHDDSwitch.bat [mount|eject|status|help] [patron hardware]'; exit 0 }
    $exe = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\dist\VHDDSwitch.exe'))
    if (-not (Test-Path -LiteralPath $exe)) { throw 'Ejecute build.bat primero.' }
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if ($Action -ne 'status' -and -not $admin) {
        $payload = @{Script=$PSCommandPath; Action=$Action; Pattern=$Pattern} | ConvertTo-Json -Compress
        $data = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($payload))
        $command = '$p=[Text.Encoding]::Unicode.GetString([Convert]::FromBase64String(''' + $data + '''))|ConvertFrom-Json; & $p.Script -Action $p.Action -Pattern $p.Pattern; exit $LASTEXITCODE'
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        $process = Start-Process -FilePath "$PSHOME\powershell.exe" -ArgumentList @('-NoProfile','-EncodedCommand',$encoded) -Verb RunAs -WindowStyle Hidden -Wait -PassThru
        exit $process.ExitCode
    }
    [void][Reflection.Assembly]::LoadFrom($exe)
    $service = New-Object VHDDSwitch.Core.StorageService
    $profile = New-Object VHDDSwitch.Core.EnclosureProfile
    $profile.hardwareFilter = $Pattern
    $disks = @($service.Discover() | Where-Object { $profile.Matches($_) })
    if ($Action -eq 'status') {
        $disks | Format-Table Number,FriendlyName,SerialNumber,BusType,IsOffline,Protected,HealthStatus -AutoSize
        $disks | ForEach-Object { $_.Volumes } | Format-Table DriveLetter,Label,FileSystem,TotalBytes,FreeBytes -AutoSize
        exit 0
    }
    if ($disks.Count -eq 0) { throw 'No hay discos coincidentes.' }
    $failed = 0
    foreach ($disk in $disks) {
        try { $service.Change($disk, ($Action -eq 'mount'), [Action[string]]{param($line) Write-Host $line}) }
        catch { $failed++; Write-Warning $_.Exception.GetBaseException().Message }
    }
    if ($failed -gt 0) { throw "$failed discos sin completar. No desconecte los discos afectados." }
    exit 0
} catch { Write-Error $_ -ErrorAction Continue; exit 1 }
