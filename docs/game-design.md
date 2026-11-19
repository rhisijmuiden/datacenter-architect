# Game design document: Datacenter Architect

*Versie 0.7, 19 november 2026. Een levend document dat met de game meegroeit.*

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
- **Servers in de racks:** een geplaatst rack is gevuld met apparatuur van verschillende hoogtes: 1U- en 2U-servers, 3U GPU-servers, 4U storage en 7U bladechassis, met bovenin één of twee switches. Elk rack heeft een eigen thema (compute, storage, blades of GPU) en altijd dezelfde inhoud. De lampjes zitten op de servers zelf (schijfbays, poorten, blades) en knipperen pas als het rack live is. In de opties kun je het knipperen uitzetten.
- **Grote zalen blijven soepel:** racks en servers worden per blok van 16 × 16 vakken getekend en alleen het blok waarin iets verandert wordt opnieuw opgebouwd. Van ver weg zie je alleen de rackfronten; dichtbij (tot 6 m inzoomen) zie je de servers en hun lampjes. Meten kan met `--bench N` (zie `docs/performance.md`).

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
- **Poorten gaan echt open (0.3.0-test.3):** er is een schuifpoort van 3 m en een **grote schuifpoort van 6 m** voor brede opritten. Het hek schuift opzij langs het hekwerk en de slagboom gaat omhoog. Voertuigen stoppen ervoor en wachten tot hek of slagboom helemaal open is; daarna gaat alles weer dicht.
- **Laadperron:** een vrachtwagen keert een paar meter voor het perron en rijdt achteruit tegen de roldeur, met de neus van het gebouw af. Hij rijdt nooit het pand in.
- **Bestrating vloeit:** asfalt, parkeren, paden en fietspaden krijgen automatisch ronde hoeken en afgeronde binnenbochten, en asfalt aan de rand van het terrein krijgt een afrit met verlaagde stoeprand naar de openbare weg.
- **Curve-tool (0.3.0-test.5):** gebogen weg, gebogen voetpad en gebogen fietspad. Klik het begin, klik het eind, beweeg de muis om de bocht te maken en klik om te bouwen; [ en ] veranderen de breedte (1 tot 8 m, weg standaard 4 m). De bocht wordt als vloeiende strook getekend (weg met middenstreep), de cellen eronder worden gewoon bestraat zodat vrachtwagens erover rijden. Je betaalt per bestrate m²; wegslopen of overschilderen haalt dat stuk van de bocht weg.

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

**Extra functies (stap 7)**

| Functie | Wat doet hij |
|---|---|
| Receptionist | Ontvangt klanten in de ontvangsthal; inchecken gaat sneller |
| Schoonmaker | Houdt de zalen stofvrij; anders gaan servers en koeling eerder stuk |
| Remote hands | Herstart servers en wisselt schijven voor klanten; levert extra inkomsten op |
| NOC-operator | Bewaakt 24/7 stroom, koeling en netwerk en meldt storingen eerder |
| Brandveiligheidsmedewerker | Onderhoudt het gasblussysteem en de branddetectie |
| Kantine- en facilitair medewerker | Houdt koffie, frisdrank en kantine op peil; goed voor de tevredenheid |
| Monteur generatoren | Tankt diesel en test de generatoren elke maand |
| Hovenier | Onderhoudt het groen; goed voor de uitstraling en reputatie |
| IT-helpdesk | Helpt je eigen kantoorpersoneel |

- Een **projectmanager bouw** (onder de operationeel manager) laat bouwvakkers en monteurs sneller werken en meer projecten tegelijk draaien.

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

*Uitgewerkt voor stap 8.*

- **Onderzoekspunten** verdien je met een **R&D-ruimte** en engineers, en extra met certificeringen en tevreden klanten.
- **Vijf takken** in de research tree:
  - **Stroom**: betere UPS'en, lithium-accu's, hoogspanning tot 150 kV, batterijopslag, eigen zon en wind.
  - **Koeling**: cold corridor, vrije koeling, waterkoeling, immersion cooling, restwarmte verkopen.
  - **Netwerk**: snellere poorten (100G, 400G, 800G), eigen dark fiber, een eigen internet exchange.
  - **Bouw**: prefab modules, sneller bouwen, hogere gebouwen, meer verdiepingen.
  - **Bedrijf**: betere contracten, marketing, overheidsklanten, het hostingbedrijf.
- **Tijdperken**: nieuwe techniek komt beschikbaar naarmate de jaren in het spel verstrijken; AI-racks bijvoorbeeld pas vanaf een bepaald jaar.
- **Voorstellen van personeel**: de netwerkarchitect of de facility manager stelt zelf iets voor, zoals "Voor € 2 miljoen krijgen we de nieuwe router van NordLink, dan wordt de latency 30% lager."
- **Merken**: verzonnen leveranciers, elk met eigen voor- en nadelen.
- In stap 8 komen ook een **marketingmanager** (meer aanvragen, sneller reputatie) en een **compliance-manager** (audits en certificeringen zoals ISO 27001 en Tier III, nodig voor overheid en banken).

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
| Operationeel manager | CEO + HR | Alle operationele zaken: technisch personeel, ploegen werken sneller; facility-, netwerk- en securitymanager vallen hieronder |
| Facility manager | Operationeel manager | Technisch personeel (engineers, monteurs), onderhoudsplanning, via **scholing** hogere spanningen en grotere netaansluitingen (10, 20, 50 MVA) |
| Netwerkmanager | Operationeel manager | Netwerkteam, NOC, carrier- en peeringcontracten |
| Security manager | Operationeel manager | Beveiligers, badgesysteem, certificeringen (via research) |

| Bedrijfsjurist | Salesdirecteur | Meebieden op grote aanbestedingen |
| Customer success manager | Salesdirecteur | Tevredener klanten, meer verlengingen, minder vertrek na storingen |
| Inkoopmanager | CFO | Goedkopere energie- en carriercontracten |

- Later komen er nog bij: marketingmanager en compliance-manager (stap 8) en een duurzaamheidsmanager (stap 9: lagere PUE, groene stroom, eigen zonnepanelen).
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

## Bouwprojecten en gevels

- Je kunt **meerdere bouwprojecten tegelijk** laten uitvoeren. In een projectenlijst geef je elk project een **prioriteit**. Bouwvakkers werken de projecten met de hoogste prioriteit eerst af.
- **Gevels**: een kantoor bouw je in baksteen, betonlook of glas, altijd met ramen. Een datacenter is een **dichte betonnen box** zonder ramen. Een kantoor kun je er direct tegenaan bouwen.
- **Verlichting** komt automatisch in elke ruimte: lampen boven de rackrijen in de hal en armaturen in kantoren. Meubilair zet je zelf neer.

## Beveiliging uitgewerkt

- **Toegang**: badgedeuren met een passysteem op de datahallen, de MMR en klantruimtes. Je bouwt dit globaal en hoeft geen bekabeling te trekken.
- **Beveiligers** controleren bij aankomst wie de klant is.
- **Upgrades**: kant-en-klare camera's buiten en binnen, een **videowall** in de beveiligingsruimte en een **glazen loge** waar de beveiliger achter zit.
- **Policies** die je aan- en uitzet, bijvoorbeeld:
  - Een klant moet zich altijd identificeren bij een beveiliger.
  - Een beveiliger loopt altijd mee met de klant om het rack te openen.
  - Er mag geen bezoek buiten kantooruren komen.

  Strengere policies verhogen de reputatie en tellen mee voor certificeringen, maar vragen meer beveiligers.
- **Klantruimte**: bezoekers kun je koffie aanbieden in een eigen kantine voor klanten.
- **Grond bijkopen**: je kunt altijd aangrenzende kavels bijkopen.

## Training, budgetten en kwaliteit

- Personeel heeft bij aanname een niveau, maar je kunt ze ook **intern laten trainen** met een opleidingsbudget. Getrainde engineers werken sneller, maken minder fouten en lossen storingen slimmer op.
- Per afdeling zet je een **budget** neer, bijvoorbeeld voor de **netwerkkwaliteit**. Met meer budget bouwt het netwerkteam een state-of-the-art netwerk.
- De NOC of de netwerkarchitect stuurt je af en toe een **voorstel**, zoals: "Voor € 240.000 krijgen we de nieuwste router van *Corelink*. Dan dalen de latency en de storingskans." Jij keurt het goed of af. Merknamen zijn altijd verzonnen.
- Kwaliteit levert **recensies** op, over netwerk, stroom, support en beveiliging. Die tellen op tot je **reputatie**, en een hogere reputatie trekt interessantere klanten.

## Klantbezoek en sfeer

- Klanten komen **af en toe** op locatie om aan hun servers te werken, net als in het echt: niet elke dag, maar willekeurig en vrij weinig. Je ziet ze parkeren, naar hun rack lopen, er een tijdje werken en weer vertrekken.
- Klanten komen **24/7**, zoals in het echt: overdag het meest, 's avonds minder en 's nachts een enkeling. Hoeveel er tegelijk rondlopen, is een **percentage van de bezette racks** (stap 7: ongeveer 2%, minimaal 3 en maximaal 40 voor de prestaties). Grote klanten zoals hyperscalers hebben vaker mensen op locatie.
- Klanten die **verhuizen** vanuit een ander datacenter komen met een vrachtwagen naar de **loading dock** en dragen hun spullen naar hun racks. Dat draait vanzelf op de achtergrond.
- Voor hen bouw je een **klantruimte** met bijvoorbeeld een frisdrankautomaat, een magnetron, banken en werkterminals. Een goede klantruimte verhoogt de klanttevredenheid.
- In de datahallen zet je **monitorkarretjes** (crash carts) neer, zodat klanten hun server ter plekke kunnen beheren.
- **Sfeer**: racks hebben knipperende statuslampjes, en dat blijft licht voor de pc. Zoom je in op een hal, dan hoor je het bekende datacentergeluid: het zoemen van de koeling en het schelle gieren van 1U-servers. Het volume hangt af van de afstand en het aantal racks.

## Hostingbedrijf uitgewerkt

*Extra ideeën voor stap 9:*

- **Producten die je zelf samenstelt**: VPS, dedicated servers, cloud-opslag, managed Kubernetes, GPU-servers voor AI en back-up as a service. Per product kies je de prijs, de specs en de SLA.
- **Eigen racks**: je hosting draait in je eigen datacenter en concurreert dus om ruimte met je colocatieklanten.
- **Serverinkoop en voorraad**: servers koop je in bij verzonnen merken, met levertijden en een magazijn. Zit een server vol, dan bestel je bij.
- **Klantenportaal-dashboard**: aantal klanten, omzet per product, churn, tickets en een uptime-meter.
- **Supportteam en tickets**: meer klanten betekent meer tickets. Te traag reageren kost recensies.
- **Marketingcampagnes**: Black Friday-acties, studentenkorting of een gratis proefperiode.
- **Incidenten**: een DDoS-aanval, een overbelaste node of een klant met een spamserver. Jij kiest hoe je reageert.
- **Eigen merk**: naam en logo van je hostingbedrijf, zichtbaar op het gebouw en de bussen.
- **Regio's**: hosting over meerdere locaties, met georedundantie als premium product.
- **Overname of beursgang** in het late spel.

Het hostingbedrijf is een tweede spel in het spel, zonder micromanagement:

- **Diensten**: een public cloud met VPS'en, en **dedicated servers** in soorten zoals webhosting, opslag en GPU.
- **Voorraad**: dedicated servers moeten op voorraad liggen in je **warehouse**. Medewerkers bouwen ze in elkaar, maken ze klaar voor de klant en installeren ze in het datacenter. Dat zie je gebeuren, maar je hoeft het niet zelf aan te sturen.
- **Animatieketen (stap 9)**: een klant bestelt een dedicated server → een warehouse-medewerker assembleert hem in het warehouse → een support engineer brengt hem naar de datazaal, plaatst hem in het rack en zorgt dat de configuratie klopt → de server komt online. Puur animatie om te volgen, geen micromanagement.
- **Afdelingen**: het hostingbedrijf heeft een eigen sales- en supportafdeling. Kleine contracten lopen vanzelf. **Grote contracten** komen af en toe naar boven en moet je zelf tekenen, bijvoorbeeld: "Wij zijn een streamingdienst en willen 2.400 servers binnen twee maanden."
- **Dashboard** met grafieken: hoeveel racks er beschikbaar zijn voor dedicated verhuur, hoeveel hardware op voorraad ligt, hoeveel er actief is en wat het per maand oplevert. Je stuurt bij met prijzen, voorraad en capaciteit.
- **Internationaal**: later bouw je ook in een ander land een datacenter, met een eigen kantoor. Daarna kan het hostingbedrijf internationale diensten aanbieden.

## Automatisering en personeel

- **Geen micromanagement** (Reinder, 19 november 2026): personeel en klanten draaien autonoom op de achtergrond als levendige opvulling. De speler richt zich op ontwerpen, bouwen en uitbreiden, en kijkt daarna hoe zijn datacenter leeft.
- Diensten, roosters, werving en training worden **automatisch** geregeld door de managers. Personeel stelt wel **eisen** die je bouwt: een kantine, werkplekken, parkeerplekken.
- De speler stuurt met **beleid en plannen**, zoals 24/7 support voor klanten (maandkosten, levert reputatie op), bezetting per afdeling, salarisbeleid en opleidingsbudget.
- Wie het leuk vindt, kan later handmatige roosters en werving per afdeling aanzetten in de opties. Voor nu niet.

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
- Overlay-weergaven voor stroom (P), warmte (H) en netwerk (N).
- Op een gebouw klikken opent een infopaneel met de gegevens.

## Techniek

- **Engine:** Godot 4.5 met C# (.NET 8). Het project staat in de map `game/`.
- **Platform:** Windows 11, als installeerbare game (installer).
- **Offline:** het spel werkt volledig offline. Savegames staan lokaal, met meerdere opslagplekken en autosave.
- **Talen:** standaard Engels, volledig vertaald naar het Nederlands.
- **Info-scherm:** versie, controleren op updates en bijwerken via GitHub Releases. Je kunt kiezen tussen het stabiele kanaal (`main`) en het testkanaal (`test`). Updaten downloadt de installer en installeert die op de achtergrond; daarna start de game opnieuw. Alleen deze functie gebruikt internet.
- **Savegames:** in `%APPDATA%\Datacenter Architect\saves`, met vrije namen en drie autosaves die elkaar afwisselen (standaard elke 5 minuten, en bij afsluiten).

## Schermen en prestaties

- Werkt op Full HD tot en met **4K**, en op 4:3, 16:9, 16:10 en ultrawide (21:9, 32:9). De interface schaalt mee en blijft aan de randen; op een breder scherm zie je meer wereld.
- 64-bit Windows-build. Godot rendert op een eigen thread; de simulatie blijft licht genoeg voor grote campussen.
- De omgeving rond je kavel is gevuld met bos, heuvels, akkers en buren, zodat je kavel in een echte wereld ligt.

## Stroom en koeling in het spel

Zo werkt stap 4 (versie 0.3.0) nu echt in de game:

- **De keten**: elk rack heeft de hele keten nodig: netaansluiting → transformatoren → hoofdschakelkast met ATS → UPS → PDU of busbar in de hal → rack. De zwakste schakel bepaalt hoeveel racks kunnen draaien. Racks die niet gevoed kunnen worden, gaan uit (de nieuwste eerst) en leveren geen huur op.
- **Netaansluiting**: elk terrein start met een compact station van 2 MVA met een eigen transformator. Upgrades naar 10, 20 en 50 MVA kosten € 1,2 / 2,8 / 7,5 miljoen en duren 30, 60 en 120 speldagen. Vanaf 10 MVA is middenspanning: dan bouw je een transformatorruimte met eigen transformatoren (2,5 MVA per stuk). De netkant draagt de IT-last maal de PUE.
- **Technische ruimtes**: transformatorruimte, schakelruimte, UPS-ruimte en accuruimte. Transformatoren, schakelkasten, UPS'en (500 kW), accukasten (40 kWh) en vloer-PDU's (150 kW) komen per vrachtwagen, monteurs zetten ze neer en de engineer sluit ze aan.
- **Busbar**: een hal-upgrade met een A- en een B-rail van elk 400 kW boven de rijen. Een hal heeft **A/B-voeding** als hij na uitval van de grootste voeding nog alle racks aankan.
- **Redundantie** per schakel: N, N+1 of 2N, en "N-" als de capaciteit tekortschiet.
- **Generatoren**: 1 MW met een dagtank van 2.000 liter. Zonder schakelkast (ATS) kunnen ze niet overnemen. Bovengrondse tanks (20.000 L) en ondergrondse tanks (50.000 L, je kunt eroverheen rijden) geven meer uren autonomie. Verbruik: 0,27 liter per kWh.
- **UPS-autonomie** in minuten: accucapaciteit gedeeld door de IT-last.
- **Energieleveranciers** (verzonnen): Polder Energie (vast), Delta Stroom (variabel, volgt de markt), Noordwind Groen (groen, vast) en Kustlicht (groen, variabel). De stroomrekening is IT-last × PUE × prijs.
- **Koeling**: CRAH-units in de hal (120 kW, te upgraden naar 162 en 204 kW) brengen de warmte naar koelmachines (600 kW) of koeltorens (1 MW) buiten of op het dak; die zijn ook te upgraden. De effectieve koeling is het minimum van beide.
- **Zaal-upgrades**: open zaal (PUE 1,65) → warme en koude gangen (+12% CRAH, PUE 1,50, € 120/m², 1 dag) → cold corridor (+35%, PUE 1,30, € 380/m², 3 dagen).
- **Temperatuur**: elke hal heeft een temperatuur. Te weinig koeling laat de hal opwarmen; vanaf 32 °C schakelen racks uit tot de rest weer gekoeld kan worden. Is de koeling weer op orde, dan gaan ze vanzelf weer aan.
- **Overlays**: P toont de stroomweergave (amber lijnen en bolletjes van netaansluiting tot rackrij, rood bij overbelasting of uitval, ondergrondse tanks zichtbaar), H de koelweergave (koude lucht naar de rijen, warm water naar de koelmachines, racks gekleurd naar de temperatuur van hun hal).
- **Overzicht stroom en koeling**: netaansluiting aanvragen, leverancier kiezen, de keten met de zwakste schakel, noodstroom, koeling, PUE, elke hal en advies.
- In de creatieve modus zetten "stroom geen beperking" en "koeling geen beperking" dit uit; "direct bouwen" maakt ook netverzwaringen en hal-upgrades direct klaar.
- Later: kelders voor transformatoren, vloerbelasting en een verzwaard dak, onderhoud en storingen (stap 7), en rackstroom als product (16 A, 32 A) bij de klanten (stap 6).

## Netwerk in het spel

Zo werkt stap 5 (versie 0.4.0) nu echt in de game:

- **De keten**: een rack is pas online als het een poort heeft op een **spine-switch** in zijn hal, die spine een poort heeft op een **core-router**, en de core via de **ODF's** (patchframes) in de **meet-me room** een carrier, de internet exchange of een peer bereikt. Racks die draaien maar offline zijn, leveren geen huur op.
- **Glasvezelputten**: in de straat liggen drie carrierputten (A, B en C). Je zet zelf een **glasvezelput** op je terrein; de mantelbuis loopt naar de dichtstbijzijnde straatput. Een put heeft 4 subducts, te upgraden naar 8 en 12 (144 vezels per kabel). Elke carrier, IX-poort en dark fiber neemt één subduct. Putten op twee verschillende straatputten geven **twee gescheiden routes**.
- **Carriers** (verzonnen): Lowlands Transit (put A, € 0,32/Mbit), Kanaal Net (put A, € 0,29), NordFiber (put B, € 0,26) en Eurolink (put C, € 0,38, premium). Je bestelt een poort van 1G, 10G, 100G of 400G (eenmalige aansluitkosten, 20 speldagen levertijd; een upgrade 10 dagen) met een **commit**. Je betaalt per maand de poort plus de commit per Mbit; vanaf 10G commit 15% korting, vanaf 100G 30%.
- **Burst**: elk speluur wordt het verkeer per carrier gemeten. Elke 30 dagen wordt het **95e percentiel** boven de commit afgerekend tegen 1,5 keer de prijs (1,2 keer met een NOC). De drukste 5% van de uren telt niet mee.
- **Internet exchange**: Polder-IX (put C). Je betaalt alleen de poort; de IX neemt 25% van je verkeer gratis over (18% zonder AI-concurrenten), tot de poort vol is.
- **Peering**: met AI-concurrenten aan vragen Zuidas Colo, Rivierhaven DC en Noordpoort Data na een tijd of ze met je mogen peeren via dark fiber. Elk heeft eigen voorwaarden: zij huren een rack in jouw MMR (jij krijgt geld) of ze zijn groter en vragen een maandbedrag. Een peer neemt 7 tot 10% van je verkeer over. Je kunt ook zelf voorstellen, maar een grote concurrent wil pas als je netwerk groot genoeg is.
- **Verkeer**: elk online rack doet gemiddeld 0,25 Gbit/s, met een dagcurve (rustig rond 08:00, piek rond 20:00). Eerst gaat verkeer naar de IX en de peers, de rest over transit. Past het niet, dan raakt een deel zoek: klanten betalen naar rato minder huur.
- **Cross-connects**: hoe meer netwerken in je MMR, hoe meer klanten een cross-connect bestellen. Een engineer patcht die bij een ODF (vrije patchpoort nodig). Elke cross-connect levert € 250 eenmalig en € 60 per maand op.
- **NOC**: een NOC-ruimte met een **videowall** bewaakt het netwerk. Dan gaat 25% meer verkeer via de IX en peering, is burst goedkoper en krijg je een waarschuwing als de transit in de piek boven 85% komt.
- **Redundantie en enkele punten van falen**: het overzicht toont per schakel N, N+1 of 2N en waarschuwt bij één carrier, één route, één MMR, één core-router of een hal met één spine.
- **Netwerkweergave** (N): oranje lijnen en bolletjes van de straatputten via je putten en mantelbuizen naar de MMR, de core-routers, de spines en de rijen. Hoe drukker, hoe sneller; vol of offline kleurt rood. Dark fiber naar peers loopt langs de weg weg.
- In de creatieve modus zet "netwerk geen beperking" de keten uit; "direct bouwen" levert poorten, upgrades en dark fiber direct.
- Later: klanten met eigen netwerkeisen en meerdere carriers (stap 6), kabelbreuk en storingen (stap 7), voorstellen van de NOC en de netwerkarchitect (stap 8).

## Organisatie, klanten en economie in het spel

Zo werkt stap 6 (versie 0.5.0) nu echt in de game:

- **Organisatie**: je begint als eigenaar. Bouw een **directiekamer** met een bureau en een stoel; dan kun je een **CEO** aannemen. Elke directeur en manager heeft een eigen ingerichte directiekamer nodig. Het bedrijfsscherm (tabblad Organisatie) toont de boom: CEO → CFO (→ inkoopmanager), HR-manager, salesdirecteur (→ bedrijfsjurist, customer success manager) en operationeel manager (vereist ook HR; → facility-, netwerk- en securitymanager). Elke functie heeft een aannamebedrag, een maandsalaris en twee trainingen met kosten en dagen. Laten gaan kan, maar niet zolang er iemand onder valt.
- **Wat ze ontgrendelen**: HR werft meer personeel dan de startploeg (4 bouwvakkers, 2 monteurs, 1 engineer); monteurs en engineers vragen ook een operationeel manager, die de ploegen 10% sneller laat werken (+10% per training). De facility manager ontgrendelt 10 MVA, zijn eerste training 20 MVA en de tweede 50 MVA. De netwerkmanager regelt carriers, de IX en peering. De security manager plus een portiersloge is nodig voor gemeentes, ministeries en banken. De CFO regelt leningen en investeerders. De inkoopmanager geeft 5% korting op energie en 10% op carriers (meer met training). De customer success manager laat de tevredenheid minder dalen en meer klanten verlengen. De bedrijfsjurist is nodig om op aanbestedingen te bieden.
- **Sales**: zonder salesdirecteur komen er geen klanten. Verkopers hebben elk een bureau in een kantoorruimte nodig en kosten € 4.200 per maand. Hoe meer verkopers, training en reputatie, hoe meer en grotere aanvragen (maximaal 8 tegelijk, elk 12 tot 22 dagen geldig).
- **Klanttypes**: startup, MKB, hostingbedrijf, websitehosting (ook adult), videostreaming, social media, cryptomining, clouddienst, gemeente en ministerie; voor aanbestedingen ook banken en hyperscalers. Elk type heeft eigen producten, omvang, verkeer per rack, aantal carriers, kans op 32 A, 2N, groene stroom en beveiliging, en een minimale reputatie.
- **Zones**: per datahal kies je wat je verkoopt: volle racks, colocatie per U (42 U per rack), kooien, een eigen hal (één klant) of eigen cloud (voor stap 9). Dat kan in het bedrijfsscherm en in het infopaneel van de hal.
- **Producten en prijzen**: colocatie € 42 per U, een volle rack van 16 A (3,7 kW) € 780 en van 32 A (7,4 kW) € 1.250 per maand; kooien +15%, een eigen hal +10%, 2N +10%, maal een factor per klanttype. Per-U-klanten kosten € 6 per U per maand aan support.
- **Eisen**: elke aanvraag toont per eis ✓, ! of ✕: vrije racks of units in een hal met de juiste zone (een kooi binnen één hal, een eigen hal zonder andere klanten), A/B-voeding, voor 32 A ook warme/koude gangen, 2N-UPS, generatoren N+1 (waarschuwing), onverkochte transitcapaciteit, aantal carriers, een groen energiecontract en beveiliging. Tekenen kan als er geen ✕ meer staat.
- **Contracten**: na het tekenen krijgt de klant vaste racks. Elke dag wordt gemeten welk deel van die racks draait en online is. Huur wordt naar rato betaald; bij uitval betaal je een SLA-boete en daalt de tevredenheid. Een gesloopt rack wordt vervangen door een vrij rack. Aan het eind verlengt een tevreden klant (60 of hoger, 45 met customer success); anders vertrekt hij. Onder de 15 vertrekt hij meteen. Voortijdig beëindigen kost drie maanden huur.
- **Aanbestedingen**: ongeveer elke maand schrijft een grote klant een aanbesteding uit (24 tot 200 racks, eigen kooi of hal, 2N, meerdere carriers, 36 tot 60 maanden, prijs per kW). Met een bedrijfsjurist bied je 85, 100 of 115% van de vraagprijs. Na 10 dagen beslist de klant: lagere prijs, betere reputatie, training van de jurist en nu al aan de eisen voldoen helpen; AI-concurrenten bieden mee. Gewonnen? Dan lever je binnen 90 tot 180 dagen op. Lukt dat niet, dan vervalt het contract, betaal je drie maanden als boete en verlies je reputatie.
- **Reputatie** (0 tot 100, start 30): stijgt met verlengingen, opgeleverde aanbestedingen en tevreden klanten; daalt bij vertrek en gemiste deadlines. Een hogere reputatie geeft meer en grotere klanten en betere kansen bij aanbestedingen.
- **Boekhouding**: per maand inkomsten en kosten per post (klantcontracten, cross-connects en peering, energie, carriers, ploegen, directie en sales, terrein en support, SLA-boetes, rente, bouw) en het bedrijfsresultaat. Klik op het saldo bovenin om de financiën te openen.
- **Leningen**: de bank leent tot € 2 miljoen plus 24 maanden omzet, tegen 6,5% (min 1% per CFO-training, min 0,5% bij reputatie 60+), terug te betalen in 60 maanden. Vervroegd aflossen kan altijd.
- **Investeerders**: verkoop 10% van het bedrijf voor 10% van de bedrijfswaarde. Investeerders krijgen elke maand hun deel van de winst. Je houdt altijd minstens 51%.
- **Energiecontracten**: een vast contract loopt 1 tot 5 jaar. Eerder overstappen kost ongeveer drie maanden stroomrekening (de eerste 14 dagen gratis). Variabel kan altijd.
- **Faillissement**: sta je in het rood, dan krijg je waarschuwingen. Na 30 dagen in het rood ga je failliet: het spel stopt en je laadt een savegame of gaat terug naar het hoofdmenu. Met onbeperkt geld (creatieve modus) kan dat niet.
- **Klantbezoeken**: klanten komen willekeurig langs: per-U-klanten ongeveer eens per 60 dagen, racks eens per 40, kooien eens per 25 en eigen hallen eens per 18 dagen. Ze dragen een cyaan bezoekersbadge. (Vanaf stap 7: 24/7 en meegroeiend met de bezette racks, zie *Personeel en gebeurtenissen in het spel*.)
- **Oudere savegames**: racks die al huur opleverden, blijven bij "Bestaande klanten" (€ 900 per rack per maand). HR, operations, facility (volledig getraind) en netwerk zijn al geregeld, zodat niets ophoudt te werken.
- In de **creatieve modus** zet "alle unlocks" de hele organisatie open.

## Personeel en gebeurtenissen in het spel

Zo werkt stap 7 (versie 0.6.0) nu echt in de game:

- **Personeelsscherm** (Personeel in de bouwbalk, het poppetjes-icoon rechtsboven of het vak Personeel bovenin) met tabbladen links: Team, Beleid, Onderhoud, en Storingen en gebeurtenissen.
- **15 functies**: elektrotechnisch engineer, koeltechnicus, monteur generatoren, brandveiligheidsmedewerker (facility manager); netwerkengineer en NOC-operator (netwerkmanager); support engineer, remote hands, receptionist, magazijnier, schoonmaker, kantine- en facilitair medewerker, hovenier en IT-helpdesk (operationeel manager); beveiliger (security manager). HR is altijd nodig. Sommige functies vragen een ruimte: NOC-ruimte, beveiligingsruimte of portiersloge, receptie, kantine.
- **Automatisch werven**: elke afdeling krijgt een advies (bijvoorbeeld één koeltechnicus per 20 koelunits, één elektrotechnicus per 30 onderdelen in de stroomketen, één support engineer per 150 racks; vanaf 60 racks drie per 24/7-functie zodat elke dienst bezet is). De manager neemt zelf mensen aan tot dat advies, met een naam, niveau (junior, medior, senior) en soms een eigenschap (snel, nauwkeurig, nachtmens, loyaal). Bij te veel mensen wordt een contract niet verlengd.
- **Beleid**: bezetting per afdeling (krap 75%, normaal, ruim 125% en elke dienst bezet), salarisbeleid (-10%, markt, +15%), opleidingsbudget (€ 0 tot € 20.000 per maand; wordt verdeeld en tilt mensen een niveau omhoog: junior 0,8×, medior 1×, senior 1,25× tempo) en preventief onderhoud (licht, normaal, grondig).
- **Serviceplannen**: 24/7 klantensupport (€ 6.000 per maand, drie support engineers, hogere klanttevredenheid en reputatie), 24/7 NOC-bewaking (€ 3.500, drie NOC-operators, elke storing meteen gezien, reputatie) en een remote hands-dienst (€ 1.500, levert € 22 per verhuurd rack per maand op, 120 racks per persoon).
- **Diensten**: dag (07-15), avond (15-23) en nacht (23-07). 24/7-functies worden automatisch verdeeld, nachtmensen nemen de nachten; de rest werkt overdag. Is er in een dienst niemand, dan wacht een storing op de volgende dienst.
- **Tevredenheid**: salaris, kantine (met kantinemedewerker beter), koffie of frisdrankautomaat, parkeerplekken (de helft van het personeel), genoeg werkplekken voor sales, support en helpdesk, een IT-helpdesk vanaf twaalf kantoormensen, opleidingsbudget en groen. Veel werk en nachtdiensten tellen negatief. Onder 30% kunnen mensen vertrekken; de manager vervangt ze.
- **Slijtage en onderhoud**: racks, CRAH-units, chillers, koeltorens, generatoren, UPS'en, accu's, PDU's, transformatoren, switchgear, routers, spines, ODF's en fiberputten slijten elke dag. Technici houden ze bij; zonder schoonmakers (één per twee zalen) slijt apparatuur in de zalen 40% sneller, in een hittegolf koelapparatuur 50%. Onderhoud kost per onderdeel per dag. Nieuwe apparatuur gaat niet stuk; vanaf 5% slijtage stijgt de kans snel. UPS'en en accu's gaan vijf jaar mee en moeten daarna worden vervangen (half de nieuwprijs).
- **Storingen**: een kapot onderdeel werkt niet meer (de stroomketen, koeling of het netwerk merkt het direct). Zonder NOC-operator in dienst merkt iemand het na twee uur. Een vrije technicus in dienst gaat erheen (je ziet hem lopen en werken). Elektrotechnici springen in voor generatoren (langzamer). Heb je niemand voor de klus, dan komt na 12 uur automatisch een externe monteur (kosten); die kun je ook zelf meteen bellen. Uitval kost klanten per uur tevredenheid en SLA-boete.
- **Reserveonderdelen**: serveronderdelen, koelonderdelen, elektrische onderdelen, UPS-accu's en optics. Een reparatie gebruikt één onderdeel; zonder onderdeel duurt het drie keer zo lang. Automatisch bijbestellen bij een minimum (standaard 2, per 5 stuks), levering in 4 dagen, één dag sneller per magazijnier.
- **Gebeurtenissen** (aan of uit bij een nieuw spel): stroomstoring in de stad (2 tot 8 uur; generatoren achter de ATS nemen het over zolang er diesel is, een monteur generatoren regelt extra diesel), hittegolf in de zomer (20% minder warmteafvoer), kabelbreuk bij graafwerk, brandalarm (zonder brandveiligheidsmedewerker gaat het gasblussysteem af: de zaal ligt vier uur plat en navullen kost € 35.000), een klant die failliet gaat en persbezoek (reputatie +2 of -1). Alles staat in een logboek.
- **Projectmanager bouw** (onder de operationeel manager): bouwvakkers en monteurs 15% sneller, +10% per training.
- **Op het terrein** lopen maximaal 24 personeelsleden tegelijk rond: technici naar storingen, beveiligers op patrouille, schoonmakers in de zalen, hoveniers in het groen, mensen in de NOC, receptie en kantine. Externe monteurs dragen een oranje helm.
- **Klantbezoek** draait 24/7 en groeit mee met de bezette racks; nieuwe klanten verhuizen hun spullen via de loading dock.
- **Oudere savegames**: bestaande apparatuur telt als nieuw geïnstalleerd op de dag van laden; het personeel wordt de eerste dag automatisch geworven.
- In de **creatieve modus** zet "geen storingen" slijtage-storingen en kabelbreuken uit.
- Nog niet in deze versie (volgt in een vervolgstap): ongedaan maken, slim slepen, zichtbare kabelgoten, VESDA en blusflessen als objecten, cross-connect-orders en de voorraadvrachtwagen.

## Grote datahallen

Reinder wil echt grote hallen kunnen bouwen, op hyperscale-formaat met duizenden racks. Wat daarvoor nodig is:

- **Groter terrein**: nu is een kavel 64 × 64 meter. Grotere kavels en aangrenzende grond bijkopen (stap 9) maken hallen van honderden meters mogelijk.
- **Snel plaatsen**: rijen langer dan 24 racks, en een blokgereedschap dat een heel vlak met rijen, gangen en containment in één keer vult.
- **Prestaties**: racks en andere herhaalde objecten worden met MultiMesh getekend, objecten krijgen een ruimtelijke index (geen lijst doorzoeken per cel), en verre hallen krijgen een eenvoudig model (LOD). Simulatie per hal in plaats van per rack waar dat kan.
- **Klanten blijven beperkt**: ook bij duizenden racks heb je meestal enkele tientallen klanten en staan er hooguit 10 tot 20 tegelijk binnen.

## Uitbreidingen en ideeën per stap

Goedgekeurd door Reinder op 19 november 2026; ze komen in de genoemde stap.

**Bouwen prettiger maken**
- *Stap 7*: **ongedaan maken / opnieuw** (Ctrl+Z, ook bij grote ingrepen), **slim slepen** (muren, rackrijen en kabelgoten in één beweging, met maten en kosten live in beeld), **snap en hulplijnen** (uitlijnen, symmetrie, draaien in stappen van 15° naast 90°).
- *Stap 9*: **kant-en-klare modules** en blauwdrukken (een datazaal, UPS-ruimte of kantoorvleugel in één keer plaatsen; eigen ontwerpen opslaan) en **prefab datacontainers** om snel uit te breiden.
- *Stap 10*: **gevelbouwer** (materiaal, kleur, logo, luifel, gevelverlichting, groene daken en muren per gevel), **doorsnede-weergave** per verdieping, **bouwanimaties** (steigers, betonmolens, een kraan die echt hijst), **interieurdetails** (verhoogde vloer met tegels, plafondgoten, brandblussers, nooduitgangsbordjes, kabels in de goten) en **sfeerlicht** (blauwe ledstrips in de koude gang, rode noodverlichting bij storing).

**Bekabeling**
- *Stap 7*: **zichtbare kabelgoten** boven de racks of onder een verhoogde vloer, die je als een pad trekt met bochten en T-splitsingen; glasvezel in oranje, koper en stroom in grijs en zwart. **Kabels liggen echt in de goten**: hoe meer klanten, hoe voller. Een overvolle goot kleurt waarschuwingsoranje en wordt een risico. **Automatisch routeren** stelt met één klik het netste pad voor. Een **kabelmanagement-score** geeft minder storingen, snellere engineers en betere audits. **Storingseffect**: een kabel die uitvalt flitst rood, de bolletjes stoppen en zoeken een weg via de B-route.
- *Stap 10*: **gloeiende flow-lijnen** waarvan de dikte de belasting toont (stroom amber, netwerk oranje, koeling cyaan, zoals overal in de huisstijl), **A/B-feeds in twee tinten**, een **close-up van een rack** (patchkabels, knipperende poortlampjes, PDU-meters) en een **röntgenweergave ondergronds** (ducts, mantelbuizen, de hoogspanningskabel naar het onderstation).

**Buiten**
- *Stap 8*: **veiligheid**: camera's op palen, een dubbel hek met een strook niemandsland, ram-barrières en paaltjes tegen voertuigen, slagbomen met kentekenherkenning, een helikopterplatform voor VIP's.
- *Stap 9*: **energie**: zonnepark op dak of veld, kleine windmolens, batterijcontainer, gasturbine of brandstofcel als backup, een eigen onderstation met hoogspanningsmasten. **Koeling**: dry coolers op het dak, een buffertank voor koud water en een warmtewisselaar naar de stadsverwarming. **Logistiek**: vrachtwagenparkeerplaats, containerstalling, afvalpers, dieseltankplaats en een overdekte fietsenstalling. **Personeel en klanten**: laadpalen, buitenterras, rookplek, bushalte en een eigen pendelbusje. **Vergunningen**: buren klagen over geluid en licht, en dan moet je groen en een geluidswal plaatsen.
- *Stap 10*: **natuur en water**: vijver of wadi voor regenwater (ook bluswater), bloemenweides, bomenlanen, sloten met bruggetjes, een geluidswal. **Uitstraling**: een groot lichtgevend logobord, vlaggenmast, entree met fontein of kunstwerk en gevelverlichting.

**In de datahallen**
- *Stap 7*: **veiligheid**: VESDA-rookdetectie, gasblussysteem met flessen, noodknoppen, camera's per gang en een mantrap bij de ingang (die laatste via research in stap 8). **Werkplekken**: crashkar met monitor, kast met reserveonderdelen, build-tafel en ladder.
- *Stap 9*: **racktypes**: 42U, 48U en 52U, open frames, GPU-racks met waterkoeling, rear-door coolers en een immersion-tank. **Afscheiding**: kooien met gaas, private suites met een eigen badgedeur, containment met deuren en een glazen dak. **Koeling en stroom in de hal**: in-row units, temperatuursensoren per rack met een warmtebeeld-overlay, PDU's per rack, RPP-kasten aan het eind van de rij en stroommeters.
- *Stap 10*: verhoogde vloer met geperforeerde tegels, plafondgoten, rij- en racklabels, vloermarkeringen, knipperende ledjes, het zoemen van de servers, blauw licht in de koude gang en rood in de warme gang, engineers met karretjes, kooideuren die opengaan en een robot die 's nachts inspecteert.

**Kantoren**
- *Stap 7*: **bezetting**: werkplekken tellen; te weinig plekken maakt personeel ontevreden, flexplekken zijn goedkoper. **Comfort en welzijn**: kantine, fitnessruimte, douches, dakterras en gamingruimte verhogen de tevredenheid en verlagen het verloop.
- *Stap 9*: **ruimtes**: open kantoortuin, vergaderzalen in verschillende maten, een boardroom, belcellen, stilteruimte, serverkast voor de eigen IT, archief en printerhoek. **Klantgericht**: een experience center met een glazen wand naar de datazaal, een ontvangstbalie met logowand en VIP-vergaderruimtes.
- *Stap 10*: **afdelingen met een eigen sfeer** (support met headsets en dashboards, sales met een gong bij elke deal, finance met dossierkasten, de NOC met een videowall), **meubels** (zit-sta-bureaus, ergonomische stoelen, lockers, whiteboards, loungehoek, pingpongtafel, koffiebar met barista), een **kantoorstijl** naar keuze (modern, industrieel, startup of corporate) die klanten en personeel merken, en **leven in het kantoor**: mensen lopen naar de koffie, vergaderen, bellen en vieren verjaardagen met taart.

**Meet-me room**
- *Stap 7*: **cross-connect-orders** in een eigen overzicht; engineers patchen ze zichtbaar met een oranje glasvezelkabel. **Kabelmanagement** met verticale goten, labels en kleurcodes; een rommelige MMR geeft meer storingen en een lagere auditscore.
- *Stap 8*: **strenge toegang**: een eigen badgedeur, logboek en camera; alleen netwerkpersoneel en carriers met een afspraak mogen erin.
- *Stap 9*: **carrier-kooien**: carriers huren een eigen kooi of rack in de MMR (extra huur). **Twee MMR's**, aan elke kant van het gebouw één, met eigen ducts en routes. **Patchpanelen per verdieping** (intermediate distribution frames), zodat niet alles via de MMR beneden hoeft. **Carrier-ecosysteem**: hoe meer carriers, hoe aantrekkelijker voor klanten, zichtbaar als groeiende netwerkkaart. **Eigen internet exchange** in het late spel: je verdient aan de poorten van anderen.

**Intern warehouse**
- *Stap 7*: **voorraad reserveonderdelen** (schijven, voedingen, glasvezelkabels, optics, PDU's, koelfilters, UPS-accu's); raakt iets op, dan duurt een storing langer. **Automatisch bijbestellen** bij een minimumvoorraad, met levertijden per leverancier, en vrachtwagens die echt aanrijden. **Warehouse-medewerkers**: een magazijnier; hoe beter georganiseerd, hoe sneller racks en onderdelen op hun plek zijn. Een **voorraadscherm** met tekorten, verwachte leveringen en kosten.
- *Stap 9*: **ruimtes**: een ontvangstzone bij het laadperron, een uitpakruimte (geen karton in de datazaal), een build room om servers te testen, opslag met stellingen, een kooi voor klantvoorraad en een afvalruimte voor karton en e-waste. **Inrichting**: palletstellingen, rolcontainers, **palletwagens** (bewust geen heftrucks), een werkbank, een inpaktafel, een kluis voor gevoelige onderdelen en barcodescanners. **Klantlogistiek**: klanten laten zendingen bezorgen die je opslaat en naar hun rack brengt (remote hands, extra inkomsten). **Dataveilig afvoeren**: een schijvenvernietiger die een certificaat geeft, goed voor de compliance.

**Overige ideeën**
- *Stap 7*: **hardware-levenscyclus**: servers en UPS-accu's verouderen en moeten worden vervangen. **Klantbezoeken en audits**: klanten komen kijken, en een nette site levert betere contracten op.
- *Stap 8*: **waterkoeling en immersion cooling** voor AI- en GPU-klanten met racks van 50 kW of meer, en **restwarmte verkopen** aan een stadsverwarming of kas (via research).
- *Stap 9*: **batterijopslag en een eigen windmolen** om pieken op te vangen en goedkoper stroom in te kopen. **Grote datahallen** (zie hierboven).
- *Stap 11*: zie *Afwerking en extra's*.

## Bewust niet in scope

- Geen mailserver en geen backups naar externe servers (FTP/SCP). Het is een offline game.
- Geen multiplayer.
- Geen klimaat- of biome-regels. Locaties kunnen overal liggen.

## Eerste mijlpalen

1. **Fundament en huisstijl** (klaar)
2. **Technische basis** (klaar): Godot-project, camera, bouwgrid, installer, talen, lokale savegames, Info-scherm
3. **Bouwen en creatieve modus**: nieuw spel met creatieve modus, terrein indelen (wegen, paden, hekwerk, groen, licht), bouwmaterialenplek met vrachtwagens, bouwvakkers die buitenmuren en dak bouwen, hallen zelf uitmeten met deurkeuze, verdiepingen, kantoren inrichten, racks per rij met de opbouwketen (levering, monteurs, aansluiten), klantruimte met koffie, monitorkarretjes, projectprioriteit, gevels (baksteen, beton, glas; datacenter als betonnen box), automatische verlichting, knipperende lampjes en servergeluid
4. **Stroom en koeling** (testversie 0.3.0): netaansluiting en hoogspanning, transformatorruimtes, UPS, generatoren met N+1, koeling en koeltorens, stroom-overlay met bolletjes. Zie *Stroom en koeling in het spel*.
5. **Netwerk** (testversie 0.4.0): fiberducts, MMR, carriers, peering, verkeersweergave, NOC. Zie *Netwerk in het spel*.
6. **Organisatie, klanten en economie** (testversie 0.5.0): CEO-kantoor en unlock-boom (CFO, inkoop, HR, sales, jurist, customer success, operations, facility, netwerk, security), sales, klanttypes, zones, contracten en aanbestedingen, boekhouding, leningen, investeerders, energiecontracten, faillissement, klantbezoeken. Zie *Organisatie, klanten en economie in het spel*.
7. **Personeel en gebeurtenissen** (testversie 0.6.0): automatisch werven, beleid en serviceplannen, training en budgetten, automatische diensten, onderhoud en slijtage, storingen en reserveonderdelen, willekeurige gebeurtenissen, projectmanager bouw, klantbezoek 24/7. Zie *Personeel en gebeurtenissen in het spel*.
8. **Beveiliging, research en reputatie**: camera's, videowall, glazen loge, beveiligingspolicies, research tree, voorstellen van NOC en netwerkarchitect, recensies, certificeringen, overheidsklanten
9. **Groei**: campus, kantoren verhuren, warehouse, hostingbedrijf (cloud, dedicated servers met voorraad, grote contracten, dashboard), meerdere locaties en landen
10. **Uiterlijk**: rondere 3D-poppetjes, first-person rondlopen door je datacenter (alleen kijken en luisteren), seizoenen en weer, dag en nacht
11. **Afwerking en extra's**: tutorial met een begeleide eerste kavel, scenario's en uitdagingen met medailles ("red een failliet datacenter", "haal binnen 5 jaar 10 MW"), statistieken en grafieken door de tijd (omzet, PUE, uptime, verbruik), fotomodus, achievements en later Steam-ondersteuning, mod-ondersteuning (eigen gebouwen en objecten)

Bij elke stap horen ook de uitbreidingen uit *Uitbreidingen en ideeën per stap*.

## Open vragen

- Hoe heet het spel definitief? *Datacenter Architect* is de werktitel.
- Is er een campagne met scenario's, of alleen vrij spelen?

## Opties en bediening

- **Bouwbalk onderaan** het scherm met duidelijke categorieën (icoon, naam en eigen accentkleur). Klik op een categorie om de lade met onderdelen te openen, nog een keer klikken sluit hem. De **bulldozer** (slopen) staat los rechtsonder; nog een keer klikken zet hem weer uit.
- **Optiemenu** vanuit het hoofdmenu, het pauzemenu (Esc) en het tandwiel rechtsboven in het spel. Alles wordt direct toegepast en lokaal bewaard (user://settings.cfg).
  - **Beeld:** venster, randloos volledig scherm of volledig scherm; resolutie (alle gangbare 4:3, 16:9, 16:10, 21:9, 32:9 en 4K-resoluties die op het scherm passen); beeldscherm kiezen bij meerdere schermen; grootte van de interface (75 tot 150%); V-Sync; maximale framerate. In volledig scherm blijft het scherm op zijn eigen resolutie en wordt alleen de 3D-wereld op de gekozen resolutie getekend (met FSR), zodat tekst scherp blijft. Een wijziging van modus, resolutie of scherm moet je binnen 15 seconden bevestigen, anders gaat hij terug. Alt+Enter of F11 wisselt altijd tussen venster en volledig scherm.
  - **Graphics:** snelkeuze Prestaties, Gebalanceerd of Kwaliteit; schaduwen, ambient occlusion, nevel en anti-aliasing los aan en uit; visuele effecten: knipperende statuslampjes, bewegende onderdelen (ventilatoren, kraanarmen), vloeiende camera.
  - **Geluid:** hoofdvolume plus aparte schuiven voor de serverruimte en de interface; geluidseffecten los aan en uit (serverruimte, klikjes, meldingsgeluid) en dempen als de game op de achtergrond staat.
  - **Spel:** taal (Engels of Nederlands) en hoe vaak automatisch wordt opgeslagen.
- **Standaardwaarden** zet beeld, graphics en geluid terug; taal en updatekanaal blijven staan.
