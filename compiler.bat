@echo off
rem Double-clique sur ce fichier pour compiler le mod et le copier dans le jeu.
cd /d "%~dp0MonPremierMod"
dotnet build -c Release
echo.
if errorlevel 1 (echo ECHEC : lis les messages d erreur ci-dessus.) else (echo OK : le mod a ete copie dans ULTRAKILL\BepInEx\plugins\MonPremierMod)
pause
