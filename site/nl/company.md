---
title: Bedrijf en klanten
kicker: Runnen
lead: Je begint als eigenaar. Neem een CEO en directeuren aan, dan runnen zij het bedrijf voor je. Sales brengt klanten binnen, jij bepaalt wat je waar verkoopt.
description: De organisatie, klanten, contracten, aanbestedingen en geld in Datacenter Architect.
---

## De organisatie

Alles begint met een **directiekamer** met een bureau en een stoel. Dan kun je onder **Bedrijf › Organisatie** (<kbd>F2</kbd>) een **CEO** aannemen. Elke directeur en manager ontgrendelt iets en heeft een eigen ingerichte kamer nodig.

| Functie | Eerst nodig | Ontgrendelt |
|---|---|---|
| CEO | – | Al het andere |
| CFO | CEO | Leningen, investeerders, naar de beurs |
| HR-manager | CEO | Werven buiten de startploeg (4 bouwvakkers, 2 monteurs, 1 engineer) |
| Salesdirecteur | CEO | Klanten. Verkopers (elk een bureau) brengen meer en grotere aanvragen |
| Bedrijfsjurist | Sales | Bieden op aanbestedingen |
| Customer success | Sales | Tevredener klanten, meer verlengingen |
| Marketing | Sales | Meer aanvragen, goede recensies tellen zwaarder |
| Inkoop | CFO | Goedkopere energie en carriers |
| Operationeel manager | CEO + HR | Snellere ploegen; ontgrendelt de managers hieronder |
| Facility manager | Operations | Netverzwaring naar 10, 20 en 50 MVA; personeel voor stroom en koeling |
| Netwerkmanager | Operations | Carriers, IX, peering; netwerkpersoneel |
| Security manager | Operations | Beveiligers, beveiligingsniveau, veeleisende klanten |
| Projectmanager bouw | Operations | Bouwvakkers en monteurs werken sneller |
| Kantoormanager | Operations | Lege kantoren verhuren |
| Duurzaamheid | Operations | ESG |
| Compliance officer | CEO | Certificaten |
| R&D-manager | CEO | Research |
| Hostingdirecteur | CEO + Sales | Je eigen hostingbedrijf |
| Directeur internationaal | CEO + CFO | Locaties in andere landen |

Elke functie heeft een aannamebedrag, een maandsalaris en twee trainingen die de functie beter maken. Met **alle unlocks** in de creatieve modus staat de hele boom open.

{% include shot.html src="company" caption="Bedrijf › Organisatie: de boom met directeuren en managers, met aannemen en trainen." %}

## Klanten

Je **salesdirecteur** brengt aanvragen binnen (maximaal acht tegelijk, elk 12 tot 22 dagen geldig). Open **Klanten › Sales** (<kbd>F3</kbd>) om ze te zien. Elke aanvraag toont zijn eisen met ✓ (voldaan), ! (waarschuwing) of ✕ (niet voldaan). Je kunt tekenen zodra er geen ✕ meer staat.

{% include shot.html src="customers" caption="Klanten › Sales: binnenkomende aanvragen met hun eisen, omvang, producten en waarde." %}

### Klanttypes

Startups, MKB, hostingbedrijven, websitehosting, videostreaming, social media, cryptomining, clouddiensten, gemeentes, ministeries, en via aanbestedingen banken, hyperscalers en AI-bedrijven. Elk type heeft een eigen omvang, producten, verkeer per rack, aantal carriers, en een kans om 32 A, 2N, groene stroom, beveiliging of certificaten te vragen. Grotere en veeleisendere klanten komen bij een hogere **reputatie**.

### Wat je verkoopt: zones per hal

Per datahal kies je wat je verkoopt (in het infopaneel van de hal of Bedrijf › Sales):

| Zone | Product | Prijs per maand |
|---|---|---|
| Volle racks | Rack van 16 A (3,7 kW) of 32 A (7,4 kW) | € 780 / € 1.250 |
| Colocatie per U | Units van een rack, 42 U per rack | € 42 per U |
| Kooien | Meerdere racks voor één klant, binnen één hal | +15% |
| Eigen hal | Een hele hal voor één klant | +10% |
| Eigen cloud | Racks voor je eigen hostingbedrijf | – |

2N-racks leveren 10% meer op, en elk klanttype heeft een eigen prijsfactor. Je kiest ook **welke klanttypes een hal neemt**, bijvoorbeeld één hal alleen voor banken en overheid en een andere voor AI-bedrijven. Aanpassen zet nooit iemand op straat.

### Veelvoorkomende eisen

| Eis | Zo voldoe je eraan |
|---|---|
| Vrije racks, units, kooi of hal | Genoeg live, draaiende, online racks in een hal met de juiste zone en klanttypes |
| A/B-voeding | Twee of meer PDU's, of een busbar, met ruimte om de grootste te laten uitvallen. Zie [Stroom]({{ '/nl/power/' | relative_url }}) |
| 32 A | A/B-voeding en warme/koude gangen |
| 2N UPS | UPS-capaciteit minstens twee keer de IT-last |
| Generatoren N+1 | Eén generator meer dan nodig (een waarschuwing, geen blokkade) |
| Bandbreedte en carriers | Onverkochte transitcapaciteit en genoeg carriers |
| Groene energie | Een groen energiecontract |
| Beveiliging | Security manager en een minimaal beveiligingsniveau |
| Certificaten | ISO 27001, Tier III, PCI DSS, overheidskeurmerk of groen label |

## Contracten

Na het tekenen krijgt de klant vaste racks. Elke dag meet de game welk deel van die racks draait en online is; de huur wordt naar rato betaald. Uitval kost een **SLA-boete** en tevredenheid. Aan het eind van het contract verlengt een tevreden klant (60 of hoger); een ontevreden klant vertrekt, en onder de 15 meteen. Grote klanten verhuizen met een konvooi verhuiswagens naar binnen.

## Aanbestedingen

Ongeveer eens per maand schrijft een grote klant een **aanbesteding** uit: 24 tot 200 racks, een eigen kooi of hal, 2N, meerdere carriers, 36 tot 60 maanden. Met een bedrijfsjurist bied je 85, 100 of 115% van de vraagprijs. Na 10 dagen beslist de klant; een lagere prijs, betere reputatie en nu al aan de eisen voldoen helpen, en AI-concurrenten bieden mee. Gewonnen? Dan lever je binnen 90 tot 180 dagen op, anders vervalt het contract met een boete en reputatieverlies.

## Geld

- **Financiën** (klik op het saldo bovenin): inkomsten en kosten per maand per post, en het resultaat.
- **Leningen**: tot € 2 miljoen plus 24 maanden omzet, tegen 6,5% (minder met CFO-training en goede reputatie), terug te betalen in 60 maanden.
- **Investeerders**: verkoop telkens 10% van het bedrijf; je houdt altijd minstens 51%. Later kun je **naar de beurs**.
- **Faillissement**: 30 dagen in het rood en het spel is voorbij. Kan niet met onbeperkt geld.
