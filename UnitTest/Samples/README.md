# Corpus Samples

Mini-applications .NET Framework utilisées pour comparer le comportement avant et après
protection par ConfuserEx.

## Règles de déterminisme

Chaque programme est **déterministe** : pour une version compilée donnée, il produit
toujours la même sortie standard et le même code de retour.

Sont exclus :
- `DateTime.Now`, `Guid.NewGuid`, `Random` sans graine
- accès réseau, fichiers externes, saisie utilisateur
- toute source d'entropie non maîtrisée

Le formatage numérique utilise `CultureInfo.InvariantCulture`.

## Compilation

Les trois projets sont inclus dans `Confuser2.sln` sous le dossier de solution `Samples`.

```powershell
# Depuis src/ConfuserEx/
msbuild Confuser2.sln /t:Build /p:Configuration=Release
```

Les binaires Release sont produits dans le dossier `bin\Release\` de chaque projet.

## Exécution

```powershell
# Depuis src/ConfuserEx/
.\UnitTest\Samples\BasicFlow\bin\Release\BasicFlow.exe
.\UnitTest\Samples\ExceptionFlow\bin\Release\ExceptionFlow.exe
.\UnitTest\Samples\Constants\bin\Release\Constants.exe
```

## Échantillons

### BasicFlow

| Propriété       | Valeur                                  |
|-----------------|-----------------------------------------|
| Sortie attendue | `RESULT:PASS`                           |
| Code de sortie  | `0`                                     |
| Couverture      | if/else, switch, for, while, récursion  |

### ExceptionFlow

| Propriété       | Valeur                                                         |
|-----------------|----------------------------------------------------------------|
| Sortie attendue | `RESULT:PASS`                                                  |
| Code de sortie  | `0`                                                            |
| Couverture      | try/catch, try/finally, DivideByZeroException, exception perso |

### Constants

| Propriété       | Valeur                                                                       |
|-----------------|------------------------------------------------------------------------------|
| Sortie attendue | `RESULT:PASS`                                                                |
| Code de sortie  | `0`                                                                          |
| Couverture      | chaînes (vide, ASCII, Unicode, répétées), int Min/Max, long Min/Max,        |
|                 | float NaN, double ±Infinity, −0.0 vs +0.0, formatage InvariantCulture       |
