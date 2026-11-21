---
title: Netwerk en glasvezel
kicker: Systemen
color: fiber
lead: Klanten betalen voor racks die online zijn. Haal glasvezel je terrein op, sluit carriers aan en bouw het pad van de straatput naar elke rij racks.
description: Netwerk en glasvezel in Datacenter Architect, carriers, de internet exchange, peering, core-routers en spine-switches.
---

## Het pad naar het internet

<ul class="chain fiber">
<li><b>Straatput</b>A, B of C in de stoep</li>
<li><b>Jouw glasvezelput</b>aan de rand van het kavel</li>
<li><b>ODF</b>patchframe in de MMR</li>
<li><b>Core-router</b>in de MMR</li>
<li><b>Spine-switch</b>in elke hal</li>
<li><b>Rack</b>online</li>
</ul>

Een rack is **online** als het een poort heeft op een spine-switch in zijn eigen hal, die spine een poort heeft op een core-router, en de core via de ODF's in de meet-me room een carrier, de internet exchange of een peer bereikt. Racks die draaien maar offline zijn, leveren geen huur op.

| Apparaat | Waar | Poorten | Capaciteit |
|---|---|---|---|
| Glasvezelput | Buiten, bij de weg | 4 subducts (8 en 12 na upgrades) | 144 vezels per kabel |
| ODF (patchframe) | Meet-me room | 48 | Elke carrier en cross-connect neemt een poort |
| Core-router | Meet-me room | 32 | 400 Gbit/s |
| Spine-switch | Datahal | 48 | 400 Gbit/s |

{% include shot.html src="network-overlay" caption="Netwerkweergave (N): oranje lijnen en bolletjes lopen van de straatputten via je glasvezelput en mantelbuizen naar de meet-me room, de core-routers, de spines en de rijen. Hoe drukker, hoe sneller de bolletjes; rood betekent vol of offline." %}

## Glasvezelputten en routes

In de stoep van de openbare weg liggen drie carrierputten: **A**, **B** en **C**. Je eigen glasvezelput sluit met een mantelbuis aan op de dichtstbijzijnde. Elke carrier, IX-poort en dark fiber neemt één **subduct** in je put; upgrade de put voor meer.

Twee putten die op **verschillende straatputten** aansluiten geven je **twee gescheiden routes**. Trekt een graafmachine één kabel kapot, dan houdt de andere je online.

## Carriers, de IX en peering

Onder **Infrastructuur › Netwerk** (<kbd>F4</kbd>) sluit je contracten. Een poort wordt in 20 dagen geleverd (een upgrade in 10).

| Carrier | Put | Prijs per Mbit | Karakter |
|---|---|---|---|
| Lowlands Transit | A | € 0,32 | Tier 1 |
| Kanaal Net | A | € 0,29 | Regionaal |
| NordFiber | B | € 0,26 | Budget |
| Eurolink | C | € 0,38 | Premium |
| Polder-IX | C | alleen de poort | Internet exchange |

- Je bestelt een **poort** (1G, 10G, 100G of 400G) met een **commit**: elke maand betaal je de poort en de commit. Vanaf 10G commit krijg je 15% korting, vanaf 100G 30%.
- **Burst**: verkeer boven de commit wordt afgerekend op het 95e percentiel, tegen 1,5 × de prijs (1,2 × met een NOC). De drukste 5% van de uren telt niet mee.
- De **internet exchange** kost alleen de poort en neemt ongeveer een kwart van je verkeer gratis over, tot de poort vol is.
- **Peering**: met AI-concurrenten aan vragen concurrerende datacenters of ze met je mogen peeren via dark fiber. Sommigen huren een rack in jouw MMR en betalen jou, grotere vragen een maandbedrag. Een peer neemt 7 tot 10% van je verkeer over.

Verkeer gaat eerst naar de IX en de peers, de rest over transit. Elk online rack doet gemiddeld 0,25 Gbit/s, rustig in de ochtend en het drukst rond 20:00. Past het verkeer niet, dan betalen klanten minder huur voor het deel dat ze missen.

{% include shot.html src="network-panel" caption="Infrastructuur › Netwerk: carriers, poorten en commits, de IX, peeringverzoeken en waarschuwingen voor enkele punten van falen." %}

## Netwerkredundantie

Het netwerkscherm toont per schakel N, N+1 of 2N en waarschuwt voor **enkele punten van falen**:

- maar **één carrier** – sluit een tweede (klanten vragen vaak om twee of meer carriers);
- maar **één route** – een tweede glasvezelput op een andere straatput;
- maar **één meet-me room** – een tweede MMR aan de andere kant van het pand;
- maar **één core-router** – zet er een tweede bij;
- een **hal met één spine** – zet een tweede spine in die hal.

## Cross-connects en de NOC

- **Cross-connects**: hoe meer netwerken in je MMR, hoe meer klanten een cross-connect van hun rack naar een carrier bestellen. Een engineer patcht die bij een ODF (er moet een poort vrij zijn). Elke cross-connect levert € 250 eenmalig en € 60 per maand op.
- **NOC**: een NOC-ruimte met een **videowall** bewaakt het netwerk. Er gaat meer verkeer via de IX en peering, burst wordt goedkoper en je krijgt een waarschuwing als transit in de piek boven 85% komt. Met het serviceplan 24/7 NOC wordt elke storing meteen gezien.

## Checklist netwerk

1. Glasvezelput bij de weg, een meet-me room met ODF's en een core-router.
2. Een spine-switch in elke hal (48 racks per spine, twee voor redundantie).
3. Twee carriers op twee routes, plus de IX.
4. Commit iets boven je gemiddelde verkeer; burst vangt de pieken op.
5. Upgrade poorten voordat de NOC je bij 85% waarschuwt.
