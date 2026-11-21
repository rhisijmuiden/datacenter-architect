---
title: Site and buildings
kicker: Build
lead: Your plot is a blank canvas. Lay out roads, parking, a fence with a gate, and the buildings themselves; builders and trucks do the work.
description: Building the site in Datacenter Architect, roads, fences, buildings, floors, basements and the build process.
---

## The plot

Every game starts on a plot of 64 × 64 m next to a public road with three carrier pits in the pavement. Later you can buy more land, up to 256 × 256 m (Growth › Locations), and open sites in other countries.

The build bar along the bottom holds everything you place in the world:

| Category | What is in it |
|---|---|
| Land | Asphalt, parking, footpaths, bike paths, curved roads and paths, the building materials yard, grass |
| Security | Fences (mesh, welded, palisade, wall, electric after research), sliding gates, pedestrian gate, barrier, guard booth, intercom, camera poles, bollards |
| Buildings | Datacenter, office, warehouse, doors (single, double, fire, badge, roll-up, mantrap), freight lift, passenger lift, stairs |
| Rooms | All room types, see [Rooms]({{ '/rooms/' | relative_url }}) |
| Interior | Desks, chairs, meeting tables, coffee and vending machines, sofas, terminals, plants, cabinets, lab benches, NOC and security video walls |
| Data halls | Racks (single, row or block), crash carts, camera domes |
| Power | Transformers, switchgear, UPS, batteries, PDUs, generators, diesel tanks (above or under ground), solar, wind, battery containers |
| Cooling | CRAH units, chillers, cooling towers, dry coolers |
| Network | Fiber pit, ODF, core router, spine switch |
| Outside | Trees, shrubs, lamp posts, benches, picnic tables, terrace sets, bike racks and sheds, EV chargers |

> **Undo is your friend.** Every build action can be undone with <kbd>Ctrl</kbd>+<kbd>Z</kbd> and redone with <kbd>Ctrl</kbd>+<kbd>Y</kbd> (or the buttons above the bulldozer). The money comes back too.
{: .tip}

## Roads, parking and the way in

- **Asphalt** carries trucks and cars. From 6 m wide a road gets a dashed centre line and traffic keeps right; trucks drive back along their own lane.
- **Parking** gets real bays of 2.5 × 5 m. From 10 m deep you get two rows facing each other with a lane in between. Half of your staff want a parking place. **EV chargers** turn a bay into a green charging bay.
- **Footpaths and bike paths** let people walk and cycle in. Some staff come by bike and park it in a **bike rack** or **bike shed**.
- **The way in**: a fence round the site with a **sliding gate** where the driveway crosses it. A truck or customer reports at the **intercom**, the guard in the **guard booth** opens the gate, the customer parks and checks in at security. A **barrier** on the driveway works the same way.

{% include shot.html src="commute" caption="Parking with EV chargers, a bike shed and cyclists riding in. Commuting is pure background life." %}

## Buildings

There are three kinds of building. You drag out the floor plan; the build cost is per square metre per floor.

| Building | For | Notes |
|---|---|---|
| Datacenter | Data halls, technical rooms, meet-me room, loading dock | Closed concrete box. Can have a basement |
| Office | Director's offices, sales, NOC, SOC, canteen, reception, lab | Brick, concrete look, glass or cladding facade. Can have a basement. Empty office rooms can be rented out |
| Warehouse | Warehouse store with pallet racks, workbenches, shredder | Needed for your own hosting company and spare parts logistics |

### How a building goes up

1. You draw the plan. **Trucks** bring material to your **materials yard** (no yard or no road = no building).
2. **Builders** dig and pour the foundation, put up the walls and finally the roof. Mobile cranes and an excavator work on site.
3. When the building is finished you draw **rooms** inside it. Builders put up the inner walls; lights are added by themselves.
4. You place **doors** by pointing at a wall. Racks and large equipment only fit through **double doors** and **roll-up doors**.

{% include shot.html src="hero" caption="A big datacenter going up: the foundation with several cranes, material trucks on the driveway and builders on the parking lot." %}

Want to sketch first and pay later? Press <kbd>B</kbd> for **plan mode**: everything you draw becomes a cyan blueprint, nothing is paid yet and builders stay away. The bar at the top shows the total cost; **Build plan** pays and starts everything at once.

{% include shot.html src="plan" caption="Plan mode: a second datacenter drawn as a blueprint, with the total cost in the bar at the top." %}

### Floors, stairs and lifts

Buildings can have up to four floors. Use <kbd>Page Up</kbd> and <kbd>Page Down</kbd> (or the floor button top right) to switch floor; everything above is hidden.

- **Freight lift** (2 × 2): needed to bring racks and equipment to another floor. People may use it too.
- **Passenger lift** (2 × 2) and **stairs** (2 × 5): for people. Walkers pick the cheapest route by themselves.
- Lifts and stairs run through every floor of the building, so place them where they fit on all floors.

**Add a floor later**: click a finished building and press **Add floor** in the inspector. Scaffolding and a crane appear and the new storey goes up while the building keeps working.

{% include shot.html src="raise" caption="An extra floor going up on a finished office, with the building inspector open." %}

### Basement

Datacenters and offices can get a **basement**: tick the basement box when you drag out the building. It is a good place for transformers, switchgear, UPS and batteries, so the ground floor stays free for halls. Rooms that need daylight or a street door (loading dock, reception) cannot go underground, and a basement has no outer doors: connect it with stairs and a freight lift.

{% include shot.html src="basement" caption="The basement view: the soil is cut away and the outlines of the buildings above are drawn. Transformer, switchgear, UPS and battery rooms fit nicely down here." %}

## Offices furnish themselves

When an office room is finished, the interior designer furnishes it in a style (standard, tech or formal): desks with screens, plants, art or dashboards, a rug. The room inspector shows a checklist of what the room still needs. You can always add or move furniture yourself.

{% include shot.html src="office" caption="A furnished office building: director's office, offices, meeting room, canteen, customer lounge and reception." %}
