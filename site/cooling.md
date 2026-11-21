---
title: Cooling
kicker: Systems
color: cooling
lead: Every kilowatt of IT becomes heat. CRAH units move it out of the hall, chillers and cooling towers get rid of it outside, and containment makes the whole thing cheaper.
description: How cooling works in Datacenter Architect, CRAH units, chillers, cooling towers, containment and PUE.
---

## Two halves

<ul class="chain cooling">
<li><b>Racks</b>6 kW of heat each</li>
<li><b>CRAH units</b>in the hall, 120 kW</li>
<li><b>Heat rejection</b>outside or on the roof</li>
</ul>

Cooling has two halves and **both** must be big enough:

1. **In the hall**: CRAH units (computer room air handlers) blow cold air into the cold aisles and take the warm air back. Each one cools **120 kW**, or 162 and 204 kW after upgrades. Units in one hall only cool that hall.
2. **Outside or on the roof**: the heat leaves the building through chillers, cooling towers or dry coolers. Their capacity is shared by all halls, in proportion to each hall's heat.

What a hall can really cool is the **smaller** of the two. Ten CRAH units do nothing without a chiller outside, and a huge cooling tower does nothing for a hall without CRAH units.

| Equipment | Where | Capacity | Notes |
|---|---|---|---|
| CRAH unit | In the hall | 120 kW (162 / 204 upgraded) | 1 × 2 m, uses 4 kW itself |
| Chiller | Outside or roof | 600 kW (upgradeable) | 3 × 2 m |
| Cooling tower | Outside or roof | 1,000 kW (upgradeable) | 2 × 2 m, uses water |
| Dry cooler | Outside or roof | 450 kW (upgradeable) | Cools without water: better for your ESG score |

Click a cooling unit and press **Upgrade** in the inspector to go to level 2 (×1.35) or 3 (×1.7).

{% include shot.html src="cooling-overlay" caption="Cooling overlay (H): cold air flows to the rows, warm water goes to the chillers on the roof. Racks are coloured by the temperature of their hall." %}

## Hall temperature

Every hall has a temperature. A well cooled hall sits around 20 to 25 °C. With too little cooling it warms up, and from **32 °C racks shut down** (newest first) until the rest can be cooled again. When cooling is back in order, they switch on by themselves.

A fire alarm without a fire safety officer on staff sets off the **gas extinguishing system**: the hall is down for four hours and refilling costs € 35,000.

## Containment and PUE

**PUE** (power usage effectiveness) is the total site power divided by the IT power. 1.0 would be perfect; everything above it is cooling and losses you pay for. You improve it by upgrading the hall's air management in the hall inspector:

| Hall | CRAH bonus | PUE | Cost | Time |
|---|---|---|---|---|
| Open hall | – | 1.65 | – | – |
| Hot and cold aisles | +12% | 1.50 | € 120 per m² | 1 day |
| Cold corridor (after research) | +35% | 1.30 | € 380 per m² | 3 days |

A lower PUE means a lower energy bill, a smaller load on the grid side of the power chain, a better ESG score and the achievements for an efficient site. Research (EC fans, free cooling, liquid cooling) lowers it further.

> **32 A racks** need hot and cold aisles in their hall. Smart drag already places the racks front to front, so upgrading is just a click in the hall inspector.
{: .cooling}

## Weather

With **weather plays along** switched on at the start of a game:

- On cold days you get **free cooling**: the PUE drops.
- A **heatwave** in summer takes 20% off your heat rejection, and cooling equipment wears 50% faster. Keep some spare capacity before summer.

## Cooling checklist

1. At least one CRAH unit per 20 racks (120 kW), plus one spare per hall.
2. Chillers, cooling towers or dry coolers outside with more capacity than all halls together.
3. Hot/cold aisles as soon as you can; a cold corridor after research.
4. A cooling technician per 20 units keeps them from breaking: the managers hire them by themselves.
5. Press <kbd>H</kbd> to see which hall runs warm.
