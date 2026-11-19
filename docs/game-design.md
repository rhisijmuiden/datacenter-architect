# Game design document: Datacenter Architect

*Versie 0.5, 19 november 2026. Een levend document dat met de game meegroeit.*

## In één zin

Een offline bouw- en managementsimulatie voor Windows 11 waarin je grond koopt en een compleet datacenter ontwerpt en runt: van parkeerplaats en beveiligingsloge tot stroomvoorziening, koeling en de racks van je klanten.

## Kernlus

1. **Kies een locatie** en koop grond.
2. **Ontwerp het terrein**: gebouwen, wegen, parkeerplaats, hekwerk, generatoren, koelmachines.
3. **Richt het datacenter in**: zalen, racks, kabelgoten, stroomverdeling, koeling.
4. **Sluit klanten aan** die racks huren en betalen per maand.
5. **Groei en houd het in balans**: meer racks vragen meer stroom en koeling. Kom je tekort, dan krijg je waarschuwingen en uiteindelijk storingen.
6. Met de winst koop je grond bij of bouw je verder uit.

## Visie: een relaxte bouwgame

- **Jij bent de directeur.** Je bepaalt de grote lijn: waar je bouwt, wat je bouwt en waarin je investeert. Personeel voert het werk zelf uit.
- **Bouwen staat voorop.** Het ontwerpen, bouwen, onderhouden en uitbreiden van een datacenter is de kern. Er zijn geen harde doelen of tijdslimieten; je speelt in je eigen tempo.
- **Geen micromanagement.** Je hoeft geen kabels te leggen of roosters te vullen. Dat gaat automatisch, of je stelt een manager aan die het regelt. Wie meer controle wil, kan dat later per onderdeel aanzetten.
- **Wel spanning als je dat wilt.** Met geld als drijfveer kun je failliet gaan als je meer uitgeeft dan er binnenkomt. In de creatieve modus zet je dat uit.
- **De keten:** meer sales → meer contracten → meer racks → meer stroom → meer koeling en meer netwerkcapaciteit. Alles hangt samen en is zichtbaar.
- **Groot bouwen:** van een klein pand in de stad tot een complete campus met meerdere gebouwen, verdiepingen, kantoren, een warehouse en een researchcenter.

## Locaties

Je bouwt op een **kaal terrein** of op een **kavel in de stad**. Elke locatie heeft eigen eigenschappen:

| Eigenschap | Kaal terrein | Stadskavel |
|---|---|---|
| Ruimte | Veel, en uitbreidbaar door grond bij te kopen | Weinig, je bouwt eerder de hoogte in |
| Grondprijs | Laag | Hoog |
| Glasvezel in de buurt | Weinig providers, verder weg | Veel providers, dichtbij |
| Netcapaciteit | Wisselend, soms een nieuwe aansluiting nodig | Vaak krap, de netbeheerder bepaalt het tempo |

Elke locatie toont vooraf de **beschikbare netcapaciteit (MVA)** en de **glasvezelputten** in de buurt, met provider en afstand. Aangrenzende kavels kun je later bijkopen.

### Kaart en meerdere locaties

- Een **regiokaart** toont de omgeving: de stad, wegen, rivieren, het hoogspanningsnet met onderstations, glasvezelroutes, de internet exchange, concurrerende datacenters en **kavels te koop**.
- Klik op een kavel om de netcapaciteit (met wachttijd), glasvezel in de buurt, afstand tot de IX, grootte en prijs te zien.
- Koop een kavel om een **nieuwe locatie te openen**. Je beheert meerdere datacenters tegelijk en wisselt tussen locaties via de kaart of de lijst "Jouw locaties".
- Kaartlagen voor stroomnet, glasvezel, concurrenten en kavels zijn los aan en uit te zetten.
- Locaties kun je onderling verbinden met eigen glasvezel, zodat klanten in twee datacenters tegelijk kunnen staan (geo-redundantie).

## Stroom

- **Netaansluiting** via een onderstation met transformatoren. De capaciteit van de locatie bepaalt hoe groot je kunt worden.
- **Generatoren** in verschillende soorten en capaciteiten (bijvoorbeeld diesel/HVO en gas), met een brandstoftank en een aantal uren autonomie.
- **Redundantie**: de game controleert of je opzet **N**, **N+1** of **2N** is en toont dat in de stroombalk. Klanten met hogere eisen betalen meer, maar willen ook een hogere redundantie.
- **Interne stroominfrastructuur**, gedetailleerd tot op het rack. Je ontwerpt de hele keten zelf:
  1. **Middenspanning**: netaansluiting, onderstation en transformatoren (MS naar LS).
  2. **Hoofdverdeler (switchgear)** met een **ATS** die automatisch omschakelt tussen net en generator.
  3. **UPS-systemen** met accubanken. Je kiest de autonomie in minuten en de opzet (N, N+1, 2N).
  4. **Verdeling naar de zalen** via kabels of **busbars** boven de rackrijen, met eigen capaciteit in ampère.
  5. **PDU's** (rack-PDU's of vloer-PDU's) per rij of per rack.
  6. **A- en B-voeding** per rack, elk via een eigen pad. Gaat één pad uit, dan blijft het rack draaien als beide paden goed zijn aangelegd.
- Elk onderdeel heeft een capaciteit. Overbelasting laat een **groep of zekering uitvallen**, en de speler ziet in de stroomweergave precies welk pad het probleem is.
- **Onderhoud**: onderdelen slijten. Een engineer moet ze testen en onderhouden, en tijdens onderhoud draai je even op je redundante pad.
- **Netuitval** is een gebeurtenis in het spel: de UPS overbrugt en de generatoren moeten binnen een paar seconden starten. Is je opzet niet goed, dan gaan racks uit.

### Netaansluiting van de netbeheerder

- De netbeheerder levert de hoofdaansluiting in een **eigen bijgebouw** dat je zelf op je terrein plaatst, bij voorkeur langs de openbare weg (de netbeheerder moet er altijd bij kunnen).
- Het gebouw is van de netbeheerder. Jij vraagt **upgrades of verbouwingen** aan, met kosten en een doorlooptijd:

| Niveau | Gebouw | Capaciteit | Doorlooptijd |
|---|---|---|---|
| 1 | Compact station | 2 MVA, 10 kV | 6 maanden |
| 2 | Klantstation (gemetseld, met transformator) | 10 MVA, 10 kV | 14 maanden |
| 3 | Eigen onderstation met transformatoren en portaal | 50 MVA, 50 kV | 30 maanden |

- Tijdens een verbouwing kan de aansluiting tijdelijk beperkt zijn. Plan dat, bijvoorbeeld met de generatoren.

### Generatoren en brandstof

- Generatoren zijn moderne **containerunits** op een betonnen poer: met radiatorventilator op het dak, uitlaatdemper, bedieningspaneel met scherm en statuslampje, en aanrijdbeveiliging.
- **Dieseltanks** plaats je bovengronds of **ondergronds**. Een ondergrondse, dubbelwandige tank met lekdetectie spaart ruimte en is veiliger, maar is duurder en vraagt graafwerk. Bovengronds zie je alleen het vulpunt, de ontluchtingspijp en de mangaten.
- Met de **ondergrondse weergave** zie je tanks, brandstofleidingen en kabels onder de grond.

## Koeling

- **Koelmachines** buiten en **CRAH-units** in de zalen, elk met een capaciteit in kW.
- Elke rack produceert warmte, afhankelijk van de klant. De koelbalk toont de belasting tegenover de capaciteit.
- Racks staan **aaneengesloten in rijen**, zonder ruimte tussen de racks in een rij. Je plaatst ze rij voor rij (slepen).
- **Tussen de rijen zit altijd een gang.** Aan de voorkant is dat de koude gang, aan de achterkant de warme gang. Engineers moeten bij de achterkant van de servers kunnen, dus een rij zonder vrije gang erachter geeft een waarschuwing.
- **Zaal-upgrades:** een datazaal kun je upgraden in stappen. Elke stap kost geld en bouwtijd, verhoogt de effectieve koelcapaciteit en verlaagt het energieverbruik (PUE):

| Niveau | Wat je bouwt | Koeling | PUE |
|---|---|---|---|
| Open zaal | Geen scheiding, lucht mengt | 100% | 1,65 |
| Warme/koude gang | Rijen met de voorkant naar elkaar, blindplaten | +12% | 1,50 |
| Cold corridor | Koude gang dicht met glazen dak en schuifdeuren | +35% | 1,30 |
| Hot aisle containment met in-row koeling (later) | Warme gang afgesloten, koeling in de rij | +50% | 1,20 |

- Een lagere PUE betekent minder stroom voor koeling, dus een lagere stroomrekening per rack.
- De **warmteweergave** kleurt racks van groen naar rood, zodat je ziet waar het te warm wordt.
- Regel: plaats je meer racks, dan moet je eerst de koelcapaciteit verhogen.

### Racks plaatsen en aansluiten

- Zie ook **Opbouwketen van racks**: eerst levering per vrachtwagen en plaatsing door monteurs.
- Een nieuw geplaatst rack is nog niet live. Er komt automatisch een **werkorder** bij, die een **datacenter technicus** oppakt.
- De technicus loopt naar het rack en sluit eerst de **A/B-stroomvoeding** aan op de PDU's en de busbar. Daarna trekt hij **glasvezel en koper** naar de ToR-switch of de MMR.
- Pas dan is het rack live en kan de klant erin. Te weinig technici betekent een wachtrij en klanten die later kunnen beginnen.
- In de 3D-wereld zie je boven een rack dat wacht een markering in amber (stroom nog aansluiten) of oranje (bekabeling nog trekken).

## Netwerk en glasvezel

- Glasvezelputten bij de weg, elk van een provider. Je legt zelf de mantelbuis naar de **MMR** (meet-me room) in je gebouw.
- Meer providers aangesloten betekent een hogere prijs per rack en meer interesse van klanten.
- Binnen leg je **kabelgoten** boven de rackrijen: geel voor glasvezel, grijs voor stroom. Kabelgoten hebben een maximale vulling.
- **Netwerkinfrastructuur**, ook gedetailleerd:
  1. **Glasvezelput en mantelbuis** van de provider naar je terrein. Twee gescheiden routes (verschillende putten en ingangen) geven **netwerkredundantie**.
  2. **MMR (meet-me room)** met ODF's en patchpanelen, waar providers binnenkomen.
  3. **Core-routers en core-switches**, bij voorkeur dubbel uitgevoerd.
  4. **Aggregatie- of spine-switches** per zaal.
  5. **Top-of-rack-switches** of patchpanelen per rack.
  6. **Cross-connects**: klanten vragen een verbinding naar een provider of naar elkaar, en een technicus legt die aan. Dat levert eenmalig en maandelijks geld op.
- Elke switch heeft poorten en bandbreedte. Te weinig poorten of een overvolle uplink geeft klachten en boetes.
- De **netwerkweergave** laat de paden zien van provider tot rack, en waar een enkel punt van falen zit.

## Gebouwen en terrein

Verplicht of sterk aangeraden:

- **Beveiligingsloge** bij de ingang, met slagboom en check-in van elke bezoeker en levering.
- **Laadperron (loading dock)** en opslag voor nieuwe racks en servers.
- **Parkeerplaats** voor personeel en bezoekers, eventueel met laadpalen.
- **Kantoor en NOC** waar je engineers alarmen opvolgen.
- **Hekwerk** rondom het terrein.

Daarnaast wegen, paden, groen en uitbreidingsruimte. Gebouwen zijn opengewerkt, en daken kun je aan- en uitzetten.

### Terrein vrij indelen

- Het hele terrein dat je hebt gekocht deel je **zelf in**: waar het pand komt, de parkeerplaatsen, het laadperron, de beveiligingsloge, de ontvangsthal, de generatoren, de koelmachines, het station van de netbeheerder, groen en paden.
- Er is geen vaste indeling. Elke keuze heeft gevolgen: een lange looproute van parkeerplaats naar ingang, vrachtwagens die niet kunnen keren, of een generator te dicht bij de buren.
- Je kunt **meerdere panden** op hetzelfde terrein zetten, bijvoorbeeld het datacenter zelf en een apart **kantoorpand**.
- **Inrichting buiten:** looppaden, fietspaden, fietsenstallingen, parkeerplaatsen (personeel, bezoekers, invaliden, laadpalen), bomen, struiken, hagen, gras, bankjes, lantaarnpalen en gevel- en terreinverlichting. Alles plaats je los, net als in de ruimte-ontwerper.
- Groen en verlichting doen iets: verlichting en camera's verhogen de beveiliging 's nachts, groen en een fietsenstalling maken personeel tevredener, en goede looppaden verkorten looptijden.

### Kantoor en afdelingen

Het kantoor kan in het datacenterpand of in een eigen pand op het terrein. Je deelt het in met dezelfde ruimte-ontwerper (muren, deuren, objecten) en wijst per ruimte een **afdeling** toe:

| Afdeling | Wat het doet | Inrichting |
|---|---|---|
| Support | Tickets en remote hands van klanten | Bureaus, headsets, schermen |
| NOC | 24/7 monitoring en alarmen | Schermwand, bureaus |
| Sales | Nieuwe klanten en offertes, nodig voor grote contracten | Bureaus, overlegtafel |
| Account management | Bestaande klanten tevreden houden en verlengen | Bureaus |
| Inkoop | Contracten met stroomleveranciers en carriers | Bureaus |
| Facilitair / HR | Personeel werven, roosters | Bureaus |
| Management | Rapportages, investeringen | Kantoor, vergadertafel |
| Kantine en vergaderruimtes | Personeelstevredenheid, klantbezoek | Tafels, keuken, scherm |

Een afdeling werkt pas met voldoende werkplekken voor het personeel dat je erin zet. Meer werkplekken en een betere inrichting geven hogere productiviteit.

### Bouwproces

Een gebouw bouw je in fases, elk met bouwtijd en kosten. Een bouwkraan en bouwvakkers zijn zichtbaar zolang er gebouwd wordt.

1. **Fundering**: zet de plattegrond uit op je kavel en stort de fundering. De fundering bepaalt het maximale aantal verdiepingen.
2. **Muren, deuren en ingangen**: buitenmuren, entrees, nooddeuren, de deur van het laadperron, en per verdieping een trap en liftschacht.
3. **Dak**: standaard, verzwaard (voor koelmachines) of met zonnepanelen.
4. **Inrichting**: binnenmuren plaatsen om ruimtes te maken, en daarna de inrichting (racks, koeling, stroom, bekabeling).

### Wegen en hekwerk

- Een **parkeerplaats** werkt pas als hij met een weg op de **openbare weg** is aangesloten. Hetzelfde geldt voor het laadperron en de beveiligingsloge: vrachtwagens, personeel en bezoekers moeten er kunnen komen.
- Je legt zelf wegen, paden en toegangen aan. De route van de openbare weg via de loge naar het terrein is de enige officiële ingang.
- **Hekwerk** trek je zelf rond (delen van) het terrein. Er zijn verschillende soorten:

| Hekwerk | Beveiliging | Kosten | Opmerking |
|---|---|---|---|
| Draadhek | Laag | Laag | Alleen geschikt voor de buitenrand van parkeerplaatsen |
| Gaashek met prikkeldraad | Middel | Middel | Standaard voor de meeste terreinen |
| Spijlenhek (staal) | Hoog | Hoog | Netjes, vaak verplicht in de stad |
| Anti-klimhek | Hoog | Hoog | Eis bij veel grote contracten |
| Betonnen muur | Zeer hoog | Zeer hoog | Ook bescherming tegen voertuigen |
| Haag | Laag | Laag | Alleen decoratief, maakt personeel en buren tevreden |

- Hekken kun je aanvullen met **poorten, slagbomen, draaipoortjes, camera's en verlichting**. Grote klanten stellen eisen aan het beveiligingsniveau van je terrein.

### Toegangsroute en aankomst

Het hekwerk kies en bouw je zelf, bijvoorbeeld hoge stalen spijlenhekken. In het hek zit één hoofdingang met een **toegangspoort**: een schuifhek, een slagboom of allebei.

- **Oprit:** je sluit de openbare weg met een **asfalt oprit** aan op je terrein. Zonder oprit kan niemand je terrein bereiken.
- **Melden bij de poort:** voor de poort staat een **meldzuil** (intercom met camera en kentekenlezer). Een klant stopt daar en meldt zich.
- **Beveiligingshok:** de beveiliger zit binnen het hek in een **beveiligingshok** naast de poort. Van daaruit opent hij het schuifhek of de slagboom. Zonder bemand hok blijft de poort dicht (later te automatiseren met kentekenherkenning voor vaste klanten en personeel).
- **Parkeren:** de klant rijdt over het eigen terrein naar de **bezoekersparkeerplaats**, parkeert en loopt naar de **beveiligingsloge / ontvangsthal** in het gebouw.
- **Check-in:** de beveiliging controleert legitimatie en afspraak, geeft een bezoekersbadge en geeft toegang tot het datacenter. De toegang is per klant beperkt tot de ruimtes waar hij spullen heeft staan: zijn rack, kooi, suite of de MMR.
- **Wachttijd telt mee:** te weinig beveiligers, een te lange looproute of een volle parkeerplaats geven wachttijd. Dat kost klanttevredenheid en is zichtbaar in de rapportage.
- **Leveranciers en vrachtwagens** gebruiken dezelfde poort maar rijden door naar het **laadperron**. Je kunt ook een aparte leveranciersingang met een eigen hok bouwen.

Volgorde van een bezoek: openbare weg → oprit → meldzuil → poort open (vanuit het hok) → parkeren → check-in bij de beveiliging → begeleid of met badge naar de eigen ruimte → terug langs de beveiliging → poort uit.

## Ruimtes

Binnen een gebouw **teken je zelf ruimtes**: muren trekken, deuren plaatsen en een ruimtetype kiezen. Elk type heeft eisen (minimale grootte, verplichte objecten, toegangsniveau) en werkt pas als daaraan is voldaan.

| Ruimte | Functie | Verplichte objecten |
|---|---|---|
| **Datazaal** | Racks van klanten | Racks, CRAH-units, kabelgoten, PDU's |
| **Kooi / private suite** | Afgeschermd deel van een zaal voor één klant | Hekwerk, toegangslezer |
| **Meet-me room (MMR)** | Hier komen alle netwerken binnen | ODF's, patchpanelen, provider-racks |
| **UPS-ruimte (technische ruimte)** | Overbrugging bij stroomuitval | UPS-systemen |
| **Accuruimte** | Accubanken voor de UPS | Accukasten, extra koeling, brandblusinstallatie |
| **Switchgear-ruimte** | Hoofdverdeling en ATS | Schakelkasten, ATS |
| **NOC** | Bewaking van het hele terrein | Werkplekken, schermwand |
| **Kantoor** | Werkplekken voor personeel | Bureaus |
| **Kantine en toiletten** | Tevredenheid van personeel | Keuken, tafels, toiletten |
| **Opslag** | Voorraad racks, servers en reserveonderdelen | Stellingen |
| **Laadperron** | Leveringen ontvangen | Laaddeuren |
| **Build room / staging** | Servers voorbereiden voordat ze de zaal in gaan | Werkbanken |
| **Beveiligingsloge** | Check-in, badges uitgeven | Balie, toegangspoortjes |
| **Ontvangsthal** | Ontvangst van klanten en bezoekers, wachten op begeleiding | Balie, zitjes, sluis naar het beveiligde deel |
| **Klantenruimte** | Werkplek voor klanten die op locatie zijn | Bureaus, kluisjes |

**Meet-me room in detail**

- De MMR is het **netwerkhart** van je datacenter. Elke provider die je aansluit krijgt hier een eigen rack of positie.
- Je kunt **providers, carriers en internet exchanges** (zoals een lokaal IX-knooppunt) aansluiten. Elke aansluiting kost geld en ruimte in de MMR, maar maakt je datacenter aantrekkelijker.
- Klanten bestellen **cross-connects** van hun rack naar een provider in de MMR. Hoe meer netwerken in je MMR, hoe meer cross-connects je verkoopt.
- Een **tweede MMR** aan de andere kant van het gebouw, met een eigen glasvezelroute, geeft netwerkredundantie. Grote klanten eisen dat vaak.
- De MMR heeft een eigen **toegangsniveau**: alleen netwerkengineers en begeleide providers mogen erin.

**Toegang en beveiliging**

- Ruimtes hebben toegangsniveaus. Deuren met een badgelezer laten alleen bevoegde mensen door.
- Een **mantrap** (sluis) tussen de publieke zone en de datazalen is een eis bij veel grote contracten.

## Meerdere verdiepingen

- Gebouwen kunnen **meerdere verdiepingen** en een **dak** hebben. Op een stadskavel is dat vaak de enige manier om te groeien.
- Elke verdieping heeft een **maximale vloerbelasting** (kg/m²). Zware installaties zoals UPS, accu's en switchgear horen daarom meestal op de begane grond.
- **Schachten (risers)** voor stroom, glasvezel en koelleidingen verbinden de verdiepingen. Een verdieping werkt pas als hij daarop is aangesloten.
- Een **goederenlift** is nodig om racks naar boven te brengen. **Trappen** zijn verplicht als vluchtroute.
- Op het **dak** kun je koelmachines, dry coolers en zonnepanelen plaatsen. Dat bespaart grond, maar vraagt een sterk dak.
- Warmte stijgt op: hogere zalen vragen iets meer koeling.
- In de bouwmodus kies je een verdieping. Alles daarboven wordt verborgen, en met de uitgeklapte weergave zie je alle verdiepingen tegelijk.

## Personeel

Personeel is een eigen module. Welke functies je kunt werven, hangt af van je directeuren (zie **Organisatie en unlocks**). Je **werft medewerkers** en ziet ze als poppetjes over het terrein lopen.

**Functies**

| Functie | Taken | Kleur in de wereld |
|---|---|---|
| Support engineer | Tickets van klanten, remote hands, cross-connects | Wit |
| Datacenter technicus | Racks plaatsen, bekabelen, hardware wisselen | Geel |
| Elektrotechnisch engineer | Onderhoud aan UPS, switchgear, generatoren en PDU's | Wit met amber |
| Koeltechnicus | Onderhoud aan koelmachines en CRAH-units | Wit met cyaan |
| Netwerkengineer | Switches, routers, providers en cross-connects | Wit met oranje |
| NOC-operator | Bewaakt alarmen en stuurt de juiste persoon | Donkergrijs |
| Beveiliger | Check-in bij de loge, patrouilles | Donkerblauw |
| Logistiek medewerker | Leveringen bij het laadperron, opslag | Oranje |
| Manager | Verhoogt de efficiëntie van een team | Pak |

**Werven en beheren**

- Een **vacaturebord** met kandidaten, elk met een salaris, ervaring (junior, medior, senior) en eigenschappen (bijvoorbeeld "snel", "nauwkeurig", "ploegendienst-ervaren").
- **Diensten en roosters**: een datacenter draait 24/7. Te weinig mensen 's nachts betekent dat storingen langer duren.
- **Training en certificering**: maakt medewerkers sneller en verkleint de kans op fouten.
- **Tevredenheid**: werkdruk, salaris en voorzieningen (kantine, parkeerplek) bepalen of mensen blijven.
- **Faciliteiten**: meer personeel vraagt meer kantoorruimte, parkeerplekken en een kantine.
- Elke storing, elk ticket en elke levering wordt een **taak** die een geschikte medewerker oppakt. Zonder de juiste mensen blijft een taak liggen.

## Leveranciers en inkoop

Stroom en bandbreedte komen niet vanzelf: je sluit zelf contracten af met leveranciers, en je moet ze op tijd upgraden als je groeit.

**Stroom**

- **Netbeheerder**: regelt je aansluiting en **gecontracteerd transportvermogen** (kVA/MVA). Een upgrade vraag je aan en heeft een **wachttijd** (maanden in speltijd) en aansluitkosten. Op een krappe stadskavel kan de wachtrij lang zijn.
- **Energieleverancier**: levert de stroom zelf, met een prijs per kWh. Je kiest uit contractvormen:
  - **Vast** voor 1 tot 5 jaar: zekerheid, maar je zit eraan vast.
  - **Variabel**: volgt de marktprijs, die in het spel schommelt.
  - **Groene stroom**: duurder, maar sommige klanten eisen het.
- Gebruik je meer dan je gecontracteerde vermogen, dan betaal je **boetes** of schakelt de netbeheerder je af.
- Later kun je eigen opwek toevoegen, bijvoorbeeld zonnepanelen op het dak of een eigen gasturbine.

**Netwerk**

- **Carriers (IP-transit)**: je koopt bandbreedte in **per Mbit**, met een **commit** (minimale afname) en de mogelijkheid tot **burst** daarboven, afgerekend via de 95e percentiel. Meer commit betekent een lagere prijs per Mbit.
- **Poortcapaciteit upgraden**: van 1G naar 10G, 100G of 400G. Een upgrade kost geld en heeft een levertijd.
- Klanten gebruiken bandbreedte. Raakt een carrier vol, dan krijgen klanten vertraging en dalen de tevredenheid en je inkomsten.
- **Peering met concurrerende datacenters**: je kunt met andere datacenters in de regio afspraken maken.
  - **Private peering** via een directe glasvezelverbinding (dark fiber) tussen jouw MMR en die van de concurrent.
  - **Publieke peering** via een **internet exchange** waar beide datacenters op zijn aangesloten.
  - Peering verlaagt je transitkosten en maakt je aantrekkelijker voor netwerkklanten, maar een concurrent wil er soms iets voor terug.
- Concurrerende datacenters zijn **AI-spelers** in de regio die ook klanten en aanbestedingen proberen te winnen. Je zet ze aan of uit bij het starten van een nieuw spel (zie *Nieuw spel*).

## Klanten en economie

Klanten huren ruimte in je datacenter in verschillende vormen:

| Product | Wat huurt de klant | Typische klant |
|---|---|---|
| **Colocatie per U** | Losse rack-units (1U, 2U, 4U ...) in een gedeeld rack | Kleine bedrijven, hobbyisten, startups |
| **Kwart- of halfrack** | Een afgesloten deel van een rack met eigen deur | MKB |
| **Volledig rack** | Eén of meer complete racks | Middelgrote bedrijven, hostingpartijen |
| **Kooi of private suite** | Een afgesloten ruimte (cage) met eigen racks en toegang | Grote bedrijven, cloudpartijen |
| **Hele zaal** | Een complete datazaal, vaak met eigen eisen aan stroom en koeling | Hyperscalers |

- Elke klant heeft eisen: **stroom per U of per rack (kW)**, **redundantie** (N+1 of 2N), **aantal providers**, en soms een eigen kooi of extra beveiliging.
- **Colocatie per U** levert de hoogste prijs per U op, maar vraagt meer werk: meer klanten in één rack, meer tickets, meer remote hands en meer cross-connects. Grote klanten betalen minder per U, maar huren veel en lang.
- Gedeelde racks moet je zelf inrichten en vullen. De game toont per rack welke U's vrij, verhuurd of gereserveerd zijn.
- **Inkomsten** per maand per product, plus eenmalige kosten (installatie, cross-connects) en extra diensten (remote hands, support).
- **Kosten** voor stroom, brandstof, personeel, onderhoud en grond.
- **Contracten** met een looptijd en een SLA. Storingen kosten boetes en tevredenheid. Ontevreden klanten vertrekken aan het einde van hun contract.
- Een contractsysteem met opdrachten, bijvoorbeeld "plaats 20 racks met 2N-stroom voor klant X" of "lever 40U colocatie met 2 providers".

### Grote contracten

Naast losse klanten kun je **grote contracten** binnenhalen bij grote klanten, zoals banken, overheden, cloudpartijen en hyperscalers.

- **Aanbestedingen (tenders)**: een grote klant zet een aanvraag uit, bijvoorbeeld "2 MW IT-capaciteit, 2N-stroom, 3 providers, eigen suite, binnen 18 maanden". Jij doet een bod met prijs en opleverdatum. Concurrenten bieden ook.
- **Afrekening per MW** in plaats van per rack of U, met een looptijd van meerdere jaren.
- **Gefaseerde oplevering**: de klant neemt capaciteit in stappen af (bijvoorbeeld 500 kW per half jaar). Jij moet op tijd bouwen.
- **Build-to-suit**: je bouwt een zaal of een heel gebouw volgens de eisen van de klant, soms met een aanbetaling vooraf.
- **Gereserveerde capaciteit**: de klant betaalt al voor stroom en ruimte die hij later gaat gebruiken. Die capaciteit kun je niet aan anderen verhuren.
- **Strenge SLA's** met hoge boetes bij storingen of te late oplevering, maar ook bonussen bij goede prestaties.
- **Audits en certificeringen** (bijvoorbeeld ISO 27001 of Tier III) als voorwaarde om mee te mogen bieden.
- **Reputatie**: een goed uitgevoerd groot contract opent de deur naar grotere klanten. Een mislukt contract kost reputatie.

#### Eigen ruimte in het contract

Een klant kan in het contract om een **eigen ruimte** vragen:

| Ruimtevorm | Wat je bouwt |
|---|---|
| **Kooi** | Een afgeschermd deel van een bestaande datazaal, met hekwerk tot het plafond, een eigen deur en een toegangslezer |
| **Private suite** | Een afgesloten ruimte binnen een zaal of naast een zaal, met eigen muren |
| **Eigen hal** | Een complete datazaal alleen voor deze klant, eventueel met eigen koeling en UPS |

Elk contract bevat concrete eisen, die de game vergelijkt met wat je bouwt:

- **Aantal racks** (bijvoorbeeld 24 racks van 42U).
- **Stroom**: kW per rack en in totaal, A/B-voeding, en eventueel 2N in plaats van N+1.
- **Netwerk**: het aantal aansluitingen en de bandbreedte in Mbit/s of Gbit/s (bijvoorbeeld 2 × 10 Gbit/s naar het internet en 4 cross-connects in de MMR), soms met een eis voor twee verschillende carriers.
- **Koeling** die past bij de stroom, en eventueel een maximale PUE.
- **Beveiliging**: alleen geautoriseerd personeel van de klant krijgt toegang tot de kooi, suite of hal.

Het contractscherm toont per eis of je eraan voldoet (✓, ! of ✕), net als de controlelijst in de ruimte-ontwerper. De klant tekent pas als alles groen is, of als je een opleverdatum toezegt. Haal je die datum niet, dan volgt een boete.

## Seizoenen en weer

De tijd loopt door een **jaarcyclus** met lente, zomer, herfst en winter, en wisselend weer. Dat is zichtbaar in de wereld (bladkleur, kale bomen, sneeuw op daken en gras, regen, mist, onweer) en heeft effect op het spel. Locaties hebben geen eigen klimaat; het weer geldt voor de hele regio.

| Situatie | Effect |
|---|---|
| Koud (onder 10 °C) | Volledig vrije koeling, koelenergie −35% |
| Mild (10 tot 18 °C) | Deels vrije koeling, koelenergie −15% |
| Warm (18 tot 27 °C) | Koelmachines draaien hoger |
| Hittegolf (vanaf 27 °C) | Koeling op de limiet, kans op oververhitting, hogere stroomprijs, netbeheerder kan vragen je verbruik te verlagen |
| Storm | Kans op netuitval ×4, schade aan installaties op het dak mogelijk |
| Sneeuw | Leveringen vertraagd, wegen en laadperron moeten gestrooid worden |
| Mist | Leveringen en bezoekers trager |
| Zon / bewolking | Opbrengst zonnepanelen hoger of lager |
| Winter | Brandstof met winteradditief, accuruimtes moeten verwarmd worden |

Een **weerbericht** in de interface waarschuwt een paar dagen vooruit, zodat je je kunt voorbereiden.

## Hoogspanning en stroomnet op de campus

- De netbeheerder levert **hoogspanning** (middenspanning, bijvoorbeeld 10 kV) met een vermogen dat je stap voor stap uitbreidt: 2, 10, 20 en 50 MVA. Elke stap heeft een wachttijd en kosten.
- Je bouwt zelf **transformatorruimtes** (vaak in de kelder) die hoogspanning omzetten naar laagspanning. Elke transformator heeft een capaciteit; extra transformatoren geven meer vermogen en redundantie.
- Je kiest tussen **fictieve stroomleveranciers** met verschillende prijzen, contractduur en groene stroom. Je kunt meerdere leveranciers combineren.
- De berekening telt alles mee: netaansluiting, transformatoren, UPS, generatoren (N, N+1 of 2N) en de verdeling naar de zalen.
- Je verkoopt rackstroom als product: bijvoorbeeld **16 A, 32 A of 3-fase 32 A** per rack. Wat je verkoopt, moet je grid kunnen leveren.
- **Stroomweergave:** in de stroom-overlay lopen bolletjes van de netaansluiting via transformatoren, UPS en PDU's naar de racks. Hoe drukker het is, hoe sneller en voller de stroom loopt. Knelpunten kleuren rood.

## Glasvezelnet buiten en netwerk binnen

- Glasvezel komt binnen via **fiberducts** (mantelbuizen) onder de stoep en het terrein. Elke duct heeft een maximaal aantal vezels. Heb je meer nodig, dan koop je een **upgrade** (extra duct of een duct met meer capaciteit).
- **Carriers** sluiten aan via de **meet-me room**. Je sluit contracten per Mbit/s of per poort af.
- **Peering:** AI-concurrenten willen met je peeren. Ze vragen bijvoorbeeld een rack in jouw meet-me room, en jij spreekt de voorwaarden af.
- **Verkeersweergave:** in de netwerk-overlay lopen bolletjes over de vezels en door de MMR. Je ziet live hoeveel verkeer je doet per carrier en per klant.
- Een **Network Operations Center (NOC)** en later een eigen **netwerkteam** beheren het netwerk, de carriers, peering en de MMR, en optimaliseren het verkeer.

## Koeling uitgebreid

- **Koeltorens** kun je op het dak zetten of los op het terrein.
- CRAH-units en koelmachines kun je **upgraden** naar meer capaciteit.
- Koelinstallaties hebben **onderhoud** nodig. Dat doen de **critical facility engineers**, die ook stroom en generatoren onderhouden. Wordt onderhoud uitgesteld, dan stijgt de kans op storingen.

## Grote zalen inrichten

- In een grote zaal plaats je racks per rij in één keer, bijvoorbeeld **een rij van 24 racks**. Een volle rij kost meer, maar scheelt klikken.
- Alle voorkanten in een rij kijken dezelfde kant op. De achterkanten (de warme kant) komen samen in de **warme gang**, de voorkanten in de **koude gang**.
- Je kunt ook **mini-datahallen** bouwen: kleine, losse zalen voor een specifieke klant of een snelle uitbreiding.
- **Zones:** je wijst een zaal of een deel ervan aan voor een bepaald gebruik:

| Zone | Wat de klant koopt |
|---|---|
| Colocatie per U | Losse rack-units, per U verkocht |
| Gedeelde colocatie | Een rack waarin meerdere klanten samen staan |
| Volle racks | Een compleet rack per klant |
| Kooi of suite | Een afgesloten deel voor één klant |
| Eigen cloud | Racks voor je eigen hostingbedrijf |

## Sales en klanttypes

- Een **salesafdeling** brengt contracten binnen. Meer salesmedewerkers geven meer aanvragen. Een aanvraag vraagt bijvoorbeeld "12 volle racks, 32 A, 2 × 10 Gbit/s". Heb je de ruimte, dan teken je.
- Klanten verschillen sterk:

| Klanttype | Racks | Stroom | Netwerk | Eisen |
|---|---|---|---|---|
| Videostreaming | Veel | Middel | Zeer veel | Meerdere carriers |
| Social media | Veel | Middel | Veel | Lage latency, peering |
| Websitehosting (ook adult) | Middel | Middel | Veel | 24/7 support |
| Cryptomining | Zeer veel | Zeer veel | Weinig | Goedkope stroom, veel koeling |
| Clouddienst | Veel | Veel | Veel | 2N-stroom, groei |
| Gemeente | Weinig | Laag | Laag | Beveiligingscertificering |
| Ministerie of overheidsdienst | Middel | Middel | Middel | Hoog beveiligingsniveau, eigen suite |

- Klanten komen willekeurig binnen, zodat elk spel anders verloopt.

## Beveiliging, certificeringen en reputatie

- Je verhoogt je **beveiligingsniveau** met upgrades: pasjessysteem, camera's binnen en buiten, een hoger hek, **stroom op het hek**, een **sluis** in plaats van alleen een slagboom, en beveiligers die **rondes lopen**.
- Een hoger niveau levert **certificeringen** op (bijvoorbeeld ISO 27001 en een overheidskeurmerk). Daarmee komen nieuwe klanten in beeld, zoals ministeries en gemeentes.
- Certificeringen en goede prestaties verhogen je **reputatie**, en een betere reputatie trekt grotere klanten aan.

## Researchcenter

- Bouw een **researchcenter** om nieuwe technieken te ontgrendelen in een **research tree**: efficiëntere koeling, betere UPS'en, nieuwe hekken en beveiliging, automatisering, hogere rackdichtheid en nieuwe producten.
- Research kost tijd en geld, en levert upgrades en betere reputatie op.

## Gebeurtenissen

- **Willekeurige gebeurtenissen** maken het spel levendig: een stroomstoring in de hele stad (de generatoren slaan aan), een CRAH-unit die kapotgaat (een engineer gaat ernaartoe), een kabelbreuk, een hittegolf of een klant die failliet gaat.
- Gebeurtenissen kun je in de creatieve modus of bij een nieuw spel uitzetten.

## Bouwen met bouwvakkers

Alles wat je bouwt, zie je echt gebeuren. Je ontwerpt, en daarna gaan aannemers aan de slag.

**Bouwplaats en materiaal**

- Je wijst zelf een **bouwmaterialenplek** aan op het terrein (een zone die je tekent, bij voorkeur aan de oprit).
- **Vrachtwagens** rijden over de openbare weg en jouw oprit naar die plek en laden daar materiaal uit: stenen, staal, dakplaten, kabelgoten.
- Zonder materialenplek of zonder route vanaf de weg kan er niet gebouwd worden. Het spel waarschuwt dan.

**Bouwvakkers en materieel**

- Je **huurt een aantal bouwvakkers** in via een aannemer. Meer bouwvakkers betekent sneller bouwen en hogere kosten per dag.
- Bouwvakkers halen materiaal op bij de materialenplek en bouwen in fases: fundering, buitenmuren, deuren, dak, binnenmuren.
- Er rijdt zichtbaar **materieel** rond: een bouwkraan, een graafmachine voor de fundering en een verreiker voor het dak.
- Een gebouw uitbreiden werkt hetzelfde: je trekt de plattegrond groter, en de bouwvakkers breken de oude muur open en bouwen verder.

**Hallen zelf uitmeten**

- Een hal teken je zelf met de muurtool: je **meet de afmetingen** zelf uit, met de maten in meters zichtbaar tijdens het slepen.
- Daarna plaats je **deuren**. Je kiest uit soorten, elk met eigen breedte, prijs en beveiliging:

| Deur | Gebruik |
|---|---|
| Enkele deur | Personeel, kantoor |
| Dubbele deur | Hallen, nodig om racks naar binnen te rijden |
| Brandwerende deur | Verplicht tussen hal en UPS- of batterijruimte |
| Beveiligde deur met badgelezer | Klantruimtes, MMR, kooien |
| Roldeur of sectionaaldeur | Laadperron, warehouse |
| Sluisdeur (mantrap) | Ingang van het datacenter, na research |

- Een hal is pas bruikbaar als er een deur in zit die breed genoeg is voor racks, met een route naar het laadperron.

## Opbouwketen van racks

Racks plaatsen is geen klik-en-klaar, maar een keten die je ziet gebeuren:

1. **Bestellen**: je plaatst racks in een hal (los of per rij van 24). Ze staan eerst als doorzichtige plannen in de hal.
2. **Levering**: een **vrachtwagen** komt naar het **laadperron** en laadt de racks uit.
3. **Plaatsen**: **monteurs** rijden de racks via de gangen en de deur naar de hal en zetten ze op hun plek.
4. **Stroom**: een **datacenter engineer** sluit de A/B-voeding aan (amber markering).
5. **Glasvezel en koper**: de engineer hangt de bekabeling aan (oranje markering).
6. **Te koop**: daarna is het rack **beschikbaar voor verkoop** en kan sales het verhuren.

Hetzelfde geldt voor mini-halls, kooien en apparatuur zoals UPS'en en koelunits: levering, plaatsen, aansluiten. In de creatieve modus met "direct bouwen" sla je de keten over.

## Organisatie en unlocks

Je begint als eigenaar met een leeg terrein. Functies en mogelijkheden komen vrij door het juiste personeel aan te nemen. Er is geen micromanagement: wie je aanneemt, regelt zijn eigen terrein.

**Eerst een kantoor**

- Het eerste wat je bouwt, is een **kantoortje voor de algemeen directeur** (CEO). Dat kan in een tijdelijke unit of in een echt kantoorpand.
- Kantoren richt je zelf in: een **bureau, stoel, kast, plant, vergadertafel**. Een kantoor werkt zodra er minimaal een bureau en een stoel staan. Een mooiere inrichting geeft een kleine bonus.
- Elke directeur heeft een eigen kantoor nodig. Een team heeft werkplekken nodig in een afdelingsruimte.

**Unlock-boom**

| Functie | Vereist | Ontgrendelt |
|---|---|---|
| Algemeen directeur (CEO) | Kantoor | Andere directeuren aannemen, het hostingbedrijf starten, een extra kantoorlocatie |
| Financieel directeur (CFO) | CEO | Leningen afsluiten en aflossen, investeerders, financiële rapportages, lagere rente en kostenvoordelen |
| HR-manager | CEO | Personeel werven (vacaturebord), salarisbeleid, trainingen |
| Salesdirecteur | CEO | Een salesteam starten, contracten binnenhalen, grote aanbestedingen |
| Facility manager | CEO + HR | Technisch personeel (engineers, monteurs), onderhoudsplanning, via **scholing** hogere spanningen en grotere netaansluitingen (10, 20, 50 MVA) |
| Netwerkmanager | CEO + HR | Netwerkteam, NOC, carrier- en peeringcontracten |
| Security manager | CEO + HR | Beveiligers, badgesysteem, certificeringen (via research) |

- Zonder salesdirecteur komen er geen contracten. Je moet dus vanaf het begin nadenken over wat je bouwt en in welke volgorde.
- **Scholing** is een upgrade per manager met tijd en kosten. Een voorbeeld: de facility manager volgt een hoogspanningscursus, en daarna mag je een 20 MVA-aansluiting aanvragen.
- In de creatieve modus kun je alle unlocks direct aanzetten.

## Campus, kantoren en warehouse

- Je ontwerpt een complete **campus** met meerdere gebouwen en bijgebouwen.
- **Kantoorpanden** met meerdere verdiepingen deel je zelf in, met afdelingen, een **personeelskantine** en vergaderruimtes. Kantoorruimte kun je ook **verhuren aan grote klanten** op contractbasis.
- In een **warehouse** sla je serverapparatuur op. Die apparatuur kun je ook verhuren aan klanten.

## Eigen hostingbedrijf

- Later in het spel kun je een **eigen hostingbedrijf** starten, een aparte fictieve BV onder jouw datacenter.
- Dat bedrijf huurt racks in jouw datacenter en heeft eigen personeel en technici nodig.
- Het verkoopt **VPS'en, dedicated servers en een public cloud**. Groeit de cloud, dan heeft het meer racks nodig.
- Je wijst een zaal aan als **eigen cloud** voor dit bedrijf.
- Het hostingbedrijf wordt ontgrendeld door de **algemeen directeur**. Je kunt klein beginnen met een **private cloud** in een kleine ruimte van je eigen datacenter, en later een **extra kantoorlocatie** openen.

## Training, budgetten en kwaliteit

- Personeel heeft bij aanname een niveau, maar je kunt ze ook **intern laten trainen** met een opleidingsbudget. Getrainde engineers werken sneller, maken minder fouten en lossen storingen slimmer op.
- Per afdeling zet je een **budget** neer, bijvoorbeeld voor de **netwerkkwaliteit**. Met meer budget bouwt het netwerkteam een state-of-the-art netwerk.
- De NOC of de netwerkarchitect stuurt je af en toe een **voorstel**, zoals: "Voor € 240.000 krijgen we de nieuwste router van *Corelink*. Dan dalen de latency en de storingskans." Jij keurt het goed of af. Merknamen zijn altijd verzonnen.
- Kwaliteit levert **recensies** op, over netwerk, stroom, support en beveiliging. Die tellen op tot je **reputatie**, en een hogere reputatie trekt interessantere klanten.

## Klantbezoek en sfeer

- Klanten komen dagelijks op locatie om hun servers te onderhouden. Je ziet ze inchecken, naar hun rack lopen en weer vertrekken.
- Voor hen bouw je een **klantruimte** met bijvoorbeeld een frisdrankautomaat, een magnetron, banken en werkterminals. Een goede klantruimte verhoogt de klanttevredenheid.
- In de datahallen zet je **monitorkarretjes** (crash carts) neer, zodat klanten hun server ter plekke kunnen beheren.
- **Sfeer**: racks hebben knipperende statuslampjes, en dat blijft licht voor de pc. Zoom je in op een hal, dan hoor je het bekende datacentergeluid: het zoemen van de koeling en het schelle gieren van 1U-servers. Het volume hangt af van de afstand en het aantal racks.

## Hostingbedrijf uitgewerkt

Het hostingbedrijf is een tweede spel in het spel, zonder micromanagement:

- **Diensten**: een public cloud met VPS'en, en **dedicated servers** in soorten zoals webhosting, opslag en GPU.
- **Voorraad**: dedicated servers moeten op voorraad liggen in je **warehouse**. Medewerkers bouwen ze in elkaar, maken ze klaar voor de klant en installeren ze in het datacenter. Dat zie je gebeuren, maar je hoeft het niet zelf aan te sturen.
- **Afdelingen**: het hostingbedrijf heeft een eigen sales- en supportafdeling. Kleine contracten lopen vanzelf. **Grote contracten** komen af en toe naar boven en moet je zelf tekenen, bijvoorbeeld: "Wij zijn een streamingdienst en willen 2.400 servers binnen twee maanden."
- **Dashboard** met grafieken: hoeveel racks er beschikbaar zijn voor dedicated verhuur, hoeveel hardware op voorraad ligt, hoeveel er actief is en wat het per maand oplevert. Je stuurt bij met prijzen, voorraad en capaciteit.
- **Internationaal**: later bouw je ook in een ander land een datacenter, met een eigen kantoor. Daarna kan het hostingbedrijf internationale diensten aanbieden.

## Automatisering en personeel

- Diensten en roosters worden **automatisch** ingevuld. Stel je een **manager** aan, dan regelt die zijn team.
- Wie het leuk vindt, kan later handmatige roosters per afdeling aanzetten.

## Schaal en techniek voor grote campussen

- De game moet **heel groot** kunnen worden: grote terreinen, meerdere gebouwen en veel verdiepingen.
- Godot 4 kan dit aan als we het slim opbouwen: het terrein wordt in **blokken (chunks)** geladen, gelijke objecten zoals racks worden met **MultiMesh** in één keer getekend, verre objecten krijgen een eenvoudiger model (**LOD**), en de simulatie (stroom, koeling, netwerk, geld) rekent per tik los van het beeld. Verdiepingen die je niet bekijkt, worden niet getekend.
- **Poppetjes** worden ronder en meer driedimensionaal, met een sterkere eigen stijl in de lijn van de referentiebeelden.

## Nieuw spel

Bij het starten van een nieuw spel kies je de instellingen:

| Instelling | Opties |
|---|---|
| **Concurrerende datacenters (AI)** | Aan / uit. Als ze aan staan: aantal (1 tot 5) en hoe agressief ze zijn |
| **Locatietype** | Kaal terrein / stadskavel |
| **Startkapitaal** | Laag / normaal / hoog |
| **Moeilijkheid** | Bepaalt onder meer storingskans, wachttijden bij de netbeheerder en schommelingen in de stroomprijs |
| **Willekeurige gebeurtenissen** | Aan / uit (netuitval, hittegolf, kabelbreuk, faillissement van een klant) |
| **Creatieve modus** | Aan / uit, met losse schakelaars: onbeperkt geld, stroom geen beperking, koeling geen beperking, netwerk geen beperking, geen storingen, direct bouwen |

**Creatieve modus** is bedoeld om zonder grinden lekker te bouwen. Je kiest zelf welke onderdelen je uitzet, bijvoorbeeld alleen onbeperkt geld en verder alles realistisch.

Zonder AI-concurrenten win je aanbestedingen op basis van je eigen prijs en kwaliteit, en is peering met andere datacenters niet beschikbaar.

## Camera en bediening

- Vrij draaibare camera (slepen of Q/E), zoomen met het scrollwiel, schuiven met rechts slepen.
- Daken aan/uit (R).
- Overlay-weergaven voor stroom, warmte en glasvezel.
- Op een gebouw klikken opent een infopaneel met de gegevens.

## Techniek

- **Engine:** Godot 4.5 met C# (.NET 8). Het project staat in de map `game/`.
- **Platform:** Windows 11, als installeerbare game (installer).
- **Offline:** het spel werkt volledig offline. Savegames staan lokaal, met meerdere opslagplekken en autosave.
- **Talen:** standaard Engels, volledig vertaald naar het Nederlands.
- **Info-scherm:** versie, controleren op updates en bijwerken via GitHub Releases. Je kunt kiezen tussen het stabiele kanaal (`main`) en het testkanaal (`test`). Updaten downloadt de installer en installeert die op de achtergrond; daarna start de game opnieuw. Alleen deze functie gebruikt internet.
- **Savegames:** in `%APPDATA%\Datacenter Architect\saves`, met vrije namen en drie autosaves die elkaar afwisselen (standaard elke 5 minuten, en bij afsluiten).

## Bewust niet in scope

- Geen mailserver en geen backups naar externe servers (FTP/SCP). Het is een offline game.
- Geen multiplayer.
- Geen klimaat- of biome-regels. Locaties kunnen overal liggen.

## Eerste mijlpalen

1. **Fundament en huisstijl** (klaar)
2. **Technische basis** (klaar): Godot-project, camera, bouwgrid, installer, talen, lokale savegames, Info-scherm
3. **Bouwen en creatieve modus**: nieuw spel met creatieve modus, terrein indelen (wegen, paden, hekwerk, groen, licht), bouwmaterialenplek met vrachtwagens, bouwvakkers die buitenmuren en dak bouwen, hallen zelf uitmeten met deurkeuze, verdiepingen, kantoren inrichten, racks per rij met de opbouwketen (levering, monteurs, aansluiten), klantruimte en monitorkarretjes, knipperende lampjes en servergeluid
4. **Stroom en koeling**: netaansluiting en hoogspanning, transformatorruimtes, UPS, generatoren met N+1, koeling en koeltorens, stroom-overlay met bolletjes
5. **Netwerk**: fiberducts, MMR, carriers, peering, verkeersweergave, NOC
6. **Organisatie, klanten en economie**: CEO-kantoor en unlock-boom (CFO met leningen, HR, salesdirecteur), sales, klanttypes, zones, contracten, energiecontracten, faillissement
7. **Personeel en gebeurtenissen**: werven, training en budgetten, automatische diensten, onderhoud, storingen en willekeurige gebeurtenissen
8. **Beveiliging, research en reputatie**: research tree, voorstellen van NOC en netwerkarchitect, recensies, certificeringen, overheidsklanten
9. **Groei**: campus, kantoren verhuren, warehouse, hostingbedrijf (cloud, dedicated servers met voorraad, grote contracten, dashboard), meerdere locaties en landen
10. **Uiterlijk**: rondere 3D-poppetjes, seizoenen en weer, dag en nacht

## Open vragen

- Hoe heet het spel definitief? *Datacenter Architect* is de werktitel.
- Is er een campagne met scenario's, of alleen vrij spelen?
