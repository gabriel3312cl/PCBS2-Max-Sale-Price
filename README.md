# PCBS2 Max Sale Price

Mod independiente para **PC Building Simulator 2** con BepInEx 6 IL2CPP.

## Funciones

- Añade el botón **PRECIO MÁXIMO** al panel de precio de un PC en venta.
- Mirando un PC, pulsa **N** para abrir el selector remoto de mostrador.
- Permite elegir un mostrador libre, transportar el PC y abrir su panel de precio.
- Usa el precio máximo calculado por el propio juego.

## Instalación

1. Instala BepInEx 6 IL2CPP en PC Building Simulator 2.
2. Descarga `PCBS2MaxSalePrice.dll` desde la sección **Releases**.
3. Copia la DLL en `BepInEx/plugins/PCBS2MaxSalePrice/`.

## Compilar localmente

Requiere Windows, .NET SDK 6 o posterior, el juego y los interop generados por BepInEx. El repositorio no contiene ni distribuye archivos del juego.

```powershell
dotnet build -c Release /p:GameDir="C:\Program Files\Epic Games\PCBuildingSimulator2"
```

Para compilar sin copiar automáticamente la DLL a la carpeta de plugins:

```powershell
dotnet build -c Release /p:GameDir="C:\Program Files\Epic Games\PCBuildingSimulator2" /p:SkipDeploy=true
```

## Release manual en GitHub

El workflow `Manual DLL release` utiliza un runner propio de Windows porque la compilación necesita las DLL locales del juego y de BepInEx. Esas dependencias nunca se suben a GitHub.

1. Registra un runner propio Windows x64 en el repositorio.
2. Asígnale la etiqueta `pcbs2-modding`.
3. Crea la variable del repositorio `PCBS2_GAME_DIR` con la ruta de instalación.
4. Abre **Actions → Manual DLL release → Run workflow**.
5. Introduce una etiqueta nueva, por ejemplo `v0.2.0`.

El release público contiene solamente `PCBS2MaxSalePrice.dll`.

## Licencia

Código distribuido bajo licencia MIT. PC Building Simulator 2 y sus archivos pertenecen a sus respectivos propietarios.