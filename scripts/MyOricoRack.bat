@echo off
setlocal EnableDelayedExpansion
title VHDDSwitch - Universal External Storage Controller

:: =====================================================================
:: DEFAULT CONFIGURATION (Overridable via CLI arguments or environment)
:: =====================================================================
:: Pattern matched against disk FriendlyName (e.g. *JMicron*, *TOSHIBA*, *ASMedia*, or * for all external)
if "%DEFAULT_PATTERN%"=="" set "DEFAULT_PATTERN=*JMicron*"
if "%DEFAULT_NAME%"=="" set "DEFAULT_NAME=Rack Orico"
if "%DEFAULT_LETTERS%"=="" set "DEFAULT_LETTERS=O,P,Q,R,S"

:: Parse CLI arguments: VHDDSwitch.bat [ACTION] [PATTERN] [LETTERS]
set "ARG_ACTION=%~1"
set "ARG_PATTERN=%~2"
set "ARG_LETTERS=%~3"

if not "%ARG_PATTERN%"=="" set "DEFAULT_PATTERN=%ARG_PATTERN%"
if not "%ARG_LETTERS%"=="" set "DEFAULT_LETTERS=%ARG_LETTERS%"

:: Check if current command is read-only (status / help don't require admin elevation)
if /i "%ARG_ACTION%"=="status" goto DO_STATUS
if /i "%ARG_ACTION%"=="help" goto DO_HELP
if /i "%ARG_ACTION%"=="--help" goto DO_HELP
if /i "%ARG_ACTION%"=="-h" goto DO_HELP

:: Auto-elevate to Administrator if required for mount/eject/menu
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Solicitando permisos de Administrador para gestionar almacenamiento...
    powershell -NoProfile -Command "Start-Process cmd.exe -ArgumentList '/c \"\"%~f0\" %*\"' -Verb RunAs"
    exit /b
)

:: Route direct CLI action if provided
if /i "%ARG_ACTION%"=="mount" goto DO_MOUNT
if /i "%ARG_ACTION%"=="eject" goto DO_EJECT
if /i "%ARG_ACTION%"=="status" goto DO_STATUS
if /i "%ARG_ACTION%"=="help" goto DO_HELP
if /i "%ARG_ACTION%"=="--help" goto DO_HELP
if /i "%ARG_ACTION%"=="-h" goto DO_HELP

:MENU
cls
echo =====================================================================
echo                VHDDSWITCH - CONTROL DE ALMACENAMIENTO
echo =====================================================================
echo  Perfil activo: %DEFAULT_NAME%
echo  Filtro hardware: [%DEFAULT_PATTERN%]
echo  Letras de referencia: [%DEFAULT_LETTERS%]
echo =====================================================================
echo.
echo   [1] EXPULSAR / DESMONTAR (Modo seguro para desconectar o apagar)
echo   [2] MONTAR / CONECTAR (Poner discos en linea y montar letras)
echo   [3] Ver ESTADO actual de los discos y unidades
echo   [4] Cambiar filtro de dispositivo temporalmente
echo   [5] Salir
echo.
echo =====================================================================
set /p "OPC= Selecciona una opcion [1-5] y presiona Enter: "

if "%OPC%"=="1" goto DO_EJECT
if "%OPC%"=="2" goto DO_MOUNT
if "%OPC%"=="3" goto DO_STATUS
if "%OPC%"=="4" goto CONFIG_FILTER
if "%OPC%"=="5" exit /b
goto MENU

:CONFIG_FILTER
cls
echo =====================================================================
echo                 CONFIGURACION TEMPORAL DE FILTRO
echo =====================================================================
echo.
echo  Filtro actual: %DEFAULT_PATTERN%
echo.
echo  Ejemplos comunes:
echo    *JMicron*   - Bahias de Rack Orico
echo    *TOSHIBA*   - Disco externo Toshiba
echo    *ASMedia*   - Cajas USB ASM / Docks NVMe
echo    *           - Todos los discos externos detectados
echo.
set /p "NEW_FILTER= Ingrese el nuevo patron (vacio para cancelar): "
if not "%NEW_FILTER%"=="" (
    set "DEFAULT_PATTERN=%NEW_FILTER%"
    set "DEFAULT_NAME=Personalizado (%NEW_FILTER%)"
)
goto MENU

:DO_EJECT
cls
echo =====================================================================
echo     VHDDSWITCH: EXPULSANDO DISCOS DE FORMA SEGURA [%DEFAULT_PATTERN%]
echo =====================================================================
echo.
echo 1. Vaciando cache y expulsando volumenes asociados...
powershell -NoProfile -Command ^
    "$pattern = '%DEFAULT_PATTERN%';" ^
    "$letters = '%DEFAULT_LETTERS%'.Split(',') | ForEach-Object { $_.Trim().TrimEnd(':') };" ^
    "$shell = New-Object -comObject Shell.Application;" ^
    "$disks = Get-Disk | Where-Object { $_.FriendlyName -like $pattern };" ^
    "$foundLetters = @();" ^
    "if ($disks) {" ^
    "    $foundLetters += ($disks | Get-Partition -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter } | ForEach-Object { $_.DriveLetter.ToString() });" ^
    "};" ^
    "$allLetters = ($letters + $foundLetters) | Select-Object -Unique;" ^
    "foreach ($l in $allLetters) {" ^
    "    $vol = Get-Volume -DriveLetter $l -ErrorAction SilentlyContinue;" ^
    "    if ($vol) {" ^
    "        Write-Host \"  - Expulsando unidad $($l): ($($vol.FileSystemLabel))...\";" ^
    "        $drive = $shell.Namespace(17).ParseName(\"$($l):\");" ^
    "        if ($drive) { $drive.InvokeVerb('Eject'); }" ^
    "    }" ^
    "}"

echo.
echo 2. Poniendo discos fisicos en estado OFFLINE (cabezales aparcados)...
powershell -NoProfile -Command ^
    "$pattern = '%DEFAULT_PATTERN%';" ^
    "$disks = Get-Disk | Where-Object { $_.FriendlyName -like $pattern };" ^
    "if ($disks) {" ^
    "    $disks | ForEach-Object {" ^
    "        Set-Disk -Number $_.Number -IsOffline $true -ErrorAction SilentlyContinue;" ^
    "        Write-Host \"  - Disco $($_.Number) ($($_.FriendlyName)) puesto en OFFLINE seguro.\";" ^
    "    }" ^
    "} else {" ^
    "    Write-Host \"  [i] No se encontraron discos que coincidan con: $pattern\" -ForegroundColor Yellow;" ^
    "}"

echo.
echo =====================================================================
echo  [LISTO] Discos expulsados y apagados de forma segura.
echo         Ya puedes retirar el cable o apagar el hardware sin riesgo.
echo =====================================================================
echo.
if not "%ARG_ACTION%"=="" exit /b
pause
goto MENU

:DO_MOUNT
cls
echo =====================================================================
echo       VHDDSWITCH: MONTANDO DISCOS EN LINEA [%DEFAULT_PATTERN%]
echo =====================================================================
echo.
echo Poniendo discos fisicos en estado ONLINE y activando particiones...
powershell -NoProfile -Command ^
    "$pattern = '%DEFAULT_PATTERN%';" ^
    "$disks = Get-Disk | Where-Object { $_.FriendlyName -like $pattern };" ^
    "if ($disks) {" ^
    "    $disks | ForEach-Object {" ^
    "        Set-Disk -Number $_.Number -IsOffline $false -ErrorAction SilentlyContinue;" ^
    "        Set-Disk -Number $_.Number -IsReadOnly $false -ErrorAction SilentlyContinue;" ^
    "        Write-Host \"  - Disco $($_.Number) ($($_.FriendlyName)) puesto ONLINE.\";" ^
    "    };" ^
    "    Update-HostStorageCache;" ^
    "} else {" ^
    "    Write-Host \"  [!] No se encontraron discos que coincidan con: $pattern\" -ForegroundColor Red;" ^
    "}"

echo.
echo =====================================================================
echo  [LISTO] Discos en linea. Unidades disponibles detectadas:
echo =====================================================================
powershell -NoProfile -Command ^
    "$pattern = '%DEFAULT_PATTERN%';" ^
    "$disks = Get-Disk | Where-Object { $_.FriendlyName -like $pattern };" ^
    "if ($disks) {" ^
    "    $partitions = $disks | Get-Partition -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter };" ^
    "    if ($partitions) {" ^
    "        Get-Volume | Where-Object { $_.DriveLetter -in $partitions.DriveLetter } | Format-Table -AutoSize DriveLetter, FileSystemLabel, FileSystem, HealthStatus, SizeRemaining;" ^
    "    } else {" ^
    "        Write-Host '  Discos en linea. No se detectaron letras de unidad asignadas todavia.';" ^
    "    }" ^
    "}"
echo.
if not "%ARG_ACTION%"=="" exit /b
pause
goto MENU

:DO_STATUS
cls
echo =====================================================================
echo         VHDDSWITCH: ESTADO DE DISCOS [%DEFAULT_PATTERN%]
echo =====================================================================
echo.
echo --- DISCOS FISICOS COINCIDENTES ---
powershell -NoProfile -Command ^
    "$pattern = '%DEFAULT_PATTERN%';" ^
    "Get-Disk | Where-Object { $_.FriendlyName -like $pattern } | Format-Table -AutoSize Number, FriendlyName, OperationalStatus, HealthStatus, BusType, TotalSize"
echo.
echo --- VOLUMENES ASOCIADOS ---
powershell -NoProfile -Command ^
    "$pattern = '%DEFAULT_PATTERN%';" ^
    "$disks = Get-Disk | Where-Object { $_.FriendlyName -like $pattern };" ^
    "if ($disks) {" ^
    "    $partitions = $disks | Get-Partition -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter };" ^
    "    if ($partitions) {" ^
    "        Get-Volume | Where-Object { $_.DriveLetter -in $partitions.DriveLetter } | Format-Table -AutoSize DriveLetter, FileSystemLabel, FileSystem, OperationalStatus, SizeRemaining;" ^
    "    } else {" ^
    "        Write-Host '  No hay letras de unidad montadas activamente para estos discos.';" ^
    "    }" ^
    "}"
echo.
if not "%ARG_ACTION%"=="" exit /b
pause
goto MENU

:DO_HELP
echo =====================================================================
echo              VHDDSWITCH CLI - GUIA DE USO RAPIDO
echo =====================================================================
echo.
echo Sintaxis:
echo   VHDDSwitch.bat [ACCION] [PATRON_HARDWARE] [LETRAS_REFERENCIA]
echo.
echo Acciones disponibles:
echo   mount       Pone los discos en linea y monta las unidades.
echo   eject       Vacia cache y desmonta de forma segura (Offline).
echo   status      Muestra el estado de salud, capacidad y unidades.
echo   help        Muestra esta ayuda.
echo.
echo Ejemplos:
echo   VHDDSwitch.bat mount
echo   VHDDSwitch.bat eject
echo   VHDDSwitch.bat status
echo   VHDDSwitch.bat mount *TOSHIBA*
echo   VHDDSwitch.bat eject *JMicron* O,P,Q,R,S
echo.
exit /b
