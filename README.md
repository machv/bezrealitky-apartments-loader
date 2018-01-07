# Bezrealitky Loader

Ve složce `src` jsou zdrojáky aplikace, která naète z webu bezrealitky.cz aktivní inzeráty pronájmu bytù v Praze z pøedem specifikovaných èvtrtí (pole `districts`) a uloží výstup do `JSON` a `CSV` souborù ve stejné složce jako zkompilovaný program.

## Statistické zpracování
Kromì naètení výpisu inzerátù je ve složce `stat` projekt pro R studio pro statistické zpracování. Je potøeba do této složky zkopírovat výstupní soubor `Rentals.csv` z aplikace `BezRealitkyLoader`.

Výstupem je HTML soubor.
