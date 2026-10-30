# VHDDSwitch

Utilidad de Windows 10/11 para gestionar discos externos y racks USB por identidad de hardware, sin depender de numeros fijos de disco.

## Documentación y nueva interfaz

La documentación está organizada en [`docs/`](docs/README.md): [ejecución](docs/EJECUCION.md), [configuración](docs/CONFIGURACION.md) y [desarrollo](docs/DESARROLLO.md).

La interfaz rediseñada tiene navegación lateral, tarjetas de unidades, detalles expandibles, indicadores de capacidad y marco nativo de Windows con estilo oscuro. Para revisarla con datos ficticios y sin consultar discos:

```cmd
build.bat dist\Windows11\VHDDSwitch.exe
dist\Windows11\VHDDSwitch.exe --demo
```

La versión alternativa evita reemplazar el ejecutable anterior si está abierto. Sin `--demo`, la aplicación sí consulta discos reales.

## Compilar y abrir

Requiere .NET Framework 4.8 y Windows PowerShell 5.1 con el modulo Storage. No usa NuGet ni descargas.

```cmd
build.bat
dist\VHDDSwitch.exe
```

El ejecutable incluye el motor y el script Storage como recurso. Distribuye `dist/VHDDSwitch.exe`, `dist/settings.json` y `dist/settings.schema.json` juntos. La compilacion tambien produce `build/VHDDSwitch.Core.dll` para integraciones. No sobrescribe una configuracion existente en dist.

La interfaz solicita UAC al arrancar. Incluye perfiles, tarjetas por disco, capacidad, modelos, series, estado, registro de operaciones y bandeja con acciones por perfil. Cerrar la ventana la oculta cuando `minimizeToTray` esta activado; usa **Salir** en la bandeja para terminar.

## Configuracion

Edita `dist/settings.json` con la aplicacion cerrada. `settings.json` en la raiz es la plantilla inicial. El filtro admite `*` y `?` y compara FriendlyName sin distinguir mayusculas. Windows no siempre expone el nombre del controlador USB en FriendlyName: comprueba el modelo reportado antes de configurar un perfil. Las letras son referencias visuales y nunca seleccionan discos.

Esta version admite tema Dark y requiere administrador mediante manifest; otras opciones para esos campos se rechazan. `expectedBays` solo compara el numero esperado con el detectado, sin inventar posiciones fisicas. Los perfiles pueden coincidir con mas de una caja del mismo modelo; usa filtros precisos. La autoactivacion esta deshabilitada por defecto. Si se habilita, actua sobre discos offline que aparecen despues de la primera deteccion, no sobre los ya presentes al arrancar.

## CLI

```cmd
scripts\VHDDSwitch.bat
scripts\VHDDSwitch.bat status
scripts\VHDDSwitch.bat mount *JMicron*
scripts\VHDDSwitch.bat eject *TOSHIBA*
```

El CLI usa el mismo motor que la interfaz. `mount` y `eject` solicitan UAC y devuelven codigo 1 ante fallos; `status` no solicita elevacion automaticamente (algunos equipos requieren ejecutar una consola de administrador para consultar Storage). El lanzador permite ejecutar el script local con ExecutionPolicy Bypass solo en ese proceso, sin cambiar la politica persistente. Las letras del antiguo CLI ya no se usan como objetivos.

Para crear un acceso directo opcional:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Create-DesktopShortcut.ps1
```

## Integridad de los datos

- Solo se modifican buses USB, 1394, SD o MMC. SATA/NVMe internos, discos de sistema/arranque, C: y el disco de SystemDrive se rechazan.
- Antes de cada cambio se descubre de nuevo el disco por UniqueId y serie; identidades ausentes o ambiguas se rechazan. El proceso de cambio vuelve a comprobar identidad y proteccion.
- Para desmontar se abren rutas GUID, se bloquean TODOS los volumenes, se vacia la cache y despues se desmontan. Un bloqueo o vaciado fallido aborta ese disco. Los handles se conservan hasta completar Offline y se liberan incluso si hay errores.
- Cada disco tiene resultado independiente. Una operacion sobre un perfil puede completarse parcialmente; consulta el registro y no desconectes discos con errores.
- Offline es un estado logico: no garantiza parada del motor, aparcado de cabezales ni retirada del dispositivo USB. Para desconectar fisicamente usa tambien la extraccion segura de Windows y las instrucciones del fabricante.
- No se usa el verbo Shell Eject antes del bloqueo: podria retirar el dispositivo antes de poder verificar la secuencia.
- Las consultas y cambios PowerShell tienen limite de 45 segundos y se ejecutan fuera del hilo de interfaz. Una llamada nativa de almacenamiento puede esperar al controlador; no se promete cancelacion de E/S bloqueada. Ante timeout, verifica el estado antes de reintentar.

## Archivos ocupados

**Archivo bloqueado...** consulta Windows Restart Manager para un archivo seleccionado y muestra proceso/PID. Puede solicitar el cierre normal de la ventana, comprobando la fecha de creacion del proceso para evitar PID reutilizado. No fuerza terminaciones ni cierra servicios.

Restart Manager no enumera todos los handles de un volumen usando su raiz. Por eso la deteccion aqui es por archivo, a peticion, y no una auditoria exhaustiva automatica. Un resultado vacio no demuestra que el volumen este libre. El bloqueo real del volumen sigue siendo obligatorio antes de desmontar.

## Verificacion

```cmd
tests\run.bat
```

Compila y comprueba filtros, proteccion de discos, configuracion y propagacion de errores del proceso PowerShell. Estas pruebas no consultan ni modifican discos fisicos.

Validado: compilacion con csc de .NET Framework, 12 comprobaciones del motor y 8 de la interfaz con datos simulados. La nueva version ocupa aproximadamente 64 KB. Se revisaron renderizaciones de los controles en formato amplio y compacto. Se realizo una consulta de solo lectura de topologia en el desarrollo inicial; no se ejecutaron montajes, desmontajes, vaciados ni cierres de procesos sobre hardware real. En el rediseño no se consultaron discos reales. Las operaciones de almacenamiento y la ventana en vivo quedan pendientes de una prueba manual con discos de prueba disponibles.

## Estructura

- `src/VHDDSwitch.Core`: modelos, configuracion, transporte Storage, Win32 y Restart Manager.
- `src/VHDDSwitch.Desktop`: Windows Forms y manifest UAC/DPI.
- `scripts`: CLI y acceso directo.
- `tests`: comprobaciones sin operaciones sobre discos.
- `.workspace/BUILD_PLAN.md`: plan original; algunas premisas se han ajustado segun las limitaciones de Windows descritas arriba.
