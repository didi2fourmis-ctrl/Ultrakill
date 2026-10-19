@echo off
rem Double-clique sur ce fichier pour compiler le mod et le copier dans le jeu.
set "PROJET=%~dp0MonPremierMod\MonPremierMod.csproj"
if not exist "%PROJET%" goto introuvable
dotnet build "%PROJET%" -c Release
echo.
if errorlevel 1 goto echec
echo OK : le mod a ete copie dans ULTRAKILL\BepInEx\plugins\MonPremierMod
pause
exit /b 0

:echec
echo ECHEC : lis les messages d erreur ci-dessus.
pause
exit /b 1

:introuvable
echo ECHEC : le dossier MonPremierMod est introuvable a cote de compiler.bat.
echo Dossier actuel : "%~dp0"
echo As-tu bien EXTRAIT le ZIP - clic droit puis Extraire tout - avant de lancer ce fichier ?
echo compiler.bat doit etre dans le meme dossier que le dossier MonPremierMod.
pause
exit /b 1
