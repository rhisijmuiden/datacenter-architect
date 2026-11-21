---
title: Stroom en redundantie
kicker: Systemen
color: power
lead: Elk rack heeft de hele stroomketen nodig, van de netbeheerder tot de PDU in de hal. De zwakste schakel bepaalt hoeveel racks draaien, en redundantie bepaalt wat er gebeurt als er iets uitvalt.
description: De stroomketen in Datacenter Architect, en hoe je N+1 en 2N haalt.
---

## De keten

<ul class="chain">
<li><b>Netaansluiting</b>2 tot 50 MVA</li>
<li><b>Transformatoren</b>2,5 MVA per stuk</li>
<li><b>Schakelkast + ATS</b>4 MW per stuk</li>
<li><b>UPS + accu's</b>500 kW / 40 kWh</li>
<li><b>PDU of busbar</b>150 kW / 2 × 400 kW</li>
<li><b>Rack</b>6 kW</li>
</ul>

- De **netkant** (net, transformatoren, schakelkasten) draagt de IT-last **maal de PUE**, want koeling en verliezen hebben ook stroom nodig. De **UPS en de zalen** dragen alleen de IT-last.
- De schakel met de minste ruimte is de **bottleneck**. Infrastructuur › Stroom (<kbd>F4</kbd>) toont de keten, de last per schakel en welke schakel de zwakste is.
- Racks die de keten niet kan dragen gaan uit, de nieuwste eerst. Ze komen vanzelf terug.

{% include shot.html src="power-panel" nl=true caption="Infrastructuur › Stroom en koeling: de keten met last en redundantie per schakel, noodstroom met brandstofuren, UPS-autonomie, koeling, PUE en elke hal." %}

## Netaansluiting

Elk terrein start met een **compact station van 2 MVA** met een eigen transformator. Een zwaardere aansluiting vraag je aan bij de netbeheerder:

| Niveau | Kosten | Levertijd | Ontgrendeld door |
|---|---|---|---|
| 2 MVA | inbegrepen | – | – |
| 10 MVA | € 1,2 miljoen | 30 dagen | Facility manager |
| 20 MVA | € 2,8 miljoen | 60 dagen | Facility manager, eerste training |
| 50 MVA | € 7,5 miljoen | 120 dagen | Facility manager, tweede training |

Vanaf 10 MVA levert het net middenspanning: bouw een **transformatorruimte** met transformatoren (2,5 MVA per stuk) om het ook echt te gebruiken. Met **netcongestie** aan (schakelaar bij een nieuw spel) wacht een zwaardere aansluiting eerst in een wachtrij, ongeveer een maand per plek. Een batterijcontainer op je terrein helpt het net en schuift je één plek op.

## Redundantie: N, N+1 en 2N

Voor elke schakel toont het stroomscherm hoe redundant hij is. De game gebruikt de echte datacentertermen:

| Label | Betekenis | Regel in de game |
|---|---|---|
| **N−** | Te weinig | Alle units samen kunnen de last niet dragen |
| **N** | Precies genoeg | De units dragen de last, maar valt er één uit, dan gaan er racks uit |
| **N+1** | Eén reserve | Haal de **grootste** unit weg en de rest draagt de last nog steeds |
| **2N** | Alles dubbel | Minstens twee units, samen minstens **twee keer** de last |

<ul class="chain">
<li><b>N</b>■■■ last 300 kW op 1 × UPS van 500 kW</li>
<li><b>N+1</b>■■■ + □ 3 UPS'en voor 900 kW last</li>
<li><b>2N</b>■■■ ■■■ 2 × 500 kW voor 240 kW</li>
</ul>

### Zo haal je N+1

Zet **één unit meer neer dan je nodig hebt** op die schakel. Een IT-last van 900 kW heeft twee UPS'en van 500 kW nodig (N); met een derde is het N+1. Hetzelfde geldt voor schakelkasten en transformatoren. De regel haalt altijd de grootste unit weg, dus maten mengen helpt niet.

### Zo haal je 2N

Zorg dat de units samen minstens **het dubbele van de last** aankunnen, met minstens twee stuks. Twee UPS'en van 500 kW geven 2N tot 500 kW IT, dat zijn ongeveer 83 racks. Groei je daarboven, dan zakt het label naar N+1 of N: zet UPS'en bij als de zalen vollopen.

> **Klanten vragen hierom.** Aanvragen en aanbestedingen kunnen een **2N UPS**, **A/B-voeding in de hal** en **generatoren N+1** eisen. De aanvraag toont per eis ✓, ! of ✕; je kunt tekenen zodra er geen ✕ meer staat. 2N-racks leveren 10% meer op.
{: .power}

### A/B-voeding in de hal

Een hal heeft **A/B-voeding** als hij al zijn racks laat draaien nadat zijn grootste voeding uitvalt. Een voeding is een PDU (150 kW) of één rail van de busbar (400 kW; een busbar heeft altijd twee rails, A en B).

| Last in de hal | Geen A/B | Wel A/B |
|---|---|---|
| 20 racks (120 kW) | 1 PDU | 2 PDU's |
| 40 racks (240 kW) | 2 PDU's (300 kW, maar valt er één uit = 150 kW) | 3 PDU's, of een busbar |
| 120 racks (720 kW) | – | busbar + 3 PDU's, of 6 PDU's |

**32 A-racks** (producten van 7,4 kW) hebben A/B-voeding **en** warme/koude gangen in de hal nodig.

## Noodstroom

### Generatoren

Generatoren (1 MW per stuk, dagtank van 2.000 liter) starten als het net in de stad uitvalt, **maar alleen achter een schakelkast met ATS**. Zonder schakelkast kunnen ze het niet overnemen.

- **Redundantie van de generatoren** vergelijkt het generatorvermogen met de last van het hele terrein (IT × PUE + de rest). Twee generatoren van 1 MW tonen **2N** zolang het terrein hooguit 1 MW nodig heeft (één generator draagt alles, de andere is een volledige reserve). Boven 1 MW zet je er een derde bij: drie generatoren geven 2N tot 1,5 MW en N+1 tot 2 MW. Vuistregel: zoveel generatoren als de last nodig heeft, plus één.
- **Brandstof**: 0,27 liter per kWh. Extra **dieseltanks** (20.000 L bovengronds) of **ondergrondse tanks** (50.000 L, je kunt eroverheen rijden) geven meer uren. Het stroomscherm toont de brandstofuren; een monteur generatoren bestelt tijdens een storing extra diesel.
- Tijdens een stroomstoring in de stad zie je de generatoren draaien en blijven de lichten op het terrein aan. Is de diesel op, dan gaat het terrein op zwart.

### UPS-autonomie

De UPS overbrugt de seconden tot de generatoren draaien. De autonomie in minuten is **accu-kWh ÷ draaiende IT-kW × 60**. Vier accukasten (160 kWh) op 240 kW racks geven 40 minuten. UPS'en en accu's gaan vijf jaar mee; daarna moeten ze voor de halve prijs vervangen worden.

## Energie

- **Leveranciers** (verzonnen): Polder Energie (vast), Delta Stroom (variabel, volgt de markt), Noordwind Groen (groen, vast) en Kustlicht (groen, variabel). De stroomrekening is IT-last × PUE × prijs.
- **Eigen energie**: zonnevelden op daken of grond, windturbines (alleen op het terrein) en **batterijcontainers**. Ze worden per uur berekend: zon volgt de zon en het weer, wind het seizoen en het weer.
- **Eigen stroom gaat voor.** Elk uur voedt eigen stroom eerst je site, laadt daarna de batterij en wordt dan verkocht aan het net tegen 60% van de marktprijs (tot je netaansluiting). Wat dan nog ontbreekt komt van het net.
- **Batterijbeleid** (Infrastructuur › Eigen energie): *eigen stroom opslaan* (standaard), *handelen op de markt* (goedkoop laden van het net, duur verkopen) of *slim combineren* (eerst eigen overschot, de rest handelt). Een reserve (standaard 20%) blijft geladen voor stroomuitval. Verkopen kan uit.
- **Scherm Eigen energie**: het deel van je verbruik uit eigen zon, wind en batterij (per dag en over 30 dagen), opwek, net, verkocht, batterijlading, een grafiek per uur en waar je stroom heen ging. De bovenbalk toont het 30-dagenpercentage.
- **Groen datacenter**: 30 dagen op 50% eigen energie of meer geeft een **nominatie**; daarna levert de audit (Reputatie › Certificaten, ook PUE 1,45 of lager) het groene keurmerk op. **Groen Goud** vraagt 80% en PUE 1,30. Onder de drempel heb je 60 dagen respijt, daarna wordt het keurmerk geschorst tot je er weer boven zit. Een groen stroomcontract geeft ESG-punten maar telt niet mee voor het keurmerk.
- Groene contracten en eigen energie verhogen je **ESG-score**, waar investeerders en de beurskoers naar kijken.

## Checklist stroom

1. Schakelkast geplaatst (anders geen ATS en geen noodstroom).
2. UPS met accu's, genoeg voor de IT-last, één extra voor N+1, dubbel voor 2N.
3. Een PDU of busbar in elke hal, twee voedingen of meer voor A/B.
4. Twee generatoren (meer naarmate je groeit) met diesel voor vele uren.
5. Vraag een zwaardere netaansluiting **op tijd** aan: dat duurt 30 tot 120 dagen, plus de wachtrij.
6. Druk af en toe op <kbd>P</kbd>: rode lijnen laten zien waar de keten overbelast is.
