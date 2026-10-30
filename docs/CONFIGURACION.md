# Configuración

Edita `dist/settings.json` con la aplicación cerrada. El archivo de la raíz es la plantilla de compilación; modificarlo no reemplaza una configuración que ya exista en `dist`.

Para la nueva versión generada en `dist/Windows11`, edita `dist/Windows11/settings.json`. Cada ejecutable carga la configuración que tiene a su lado.

## Aplicación

| Campo | Comportamiento |
| --- | --- |
| `theme` | Esta versión admite `Dark`. |
| `refreshIntervalSeconds` | Intervalo de detección, entre 2 y 3600 segundos. |
| `minimizeToTray` | Oculta la ventana normal al minimizar o cerrar. |
| `requireAdminOnStartup` | Debe ser `true`; el manifest exige administrador. |

La interfaz usa tonos neutros oscuros, acento azul, fuentes Segoe UI y símbolos de Windows. Los atributos de la barra de ventana se aplican mediante DWM cuando están disponibles. Es Windows Forms con controles dibujados para este proyecto; no depende de WinUI 3 ni usa Mica como fondo del contenido. La escala se calcula al iniciar; si cambias la escala de pantalla, reinicia la aplicación.

## Perfiles

```json
{
  "id": "orico-5bay",
  "name": "Orico 5-Bay Storage Rack",
  "hardwareFilter": "*JMicron*",
  "expectedBays": 5,
  "referenceLetters": ["O", "P", "Q", "R", "S"],
  "autoMountOnDetect": false
}
```

| Campo | Uso |
| --- | --- |
| `id` | Identificador único, obligatorio. |
| `name` | Nombre del perfil mostrado en la cabecera y bandeja. |
| `hardwareFilter` | Compara `FriendlyName`, sin distinguir mayúsculas. `*` significa cualquier secuencia y `?` un carácter. |
| `expectedBays` | Entre 1 y 64; referencia del número esperado de discos, no asigna bahías físicas. |
| `referenceLetters` | Referencias de configuración; nunca se usan para seleccionar ni modificar unidades. Las tarjetas muestran las letras reales detectadas. |
| `autoMountOnDetect` | Si es `true`, intenta activar discos offline nuevos después de la primera detección. |

Usa filtros precisos: dos cajas con el mismo `FriendlyName` pueden coincidir con el mismo perfil. No se identifica el controlador USB padre; se compara el nombre que expone Windows para el disco.

Aunque un perfil coincida con un disco interno o del sistema, el motor rechaza su modificación. Cambiar el filtro no desactiva esa protección.

Durante la revisión con discos ocupados, conserva `autoMountOnDetect: false` y abre la interfaz con `--demo`.
