---
title: Koeling
kicker: Systemen
color: cooling
lead: Elke kilowatt IT wordt warmte. CRAH-units halen die uit de hal, koelmachines en koeltorens voeren hem buiten af, en containment maakt het geheel goedkoper.
description: Zo werkt koeling in Datacenter Architect, CRAH-units, koelmachines, koeltorens, containment en PUE.
---

## Twee helften

<ul class="chain cooling">
<li><b>Racks</b>6 kW warmte per stuk</li>
<li><b>CRAH-units</b>in de hal, 120 kW</li>
<li><b>Warmteafvoer</b>buiten of op het dak</li>
</ul>

Koeling heeft twee helften en **beide** moeten groot genoeg zijn:

1. **In de hal**: CRAH-units (computer room air handlers) blazen koude lucht in de koude gangen en nemen de warme lucht terug. Elk koelt **120 kW**, of 162 en 204 kW na upgrades. Units in een hal koelen alleen die hal.
2. **Buiten of op het dak**: de warmte verlaat het pand via koelmachines, koeltorens of dry coolers. Hun capaciteit wordt gedeeld door alle zalen, naar verhouding van de warmte per hal.

Wat een hal echt kan koelen is de **kleinste** van de twee. Tien CRAH-units doen niets zonder koelmachine buiten, en een enorme koeltoren doet niets voor een hal zonder CRAH-units.

| Apparaat | Waar | Capaciteit | Let op |
|---|---|---|---|
| CRAH-unit | In de hal | 120 kW (162 / 204 na upgrade) | 1 × 2 m, gebruikt zelf 4 kW |
| Koelmachine | Buiten of dak | 600 kW (te upgraden) | 3 × 2 m |
| Koeltoren | Buiten of dak | 1.000 kW (te upgraden) | 2 × 2 m, gebruikt water |
| Dry cooler | Buiten of dak | 450 kW (te upgraden) | Koelt zonder water: beter voor je ESG-score |

Klik op een koelunit en druk in het infopaneel op **Upgrade** om naar niveau 2 (×1,35) of 3 (×1,7) te gaan.

{% include shot.html src="cooling-overlay" caption="Koelweergave (H): koude lucht stroomt naar de rijen, warm water gaat naar de koelmachines op het dak. Racks kleuren naar de temperatuur van hun hal." %}

## Temperatuur in de hal

Elke hal heeft een temperatuur. Een goed gekoelde hal zit rond de 20 tot 25 °C. Met te weinig koeling warmt hij op, en vanaf **32 °C gaan racks uit** (de nieuwste eerst) tot de rest weer gekoeld kan worden. Is de koeling weer op orde, dan gaan ze vanzelf weer aan.

Een brandalarm zonder brandveiligheidsmedewerker in dienst laat het **gasblussysteem** afgaan: de hal ligt vier uur plat en navullen kost € 35.000.

## Containment en PUE

**PUE** (power usage effectiveness) is het totale stroomverbruik van het terrein gedeeld door het IT-verbruik. 1,0 zou perfect zijn; alles daarboven is koeling en verlies waarvoor je betaalt. Je verbetert het door de luchtbeheersing van de hal te upgraden in het infopaneel van de hal:

| Hal | CRAH-bonus | PUE | Kosten | Duur |
|---|---|---|---|---|
| Open zaal | – | 1,65 | – | – |
| Warme en koude gangen | +12% | 1,50 | € 120 per m² | 1 dag |
| Cold corridor (na research) | +35% | 1,30 | € 380 per m² | 3 dagen |

Een lagere PUE betekent een lagere energierekening, minder last aan de netkant van de stroomketen, een betere ESG-score en de prestaties voor een zuinig datacenter. Research (EC-fans, free cooling, vloeistofkoeling) verlaagt hem nog verder.

> **32 A-racks** hebben warme en koude gangen in hun hal nodig. Slim slepen zet de racks al met de voorkanten naar elkaar, dus upgraden is één klik in het infopaneel van de hal.
{: .cooling}

## Weer

Met **weer speelt mee** aan bij het begin van een spel:

- Op koude dagen krijg je **free cooling**: de PUE daalt.
- Een **hittegolf** in de zomer haalt 20% van je warmteafvoer af, en koelapparatuur slijt 50% sneller. Houd voor de zomer wat reservecapaciteit.

## Checklist koeling

1. Minstens één CRAH-unit per 20 racks (120 kW), plus één reserve per hal.
2. Koelmachines, koeltorens of dry coolers buiten met meer capaciteit dan alle zalen samen.
3. Warme/koude gangen zodra het kan; een cold corridor na research.
4. Een koeltechnicus per 20 units houdt ze heel: de managers nemen ze zelf aan.
5. Druk op <kbd>H</kbd> om te zien welke hal warm loopt.
