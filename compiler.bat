@echo off
rem Double-clique sur ce fichier pour compiler le mod et le copier dans le jeu.
set "PROJET=%~dp0MonPremierMod\MonPremierMod.csproj"
if not exist "%PROJET%" (
  echo ECHEC : le dossier MonPremierMod est introuvable a cote de compiler.bat.
  echo Dossier actuel : %~dp0
  echo As-tu bien EXTRAIT le ZIP ^(clic droit - Extraire tout^) avant de lancer ce fichier ?
  echo compiler.bat doit etre dans le meme dossier que le dossier MonPremierMod.
  pause
  exit /b 1
)
dotnet build "%PROJET%" -c Release
echo.
if errorlevel 1 (echo ECHEC : lis les messages d erreur ci-dessus.) else (echo OK : le mod a ete copie dans ULTRAKILL\BepInEx\plugins\MonPremierMod)
pause
