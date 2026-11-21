---
title: Racks online
kicker: Build
lead: A rack only earns money when it is live, running and online. This page explains the delivery chain and gives a checklist for when racks stay dark.
description: How to get racks online in Datacenter Architect, and a troubleshooting checklist.
---

## Placing racks

Open **Data halls** in the build bar and pick **Rack**:

- **Click** to place one rack. <kbd>R</kbd> turns it; an arrow on the ghost shows the **front**.
- **Drag** a line for a row of up to 24 racks.
- **Drag wide** for a whole block: the game makes pairs of rows that face each other across a **1 m cold aisle**, with a **2 m hot aisle** between the pairs. <kbd>R</kbd> starts with the backs instead.

While you drag, the ghost shows the number of racks, rows, the size, the kW and the cost. Cyan tiles are the cold aisles, orange tiles the hot aisles.

{% include shot.html src="rack-drag" caption="Smart drag: one wide drag fills the hall with pairs of rows, cold aisles between the fronts and hot aisles between the pairs." %}

> **Every rack needs a free cell in front and behind it, inside the hall.** Engineers work on both sides. Nothing may stand in those cells, not even another rack. Leave aisles along the walls too.
{: .warn}

## From order to live

A placed rack is an order. You can follow it in the world:

<ul class="chain">
<li><b>Ordered</b>a see-through plan in the hall</li>
<li><b>Delivered</b>a truck backs up to the loading dock</li>
<li><b>Placed</b>fitters wheel it to its spot</li>
<li><b>Powered</b>an engineer connects A/B power (amber)</li>
<li><b>Live</b>then fiber (orange); ready to sell</li>
</ul>

With **instant build** in creative mode this chain is skipped.

## Live is not enough: running and online

A live rack still has to pass three checks every moment:

1. **Power** – the whole power chain must be able to carry it (see [Power]({{ '/power/' | relative_url }})). Each rack counts as **6 kW**.
2. **Cooling** – the hall must not overheat. From **32 °C** racks shut down until the hall can be cooled again (see [Cooling]({{ '/cooling/' | relative_url }})).
3. **Network** – the rack needs a port on a spine switch in its hall, that spine a port on a core router, and the core a carrier, the IX or a peer through an ODF in the meet-me room (see [Network]({{ '/network/' | relative_url }})).

When there is not enough power or cooling, the **newest racks are switched off first**. They come back on by themselves when the problem is solved. Racks that do not run or are offline earn no rent, and customers on them get an SLA penalty and grow less happy.

## Troubleshooting checklist

Click the rack or the hall: the inspector tells you which step it is stuck on. Then work down this list.

| Symptom | Likely cause | Fix |
|---|---|---|
| Rack stays a see-through plan | No truck can reach the dock | A loading dock room with a **roll-up door** in an outer wall, asphalt up to that door, a driveway from the public road |
| Truck arrives but the rack is not placed | Fitters cannot get from the dock to the hall | A corridor from the dock to the hall and a **double door** in the hall; a freight lift if the hall is on another floor |
| "An engineer cannot reach a rack" | No aisle | Leave a free cell in front of and behind every rack |
| Rack is placed but not powered | No engineer, or the engineer is busy | Look under Staff › Team; the starting crew has one engineer, hire more with HR and an operations manager |
| Rack is live but dark, "down for power" | Hall distribution too small | Add a **PDU** (150 kW = 25 racks) or a **busbar** (2 × 400 kW) in that hall |
| Many racks dark across the site | A link of the chain is full | Infrastructure › Power shows the **weakest link**: grid, transformers, switchgear or UPS |
| Racks go dark during a city outage | No backup | Generators need **switchgear** (the ATS), enough capacity and diesel |
| Racks dark, hall red in the cooling overlay | Too hot | More **CRAH units** in the hall, more **chillers/cooling towers** outside, or containment |
| Rack runs but is "offline" | No network path | Spine switch in the hall, core router + ODF in the MMR, a carrier contract with a delivered port |
| Rack is online but nobody rents it | Wrong zone or no requests | Check the hall zone and customer types; hire a sales director and sales people |

{% include shot.html src="power-overlay" caption="Power overlay (P): amber lines and dots flow from the grid through the chain to the rack rows. Red means overloaded or down." %}

> **Quick rule of thumb per 20 racks** (120 kW): one PDU covers 25 racks, one CRAH unit cools 20 racks (more with upgrades and containment), one UPS carries about 80 racks, one spine switch has 48 ports. Double what needs redundancy.
{: .tip}

## Selling racks

A rack that is live, running and online is **for sale**. Your sales team finds customers that fit the hall's zone:

- **Full racks** – 16 A (3.7 kW) or 32 A (7.4 kW) products.
- **Colocation per U** – 42 U per rack, sold to many small customers.
- **Cages** – a group of racks in one hall for one customer.
- **Private hall** – a whole hall for one customer.
- **Own cloud** – racks for your own hosting company.

More in [Company and customers]({{ '/company/' | relative_url }}).
