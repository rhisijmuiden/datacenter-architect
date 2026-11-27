---
title: Home
description: What you can do in Datacenter Architect, and where to start.
---

<div class="hero">
  <img src="{{ '/assets/shots/photo.jpg' | relative_url }}" alt="A datacenter campus with chillers on the roof, generators, an office and a parking lot">
  <div class="hero-text">
    <h1>Datacenter Architect</h1>
    <p>Buy land, design the whole site from the parking lot to the data hall, keep power, cooling and fiber in balance, and grow into a campus. An offline building game for Windows 11, macOS and Linux.</p>
    <div class="cta">
      <a class="btn" href="{{ site.repo }}/releases/latest">Download the game</a>
      <a class="lang" href="{{ '/getting-started/' | relative_url }}">Build your first datacenter</a>
    </div>
  </div>
</div>

You are the **director**. You decide where to build, what to build and what to invest in. Builders, fitters, engineers, managers and customers do the rest by themselves, so you can spend your time on what the game is about: **designing, building and expanding a datacenter**.

## What you can do

<div class="cards">
  <a class="card" href="{{ '/buildings/' | relative_url }}"><img src="{{ '/assets/shots/site.jpg' | relative_url }}" alt=""><div><b>Design the whole site</b><span>Driveway, fence and gate, parking, loading dock, generators, offices and a warehouse. Builders and trucks build what you draw.</span></div></a>
  <a class="card" href="{{ '/rooms/' | relative_url }}"><img src="{{ '/assets/shots/hall.jpg' | relative_url }}" alt=""><div><b>Draw your own rooms</b><span>Data halls, meet-me room, UPS and battery rooms, NOC, offices, canteen. Basements and extra floors too.</span></div></a>
  <a class="card" href="{{ '/racks/' | relative_url }}"><img src="{{ '/assets/shots/rack-drag.jpg' | relative_url }}" alt=""><div><b>Fill halls with racks</b><span>Drag whole blocks of rows with cold and hot aisles. A truck delivers them, engineers connect power and fiber.</span></div></a>
  <a class="card power" href="{{ '/power/' | relative_url }}"><img src="{{ '/assets/shots/power-overlay.jpg' | relative_url }}" alt=""><div><b>Power with N+1 and 2N</b><span>Grid connection up to 50 MVA, transformers, switchgear, UPS, PDUs or busbars, and generators with diesel.</span></div></a>
  <a class="card cooling" href="{{ '/cooling/' | relative_url }}"><img src="{{ '/assets/shots/cooling-overlay.jpg' | relative_url }}" alt=""><div><b>Keep it cool</b><span>CRAH units in the halls, chillers, cooling towers and dry coolers outside, and containment for a lower PUE.</span></div></a>
  <a class="card fiber" href="{{ '/network/' | relative_url }}"><img src="{{ '/assets/shots/network-overlay.jpg' | relative_url }}" alt=""><div><b>Connect the internet</b><span>Fiber pits, carriers, an internet exchange, peering with rivals, core routers and spine switches.</span></div></a>
  <a class="card" href="{{ '/company/' | relative_url }}"><img src="{{ '/assets/shots/customers.jpg' | relative_url }}" alt=""><div><b>Run a company</b><span>Hire a CEO and directors, sign customers and tenders, borrow money or go public.</span></div></a>
  <a class="card" href="{{ '/staff/' | relative_url }}"><img src="{{ '/assets/shots/staff-site.jpg' | relative_url }}" alt=""><div><b>A living site</b><span>Staff hire, roster and train themselves. Things wear out, break and get repaired while you watch.</span></div></a>
  <a class="card" href="{{ '/world/' | relative_url }}"><img src="{{ '/assets/shots/snow.jpg' | relative_url }}" alt=""><div><b>Seasons, weather, photos</b><span>Day and night, snow and storms, a first-person walk, photo mode, a timelapse and awards.</span></div></a>
</div>

## Start here

1. **[Your first datacenter](getting-started/)** walks you from an empty plot to your first paying customer, step by step.
2. **[How the game thinks](design/)** explains the idea behind the game: you design, the game runs the details.
3. **[Racks online](racks/)** is the checklist for when a rack does not come online, and **[Power and redundancy](power/)** shows how to reach N+1 and 2N.
4. **[Tips and tricks](tips/)** collects the things that make life easier.

> **New to datacenters?** Start a new game with the **Tutorial** box ticked. A coach card follows the checklist, explains each step and makes the right button in the build bar glow. Prefer to just build? Pick **Creative mode** and switch on unlimited money.
{: .tip}

## In short

| | |
|---|---|
| Platform | Windows 11 (64-bit) and macOS 12 or newer (Apple Silicon and Intel) and Linux (x86_64, Vulkan). Offline; only the update check uses the internet |
| Language | English and Dutch (switch any time in the main menu or under Options) |
| Engine | Godot 4.5 with C# |
| Saves | Local savegames plus three rotating autosaves |
| Channels | Stable (`main`) and Test (`test`), switchable in the in-game Info screen |
