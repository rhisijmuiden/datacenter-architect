---
title: Tips en trucs
kicker: Hulp
lead: Dingen die bouwen makkelijker maken, je racks online houden en geld besparen. Verzameld uit de speeltests.
description: Tips en trucs voor Datacenter Architect.
---

## Bouwen

- **Begin klein, groei later.** Een pand van zo'n 20 × 14 m is ruim genoeg voor de eerste klanten. Je kunt altijd een pand, een kelder of een extra verdieping toevoegen.
- **Eerst plannen met <kbd>B</kbd>.** In de planmodus teken je gratis een heel nieuw pand met ruimtes, racks en apparatuur, zie je de totale kosten, en bouw je het in één keer als het geld er is.
- **Alles is terug te draaien** met <kbd>Ctrl</kbd>+<kbd>Z</kbd>; het geld komt terug.
- **Eén brede sleep vult een hal.** Het rackgereedschap maakt zelf paren rijen met koude en warme gangen. Houd de sleep vast en kijk naar het aantal, de kW en de kosten in de geest.
- **Laat gangen langs de muren vrij.** Elk rack heeft een vrije cel voor en achter nodig. Een rij tegen een muur blijft onbereikbaar.
- **Houd een gang van het laadperron naar elke hal**, met dubbele deuren. Racks reizen per kar.
- **Zware installaties in de kelder.** Transformatoren, schakelkasten, UPS en accu's ondergronds houden de begane grond vrij voor zalen.
- **Meer roldeuren = meer vrachtwagens tegelijk.** Elke roldeur krijgt een eigen laadvak.
- **Koelmachines op het dak** besparen grond. Zonnepanelen en terrassets kunnen ook op daken.
- **Wegen van 6 m of breder** hebben verkeer in twee richtingen. Een smalle oprit werkt, maar vrachtwagens wachten dan op elkaar.
- <kbd>T</kbd> verbergt de daken, <kbd>Page Up</kbd>/<kbd>Page Down</kbd> wisselen van verdieping, <kbd>Q</kbd>/<kbd>E</kbd> draaien de camera 90°.

## Racks online houden

- **Kijk naar de balken bovenin.** Stroom, noodstroom, koeling en netwerk tonen last tegen capaciteit. Loopt er een bijna vol, grijp dan in voordat hij vol is.
- **Gebruik de weergaven.** <kbd>P</kbd> stroom, <kbd>H</kbd> koeling, <kbd>N</kbd> netwerk. Rood laat zien waar het vastloopt.
- **Volg de adviezen op het Overzicht** (<kbd>F1</kbd>). Elk advies heeft een knop die het scherm opent dat het oplost.
- **Vraag de netverzwaring vroeg aan.** Die duurt 30 tot 120 dagen, plus de wachtrij met netcongestie aan. Vraag hem aan als je op zo'n 60% van je huidige aansluiting zit.
- **Vergeet nooit de schakelkast.** Zonder schakelkast geen ATS, dus de generatoren kunnen bij een storing niet overnemen.
- **Tel diesel in uren, niet in liters.** Het stroomscherm toont brandstofuren; mik op minstens een hele dag.
- **Van alles een reserve.** Eén UPS extra, één CRAH extra per hal, één generator extra, een tweede carrier, een tweede spine. Dat is het verschil tussen een melding en een storing.

## Redundantie in één minuut

| Je wilt | Doe dit |
|---|---|
| **N+1** op een schakel | Units voor de last, plus één. De game haalt de grootste unit weg en kijkt of de rest alles nog draagt |
| **2N** op een schakel | Totale capaciteit minstens twee keer de last, met minstens twee units |
| **A/B** in een hal | Twee of meer PDU's of een busbar, zodat de hal het uitvallen van zijn grootste voeding overleeft |
| Generatoren N+1 | Eén generator meer dan het terrein nodig heeft (IT × PUE + de rest) |
| Netwerkredundantie | Twee carriers, twee glasvezelputten op verschillende straatputten, twee core-routers, twee spines per hal |

De volledige uitleg met voorbeelden staat bij [Stroom en redundantie]({{ '/nl/power/' | relative_url }}).

## Geld

- **Teken klanten die passen.** Een aanvraag met alleen ! en ✓ kun je tekenen; een ✕ betekent dat je vanaf dag één een belofte breekt.
- **Let op de SLA.** Uitval kost een boete en tevredenheid. Een klant onder de 15 vertrekt meteen.
- **Containment betaalt zichzelf terug.** Warme/koude gangen brengen de PUE van 1,65 naar 1,50 voor € 120 per m²; de energierekening daalt meteen.
- **Inkoopmanager**: 5% korting op energie en 10% op carriers, meer met training.
- **IX en peering** nemen gratis of goedkoop een deel van je verkeer over; minder transit om te betalen.
- **Het serviceplan remote hands** levert extra op per verhuurd rack.
- **Verhuur lege kantoren** met een kantoormanager.
- **Vast of variabel energiecontract?** Vast is rustig; variabel volgt de markt en kan goedkoper zijn met batterijen die voor je handelen.

## Personeel

- **Neem geen individuen aan; stel beleid in.** Komt een afdeling tekort, zet de bezetting op ruim of neem een serviceplan.
- **Een kantine, een koffieautomaat en parkeerplekken** houden mensen tevreden en voorkomen dat ze vertrekken.
- **Schoonmakers** (één per twee zalen) laten apparatuur in de zalen 40% langer meegaan.
- **Een NOC-operator in dienst** betekent dat storingen meteen worden gezien in plaats van na twee uur.
- **Een brandveiligheidsmedewerker** voorkomt dat het gasblussysteem een hal vier uur platlegt.

## Gewoon genieten

- Druk op <kbd>F</kbd> en loop 's nachts door je eigen zalen.
- Druk op <kbd>F9</kbd>, zet de filmcamera aan en laat hem zweven.
- Zet dag/nacht en weer aan of uit met de knoppen met de zon en de wolk, wanneer je wilt.
- Bekijk de timelapse van je groeiende campus onder Voortgang › Timelapse.
