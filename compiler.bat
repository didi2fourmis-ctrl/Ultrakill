@echo off
rem Double-clique sur ce fichier pour compiler TOUS les mods de ce dossier et les copier dans le jeu.
set "RACINE=%~dp0"
if not exist "%RACINE%Directory.Build.props" goto introuvable
set ERREUR=0
for /d %%D in ("%RACINE%*") do if exist "%%D\%%~nxD.csproj" (
  echo.
  echo ===== Compilation de %%~nxD =====
  dotnet build "%%D\%%~nxD.csproj" -c Release
  if errorlevel 1 set ERREUR=1
)
echo.
if "%ERREUR%"=="1" goto echec
echo OK : les mods ont ete copies dans ULTRAKILL\BepInEx\plugins
pause
exit /b 0

:echec
echo ECHEC : lis les messages d erreur ci-dessus.
pause
exit /b 1

:introuvable
echo ECHEC : les fichiers du projet sont introuvables a cote de compiler.bat.
echo Dossier actuel : "%~dp0"
echo As-tu bien EXTRAIT le ZIP - clic droit puis Extraire tout - avant de lancer ce fichier ?
pause
exit /b 1
