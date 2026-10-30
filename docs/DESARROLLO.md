# Arquitectura y verificación

## Código

| Ruta | Responsabilidad |
| --- | --- |
| `src/VHDDSwitch.Core/Models.cs` | Modelos, perfiles y validación de configuración. |
| `src/VHDDSwitch.Core/Storage.ps1` | Consulta de discos, particiones y volúmenes. |
| `src/VHDDSwitch.Core/StorageService.cs` | Identidad de discos, restricciones, bloqueo, vaciado, desmontaje y cambio de estado. |
| `src/VHDDSwitch.Core/LockResolver.cs` | Consulta de procesos mediante Restart Manager para archivos concretos. |
| `src/VHDDSwitch.Desktop/Program.cs` | Ventana, navegación, bandeja, registro y coordinación de tareas. |
| `src/VHDDSwitch.Desktop/Fluent.cs` | Controles visuales, estilos DWM, icono y datos simulados. |
| `tests/DesktopPreview.cs` | Entrada de demostración y renderización sin consultas a discos. |

El proyecto compila con `csc.exe` de .NET Framework y sintaxis compatible con C# 5. La interfaz conserva el marco nativo de Windows para el ajuste de ventanas y los botones del sistema. El modo oscuro y el redondeo se solicitan mediante los [atributos oficiales de DWM](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute); si no están disponibles, se conserva el marco que proporciona el sistema.

## Pruebas sin hardware

```cmd
tests\run.bat dist\Windows11\VHDDSwitch.exe
tests\build-preview.bat
build\VHDDSwitch.Preview.exe --render
```

El primer comando compila y ejecuta las comprobaciones del motor con modelos en memoria y procesos PowerShell de prueba. No llama a `Get-Disk` ni modifica discos. El parámetro de salida evita reemplazar el ejecutable anterior si sigue abierto.

El segundo genera una entrada que siempre construye la ventana en modo de demostración. Con `--render`, genera PNG en `build/` sin mostrar la ventana y comprueba navegación, disposición de tarjetas, estados simulados, persistencia de detalles y estado vacío. Las imágenes renderizan los controles del cliente, no la barra de ventana de DWM. Sin argumentos abre la ventana de demostración.

## Límites actuales

- Solo se permiten modificaciones en buses externos USB, 1394, SD o MMC. Los discos del sistema/arranque y C: se rechazan.
- Antes de cambiar el estado se redescubre la identidad. No se actúa usando solamente el número que conserva la interfaz.
- Se bloquean todos los volúmenes antes de desmontarlos. El vaciado o bloqueo fallido aborta ese disco.
- Offline es un estado lógico; no prueba apagado físico ni aparcado de cabezales.
- PowerShell tiene un límite de 45 segundos. Las llamadas nativas dependen del controlador y no ofrecen cancelación garantizada.
- Restart Manager consulta archivos concretos, no todos los handles de un volumen. El cierre de procesos requiere intervención del usuario y solo solicita el cierre normal de la ventana.
- Las operaciones reales de montaje/desmontaje siguen pendientes de validación con discos de prueba disponibles. No deben probarse con las unidades actualmente ocupadas.

El plan original está en `.workspace/BUILD_PLAN.md`; la documentación describe las funciones y límites implementados.
