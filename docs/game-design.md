# Game design document: Datacenter Architect

*Versie 0.2, 19 november 2026. Een levend document dat met de game meegroeit.*

## In één zin

Een offline bouw- en managementsimulatie voor Windows 11 waarin je grond koopt en een compleet datacenter ontwerpt en runt: van parkeerplaats en beveiligingsloge tot stroomvoorziening, koeling en de racks van je klanten.

## Kernlus

1. **Kies een locatie** en koop grond.
2. **Ontwerp het terrein**: gebouwen, wegen, parkeerplaats, hekwerk, generatoren, koelmachines.
3. **Richt het datacenter in**: zalen, racks, kabelgoten, stroomverdeling, koeling.
4. **Sluit klanten aan** die racks huren en betalen per maand.
5. **Groei en houd het in balans**: meer racks vragen meer stroom en koeling. Kom je tekort, dan krijg je waarschuwingen en uiteindelijk storingen.
6. Met de winst koop je grond bij of bouw je verder uit.

## Locaties

Je bouwt op een **kaal terrein** of op een **kavel in de stad**. Elke locatie heeft eigen eigenschappen:

| Eigenschap | Kaal terrein | Stadskavel |
|---|---|---|
| Ruimte | Veel, en uitbreidbaar door grond bij te kopen | Weinig, je bouwt eerder de hoogte in |
| Grondprijs | Laag | Hoog |
| Glasvezel in de buurt | Weinig providers, verder weg | Veel providers, dichtbij |
| Netcapaciteit | Wisselend, soms een nieuwe aansluiting nodig | Vaak krap, de netbeheerder bepaalt het tempo |

Elke locatie toont vooraf de **beschikbare netcapaciteit (MVA)** en de **glasvezelputten** in de buurt, met provider en afstand. Aangrenzende kavels kun je later bijkopen.

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

## Koeling

- **Koelmachines** buiten en **CRAH-units** in de zalen, elk met een capaciteit in kW.
- Elke rack produceert warmte, afhankelijk van de klant. De koelbalk toont de belasting tegenover de capaciteit.
- **Warme en koude gangen** en **gangafscherming** verhogen het rendement.
- De **warmteweergave** kleurt racks van groen naar rood, zodat je ziet waar het te warm wordt.
- Regel: plaats je meer racks, dan moet je eerst de koelcapaciteit verhogen.

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

## Ruimtes

Binnen een gebouw **teken je zelf ruimtes**: muren trekken, deuren plaatsen en een ruimtetype kiezen. Elk type heeft eisen (minimale grootte, verplichte objecten, toegangsniveau) en werkt pas als daaraan is voldaan.

| Ruimte | Functie | Verplichte objecten |
|---|---|---|
| **Datazaal** | Racks van klanten | Racks, CRAH-units, kabelgoten, PDU's |
| **Kooi / private suite** | Afgeschermd deel van een zaal voor één klant | Hekwerk, toegangslezer |
| **Meet-me room (MMR)** | Hier komen alle netwerken binnen | ODF's, patchpanelen, provider-racks |
| **UPS-ruimte** | Overbrugging bij stroomuitval | UPS-systemen |
| **Accuruimte** | Accubanken voor de UPS | Accukasten, extra koeling, brandblusinstallatie |
| **Switchgear-ruimte** | Hoofdverdeling en ATS | Schakelkasten, ATS |
| **NOC** | Bewaking van het hele terrein | Werkplekken, schermwand |
| **Kantoor** | Werkplekken voor personeel | Bureaus |
| **Kantine en toiletten** | Tevredenheid van personeel | Keuken, tafels, toiletten |
| **Opslag** | Voorraad racks, servers en reserveonderdelen | Stellingen |
| **Laadperron** | Leveringen ontvangen | Laaddeuren |
| **Build room / staging** | Servers voorbereiden voordat ze de zaal in gaan | Werkbanken |
| **Beveiligingsloge** | Check-in, badges uitgeven | Balie, toegangspoortjes |
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

## Personeel

Personeel is een eigen module. Je **werft medewerkers** en ziet ze als poppetjes over het terrein lopen.

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
- Concurrerende datacenters zijn **AI-spelers** in de regio die ook klanten en aanbestedingen proberen te winnen.

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

## Camera en bediening

- Vrij draaibare camera (slepen of Q/E), zoomen met het scrollwiel, schuiven met rechts slepen.
- Daken aan/uit (R).
- Overlay-weergaven voor stroom, warmte en glasvezel.
- Op een gebouw klikken opent een infopaneel met de gegevens.

## Techniek

- **Engine:** Godot 4 met C#.
- **Platform:** Windows 11, als installeerbare game (installer).
- **Offline:** het spel werkt volledig offline. Savegames staan lokaal, met meerdere opslagplekken en autosave.
- **Talen:** standaard Engels, volledig vertaald naar het Nederlands.
- **Info-scherm:** versie, controleren op updates en bijwerken via GitHub Releases. Je kunt kiezen tussen het stabiele kanaal (`main`) en het testkanaal (`test`). Alleen deze functie gebruikt internet.

## Bewust niet in scope

- Geen mailserver en geen backups naar externe servers (FTP/SCP). Het is een offline game.
- Geen multiplayer.
- Geen klimaat- of biome-regels. Locaties kunnen overal liggen.

## Eerste mijlpalen

1. **Fundament en huisstijl** (dit document, de huisstijl, de repo-structuur)
2. **Technische basis**: Godot-project, camera, bouwgrid, installer, talen, lokale savegames, Info-scherm
3. **Terreinprototype**: grond kopen, gebouwen plaatsen, netstroom en glasvezel, generatoren met N+1-check
4. **Datacenter van binnen**: zalen, racks, kabelgoten, interne stroom- en netwerkinfrastructuur, koeling, klanten en economie
5. **Personeelsmodule**: werven, functies, roosters, taken en tevredenheid

## Open vragen

- Hoe heet het spel definitief? *Datacenter Architect* is de werktitel.
- Is er een campagne met scenario's, of alleen vrij spelen?
