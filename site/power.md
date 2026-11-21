---
title: Power and redundancy
kicker: Systems
color: power
lead: Every rack needs the whole power chain, from the grid operator to the PDU in the hall. The weakest link decides how many racks run, and redundancy decides what happens when something fails.
description: The power chain in Datacenter Architect, and how to reach N+1 and 2N.
---

## The chain

<ul class="chain">
<li><b>Grid connection</b>2 to 50 MVA</li>
<li><b>Transformers</b>2.5 MVA each</li>
<li><b>Switchgear + ATS</b>4 MW each</li>
<li><b>UPS + batteries</b>500 kW / 40 kWh</li>
<li><b>PDU or busbar</b>150 kW / 2 × 400 kW</li>
<li><b>Rack</b>6 kW</li>
</ul>

- The **grid side** (grid, transformers, switchgear) carries the IT load **times the PUE**, because cooling and losses also need power. The **UPS and the halls** only carry the IT load.
- The link with the least room is the **bottleneck**. Infrastructure › Power (<kbd>F4</kbd>) shows the chain, its load per link and which link is the weakest.
- Racks the chain cannot carry are switched off, newest first. They come back by themselves.

{% include shot.html src="power-panel" nl=true caption="Infrastructure › Power and cooling: the chain with load and redundancy per link, backup power with fuel hours, UPS autonomy, cooling, PUE and every hall." %}

## Grid connection

Every site starts with a **compact station of 2 MVA** with its own transformer. Bigger connections are requested from the grid operator:

| Level | Cost | Lead time | Unlocked by |
|---|---|---|---|
| 2 MVA | included | – | – |
| 10 MVA | € 1.2 million | 30 days | Facility manager |
| 20 MVA | € 2.8 million | 60 days | Facility manager, first training |
| 50 MVA | € 7.5 million | 120 days | Facility manager, second training |

From 10 MVA the grid delivers medium voltage: build a **transformer room** with transformers (2.5 MVA each) to actually use it. With **grid congestion** on (new game switch) a bigger connection first waits in a queue, about a month per place in line. A battery container on your site helps the grid and moves you up one place.

## Redundancy: N, N+1 and 2N

For every link the power screen shows how redundant it is. The game uses the real datacenter terms:

| Label | Meaning | Rule in the game |
|---|---|---|
| **N−** | Not enough | All units together cannot carry the load |
| **N** | Just enough | The units carry the load, but if one fails, racks go down |
| **N+1** | One spare | Take away the **largest** unit and the rest still carries the load |
| **2N** | Everything twice | At least two units, together at least **twice** the load |

<ul class="chain">
<li><b>N</b>■■■ load 300 kW on 1 × 500 kW UPS</li>
<li><b>N+1</b>■■■ + □ 3 UPS for a 900 kW load</li>
<li><b>2N</b>■■■ ■■■ 2 × 500 kW for 240 kW</li>
</ul>

### How to reach N+1

Add **one unit more than you need** on that link. A 900 kW IT load needs two UPS units of 500 kW (N); with a third one it is N+1. Same for switchgear and transformers. The rule always takes away the biggest unit, so mixing sizes does not help.

### How to reach 2N

Make sure the units together are at least **double the load**, with at least two of them. Two UPS units of 500 kW give 2N up to 500 kW of IT, which is about 83 racks. Grow beyond that and the label drops to N+1 or N: add UPS units as the halls fill up.

> **Customers ask for this.** Requests and tenders can require a **2N UPS**, **A/B power in the hall** and **generators N+1**. The request shows ✓, ! or ✕ per requirement; you can sign as soon as there is no ✕ left. 2N racks sell for 10% more.
{: .power}

### A/B power in the hall

A hall has **A/B power** when it keeps all its racks running after its biggest feed fails. A feed is a PDU (150 kW) or one rail of the busbar (400 kW; a busbar is always two rails, A and B).

| Hall load | Not A/B | A/B |
|---|---|---|
| 20 racks (120 kW) | 1 PDU | 2 PDUs |
| 40 racks (240 kW) | 2 PDUs (300 kW, but one fails = 150 kW) | 3 PDUs, or a busbar |
| 120 racks (720 kW) | – | busbar + 3 PDUs, or 6 PDUs |

**32 A racks** (7.4 kW products) need A/B power **and** hot/cold aisles in the hall.

## Backup power

### Generators

Generators (1 MW each, 2,000 litre day tank) start when the city grid fails, **but only behind switchgear with an ATS**. Without switchgear they cannot take over.

- **Generator redundancy** compares generator capacity with the whole site load (IT × PUE + everything else). Two 1 MW generators show **2N** as long as the site needs at most 1 MW (one generator carries it all, the other is a full spare). Above 1 MW add a third: three generators give 2N up to 1.5 MW and N+1 up to 2 MW. Rule of thumb: generators needed for the load, plus one.
- **Fuel**: 0.27 litre per kWh. Extra **diesel tanks** (20,000 L above ground) or **underground tanks** (50,000 L, you can drive over them) give more hours. The power screen shows fuel hours; a generator mechanic orders extra diesel during an outage.
- During a city outage you see the generators run and the site lights stay on. When the diesel runs out, the site goes dark.

### UPS autonomy

The UPS bridges the seconds until the generators run. Its autonomy in minutes is **battery kWh ÷ running IT kW × 60**. Four battery cabinets (160 kWh) on 240 kW of racks give 40 minutes. UPS units and batteries last five years; then they must be replaced at half the price.

## Energy

- **Suppliers** (fictional): Polder Energie (fixed), Delta Stroom (variable, follows the market), Noordwind Groen (green, fixed) and Kustlicht (green, variable). The energy bill is IT load × PUE × price.
- **Own energy**: solar fields on roofs or ground, wind turbines (ground only) and **battery containers**. They are worked out hour by hour: solar follows the sun and the weather, wind the season and the weather.
- **Your own power comes first.** Every hour own power feeds your site, then charges the battery, then is sold to the grid at 60% of the market price (up to your grid connection). Whatever is still missing comes from the grid.
- **Battery policy** (Infrastructure › Own energy): *store own power* (default), *trade on the market* (charge cheap from the grid, sell dear) or *smart* (own surplus first, the rest trades). A reserve (20% by default) stays charged for outages. Selling can be switched off.
- **Own energy screen**: the share of your use that came from your own sun, wind and battery (per day and over 30 days), generation, grid, sold, battery charge, an hourly chart and where your power went. The top bar shows the 30-day share.
- **Green datacenter**: 30 days at 50% own energy or more brings a **nomination**; then the audit (Reputation › Certificates, also PUE 1.45 or lower) gives the green label. **Green Gold** needs 80% and PUE 1.30. Under the threshold you get 60 days of grace, then the label is suspended until you are back above it. A green supplier contract gives ESG points but does not count for the label.
- Green contracts and own energy raise your **ESG score**, which investors and the share price look at.

## Power checklist

1. Switchgear placed (otherwise no ATS and no backup).
2. UPS with batteries, enough for the IT load, one extra for N+1, double for 2N.
3. A PDU or busbar in every hall, two feeds or more for A/B.
4. Two generators (more as you grow) with enough diesel for many hours.
5. Upgrade the grid connection **in time**: it takes 30 to 120 days, plus the queue.
6. Press <kbd>P</kbd> now and then: red lines show where the chain is overloaded.
