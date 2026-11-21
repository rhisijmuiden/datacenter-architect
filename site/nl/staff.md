---
title: Personeel en storingen
kicker: Runnen
lead: Managers werven, roosteren en trainen hun eigen mensen. Jij stelt het beleid en de serviceplannen in; het team houdt het terrein dag en nacht draaiende.
description: Personeel, beleid, onderhoud, storingen en gebeurtenissen in Datacenter Architect.
---

## Een team dat zichzelf runt

Open **Personeel** (<kbd>F5</kbd>). Je kiest nooit zelf kandidaten en vult nooit roosters. Elke afdeling krijgt een **advies** (bijvoorbeeld één koeltechnicus per 20 koelunits, één elektrotechnicus per 30 onderdelen van de stroomketen, één support engineer per 150 racks) en de manager werft zelf tot dat advies, met een naam, een niveau (junior, medior, senior) en soms een eigenschap (snel, nauwkeurig, nachtmens, loyaal).

| Afdeling (manager) | Functies |
|---|---|
| Facility | Elektrotechnisch engineer, koeltechnicus, monteur generatoren, brandveiligheidsmedewerker |
| Netwerk | Netwerkengineer, NOC-operator |
| Operations | Support engineer, remote hands, receptionist, magazijnier, schoonmaker, kantinemedewerker, hovenier, IT-helpdesk |
| Beveiliging | Beveiliger, SOC-analist |
| Bouwploeg | Bouwvakkers, monteurs, engineers (ingehuurd via een aannemer) |

Sommige functies hebben een ruimte nodig: NOC-operators een NOC, beveiligers een beveiligingsruimte of portiersloge, de receptionist een receptie, kantinemedewerkers een kantine, onderzoekers een lab, SOC-analisten een SOC.

{% include shot.html src="staff" caption="Personeel › Team: elke afdeling met zijn advies, de mensen in dienst, hun niveau en dienst." %}

## Beleid en serviceplannen

Onder **Personeel › Beleid** stuur je het team zonder individuen aan te raken:

| Beleid | Keuzes |
|---|---|
| Bezetting | Krap (75%), normaal, ruim (125% en elke dienst bezet) |
| Salaris | −10%, markt, +15% |
| Opleidingsbudget | € 0 tot € 20.000 per maand; tilt mensen een niveau omhoog (junior 0,8×, medior 1×, senior 1,25× tempo) |
| Preventief onderhoud | Licht, normaal, grondig |

**Serviceplannen**: 24/7 klantensupport (tevredener klanten, reputatie), 24/7 NOC-bewaking (elke storing meteen gezien) en een remote hands-dienst (extra inkomsten per verhuurd rack).

Diensten: dag (07–15), avond (15–23) en nacht (23–07). 24/7-functies worden vanzelf over de diensten verdeeld; nachtmensen nemen de nachten.

## Tevredenheid

Personeel let op salaris, een **kantine** (beter met een kantinemedewerker), een koffie- of frisdrankautomaat, **parkeerplekken** voor de helft, genoeg bureaus, een IT-helpdesk vanaf twaalf kantoormensen, het opleidingsbudget en wat groen rond het terrein. Veel werk en nachtdiensten tellen negatief. Onder 30% kunnen mensen vertrekken; de manager vervangt ze.

## Slijtage, storingen en reparaties

- Racks, CRAH-units, koelmachines, koeltorens, generatoren, UPS'en, accu's, PDU's, transformatoren, schakelkasten, routers, spines, ODF's en glasvezelputten **slijten** elke dag een beetje. Nieuwe apparatuur gaat niet stuk; vanaf ongeveer 5% slijtage stijgt de kans snel.
- Technici houden alles op orde. Zonder **schoonmakers** (één per twee zalen) slijt apparatuur in de zalen 40% sneller.
- Een **kapot** onderdeel werkt meteen niet meer, en de stroomketen, koeling of het netwerk merkt het. Zonder NOC-operator in dienst merkt iemand het na twee uur. Een vrije technicus in dienst loopt erheen en repareert het; je ziet het werk gebeuren.
- Niemand voor de klus? Na 12 uur komt een **externe monteur** (met oranje helm, tegen betaling). Je kunt er ook meteen een bellen.
- **Reserveonderdelen** (server, koeling, elektrisch, UPS-accu's, optics) worden automatisch bijbesteld als de voorraad op het minimum komt. Een reparatie zonder onderdeel duurt drie keer zo lang.
- UPS'en en accu's gaan vijf jaar mee en worden dan voor de halve prijs vervangen.

{% include shot.html src="incidents" caption="Personeel › Storingen en gebeurtenissen: open storingen met wie er onderweg is, en het logboek." %}

## Gebeurtenissen

Met **willekeurige gebeurtenissen** aan (instelling bij een nieuw spel):

| Gebeurtenis | Wat er gebeurt | Wees klaar met |
|---|---|---|
| Stroomstoring in de stad | 2 tot 8 uur zonder net | Generatoren achter een schakelkast, diesel, een monteur generatoren |
| Hittegolf | 20% minder warmteafvoer, koeling slijt sneller | Reservecapaciteit koeling |
| Kabelbreuk | Een graafmachine trekt een glasvezelroute kapot | Twee glasvezelputten op verschillende straatputten |
| Brandalarm | Zonder brandveiligheidsmedewerker gaat het gas af: hal 4 uur plat, € 35.000 | Een brandveiligheidsmedewerker |
| Faillissement klant | Een klant verdwijnt | Spreiding over veel klanten |
| Persbezoek | Reputatie +2 of −1 | Een net, goed draaiend terrein |

In de creatieve modus zet **geen storingen** slijtagestoringen en kabelbreuken uit.

{% include shot.html src="staff-site" caption="Een kapotte CRAH-unit naast de racks. De meldingen tonen de storing en de nieuwe mensen; het infopaneel zegt dat eraan gewerkt wordt en biedt aan een externe monteur te bellen." %}
