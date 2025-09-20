# Preload & Balance Bootstrap

Tento dokument shrnuje nové utility pro rychlejší start hry a ladění balancu bez úprav kódu.

## Soubory
| Soubor | Účel |
|--------|------|
| `Assets/Scripts/Preload/PreloadConfig.cs` | ScriptableObject – definice které Resources cesty a pooly přednačíst. |
| `Assets/Scripts/Preload/Preloader.cs` | MonoBehaviour který krokuje načítání a warmup. Volitelný progress UI. |
| `Assets/Scripts/Preload/AutoPreloader.cs` | Automaticky vytvoří/spustí `Preloader` pokud není ve scéně. |
| `Assets/Scripts/Bootstrap/PlatformPaceBootstrap.cs` | Nastaví rychlost nepřátel dle platformy (mobil = 1×). |
| `Assets/Scripts/Bootstrap/BalanceBootstrap.cs` | Úprava HP faktoru a enemy speed z inspectoru. |
| `Assets/Scripts/Balance/EnemyDynamicBalance.cs` | Runtime hodnota `RapidBaseHitFactor` (baseline HP). |
| `Assets/Scripts/Entities/EnemyPace.cs` | Globální multiplikátor rychlosti pouze pro nepřátele. |

## PreloadConfig – pole
- `resources`: položky (path + loadAll). Path je relativní pod `Resources/`.
- `directAssets`: seznam explicitních asset referencí (prefaby, fonty). Stačí přetáhnout do pole – *touch* probudí jejich závislosti.
- `pools`: ID definované v `ObjectPoolManager` + počet instancí k warmupu (Spawn → Release).
- `yieldBetweenSteps`: pokud true, rozloží každý krok do samostatného frame (nižší CPU špička; doporučeno pro mobily).

## Preloader – použití
1. Vytvoř `PreloadConfig` (Create → BHFD → Preload Config).
2. Přidej `Preloader` do první scény (nebo použij `AutoPreloader`).
3. Přiřaď config + volitelně slider & text pro progress.
4. Nastav:
   - `nextSceneName` (pokud chceš přejít do jiné scény po dokončení), nebo
   - ponech prázdné a `switchToMainMenuState = true` → zavolá `GameManager.ReturnToMenu()`.

### AutoPreloader
Pokud nechceš ručně vkládat `Preloader`:
- Přidej `AutoPreloader` do scény.
- Nastav `explicitConfig` nebo `configResourcePath` (např. uložený config v `Resources/PreloadConfig.asset`).

## BalanceBootstrap
- Pole `rapidBaseHitFactor`: definuje, kolik zásahů základního Rapid Tower tvoří wave 1 HP (default 2). Sniž na 1.3–1.5 pro rychlejší kill feeling.
- `enemySpeedMultiplier`: volitelný override pro `EnemyPace.SpeedMultiplier`. Pokud 0 nebo méně → ignoruje.
- Přepínač `applyInAwake`: jestli aplikovat v `Awake` (nejdřív) nebo `Start` (po ostatních inicializacích).

## Doporučené hodnoty
| Cíl | rapidBaseHitFactor | EnemyPace.SpeedMultiplier |
|-----|--------------------|---------------------------|
| Rychlá akce (casual) | 1.3 – 1.5 | 1.0 – 1.1 |
| Výchozí původní | 2.0 | 0.5 (už nepoužívat globálně; jen pokud chceš slow-mo) |
| Hard / Endurance | 2.5 – 3.0 | 1.0 – 1.2 |

## Typické scénáře
### a) Jen rychlejší mobil
- `PlatformPaceBootstrap` (mobile=1.0) + `BalanceBootstrap` s rapidBaseHitFactor=1.4.

### b) Chci splash / loading obrazovku
- Vytvoř scénu `Preload` → UI logo + progress slider.
- Umísti `Preloader` + config.
- Nastav `nextSceneName = MainMenu`.
- Build nastav jako první scénu v Build Settings.

### c) Přidání nového projectile poolu
1. Přidej pool do `ObjectPoolManager`.
2. V `PreloadConfig` přidej `poolId`, warmCount třeba 8.

## Rozšíření do budoucna
- Addressables async kroky (Add list AsyncEntries + await a progress merge).
- Komprese steps do skupin (Resources vs Pools) s mezititulky.
- Telemetrie: průměrná doba kroku → log / analytics.

## Krátký FAQ
**Proč dělat warmup Spawn/Release?**  Probudí skripty a vyhneš se prvním drobným lagům při skutečné střelbě.

**Může to zvýšit memory footprint?**  Minimálně – instance se stejně vytvoří později; jen je vytvoříš dřív ve vhodný čas.

**Kdy zvednout yieldBetweenSteps = false?**  Na PC s dost výkonem pro okamžitý load bez viditelných dropů.

---
Hotovo: Preload + balanc bootstrap systém připraven. Uprav dle potřeby. Pokud chceš přidat i mini debug overlay (runtime slider), dej vědět.
