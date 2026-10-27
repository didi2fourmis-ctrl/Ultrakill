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

1. Ouvre `Directory.Build.props` (à la racine) dans le Bloc-notes et vérifie la ligne
   `GamePath` : elle doit pointer vers ton dossier ULTRAKILL. Ce réglage sert à tous les mods.
2. Double-clique sur **`compiler.bat`** (à la racine du dossier). Il faut avoir installé
   le **.NET SDK 8** : https://dotnet.microsoft.com/download
   (ou ouvre le `.csproj` avec Visual Studio, puis **Générer → Générer la solution**).
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

## 5 bis. Le mod « Seulement les pièces » (`SeulementLesPieces/`)

- Tu ne peux tenir **que le Marksman** (le revolver vert) : toute autre arme est remplacée.
- Les ennemis ne prennent des dégâts **que des pièces** : ricoshot et coin punch.
  Tout le reste (tir direct, coup de poing, explosions…) est bloqué.
- **La parade (parry) est désactivée**, mais le bras reste utilisable (coup de poing, coin punch).
- Options dans `BepInEx/config/fr.samandine.seulementlespieces.cfg`.
- Avec `AfficherLogs = true` (désactivé par défaut), la console affiche chaque dégât `AUTORISÉ` ou `bloqué`, avec
  sa source : c'est le moyen de vérifier que le mod fait bien ce qu'on veut.
- Pense à retirer `BepInEx/plugins/MonPremierMod` si tu ne veux plus des dégâts réduits.

Comment ça marche : le jeu appelle `EnemyIdentifier.DeliverDamage` pour blesser un ennemi.
Le mod retient si le code en cours vient d'une pièce (`Coin`) ou d'un tir créé par une
pièce, et annule les dégâts sinon. Le code est commenté dans `SeulementLesPieces/Plugin.cs`.

---

## 5 ter. Le mod « Clones vengeurs » (`ClonesVengeurs/`)

- **Chaque fois qu'un ennemi te fait perdre de la vie, il se duplique** : un clone apparaît à côté de lui.
- Ça marche pour les coups au corps à corps, les tirs, les projectiles et les explosions ennemies.
- Options dans `BepInEx/config/fr.samandine.clonesvengeurs.cfg` :
  `CopiesParCoup` (1), `MaxClones` (20, sécurité anti-lag), `DelaiParEnnemi` (1 seconde),
  `ClonesSeDupliquent` (oui), `ExclureBoss` (non : les boss se dupliquent aussi), `SupprimerClonesALaMort` (oui).
- Les clones ne comptent pas pour les vagues d'arène : il faut toujours tuer les vrais ennemis pour avancer.
- Avec `AfficherLogs = true`, la console affiche `Blessé (100 -> 70) par : ...` et chaque clone créé.
  Si elle affiche `par : inconnu`, le mod n'a pas trouvé le coupable pour cette attaque.

Comment ça marche : `NewMovement.GetHurt` (le joueur prend des dégâts) ne dit pas qui a frappé.
Au démarrage, le mod lit le code du jeu et surveille toutes les fonctions qui appellent `GetHurt`
(projectiles, coups, explosions…). Au moment du coup, il regarde quel objet était en train
d'exécuter son code et remonte jusqu'à l'ennemi (`EnemyIdentifier`). Pour un projectile, il retient
quel ennemi l'a tiré au moment où il apparaît. Puis il copie l'ennemi avec `Instantiate`.

---

## 5 quater. Le mod « Ouroboros » (`Ouroboros/`)

*« Le sang se souvient. »* Le mod enregistre en continu tes mouvements et tes tirs. Quand ton
rang de style monte, des **Échos** apparaissent : des fantômes de toi, 5 secondes dans le
passé, qui refont exactement ce que tu as fait, avec de vrais dégâts.

- **ÉCHOS** : 0 Écho au rang D, 1 en C/B, 2 en A/S, 3 en SS/SSS, 4 au rang ULTRAKILL.
  Revolver (tir chargé compris), shotgun, nailgun, railcannon et roquettes sont imités.
- **BOUCLE CAUSALE** : si toi et un Écho touchez le même ennemi à moins de 0,6 s
  d'intervalle, le second coup fait le double de dégâts.
- **PARADOXE** : un coup de poing sur ton propre Écho te téléporte à sa place, déclenche
  une onde de choc autour de toi et te rend 25 PV.
- **DETTE TEMPORELLE** : si tu retombes au rang D avec des Échos actifs, chacun t'inflige 15 dégâts.
- **F8** active ou désactive le mod. Un compteur `∞ ●●○· ∞` en haut de l'écran montre tes Échos.
- Options dans `BepInEx/config/fr.samandine.ouroboros.cfg`.

Comment ça marche : le code est découpé en plusieurs fichiers.
- `ChronoRecorder.cs` note 60 fois par seconde ta position et ton regard.
- `Patches.cs` s'accroche à la fonction `Shoot` de chaque arme pour noter tes tirs.
- `Echo.cs` relit cet enregistrement avec 5 secondes de retard, et `EchoGunfire.cs`
  rejoue chaque tir avec un rayon (`Physics.Raycast`) qui appelle `EnemyIdentifier.DeliverDamage`.
- `Causality.cs` détecte les BOUCLES CAUSALES grâce à un `Prefix` sur `DeliverDamage`.

Les fonctions du jeu dont les paramètres changent souvent sont appelées par réflexion
(`Reflect.cs`) : si une mise à jour en casse une, seule la fonctionnalité concernée
s'arrête et un avertissement apparaît dans la console.

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
| Erreur « type ou espace de noms introuvable » | `GamePath` incorrect dans `Directory.Build.props` |
| Pas de console au lancement | Active `[Logging.Console] Enabled = true` dans `BepInEx.cfg` |
| Le mod ne se charge pas | Vérifie que le `.dll` est bien dans `BepInEx/plugins/` ; regarde `BepInEx/LogOutput.log` |
| Erreur Harmony « Ambiguous match » | La fonction a plusieurs versions : précise les types de paramètres dans `[HarmonyPatch]` |
