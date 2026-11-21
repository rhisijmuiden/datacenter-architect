---
title: Home
description: Wat je allemaal kunt in Datacenter Architect, en waar je begint.
---

<div class="hero">
  <img src="{{ '/assets/shots/hero.jpg' | relative_url }}" alt="Een datacentercampus in aanbouw, met kranen, vrachtwagens en een parkeerplaats">
  <div class="hero-text">
    <h1>Datacenter Architect</h1>
    <p>Koop grond, ontwerp het hele terrein van parkeerplaats tot datazaal, houd stroom, koeling en glasvezel in balans en groei uit tot een campus. Een offline bouwgame voor Windows 11.</p>
    <div class="cta">
      <a class="btn" href="{{ site.repo }}/releases/latest">Download de game</a>
      <a class="lang" href="{{ '/nl/getting-started/' | relative_url }}">Bouw je eerste datacenter</a>
    </div>
  </div>
</div>

Jij bent de **directeur**. Jij bepaalt waar je bouwt, wat je bouwt en waarin je investeert. Bouwvakkers, monteurs, engineers, managers en klanten doen de rest zelf, zodat jij je tijd kunt steken in waar de game om draait: **een datacenter ontwerpen, bouwen en uitbreiden**.

## Wat je kunt doen

<div class="cards">
  <a class="card" href="{{ '/nl/buildings/' | relative_url }}"><img src="{{ '/assets/shots/site.jpg' | relative_url }}" alt=""><div><b>Ontwerp het hele terrein</b><span>Oprit, hek en poort, parkeerplaats, laadperron, generatoren, kantoren en een warehouse. Bouwvakkers en vrachtwagens bouwen wat jij tekent.</span></div></a>
  <a class="card" href="{{ '/nl/rooms/' | relative_url }}"><img src="{{ '/assets/shots/hall.jpg' | relative_url }}" alt=""><div><b>Teken je eigen ruimtes</b><span>Datazalen, meet-me room, UPS- en accuruimtes, NOC, kantoren, kantine. Ook kelders en extra verdiepingen.</span></div></a>
  <a class="card" href="{{ '/nl/racks/' | relative_url }}"><img src="{{ '/assets/shots/rack-drag.jpg' | relative_url }}" alt=""><div><b>Vul zalen met racks</b><span>Sleep hele blokken rijen met koude en warme gangen. Een vrachtwagen levert ze, engineers sluiten stroom en glasvezel aan.</span></div></a>
  <a class="card power" href="{{ '/nl/power/' | relative_url }}"><img src="{{ '/assets/shots/power-overlay.jpg' | relative_url }}" alt=""><div><b>Stroom met N+1 en 2N</b><span>Netaansluiting tot 50 MVA, transformatoren, schakelkasten, UPS, PDU's of busbars, en generatoren op diesel.</span></div></a>
  <a class="card cooling" href="{{ '/nl/cooling/' | relative_url }}"><img src="{{ '/assets/shots/cooling-overlay.jpg' | relative_url }}" alt=""><div><b>Houd het koel</b><span>CRAH-units in de zalen, koelmachines, koeltorens en dry coolers buiten, en containment voor een lagere PUE.</span></div></a>
  <a class="card fiber" href="{{ '/nl/network/' | relative_url }}"><img src="{{ '/assets/shots/network-overlay.jpg' | relative_url }}" alt=""><div><b>Sluit het internet aan</b><span>Glasvezelputten, carriers, een internet exchange, peering met concurrenten, core-routers en spine-switches.</span></div></a>
  <a class="card" href="{{ '/nl/company/' | relative_url }}"><img src="{{ '/assets/shots/customers.jpg' | relative_url }}" alt=""><div><b>Run een bedrijf</b><span>Neem een CEO en directeuren aan, teken klanten en aanbestedingen, leen geld of ga naar de beurs.</span></div></a>
  <a class="card" href="{{ '/nl/staff/' | relative_url }}"><img src="{{ '/assets/shots/staff-site.jpg' | relative_url }}" alt=""><div><b>Een levend terrein</b><span>Personeel werft, roostert en traint zichzelf. Dingen slijten, gaan stuk en worden gerepareerd terwijl jij kijkt.</span></div></a>
  <a class="card" href="{{ '/nl/world/' | relative_url }}"><img src="{{ '/assets/shots/snow.jpg' | relative_url }}" alt=""><div><b>Seizoenen, weer, foto's</b><span>Dag en nacht, sneeuw en storm, zelf rondlopen, een fotomodus, een timelapse en prijzen.</span></div></a>
</div>

## Begin hier

1. **[Je eerste datacenter](getting-started/)** neemt je stap voor stap mee van een leeg kavel naar je eerste betalende klant.
2. **[Hoe de game denkt](design/)** legt het idee achter de game uit: jij ontwerpt, de game regelt de details.
3. **[Racks online krijgen](racks/)** is de checklist voor als een rack niet online komt, en **[Stroom en redundantie](power/)** laat zien hoe je N+1 en 2N haalt.
4. **[Tips en trucs](tips/)** verzamelt de dingen die het leven makkelijker maken.

> **Nieuw in datacenters?** Start een nieuw spel met het vinkje **Tutorial** aan. Een coachkaart volgt de checklist, legt elke stap uit en laat de juiste knop in de bouwbalk oplichten. Wil je gewoon bouwen? Kies **Creatieve modus** en zet onbeperkt geld aan.
{: .tip}

## In het kort

| | |
|---|---|
| Platform | Windows 11, 64-bit. Offline; alleen de updatecheck gebruikt internet |
| Taal | Engels en Nederlands (wissel wanneer je wilt in het hoofdmenu of onder Opties) |
| Engine | Godot 4.5 met C# |
| Savegames | Lokale savegames plus drie autosaves die elkaar afwisselen |
| Kanalen | Stabiel (`main`) en Test (`test`), te wisselen in het Info-scherm in de game |
