---
title: Hoe de game denkt
kicker: Start
lead: "Datacenter Architect is een relaxte bouwgame. Jij bent de directeur: jij ontwerpt en breidt uit, de game regelt de details."
description: Het idee achter Datacenter Architect, geen micromanagement, en de keten van sales tot koeling.
---

## Jij ontwerpt, zij bouwen

De kern van de game is **een datacenter ontwerpen, bouwen en uitbreiden**. Jij tekent het terrein, de gebouwen en de ruimtes, en jij kiest wat erin komt. Alles daarna gebeurt vanzelf, en je kunt het zien gebeuren:

- **Vrachtwagens** brengen materiaal naar je bouwplaats en apparatuur naar het laadperron.
- **Bouwvakkers** storten funderingen, zetten muren en daken, kranen en graafmachines rijden rond.
- **Monteurs** rijden racks en apparatuur van het laadperron naar hun plek.
- **Engineers** rollen naar elk nieuw rack een amberkleurige stroomkabel en een oranje glasvezelkabel uit.
- **Managers** werven, roosteren en trainen personeel. **Sales** zoekt klanten. Klanten komen langs, verhuizen hun servers met een konvooi verhuiswagens en schrijven recensies.

Er zijn geen harde doelen en geen tijdslimieten (tenzij je een scenario kiest). Je speelt in je eigen tempo.

{% include shot.html src="cables" caption="Engineers dragen een kabelhaspel naar een nieuw rack en rollen hem af: amber voor stroom, oranje voor glasvezel. Puur sfeer: zelf leg je nooit een kabel." %}

## Geen micromanagement

Dit is de belangrijkste regel van de game, en elk systeem is erop gebouwd:

| Jij doet | De game doet |
|---|---|
| Gebouwen, ruimtes, wegen en parkeerplaatsen tekenen | Bouwvakkers bouwen het in fases, met echte vrachtwagens en machines |
| Racks, stroom, koeling en netwerkapparatuur plaatsen | Levering, plaatsen en aansluiten, in de goede volgorde |
| Directeuren en managers aannemen | Managers werven hun eigen mensen, plannen diensten en trainen |
| **Beleid** instellen: bezetting, salaris, opleidingsbudget, onderhoud, beveiligingsregels | Personeel volgt dat beleid, dag en nacht |
| **Serviceplannen** kiezen: 24/7 support, 24/7 NOC, remote hands | De juiste mensen worden aangenomen en ingeroosterd |
| Klanten tekenen en bieden op aanbestedingen | Klanten worden aangesloten, gefactureerd en tevreden gehouden |
| Voorstellen van managers goedkeuren of laten liggen | Reparaties, reserveonderdelen en slijtage regelt het team |

Gaat er iets mis, dan meldt de game dat met een bericht en een advies op het Overzicht, en elk advies heeft een knop naar het scherm dat het oplost.

> **De creatieve modus** gaat nog een stap verder: zet geld, stroomlimieten, koellimieten, netwerklimieten of storingen een voor een uit. Een populaire mix: onbeperkt geld, de rest realistisch.
{: .tip}

## De keten

Alles in de game hangt samen. Meer van het een vraagt meer van het volgende:

<ul class="chain">
<li><b>Sales</b>meer aanvragen</li>
<li><b>Klanten</b>meer contracten</li>
<li><b>Racks</b>meer racks live</li>
<li><b>Stroom</b>net, UPS, PDU's</li>
<li><b>Koeling</b>CRAH, koelmachines</li>
<li><b>Netwerk</b>carriers, poorten</li>
</ul>

De **zwakste schakel beslist**. Een hal vol racks met te weinig stroom, koeling of bandbreedte verdient niets aan de racks die niet kunnen draaien. De statusbalk bovenin toont elke schakel als balk (stroom amber, koeling cyaan, netwerk oranje), en de overlays <kbd>P</kbd>, <kbd>H</kbd> en <kbd>N</kbd> laten zien waar het stroomt en waar het vastloopt.

## Kleuren die je één keer leert

| Kleur | Betekent | Waar je het ziet |
|---|---|---|
| <span style="color:var(--power)">■</span> Amber | Stroom | Stroomweergave, stroomkabels, strepen op generatoren, de stroombalk |
| <span style="color:var(--cooling)">■</span> Cyaan | Koeling | Koelweergave, CRAH-units, de koelbalk, bezoekersbadges |
| <span style="color:var(--fiber)">■</span> Oranje | Glasvezel en netwerk | Netwerkweergave, glasvezelputten, fibergoten, de netwerkbalk |
| <span style="color:var(--ok)">■</span> Groen | Gezond | OK-status, het saldo |
| <span style="color:var(--critical)">■</span> Rood | Probleem | Overbelasting, storingen, dingen die niet geplaatst kunnen worden |

## Kleine details, soepel spelen

De wereld zit vol kleine dingen om naar te kijken: knipperende rackledjes, deuren die opengaan als er iemand doorloopt, fietsers die hun fiets parkeren, regen op het dak, sneeuw op de bomen. Niets daarvan vraagt je aandacht. Zalen kunnen groeien tot duizenden racks en de game blijft soepel: racks worden in groepen getekend en de simulatie rekent per hal, niet per rack.
