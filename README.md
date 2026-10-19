# Apprendre à modder ULTRAKILL (en partant de zéro)

Ce dépôt contient un premier mod complet et commenté : **MonPremierMod**, qui réduit
(ou augmente) les dégâts que tu reçois. Le but est de comprendre chaque étape.

---

## 0. Comment ça marche, en une minute

- ULTRAKILL est fait avec le moteur **Unity**. Tout le code du jeu est compilé dans un
  fichier : `ULTRAKILL_Data/Managed/Assembly-CSharp.dll`.
- **BepInEx** est un « chargeur de mods » : au lancement du jeu, il charge les fichiers
  `.dll` placés dans `BepInEx/plugins/`.
- **Harmony** (inclus dans BepInEx) permet de « s'accrocher » à une fonction du jeu pour
  exécuter ton code avant/après, ou modifier ses paramètres.
- Un mod = un petit projet C# que tu compiles en `.dll`.

---

## 1. Installer les outils (une seule fois)

1. **Visual Studio 2022 Community** (gratuit) → https://visualstudio.microsoft.com/
   Pendant l'installation, coche **« Développement .NET Desktop »**.
   (Alternative plus légère : VS Code + le **.NET SDK 8**.)
2. **BepInEx 5** (version *x64*, la 5.4.x) → https://github.com/BepInEx/BepInEx/releases
   - Ouvre le dossier du jeu (Steam → clic droit sur ULTRAKILL → Gérer → Parcourir les fichiers locaux).
   - Dézippe BepInEx **dedans** (à côté de `ULTRAKILL.exe`).
   - Lance le jeu une fois puis quitte-le : les dossiers `BepInEx/plugins` et `BepInEx/config` sont créés.
3. (Recommandé) Dans `BepInEx/config/BepInEx.cfg`, mets `Enabled = true` dans la
   section `[Logging.Console]` pour voir une console avec les messages des mods.
4. **dnSpyEx** ou **ILSpy** → pour **lire le code du jeu** (indispensable !).
   https://github.com/dnSpyEx/dnSpy/releases

> 💡 Alternative pratique : le gestionnaire de mods **r2modman / Thunderstore** installe
> BepInEx pour toi. Mais pour apprendre, l'installation manuelle aide à comprendre.

---

## 2. Compiler le mod d'exemple

1. Ouvre `MonPremierMod/MonPremierMod.csproj` dans un éditeur de texte et vérifie la ligne
   `GamePath` : elle doit pointer vers ton dossier ULTRAKILL.
2. Ouvre le `.csproj` avec Visual Studio, puis **Générer → Générer la solution**
   (ou en ligne de commande, dans le dossier `MonPremierMod` : `dotnet build`).
3. Le fichier `MonPremierMod.dll` est copié automatiquement dans
   `ULTRAKILL/BepInEx/plugins/MonPremierMod/`.
4. Lance le jeu. Dans la console BepInEx tu dois voir : `Mon Premier Mod est chargé !`
5. Prends des dégâts : la console affiche `Dégâts reçus : 30 -> 15`.
6. Change la valeur dans `BepInEx/config/fr.samandine.monpremiermod.cfg` (ex. `2` pour
   doubler les dégâts), relance, observe.

---

## 3. Comprendre le code (`MonPremierMod/Plugin.cs`)

Le fichier est commenté ligne par ligne. Les 3 idées clés :

| Élément | Rôle |
|---|---|
| `[BepInPlugin(...)]` + `class Plugin : BaseUnityPlugin` | Déclare ton mod à BepInEx |
| `Awake()` | Exécutée une fois au chargement : on y crée les options et on active Harmony |
| `[HarmonyPatch(typeof(NewMovement), "GetHurt")]` + `Prefix` | Exécute ton code juste **avant** que le joueur prenne des dégâts |

Comment savoir que le joueur s'appelle `NewMovement` et que la fonction s'appelle
`GetHurt` ? → **En lisant le code du jeu** avec dnSpy (étape suivante).

---

## 4. Explorer le code du jeu avec dnSpy

1. Ouvre dnSpy → *Fichier → Ouvrir* → `ULTRAKILL_Data/Managed/Assembly-CSharp.dll`.
2. Dans l'arbre à gauche, déplie `Assembly-CSharp` → `-` (espace de noms vide).
   Tu vois toutes les classes du jeu.
3. Utilise la recherche (**Ctrl+Maj+K**) pour trouver des classes intéressantes :
   - `NewMovement` → le joueur (vie : `hp`, saut, dash…)
   - `GunControl`, `Revolver`, `Shotgun`, `Nailgun`, `RocketLauncher` → les armes
   - `EnemyIdentifier` → tous les ennemis
   - `StyleHUD` → le système de style
4. Clique sur une fonction pour lire son code. Repère ce que tu veux modifier, puis
   écris un patch Harmony dessus.

> ⚠️ Les noms peuvent changer avec les mises à jour du jeu. Si ton mod ne compile plus
> après une mise à jour, vérifie les noms dans dnSpy.

---

## 5. Petits exercices pour progresser

1. **Facile** : change le message affiché dans `Awake()`. Recompile, vérifie la console.
2. **Facile** : ajoute une option `Activer` (`bool`) et ne modifie les dégâts que si elle vaut `true`.
3. **Moyen** : écris un `Postfix` (code exécuté **après** la fonction) sur
   `NewMovement.GetHurt` qui affiche la vie restante : `__instance.hp`.
   (`__instance` = l'objet sur lequel la fonction est appelée.)
4. **Moyen** : trouve dans dnSpy la fonction qui fait mourir un ennemi
   (indice : cherche dans `EnemyIdentifier`) et affiche un message à chaque kill.
5. **Avancé** : ajoute une méthode `Update()` dans `Plugin` (appelée à chaque image)
   pour réagir à une touche du clavier.

---

## 6. Apprendre le C# (bases utiles)

Pas besoin de tout connaître. Concentre-toi sur : variables, `if`, fonctions, classes.
- Cours gratuit Microsoft (en français) : https://learn.microsoft.com/fr-fr/dotnet/csharp/tour-of-csharp/
- Documentation Harmony : https://harmony.pardeike.net/articles/patching.html
- Documentation BepInEx : https://docs.bepinex.dev/
- Communauté : le serveur Discord de modding d'ULTRAKILL et les mods open source sur
  Thunderstore (https://thunderstore.io/c/ultrakill/) — lire le code des autres est la
  meilleure façon d'apprendre.

---

## 7. En cas de problème

| Problème | Solution |
|---|---|
| Erreur « type ou espace de noms introuvable » | `GamePath` incorrect dans le `.csproj` |
| Pas de console au lancement | Active `[Logging.Console] Enabled = true` dans `BepInEx.cfg` |
| Le mod ne se charge pas | Vérifie que le `.dll` est bien dans `BepInEx/plugins/` ; regarde `BepInEx/LogOutput.log` |
| Erreur Harmony « Ambiguous match » | La fonction a plusieurs versions : précise les types de paramètres dans `[HarmonyPatch]` |
