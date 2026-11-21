---
title: Je eerste datacenter
kicker: Start
lead: Van een leeg kavel naar je eerste betalende klant. Dit is dezelfde route als de tutorial in de game, met bij elke stap het waarom.
description: Stap voor stap je eerste datacenter bouwen in Datacenter Architect.
---

## Een nieuw spel starten

Open **Nieuw spel** in het hoofdmenu. Wat je hier kiest, geldt voor het hele spel.

{% include shot.html src="new-game" nl=true caption="Het venster Nieuw spel met de creatieve modus aan. Elke creatieve schakelaar werkt los: je kunt bijvoorbeeld alleen onbeperkt geld nemen en verder alles realistisch houden." %}

| Instelling | Wat het doet |
|---|---|
| Bedrijfsnaam | Je naam in de wereld: gevelborden, belettering op vrachtwagens en het logo (later aan te passen onder Bedrijf › Huisstijl) |
| Moeilijkheid | Makkelijk, normaal of moeilijk. Startkapitaal € 25, € 12 of € 6 miljoen; moeilijk betekent meer storingen, langer wachten op de netbeheerder en wildere energieprijzen |
| Scenario | Vrij spelen, of een scenario met brons, zilver en goud (zie [Wereld en voortgang]({{ '/nl/world/' | relative_url }})) |
| Tutorial | Een coachkaart die elke stap hieronder uitlegt. Staat standaard aan als je nog geen savegames hebt |
| Concurrenten, nieuws, gebeurtenissen | AI-datacenters (1 tot 5, en hoe agressief), marktnieuws, willekeurige gebeurtenissen, dynamische energieprijzen, rampen, leveringsproblemen. Allemaal optioneel |
| Netcongestie | Een zwaardere netaansluiting wacht eerst in de wachtrij van de netbeheerder |
| Weer speelt mee | Hittegolven, vorst en storm hebben effect op het spel (uit = het weer is alleen voor de sfeer) |
| Creatieve modus | Losse schakelaars voor onbeperkt geld, stroom geen beperking, koeling geen beperking, netwerk geen beperking, geen storingen, direct bouwen en alle unlocks |

> **Wil je gewoon bouwen?** Creatieve modus met **onbeperkt geld** en **alle unlocks** is de relaxte manier van spelen. Je ziet de hele keten nog steeds werken, alleen raakt het geld nooit op.
{: .tip}

## De checklist

Rechts in beeld staat een checklist (Overzicht › Doelen, of <kbd>F1</kbd>) met alles wat een eerste datacenter nodig heeft. Met de tutorial aan legt een coachkaart linksboven de huidige stap uit, en **Laat zien** opent de juiste bouwcategorie of het juiste scherm.

{% include shot.html src="tutorial" nl=true caption="De tutorialcoach (linksboven) en de oplichtende bouwcategorie onderin. Laat zien opent de juiste plek, Volgende tip slaat een tip over." %}

<ol class="steps">
<li><strong>Oprit.</strong> Vrachtwagens en bouwvakkers moeten erin kunnen. Open in de bouwbalk <b>Grond</b> en sleep <b>Asfalt</b> vanaf de openbare weg aan de rand van je kavel naar de plek waar je gaat bouwen. Wegen van 6 m of breder krijgen een middenstreep en verkeer in twee richtingen.</li>
<li><strong>Bouwmaterialenplek.</strong> Bouwvakkers leggen hun materiaal op een bouwplaats. Schilder een kleine <b>Bouwmaterialenplek</b> (ongeveer 4 × 4) naast de oprit. Zonder materialenplek en zonder route vanaf de weg wordt er niets gebouwd.</li>
<li><strong>Datacenterpand.</strong> Trek onder <b>Gebouwen</b> een <b>Datacenterpand</b> uit op het gras. Begin bescheiden, zo'n 20 × 14 m; je kunt later altijd panden, een kelder of een extra verdieping toevoegen. Vrachtwagens brengen materiaal, bouwvakkers storten de fundering, zetten de muren en het dak. Je ziet kranen en een graafmachine aan het werk.</li>
<li><strong>Datahal.</strong> Open in het afgebouwde pand <b>Ruimtes</b> en trek een <b>Datahal</b> uit. Hier komen de racks te staan. Laat ruimte over voor een laadperron, technische ruimtes en een gang.</li>
<li><strong>Laadperron.</strong> Racks komen per vrachtwagen. Maak een <b>Laadperron</b> tegen een buitenmuur en zet een <b>roldeur</b> in die muur, aan het asfalt. Geef de hal een <b>dubbele deur</b>: racks passen alleen door dubbele deuren en roldeuren.</li>
<li><strong>Racks.</strong> Open <b>Datazalen</b> en sleep rijen racks in de hal. Breed slepen maakt paren rijen met een koude gang tussen de voorkanten en een warme gang tussen de paren. Een vrachtwagen brengt ze, monteurs zetten ze neer en een engineer sluit stroom en glasvezel aan.</li>
<li><strong>Directiekamer.</strong> Maak een <b>Directiekamer</b> en zet er een bureau en een stoel in (<b>Inrichting</b>). Elke directeur en manager heeft een eigen ingerichte kamer nodig.</li>
<li><strong>Neem een CEO aan</strong> onder <b>Bedrijf</b> (<kbd>F2</kbd>). Daarna runnen de managers het bedrijf zelf.</li>
<li><strong>Neem een salesdirecteur aan.</strong> Zonder salesdirecteur komen er geen klanten. Verkopers hebben elk een bureau in een kantoorruimte nodig.</li>
<li><strong>Hoofdschakelkast.</strong> Racks hebben stroom nodig. Maak een <b>Schakelruimte</b> en plaats de <b>Hoofdschakelkast</b>: die neemt de netaansluiting binnen en heeft de ATS waarmee generatoren het overnemen.</li>
<li><strong>UPS met accu's.</strong> Een UPS overbrugt de seconden van een stroomstoring tot de generatoren draaien. Zet een <b>UPS</b> in een <b>UPS-ruimte</b> en <b>accukasten</b> in de UPS-ruimte of een accuruimte.</li>
<li><strong>PDU of busbar.</strong> Een <b>PDU</b> in de datahal (of een busbar-upgrade op de hal) brengt stroom naar de racks. Twee ervan geven de hal A/B-voeding.</li>
<li><strong>Koeling.</strong> Elke kW IT wordt warmte. Zet <b>CRAH-units</b> in de hal en een <b>koelmachine</b> of <b>koeltoren</b> buiten of op het dak om de warmte af te voeren.</li>
<li><strong>Twee generatoren.</strong> Zet twee generatoren buiten: één draagt de last, één is reserve. Dat is <b>N+1</b>.</li>
<li><strong>Glasvezel.</strong> Plaats een <b>glasvezelput</b> aan de rand van het kavel, bij de weg, en maak een <b>Meet-me room</b> met een <b>ODF</b> (patchframe) in het pand.</li>
<li><strong>Twee carriers.</strong> Sluit onder Infrastructuur › Netwerk (<kbd>F4</kbd>) contracten met twee carriers. Twee routes houden je online als er één uitvalt.</li>
<li><strong>Core-router en spines.</strong> Zet een <b>core-router</b> in de meet-me room en een <b>spine-switch</b> in elke hal, zodat de racks de carriers bereiken. Zodra het eerste rack online is, kun je het verkopen.</li>
<li><strong>Teken je eerste klant.</strong> Open <b>Klanten</b> (<kbd>F3</kbd>): je salesdirecteur brengt aanvragen binnen. Teken er één en het geld gaat stromen.</li>
</ol>

{% include shot.html src="build-menu" nl=true caption="De bouwbalk onderin. Elke categorie heeft een eigen kleur (stroom amber, koeling cyaan, netwerk oranje); de lade erboven toont de onderdelen met hun prijs. De bulldozer staat los rechtsonder." %}

## Hoe een klein eerste datacenter eruitziet

Een compact eerste pand dat werkt, ongeveer 24 × 16 m:

| Ruimte | Erin | Waarom |
|---|---|---|
| Datahal (ongeveer 12 × 10) | Rijen racks, 2 CRAH-units, 2 PDU's, 1 spine-switch | De racks, gekoeld en gevoed vanuit twee voedingen |
| Laadperron | Roldeur naar het asfalt | Vrachtwagens leveren hier racks en apparatuur |
| Schakelruimte | Hoofdschakelkast | Neemt het net binnen, ATS voor de generatoren |
| UPS-ruimte | UPS + accukasten | Overbrugt een stroomstoring |
| Meet-me room | ODF + core-router | Hier komen de carriers binnen |
| Gang | | Mensen en racks bereiken zo elke ruimte |

Buiten: twee generatoren, één koelmachine of koeltoren, een glasvezelput bij de straat, de oprit, de materialenplek en een paar parkeerplaatsen. De directiekamer kan in een apart kantoorpand, naast sales.

{% include shot.html src="site" caption="Een werkende startcampus: datacenter met koelmachines op het dak, generatoren ernaast, een kantoor, een warehouse, parkeerplaatsen en een omheind terrein." %}

> **Racks verschijnen maar blijven donker?** Dat is de meest gestelde vraag. Kijk bij [Racks online krijgen]({{ '/nl/racks/' | relative_url }}) voor de hele checklist.
{: .power}

## Wat waar in beeld staat

- **Onder:** de bouwbalk met alles wat je in de wereld plaatst. De **bulldozer** (slopen) staat rechtsonder, met daarboven ongedaan maken, opnieuw en de knop voor de planmodus.
- **Links:** de beheerbalk, <kbd>F1</kbd> tot <kbd>F8</kbd>: Overzicht, Bedrijf, Klanten, Infrastructuur, Personeel, Reputatie en onderzoek, Groei, Voortgang. Schermen openen als lade; de wereld blijft ernaast zichtbaar en klikbaar.
- **Boven:** de statusbalk (stroom, noodstroom, koeling, netwerk, racks, PUE, personeel, reputatie, saldo). Elk getal is klikbaar en opent het passende scherm.
- **Rechtsboven:** de weergaveknoppen: draaien, raster, rondlopen, foto, daken, verdieping, de overlays voor stroom, koeling en netwerk, dag/nacht en weer.
- **Rechtsonder:** klok, weer en spelsnelheid.

{% include shot.html src="overview" nl=true caption="Overzicht (F1): kerncijfers, adviezen met een knop naar het scherm dat het oplost, doelen en meldingen." %}
