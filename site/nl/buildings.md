---
title: Terrein en gebouwen
kicker: Bouwen
lead: Je kavel is een leeg canvas. Leg wegen, parkeerplaatsen, een hek met poort en de gebouwen zelf aan; bouwvakkers en vrachtwagens doen het werk.
description: Het terrein bouwen in Datacenter Architect, wegen, hekken, gebouwen, verdiepingen, kelders en het bouwproces.
---

## Het kavel

Elk spel begint op een kavel van 64 × 64 m aan een openbare weg met drie carrierputten in de stoep. Later koop je grond bij, tot 256 × 256 m (Groei › Locaties), en open je locaties in andere landen.

De bouwbalk onderin bevat alles wat je in de wereld plaatst:

| Categorie | Wat erin zit |
|---|---|
| Grond | Asfalt, parkeerplaats, voetpaden, fietspaden, gebogen wegen en paden, de bouwmaterialenplek, gras |
| Beveiliging | Hekken (gaas, staalmat, palissade, muur, elektrisch na research), schuifpoorten, voetgangerspoort, slagboom, portiersloge, intercom, camerapalen, palen tegen rammen |
| Gebouwen | Datacenterpand, kantoorpand, warehouse, deuren (enkel, dubbel, brand, badge, rol, sluis), goederenlift, personenlift, trap |
| Ruimtes | Alle ruimtetypes, zie [Ruimtes]({{ '/nl/rooms/' | relative_url }}) |
| Inrichting | Bureaus, stoelen, vergadertafels, koffie- en frisdrankautomaten, banken, terminals, planten, kasten, labtafels, videowalls voor NOC en beveiliging |
| Datazalen | Racks (los, rij of blok), crashkarren, cameradomes |
| Stroom | Transformatoren, schakelkasten, UPS, accu's, PDU's, generatoren, dieseltanks (boven- of ondergronds), zon, wind, batterijcontainers |
| Koeling | CRAH-units, koelmachines, koeltorens, dry coolers |
| Netwerk | Glasvezelput, ODF, core-router, spine-switch |
| Buiten | Bomen, struiken, lantaarnpalen, bankjes, picknicktafels, terrassets, fietsenrekken en -schuren, laadpalen |

> **Ongedaan maken is je vriend.** Elke bouwactie draai je terug met <kbd>Ctrl</kbd>+<kbd>Z</kbd> en doe je opnieuw met <kbd>Ctrl</kbd>+<kbd>Y</kbd> (of de knoppen boven de bulldozer). Het geld komt ook terug.
{: .tip}

## Wegen, parkeren en de toegang

- **Asfalt** draagt vrachtwagens en auto's. Vanaf 6 m breed krijgt een weg een onderbroken middenstreep en rijdt het verkeer rechts; vrachtwagens rijden over hun eigen baan terug.
- **Parkeerplaatsen** krijgen echte vakken van 2,5 × 5 m. Vanaf 10 m diep krijg je twee rijen tegenover elkaar met een rijbaan ertussen. De helft van je personeel wil een parkeerplek. **Laadpalen** maken van een vak een groen laadvak.
- **Voetpaden en fietspaden** laten mensen lopen en fietsen. Een deel van het personeel komt op de fiets en zet die in een **fietsenrek** of **fietsenschuur**.
- **De toegang**: een hek rondom met een **schuifpoort** waar de oprit het hek kruist. Een vrachtwagen of klant meldt zich bij de **intercom**, de beveiliger in de **portiersloge** opent de poort, de klant parkeert en meldt zich bij de beveiliging. Een **slagboom** op de oprit werkt net zo.

{% include shot.html src="commute" caption="Parkeerplaats met laadpalen, een fietsenschuur en fietsers die binnenkomen. Woon-werkverkeer is puur achtergrondleven." %}

## Gebouwen

Er zijn drie soorten gebouwen. Je trekt de plattegrond uit; de bouwkosten gaan per vierkante meter per verdieping.

| Gebouw | Voor | Let op |
|---|---|---|
| Datacenterpand | Datazalen, technische ruimtes, meet-me room, laadperron | Een dichte betonnen doos. Kan een kelder krijgen |
| Kantoorpand | Directiekamers, sales, NOC, SOC, kantine, receptie, lab | Gevel van baksteen, betonlook, glas of panelen. Kan een kelder krijgen. Lege kantoorruimtes kun je verhuren |
| Warehouse | Magazijn met palletstellingen, werkbanken, schijvenvernietiger | Nodig voor je eigen hostingbedrijf en de logistiek van reserveonderdelen |

### Zo komt een gebouw er

1. Jij tekent de plattegrond. **Vrachtwagens** brengen materiaal naar je **bouwmaterialenplek** (geen materialenplek of geen weg = niet bouwen).
2. **Bouwvakkers** graven en storten de fundering, zetten de muren en tot slot het dak. Mobiele kranen en een graafmachine werken op de bouwplaats.
3. Is het pand af, dan teken je er **ruimtes** in. Bouwvakkers zetten de binnenmuren; verlichting komt er vanzelf in.
4. **Deuren** zet je door naar een muur te wijzen. Racks en grote apparatuur passen alleen door **dubbele deuren** en **roldeuren**.

{% include shot.html src="hero" caption="Een groot datacenter in aanbouw: de fundering met meerdere kranen, materiaalwagens op de oprit en bouwvakkers op de parkeerplaats." %}

Eerst schetsen en later betalen? Druk op <kbd>B</kbd> voor de **planmodus**: alles wat je tekent wordt een cyaan blauwdruk, er wordt nog niets betaald en bouwvakkers blijven eraf. De balk bovenin toont de totale kosten; **Plan bouwen** betaalt en start alles in één keer.

{% include shot.html src="plan" caption="Planmodus: een tweede datacenter als blauwdruk getekend, met de totale kosten in de balk bovenin." %}

### Verdiepingen, trappen en liften

Gebouwen kunnen tot vier verdiepingen hebben. Met <kbd>Page Up</kbd> en <kbd>Page Down</kbd> (of de verdiepingsknop rechtsboven) wissel je van verdieping; alles daarboven wordt verborgen.

- **Goederenlift** (2 × 2): nodig om racks en apparatuur naar een andere verdieping te brengen. Mensen mogen hem ook gebruiken.
- **Personenlift** (2 × 2) en **trap** (2 × 5): voor mensen. Lopers kiezen zelf de goedkoopste route.
- Liften en trappen lopen door alle verdiepingen van het pand, dus zet ze op een plek die op elke verdieping past.

**Later een verdieping erop**: klik op een afgebouwd pand en druk in het infopaneel op **Verdieping toevoegen**. Er komen steigers en een kraan en de nieuwe verdieping gaat omhoog terwijl het pand gewoon doorwerkt.

{% include shot.html src="raise" caption="Een extra verdieping gaat omhoog op een afgebouwd kantoor, met het infopaneel van het pand open." %}

### Kelder

Datacenters en kantoren kunnen een **kelder** krijgen: vink de kelder aan als je het pand uittrekt. Een goede plek voor transformatoren, schakelkasten, UPS en accu's, zodat de begane grond vrij blijft voor zalen. Ruimtes die daglicht of een straatdeur nodig hebben (laadperron, receptie) kunnen niet ondergronds, en een kelder heeft geen buitendeuren: verbind hem met een trap en een goederenlift.

{% include shot.html src="basement" caption="De kelderweergave: de grond is weggesneden en de omtrek van de panden erboven is getekend. Transformator-, schakel-, UPS- en accuruimtes passen hier prima." %}

## Kantoren richten zichzelf in

Is een kantoorruimte klaar, dan richt de interieurontwerper hem in een stijl in (standaard, tech of formeel): bureaus met schermen, planten, kunst of dashboards, een kleed. Het infopaneel van de ruimte toont een checklist van wat er nog mist. Je kunt altijd zelf meubels toevoegen of verplaatsen.

{% include shot.html src="office" caption="Een ingericht kantoorpand: directiekamer, kantoren, vergaderruimte, kantine, klantruimte en receptie." %}
