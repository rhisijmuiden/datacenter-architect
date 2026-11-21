---
title: Installeren, updates en savegames
kicker: Hulp
lead: Zo installeer je de game, houd je hem bijgewerkt, wissel je tussen de stabiele en de testversie, en vind je je savegames.
description: Datacenter Architect installeren, updates, stabiel en testkanaal, savegames, opties en besturing.
---

## Installeren

1. Ga naar de [nieuwste release]({{ site.repo }}/releases/latest) en download `DatacenterArchitect-<versie>-Setup.exe`.
2. Start het bestand. Het installeert alleen voor jouw Windows-gebruiker en vraagt dus geen beheerdersrechten.
3. Start **Datacenter Architect** vanuit het Startmenu.

Nodig: Windows 11 (64-bit) en een videokaart met Vulkan. De game werkt helemaal offline; alleen de updatecheck gebruikt internet.

## Updates en kanalen

Open het **Info**-scherm (de ⓘ-knop onderaan de balk links, of Info in het hoofdmenu). Je ziet welke versie je speelt en je kunt:

- **Controleren op updates** – de game vraagt GitHub naar de nieuwste release op jouw kanaal.
- **Updaten** – downloadt de installer, controleert de grootte en de SHA-256-vingerafdruk, installeert op de achtergrond en start de game opnieuw.
- **Van kanaal wisselen**:
  - **Stabiel** krijgt de geteste releases (van de branch `main`).
  - **Test** krijgt ook de testbuilds (van de branch `test`): nieuwe dingen eerst, soms met een ruw randje.

Je kunt altijd van test terug naar stabiel; de volgende update brengt je naar de nieuwste stabiele versie.

{% include shot.html src="info" nl=true caption="Het Info-scherm: versie, controleren op updates, update installeren en de keuze tussen het stabiele en het testkanaal." %}

## Savegames

- Savegames staan in `%APPDATA%\Datacenter Architect\saves`. Geef ze elke naam die je wilt met **Opslaan** in het spelmenu (<kbd>Esc</kbd> of ☰ onderaan de balk links).
- <kbd>Ctrl</kbd>+<kbd>S</kbd> slaat snel op.
- **Autosave**: drie autosaves die elkaar afwisselen, standaard elke 5 minuten en bij afsluiten. Hoe vaak stel je in onder Opties › Spel.
- **Verder spelen** in het hoofdmenu laadt je laatste savegame.
- Oudere savegames blijven werken na een update; nieuwe dingen die een oud spel zouden veranderen (zoals weereffecten) blijven daarin uit.

Prestaties en je beste scenariomedailles worden bewaard in een profiel over alle savegames. Timelapsefoto's worden per spel bewaard.

## Opties

Opties open je vanuit het hoofdmenu, het pauzemenu en het tandwiel onderaan de balk links. Alles wordt meteen toegepast.

| Tabblad | Wat je instelt |
|---|---|
| Beeld | Venster, randloos of volledig scherm, resolutie (4:3 tot 32:9 en 4K), beeldscherm, grootte van de interface (75–150%), V-Sync, maximale framerate. <kbd>Alt</kbd>+<kbd>Enter</kbd> of <kbd>F11</kbd> wisselt volledig scherm |
| Graphics | Snelkeuze Prestaties / Gebalanceerd / Kwaliteit, schaduwen, ambient occlusion, nevel, anti-aliasing, lichtkwaliteit, gras, knipperende lampjes, bewegende onderdelen, vloeiende camera, dag/nacht, weer, seizoenen |
| Geluid | Hoofdvolume, muziek, serverruimte, interface; geluidseffecten aan of uit; dempen op de achtergrond |
| Spel | Taal (Engels of Nederlands), hoe vaak automatisch opslaan, muisgevoeligheid en omgekeerd kijken bij rondlopen |

## Besturing

| Toets | Actie |
|---|---|
| Rechts slepen | Camera draaien |
| Scrollwiel | Zoomen |
| <kbd>W</kbd> <kbd>A</kbd> <kbd>S</kbd> <kbd>D</kbd> of middelste knop slepen | Schuiven |
| <kbd>Q</kbd> / <kbd>E</kbd> | 90° draaien |
| <kbd>R</kbd> | Draai wat je plaatst |
| <kbd>T</kbd> | Daken aan of uit |
| <kbd>Page Up</kbd> / <kbd>Page Down</kbd> | Verdieping omhoog of omlaag |
| <kbd>P</kbd> / <kbd>H</kbd> / <kbd>N</kbd> | Weergave stroom, koeling en netwerk |
| <kbd>B</kbd> | Planmodus |
| <kbd>F</kbd> | Zelf rondlopen |
| <kbd>F9</kbd> | Fotomodus |
| <kbd>F1</kbd> – <kbd>F8</kbd> | Overzicht, Bedrijf, Klanten, Infrastructuur, Personeel, Reputatie en onderzoek, Groei, Voortgang |
| <kbd>Spatie</kbd> | Pauze / spelen |
| <kbd>1</kbd> <kbd>2</kbd> <kbd>3</kbd> | Spelsnelheid |
| <kbd>Ctrl</kbd>+<kbd>Z</kbd> / <kbd>Ctrl</kbd>+<kbd>Y</kbd> | Ongedaan maken / opnieuw |
| <kbd>Ctrl</kbd>+<kbd>S</kbd> | Snel opslaan |
| <kbd>[</kbd> / <kbd>]</kbd> | Gebogen weg smaller of breder |
| <kbd>Esc</kbd> | Scherm sluiten, annuleren of het spelmenu openen |

## Problemen?

Een bug gevonden of een idee? Open een [issue op GitHub]({{ site.repo }}/issues). Logbestanden staan in `%APPDATA%\Datacenter Architect\logs`.
