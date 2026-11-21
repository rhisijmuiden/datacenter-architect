---
title: Ruimtes
kicker: Bouwen
lead: In een afgebouwd pand teken je je eigen ruimtes. Elk ruimtetype heeft een taak, en de meeste hebben eerst bepaalde apparatuur nodig voordat ze die taak doen.
description: Elke ruimte in Datacenter Architect, wat hij doet, wat erin moet en wat hij nodig heeft.
---

## Zo werken ruimtes

1. Open **Ruimtes** in de bouwbalk, kies een type en sleep het in een afgebouwd pand, op de verdieping die je bekijkt. Bouwvakkers zetten de binnenmuren.
2. Zet een **deur** in een muur (Gebouwen › Deuren). Mensen en racks komen alleen binnen via een deur; racks en grote apparatuur hebben een **dubbele deur** of een **roldeur** nodig.
3. Richt de ruimte in (Inrichting, Stroom, Koeling, Netwerk). Apparatuur die bij een bepaalde ruimte hoort, kan alleen daar staan: een UPS alleen in een UPS-ruimte, een core-router alleen in de meet-me room.
4. Klik op de ruimte voor het **infopaneel**: wat de ruimte doet, wat erin staat en wat er nog mist.

Ruimtes zijn verbonden via **gangen**. Houd een gang van het laadperron naar elke hal en technische ruimte, zodat monteurs er apparatuur naartoe kunnen rijden.

## Ruimtes in het datacenter

### Datahal
**Wat hij doet:** hier staan de racks die je verhuurt. Hier wordt het geld verdiend.<br>
**Zet erin:** racks in rijen, **CRAH-units** voor koeling, **PDU's** (of een busbar-upgrade) voor stroom, een **spine-switch** voor het netwerk. Optioneel: crashkarren, cameradomes.<br>
**Nodig:** een dubbele deur, een vrije gang voor en achter elk rack, stroom uit de keten, genoeg koeling en een netwerkpad.<br>
**Keuzes in het infopaneel:** zone (volle racks, colocatie per U, kooien, eigen hal of eigen cloud), welke klanttypes de hal neemt, containment-upgrade (warme/koude gangen, cold corridor), busbar.

{% include shot.html src="hall" caption="Een datahal met rijen racks onder oranje fibergoten, CRAH-units langs de muur en het laadperron ernaast. Het infopaneel rechts toont een rack dat live is, draait, online is en te koop staat." %}

### Meet-me room (MMR)
**Wat hij doet:** het netwerkhart van het pand. Carriers, de internet exchange en peers komen hier binnen; klanten bestellen cross-connects naar hen.<br>
**Zet erin:** **ODF's** (patchframes, 48 poorten elk) en **core-routers** (32 poorten, 400 Gbit/s).<br>
**Nodig:** een glasvezelput buiten. Tip: een badgedeur, want certificaten en klanten die op beveiliging letten kijken daarnaar.

### Transformatorruimte
**Wat hij doet:** zet middenspanning van het net om. Nodig zodra je de netaansluiting zwaarder maakt dan het compacte station van 2 MVA.<br>
**Zet erin:** **transformatoren** (2,5 MVA per stuk).

### Schakelruimte
**Wat hij doet:** de hoofdverdeling. De schakelkast heeft de **ATS** die naar de generatoren schakelt als het net uitvalt. Geen schakelkast = generatoren kunnen het niet overnemen.<br>
**Zet erin:** **hoofdschakelkasten** (4 MW per stuk).

### UPS-ruimte
**Wat hij doet:** overbrugt een stroomstoring tot de generatoren draaien.<br>
**Zet erin:** **UPS'en** (500 kW per stuk) en eventueel **accukasten**.

### Accuruimte
**Wat hij doet:** ruimte voor de UPS-accu's.<br>
**Zet erin:** **accukasten** (40 kWh per stuk). Accu's kunnen ook in de UPS-ruimte staan.

### Laadperron
**Wat hij doet:** vrachtwagens leveren hier racks, apparatuur en servers van klanten af.<br>
**Nodig:** een buitenmuur met een **roldeur** aan het asfalt. Elke roldeur krijgt een eigen genummerd laadvak; meer deuren betekent meer vrachtwagens tegelijk. Kan niet in een kelder.

### Opslag
**Wat hij doet:** voorraad reserveonderdelen, servers en pallets.<br>
**Zet erin:** palletstellingen, werkbanken, palletwagens, een schijvenvernietiger.

### Gang
**Wat hij doet:** verbindt ruimtes. Mensen, karren en racks gaan door gangen. Cameradomes kunnen hier hangen.

## Ruimtes in het kantoor

| Ruimte | Wat hij doet | Zet erin | Nodig wanneer |
|---|---|---|---|
| Directiekamer | Werkplek voor één directeur of manager | Bureau en stoel (een plant staat leuk) | Eén per directeur, vanaf de CEO |
| Kantoor | Werkplekken voor sales, support en helpdesk | Een bureau per persoon | Als je verkopers en supportmensen aanneemt |
| Vergaderruimte | Overleg | Vergadertafel, stoelen | Vanaf 6 mensen in het bedrijf |
| Personeelskantine | Tevredenheid van personeel | Koffieautomaat, frisdrankautomaat, magnetron, tafels | Vanaf 10 mensen; een kantinemedewerker maakt hem beter |
| Klantruimte | Klanten op bezoek wachten en werken hier | Banken, terminals, frisdrankautomaat, koffie | Vanaf 25 mensen; klanten waarderen het |
| Receptie | Ontvangt klanten en bezoekers | Glazen loge of balie | Altijd één; hier werkt de receptionist |
| Beveiligingsruimte | Beveiligers bewaken het terrein | Videowall beveiliging, glazen loge | Voor beveiligers (of een portiersloge bij de poort) en een hoger beveiligingsniveau |
| NOC | Network operations centre: bewaakt stroom, koeling en netwerk 24/7 | NOC-videowall, bureaus | Voor NOC-operators; meer verkeer via de IX en peering, goedkopere burst |
| SOC | Security operations centre tegen cyberaanvallen | SOC-bureaus | Voor SOC-analisten en de 24/7 SOC |
| R&D-lab | Research | Labtafels, één onderzoeker per tafel | Voor de researchboom |
| Magazijn | Servers voor je eigen hostingbedrijf, reserveonderdelen | Palletstellingen (24 servers elk), werkbanken, palletwagens, vernietiger | In een warehouse |

{% include shot.html src="office" caption="Kantoorruimtes richten zichzelf in een stijl in. Het infopaneel toont wat het kantoor nog nodig heeft, bijvoorbeeld een vergaderruimte zodra je zes mensen hebt." %}

> **Welke ruimtes heb ik eerst nodig?** Datahal, laadperron, schakelruimte, UPS-ruimte, meet-me room en een gang in het datacenter, plus een directiekamer en een kantoor voor sales. De rest komt als de game erom vraagt: het Overzicht geeft een advies als er een ruimte ontbreekt.
{: .tip}

## Deuren

| Deur | Breedte | Gebruik |
|---|---|---|
| Enkele deur | 1 | Kantoren, personeel |
| Dubbele deur | 2 | Zalen; racks passen erdoor |
| Branddeur | 1 | Tussen hal en UPS- of accuruimte |
| Badgedeur | 1 | Zalen, MMR, klantruimtes; telt mee voor het beveiligingsniveau |
| Roldeur | 3 | Laadperron en warehouse; racks en vrachtwagens |
| Sluisdeur (mantrap) | 1 | Ingang naar het beveiligde deel, na research |

Deuren zijn dicht en gaan vanzelf open als er iemand aan komt lopen.
