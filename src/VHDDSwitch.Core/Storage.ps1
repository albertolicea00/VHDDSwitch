$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$result = @(Get-Disk | ForEach-Object {
    $d = $_
    $parts = @(Get-Partition -DiskNumber $d.Number -ErrorAction Stop)
    $protected = $d.IsBoot -or $d.IsSystem -or ($parts | Where-Object { $_.IsBoot -or $_.IsSystem -or $_.DriveLetter -eq 'C' -or ($_.DriveLetter -and ($_.DriveLetter.ToString() + ':') -eq $env:SystemDrive) }).Count -gt 0
    $volumes = @($parts | ForEach-Object {
        $p = $_
        $v = $p | Get-Volume -ErrorAction Stop
        if ($v) {
            [pscustomobject]@{ Path=[string]$v.Path; DriveLetter=[string]$v.DriveLetter; Label=[string]$v.FileSystemLabel; FileSystem=[string]$v.FileSystem; TotalBytes=[long]$v.Size; FreeBytes=[long]$v.SizeRemaining }
        }
    })
    [pscustomobject]@{ Number=[int]$d.Number; UniqueId=[string]$d.UniqueId; FriendlyName=[string]$d.FriendlyName; SerialNumber=[string]$d.SerialNumber; BusType=[string]$d.BusType; OperationalStatus=($d.OperationalStatus -join ', '); HealthStatus=[string]$d.HealthStatus; PartitionStyle=[string]$d.PartitionStyle; IsOffline=[bool]$d.IsOffline; Protected=[bool]$protected; Size=[long]$d.Size; Volumes=$volumes }
})
ConvertTo-Json -InputObject $result -Depth 6 -Compress
