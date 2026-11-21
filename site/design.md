---
title: How the game thinks
kicker: Start
lead: "Datacenter Architect is a relaxed building game. You are the director: you design and expand, the game takes care of the details."
description: The design idea behind Datacenter Architect, no micromanagement, and the chain from sales to cooling.
---

## You design, they build

The heart of the game is **designing, building and expanding** a datacenter. You draw the site, the buildings and the rooms, and you choose what goes inside. Everything after that happens by itself, and you can watch it happen:

- **Trucks** bring materials to your yard and equipment to the loading dock.
- **Builders** lay foundations, put up walls and roofs, cranes and excavators drive around.
- **Fitters** wheel racks and equipment from the dock to their place.
- **Engineers** roll out an amber power cable and an orange fiber cable to every new rack.
- **Managers** hire, roster and train staff. **Sales** finds customers. Customers visit, move their servers in with a convoy of moving trucks, and leave reviews.

There are no hard goals and no time limits (unless you pick a scenario). You play at your own pace.

{% include shot.html src="cables" caption="Engineers carry a cable reel to a new rack and unroll it: amber for power, orange for fiber. Pure atmosphere: you never lay a cable yourself." %}

## No micromanagement

This is the most important rule of the game, and it shapes every system:

| You do | The game does |
|---|---|
| Draw buildings, rooms, roads and parking | Builders build it in phases, with real trucks and machines |
| Place racks, power, cooling and network equipment | Delivery, placement and connecting, in the right order |
| Hire directors and managers | Managers hire their own staff, plan shifts and train people |
| Set **policies**: staffing level, pay, training budget, maintenance, security rules | Staff follow those policies, day and night |
| Choose **service plans**: 24/7 support, 24/7 NOC, remote hands | The right people are hired and rostered |
| Sign customers and bid on tenders | Customers are installed, billed and kept happy |
| Approve or ignore proposals from managers | Repairs, spare parts and wear are handled by the team |

If something goes wrong, the game tells you with a message and a piece of advice on the Overview, and every piece of advice has a button that takes you to the screen that fixes it.

> **Creative mode** goes one step further: switch off money, power limits, cooling limits, network limits or breakdowns, one by one. A popular mix: unlimited money, everything else realistic.
{: .tip}

## The chain

Everything in the game hangs together. More of one thing asks for more of the next:

<ul class="chain">
<li><b>Sales</b>more requests</li>
<li><b>Customers</b>more contracts</li>
<li><b>Racks</b>more racks live</li>
<li><b>Power</b>grid, UPS, PDUs</li>
<li><b>Cooling</b>CRAH, chillers</li>
<li><b>Network</b>carriers, ports</li>
</ul>

The **weakest link decides**. A hall full of racks with too little power, cooling or bandwidth earns nothing for the racks it cannot run. The status bar at the top shows each link as a bar (power amber, cooling cyan, network orange), and the overlays <kbd>P</kbd>, <kbd>H</kbd> and <kbd>N</kbd> show where it flows and where it gets stuck.

## Colours you learn once

| Colour | Means | Where you see it |
|---|---|---|
| <span style="color:var(--power)">■</span> Amber | Power | Power overlay, power cables, generator stripes, the power bar |
| <span style="color:var(--cooling)">■</span> Cyan | Cooling | Cooling overlay, CRAH units, the cooling bar, visitor badges |
| <span style="color:var(--fiber)">■</span> Orange | Fiber and network | Network overlay, fiber pits, fiber trays, the network bar |
| <span style="color:var(--ok)">■</span> Green | Healthy | OK states, the balance |
| <span style="color:var(--critical)">■</span> Red | Problem | Overload, faults, things that cannot be placed |

## Small details, smooth play

The world is full of small things to look at: blinking rack LEDs, doors that open when someone walks through, cyclists parking their bikes, rain on the roof, snow on the trees. None of it needs your attention. Halls can grow to thousands of racks and the game stays smooth: racks are drawn in batches and the simulation works per hall, not per rack.
