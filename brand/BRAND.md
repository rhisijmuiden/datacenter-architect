# Huisstijl: Datacenter Architect

Deze huisstijl hoort bij de game *Datacenter Architect*. Alle waarden staan ook machineleesbaar in [`tokens.json`](tokens.json). De Godot-UI-theme wordt daaruit opgebouwd.

![Logo](logo-wordmark.svg)

## Karakter

Technisch, overzichtelijk en vriendelijk. De wereld is kleurrijk en licht cartoonachtig 3D, schuin van bovenaf. De interface ligt als donkere, halfdoorzichtige panelen over de wereld, zodat het terrein altijd de hoofdrol houdt.

## Logo

- **Beeldmerk** ([`logo.svg`](logo.svg)): een amberkleurig vlak met drie serverunits. Twee statuslampjes zijn groen en één is amber, zoals een N+1-opstelling met één unit stand-by.
- **Woordmerk** ([`logo-wordmark.svg`](logo-wordmark.svg)): het beeldmerk met "DATACENTER ARCHITECT" in Barlow Condensed Bold, in hoofdletters en met wat letterspatiëring.
- Houd rondom het logo minstens de breedte van één serverunit vrij. Zet het beeldmerk niet op een amberkleurige achtergrond.

## Kleuren

| Rol | Kleur | Gebruik |
|---|---|---|
| Paneel | `#111D2B` (88% dekking) | Alle HUD-panelen |
| Paneel hover | `#1D3247` | Hover en geselecteerde knop |
| Tekst | `#EAF2F8` | Hoofdtekst |
| Tekst gedimd | `#93A9BC` | Labels en secundaire tekst |
| **Amber (merk)** | `#FFB23E` | Merkkleur, primaire knoppen, en alles wat met **stroom** te maken heeft |
| Koeling | `#55D2F2` | Koelcapaciteit, leidingen, CRAH-units |
| Glasvezel | `#FF7A2F` | Glasvezelputten, providers, netwerkweergave |
| OK | `#5FD49B` | Gezonde status, saldo |
| Waarschuwing | `#FFB23E` | Bijna aan de limiet |
| Kritiek | `#FF6B5E` | Storing, overbelasting |

Vaste regel: stroom is altijd amber, koeling is altijd cyaan en glasvezel is altijd oranje, in de balken, de overlay-weergaven en in de 3D-wereld. Zo leert de speler de kleuren één keer en herkent hij ze overal.

## Typografie

- **Barlow Condensed** (500/600/700) voor koppen, labels, getallen en knoppen. Labels in hoofdletters met 0,08em letterspatiëring.
- **Barlow** (400/500/600) voor lopende tekst en meldingen.
- Getallen altijd met tabulaire cijfers, zodat ze niet verspringen.
- Beide lettertypes vallen onder de SIL Open Font License en mogen in de game worden meegeleverd.

## Interface-indeling

- **Onder:** de bouwbalk met categorieën als tabbladen met icoon (Selecteren, Grond, Beveiliging, Gebouwen, Ruimtes, Inrichting, Datazalen, Stroom, Koeling, Netwerk, Personeel, Klanten). Elke categorie heeft een eigen accentkleur (stroom amber, koeling cyaan, glasvezel oranje); de open categorie krijgt een streep bovenaan in die kleur. Daarboven schuift een lade met de onderdelen als kaartjes met naam en prijs.
- **Rechtsonder:** de bulldozer (slopen) als losse grote knop, met links daarvan de tijdsbediening.
- **Boven:** de resourcebalken voor stroom, koeling, racks en glasvezel, met het saldo rechts.
- **Rechts:** de camera (draaien, daken aan/uit), de overlay-weergaven, de taalwissel en de Info-knop.
- **Links boven de bouwbalk:** meldingen.
- Panelen hebben 10px afgeronde hoeken, een dunne lichte rand en een zachte schaduw.

## 3D-stijl

- Low-poly met vlakke kleuren en zachte schaduwen, zonder fotorealistische texturen.
- Gebouwen zijn opengewerkt (daken kunnen uit), zodat je binnen kunt kijken.
- Herkenbare accenten in de wereld volgen de systeemkleuren: amberkleurige strepen op gebouwen en generatoren, cyaan op koelunits, gele kabelgoten en oranje glasvezel.
- Personeel is herkenbaar aan kleur en een eigen voorwerp (zie [art-style-v1.html](../docs/concept/art-style-v1.html)):

| Functie | Kleur | Herkenning |
|---|---|---|
| Support engineer | Wit | Headset en laptop |
| Datacenter technicus | Geel hesje | Toolbelt, draagt een server |
| Elektrotechnisch engineer | Wit met amber streep | Amber helm, multimeter |
| Koeltechnicus | Cyaan overall | Muts, moersleutel |
| Netwerkengineer | Donker met oranje streep | Haspel glasvezel |
| NOC-operator | Grijs | Headset, koffiemok |
| Beveiliger | Donkerblauw uniform | Pet, portofoon, epauletten |
| Logistiek medewerker | Oranje | Pet, steekwagen met dozen |
| Manager | Pak met amber das | Tablet |
| Klant op bezoek | Eigen kleding | Rugzak, rode bezoekersbadge |

- **Figuren**: stevige, ronde vormen met een groot hoofd, simpele ogen en een glimlach. Verschillende huidskleuren en kapsels, zodat het team divers oogt.
- **Omgeving**: subtiele procedurele patronen (bestrating, baksteen, gevelpanelen), afgeronde bomen, bloesembomen, hagen, bankjes, lantaarnpalen en fietsenrekken. Op de achtergrond een wazige skyline en heuvels.
- **Dag en nacht**: 's nachts gloeien lantaarnpalen, ramen en statuslampjes.

## Taal

De game is standaard Engels en volledig vertaald naar het Nederlands. Speel je in het Nederlands, dan blijven vaktermen zoals *rack*, *CRAH*, *UPS* en *N+1* gewoon in het Engels.
