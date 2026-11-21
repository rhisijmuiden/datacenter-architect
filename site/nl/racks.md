---
title: Racks online krijgen
kicker: Bouwen
lead: Een rack verdient pas geld als het live is, draait en online is. Deze pagina legt de opbouwketen uit en geeft een checklist voor als racks donker blijven.
description: Zo krijg je racks online in Datacenter Architect, met een checklist voor problemen.
---

## Racks plaatsen

Open **Datazalen** in de bouwbalk en kies **Rack**:

- **Klik** om één rack te plaatsen. <kbd>R</kbd> draait het; een pijl op de geest toont de **voorkant**.
- **Sleep** een lijn voor een rij van maximaal 24 racks.
- **Sleep breed** voor een heel blok: de game maakt paren rijen met de voorkanten naar elkaar over een **koude gang van 1 m**, en een **warme gang van 2 m** tussen de paren. Met <kbd>R</kbd> begin je met de achterkanten.

Tijdens het slepen toont de geest het aantal racks, rijen, de afmeting, de kW en de kosten. Cyaan tegels zijn de koude gangen, oranje tegels de warme.

{% include shot.html src="rack-drag" caption="Slim slepen: één brede sleep vult de hal met paren rijen, koude gangen tussen de voorkanten en warme gangen tussen de paren." %}

> **Elk rack heeft een vrije cel voor en achter zich nodig, binnen de hal.** Engineers werken aan beide kanten. Daar mag niets staan, ook geen ander rack. Laat ook langs de muren een gang vrij.
{: .warn}

## Van bestelling tot live

Een geplaatst rack is een bestelling. Je kunt het volgen in de wereld:

<ul class="chain">
<li><b>Besteld</b>een doorzichtig plan in de hal</li>
<li><b>Geleverd</b>een vrachtwagen rijdt achteruit naar het laadperron</li>
<li><b>Geplaatst</b>monteurs rijden het naar zijn plek</li>
<li><b>Gevoed</b>een engineer sluit A/B-voeding aan (amber)</li>
<li><b>Live</b>daarna glasvezel (oranje); klaar voor verkoop</li>
</ul>

Met **direct bouwen** in de creatieve modus sla je deze keten over.

## Live is niet genoeg: draaien en online

Een live rack moet op elk moment nog drie controles halen:

1. **Stroom** – de hele stroomketen moet het kunnen dragen (zie [Stroom]({{ '/nl/power/' | relative_url }})). Elk rack telt als **6 kW**.
2. **Koeling** – de hal mag niet oververhit raken. Vanaf **32 °C** gaan racks uit tot de hal weer gekoeld kan worden (zie [Koeling]({{ '/nl/cooling/' | relative_url }})).
3. **Netwerk** – het rack heeft een poort nodig op een spine-switch in zijn hal, die spine een poort op een core-router, en de core een carrier, de IX of een peer via een ODF in de meet-me room (zie [Netwerk]({{ '/nl/network/' | relative_url }})).

Is er te weinig stroom of koeling, dan gaan **de nieuwste racks het eerst uit**. Ze gaan vanzelf weer aan als het probleem is opgelost. Racks die niet draaien of offline zijn leveren geen huur op, en klanten op die racks krijgen een SLA-boete en worden minder tevreden.

## Checklist bij problemen

Klik op het rack of de hal: het infopaneel vertelt bij welke stap het vastzit. Werk daarna deze lijst af.

| Wat je ziet | Waarschijnlijke oorzaak | Oplossing |
|---|---|---|
| Rack blijft een doorzichtig plan | Geen vrachtwagen bereikt het laadperron | Een laadperron met een **roldeur** in een buitenmuur, asfalt tot aan die deur, een oprit vanaf de openbare weg |
| Vrachtwagen komt, maar het rack wordt niet geplaatst | Monteurs komen niet van het laadperron bij de hal | Een gang van het laadperron naar de hal en een **dubbele deur** in de hal; een goederenlift als de hal op een andere verdieping ligt |
| "Een engineer kan een rack niet bereiken" | Geen gang | Laat een vrije cel voor en achter elk rack |
| Rack staat er maar is niet gevoed | Geen engineer, of de engineer is bezig | Kijk onder Personeel › Team; de startploeg heeft één engineer, neem er meer aan met HR en een operationeel manager |
| Rack is live maar donker, "uit door stroom" | Verdeling in de hal te klein | Zet er een **PDU** bij (150 kW = 25 racks) of een **busbar** (2 × 400 kW) in die hal |
| Veel racks donker op het hele terrein | Een schakel van de keten zit vol | Infrastructuur › Stroom toont de **zwakste schakel**: net, transformatoren, schakelkasten of UPS |
| Racks gaan uit bij een stroomstoring in de stad | Geen noodstroom | Generatoren hebben een **schakelkast** (de ATS) nodig, genoeg vermogen en diesel |
| Racks donker, hal rood in de koelweergave | Te warm | Meer **CRAH-units** in de hal, meer **koelmachines/koeltorens** buiten, of containment |
| Rack draait maar is "offline" | Geen netwerkpad | Spine-switch in de hal, core-router + ODF in de MMR, een carriercontract met een geleverde poort |
| Rack is online maar niemand huurt het | Verkeerde zone of geen aanvragen | Controleer de zone en klanttypes van de hal; neem een salesdirecteur en verkopers aan |

{% include shot.html src="power-overlay" caption="Stroomweergave (P): amber lijnen en bolletjes lopen van het net door de keten naar de rijen racks. Rood betekent overbelast of uitgevallen." %}

> **Vuistregel per 20 racks** (120 kW): één PDU dekt 25 racks, één CRAH-unit koelt 20 racks (meer met upgrades en containment), één UPS draagt zo'n 80 racks, één spine-switch heeft 48 poorten. Verdubbel wat redundant moet zijn.
{: .tip}

## Racks verkopen

Een rack dat live is, draait en online is, is **te koop**. Je salesteam zoekt klanten die bij de zone van de hal passen:

- **Volle racks** – producten van 16 A (3,7 kW) of 32 A (7,4 kW).
- **Colocatie per U** – 42 U per rack, verkocht aan veel kleine klanten.
- **Kooien** – een groep racks in één hal voor één klant.
- **Eigen hal** – een hele hal voor één klant.
- **Eigen cloud** – racks voor je eigen hostingbedrijf.

Meer bij [Bedrijf en klanten]({{ '/nl/company/' | relative_url }}).
