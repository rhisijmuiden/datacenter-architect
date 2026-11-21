---
title: Your first datacenter
kicker: Start
lead: From an empty plot to your first paying customer. This is the same route the in-game tutorial follows, with the why behind every step.
description: Step by step guide to building your first datacenter in Datacenter Architect.
---

## Start a new game

Open **New game** in the main menu. The settings you choose here hold for the whole game.

{% include shot.html src="new-game" nl=true caption="The New game dialog with creative mode switched on. Every creative switch works on its own: you can, for example, only take unlimited money and keep everything else realistic." %}

| Setting | What it does |
|---|---|
| Company name | Your name in the world: facade signs, truck livery and the logo (change it later under Company › Brand) |
| Difficulty | Easy, normal or hard. Starting money is € 25, € 12 or € 6 million, and hard means more faults, longer grid waits and wilder energy prices |
| Scenario | Free play, or a scenario with bronze, silver and gold medals (see [World and progress]({{ '/world/' | relative_url }})) |
| Tutorial | A coach card that explains every step below. On by default when you have no saves yet |
| Rivals, news, events | AI datacenters (1 to 5, and how aggressive), market news, random events, dynamic energy prices, disasters, supply delays. All optional |
| Grid congestion | Bigger grid connections first wait in the grid operator's queue |
| Weather plays along | Heatwaves, frost and storms have gameplay effects (off = weather is only for looks) |
| Creative mode | Separate switches for unlimited money, no power limit, no cooling limit, no network limit, no breakdowns, instant build and all unlocks |

> **Just want to build?** Creative mode with **unlimited money** and **all unlocks** is the relaxed way to play. You still see the whole chain work, you just never run out of money.
{: .tip}

## The checklist

On the right of the screen sits a checklist (Overview › Goals, or <kbd>F1</kbd>) with everything a first datacenter needs. With the tutorial on, a coach card in the top left explains the current step, and **Show me** opens the right build category or screen.

{% include shot.html src="tutorial" nl=true caption="The tutorial coach (top left) and the glowing build category at the bottom. Show me opens the right place, Next tip skips a tip." %}

<ol class="steps">
<li><strong>Driveway.</strong> Trucks and builders need a way in. In the build bar, open <b>Land</b> and drag <b>Asphalt</b> from the public road at the edge of your plot towards where you will build. Roads of 6 m or wider get a centre line and two-way traffic.</li>
<li><strong>Materials yard.</strong> Builders store their materials on a yard. Paint a small <b>Building material</b> yard (about 4 × 4) next to the driveway. Without a yard and a route from the road, nothing gets built.</li>
<li><strong>Datacenter building.</strong> Under <b>Buildings</b>, drag out a <b>Datacenter</b> on the grass. Start modest, around 20 × 14 m; you can always add more buildings, a basement or an extra floor later. Trucks bring material, builders lay the foundation, put up the walls and the roof. You see cranes and an excavator at work.</li>
<li><strong>Data hall.</strong> Inside the finished building, open <b>Rooms</b> and drag a <b>Data hall</b>. This is where the racks will stand. Leave room for a loading dock, technical rooms and a corridor.</li>
<li><strong>Loading dock.</strong> Racks arrive by truck. Make a <b>Loading dock</b> room against an outer wall and put a <b>roll-up door</b> in that wall, next to the asphalt. Give the hall a <b>double door</b>: racks only fit through double and roll-up doors.</li>
<li><strong>Racks.</strong> Open <b>Data halls</b> and drag rows of racks in the hall. A wide drag makes pairs of rows with a cold aisle between the fronts and a hot aisle between the pairs. A truck brings them, fitters place them and an engineer connects power and fiber.</li>
<li><strong>Director's office.</strong> Make a <b>Director's office</b> room and furnish it with a desk and a chair (<b>Interior</b>). Every director and manager needs a furnished office of their own.</li>
<li><strong>Hire a CEO</strong> under <b>Company</b> (<kbd>F2</kbd>). From then on the managers run the company by themselves.</li>
<li><strong>Hire a sales director.</strong> Without one, no customers come in. Sales people each need a desk in an office room.</li>
<li><strong>Main switchgear.</strong> Racks need power. Make a <b>Switchgear room</b> and place the <b>Switchgear</b>: it takes the grid connection in and has the ATS that lets generators take over.</li>
<li><strong>UPS with batteries.</strong> A UPS bridges the seconds of a power cut until the generators run. Place a <b>UPS</b> in a <b>UPS room</b> and <b>battery cabinets</b> in the UPS room or a battery room.</li>
<li><strong>PDU or busbar.</strong> A <b>PDU</b> in the data hall (or a busbar upgrade on the hall) brings power to the racks. Two of them give the hall A/B power.</li>
<li><strong>Cooling.</strong> Every kW of IT becomes heat. Put <b>CRAH units</b> in the hall and a <b>chiller</b> or <b>cooling tower</b> outside or on the roof to carry the heat away.</li>
<li><strong>Two generators.</strong> Place two generators outside: one carries the load, one is spare. That is <b>N+1</b>.</li>
<li><strong>Fiber.</strong> Place a <b>fiber pit</b> at the edge of the plot, near the road, and make a <b>Meet-me room</b> with an <b>ODF</b> (patch frame) inside the building.</li>
<li><strong>Two carriers.</strong> Under Infrastructure › Network (<kbd>F4</kbd>) sign contracts with two carriers. Two routes keep you online when one fails.</li>
<li><strong>Core router and spines.</strong> Place a <b>core router</b> in the meet-me room and a <b>spine switch</b> in each hall, so the racks reach the carriers. As soon as the first rack is online, you can sell it.</li>
<li><strong>Sign your first customer.</strong> Open <b>Customers</b> (<kbd>F3</kbd>): your sales director brings in requests. Sign one and the money starts to flow.</li>
</ol>

{% include shot.html src="build-menu" caption="The build bar along the bottom. Each category has its own colour (power amber, cooling cyan, network orange); the tray above shows the items with their price. The bulldozer sits on its own in the bottom right." %}

## What a small first datacenter looks like

A compact first building that works, roughly 24 × 16 m:

| Room | Inside | Why |
|---|---|---|
| Data hall (about 12 × 10) | Rack rows, 2 CRAH units, 2 PDUs, 1 spine switch | The racks, cooled and powered from two feeds |
| Loading dock | Roll-up door to the asphalt | Trucks deliver racks and equipment here |
| Switchgear room | Switchgear | Takes the grid in, ATS for the generators |
| UPS room | UPS + battery cabinets | Rides through a power cut |
| Meet-me room | ODF + core router | Where the carriers come in |
| Corridor | | Lets people and racks reach every room |

Outside: two generators, one chiller or cooling tower, a fiber pit near the street, the driveway, the yard and a few parking places. The director's office can go in a separate office building, next to sales.

{% include shot.html src="site" caption="A working starter campus: datacenter with chillers on the roof, generators beside it, an office, a warehouse, parking and a fenced site." %}

> **Racks show up but stay dark?** That is the most common question. Go to [Racks online]({{ '/racks/' | relative_url }}) for the full checklist.
{: .power}

## Where things are on screen

- **Bottom:** the build bar with everything you place in the world. The **bulldozer** (demolish) sits bottom-right, with undo and redo and the plan-mode button above it.
- **Left:** the management rail, <kbd>F1</kbd> to <kbd>F8</kbd>: Overview, Company, Customers, Infrastructure, Staff, Reputation and research, Growth, Progress. Screens open as a drawer; the world stays visible and clickable next to it.
- **Top:** the status bar (power, backup, cooling, network, racks, PUE, staff, reputation, balance). Every number is clickable and opens the matching screen.
- **Top right:** view controls: rotate, grid, walk, photo, roofs, floor, overlays for power, cooling and network, day/night and weather.
- **Bottom right:** clock, weather and game speed.

{% include shot.html src="overview" nl=true caption="Overview (F1): key figures, advice with a button to the screen that solves it, goals and messages." %}
