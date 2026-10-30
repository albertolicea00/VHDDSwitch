# Compilar y ejecutar

## Requisitos

Windows 10/11, .NET Framework 4.8 y Windows PowerShell 5.1 con el módulo Storage. El compilador se busca en las carpetas de .NET Framework de Windows. No se usa NuGet ni se descargan dependencias al compilar.

## Compilar

Desde CMD, en la carpeta del proyecto:

```cmd
build.bat
```

Desde PowerShell:

```powershell
.\build.bat
```

Genera `dist/VHDDSwitch.exe` y `build/VHDDSwitch.Core.dll`. El ejecutable lleva el motor integrado y no necesita esa DLL. `settings.json` se copia a `dist` solo si no existe allí; la configuración personalizada se conserva.

Si la aplicación está abierta, Windows puede impedir reemplazar el ejecutable. Cierra la aplicación desde **Salir** en la bandeja y vuelve a compilar, o genera otro archivo:

```cmd
build.bat dist\Windows11\VHDDSwitch.exe
```

La versión de la interfaz rediseñada se ha generado con este último nombre para conservar la instancia anterior que estaba en uso.

## Revisar el diseño sin consultar discos

Puedes abrir `scripts/Vista-Previa.bat` con doble clic. Este lanzador siempre pasa `--demo` y compila la nueva versión si todavía no existe.

```cmd
dist\Windows11\VHDDSwitch.exe --demo
```

La ventana muestra **Demostración** y usa datos ficticios. Actualizar no consulta hardware; montar y desmontar solo cambian esos datos en memoria. No se inicia el temporizador de detección y la consulta de archivos ocupados está desactivada. Los perfiles de la bandeja también respetan este modo. El manifest del ejecutable sigue solicitando UAC al arrancar.

Esta es la forma indicada para revisar la interfaz mientras los discos reales están en uso.

## Ejecución normal

```cmd
dist\Windows11\VHDDSwitch.exe
```

Sin `--demo`, la aplicación consulta el almacenamiento al abrirse y periódicamente. **Montar todos** y **Desmontar todos** solicitan confirmación antes de actuar sobre los discos coincidentes del perfil activo. La función de montaje automático depende de la configuración y está desactivada por defecto.

1. Elige un dispositivo en la barra lateral.
2. Consulta el número de discos detectados, capacidad y espacio disponible.
3. Cada tarjeta muestra letras, etiquetas, estado y capacidad utilizada.
4. Abre **Detalles** para consultar modelo, serie y salud.
5. Revisa **Actividad reciente** o **Ver registro** para ver resultados y errores.

Un perfil puede terminar con resultados parciales: revisa cada error antes de desconectar hardware. Offline no garantiza apagado del motor ni expulsión física del USB.

## Bandeja y salida

Con `minimizeToTray: true`, minimizar o cerrar la ventana normal la oculta. Doble clic en el icono de bandeja la restaura. **Salir** en el menú de bandeja termina la aplicación cuando no hay una operación en curso. La demostración se cierra directamente con el botón de cerrar.

## Acceso directo

Para la nueva versión:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\Create-DesktopShortcut.ps1 -ExecutableName Windows11\VHDDSwitch.exe
```

El acceso directo normal no incluye `--demo`. Puedes añadir ese argumento manualmente si quieres usarlo exclusivamente para revisar el diseño.

## CLI

```cmd
scripts\VHDDSwitch.bat help
scripts\VHDDSwitch.bat status
scripts\VHDDSwitch.bat mount *JMicron*
scripts\VHDDSwitch.bat eject *TOSHIBA*
```

El CLI carga `dist/VHDDSwitch.exe`, no el archivo alternativo `Windows11/VHDDSwitch.exe`, y usa un patrón proporcionado por argumento; no lee los perfiles de la interfaz. Ejecuta primero `build.bat` cuando puedas reemplazar el ejecutable principal. No hay modo de demostración en el CLI: `status` consulta hardware y los otros comandos actúan sobre discos. No ejecutes estos comandos para revisar solo el diseño.

Las mutaciones solicitan UAC. El código de salida es 0 al completar y 1 si falla algún disco. En algunos equipos, `status` también necesita una consola de administrador. El lanzador aplica `ExecutionPolicy Bypass` únicamente a su proceso y no modifica la política persistente.

## Distribución

Conserva juntos el ejecutable elegido, `settings.json` y `settings.schema.json`. El archivo de configuración se busca junto al ejecutable, independientemente del directorio desde el que se abra.
