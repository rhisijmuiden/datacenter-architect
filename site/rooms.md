---
title: Rooms
kicker: Build
lead: Inside a finished building you draw your own rooms. Every room type has a job, and most of them need certain equipment inside before they do it.
description: Every room in Datacenter Architect, what it does, what goes inside and what it needs.
---

## How rooms work

1. Open **Rooms** in the build bar, pick a type and drag it inside a finished building, on the floor you are viewing. Builders put up the inner walls.
2. Put a **door** in a wall (Buildings › Doors). People and racks only get in through a door; racks and big equipment need a **double door** or a **roll-up door**.
3. Furnish the room (Interior, Power, Cooling, Network). Equipment that belongs in a specific room can only be placed there: a UPS only goes in a UPS room, a core router only in the meet-me room.
4. Click the room to open the **inspector**: it shows what the room does, what it holds and what it still lacks.

Rooms connect through **corridors**. Keep a corridor running from the loading dock to every hall and technical room, so fitters can wheel equipment in.

## Datacenter rooms

### Data hall
**What it does:** holds the racks you rent out. This is where the money is made.<br>
**Put inside:** racks in rows, **CRAH units** for cooling, **PDUs** (or a busbar upgrade) for power, a **spine switch** for the network. Optional: crash carts, camera domes.<br>
**Needs:** a double door, a free aisle in front of and behind every rack, power from the chain, enough cooling, and a network path.<br>
**Options in the inspector:** zone (full racks, colocation per U, cages, private hall or own cloud), which customer types the hall takes, containment upgrade (hot/cold aisles, cold corridor), busbar.

{% include shot.html src="hall" caption="A data hall with rows of racks under orange fiber trays, CRAH units along the wall and the loading dock next door. The inspector on the right shows a rack that is live, running, online and for sale." %}

### Meet-me room (MMR)
**What it does:** the network heart of the building. Carriers, the internet exchange and peers connect here; customers order cross-connects to them.<br>
**Put inside:** **ODFs** (patch frames, 48 ports each) and **core routers** (32 ports, 400 Gbit/s).<br>
**Needs:** a fiber pit outside. Tip: a badge door, because certificates and security-minded customers look at it.

### Transformer room
**What it does:** steps down medium voltage from the grid. Needed once you upgrade the grid connection beyond the compact 2 MVA station.<br>
**Put inside:** **transformers** (2.5 MVA each).

### Switchgear room
**What it does:** main distribution. The switchgear has the **ATS** that switches to the generators when the grid fails. No switchgear = generators cannot take over.<br>
**Put inside:** **switchgear** (4 MW each).

### UPS room
**What it does:** bridges a power cut until the generators run.<br>
**Put inside:** **UPS** units (500 kW each) and optionally **battery cabinets**.

### Battery room
**What it does:** room for the UPS batteries.<br>
**Put inside:** **battery cabinets** (40 kWh each). Batteries can also stand in the UPS room.

### Loading dock
**What it does:** trucks deliver racks, equipment and customer servers here.<br>
**Needs:** an outer wall with a **roll-up door** next to the asphalt. Every roll-up door gets its own numbered loading bay; more doors means more trucks at once. Cannot go in a basement.

### Storage
**What it does:** stock of spare parts, servers and pallets.<br>
**Put inside:** pallet racks, workbenches, pallet jacks, a shredder.

### Corridor
**What it does:** connects rooms. People, trolleys and racks move through corridors. Camera domes can hang here.

## Office rooms

| Room | What it does | Put inside | Needed when |
|---|---|---|---|
| Director's office | Workplace for one director or manager | Desk and chair (a plant is nice) | One per director, from the CEO onwards |
| Office | Workplaces for sales, support and helpdesk | A desk per person | When you hire sales people and support staff |
| Meeting room | Meetings | Meeting table, chairs | From 6 people in the company |
| Staff canteen | Staff happiness | Coffee machine, vending machine, microwave, tables | From 10 people; a canteen worker makes it better |
| Customer lounge | Visiting customers wait and work here | Sofas, terminals, vending machine, coffee | From 25 people; customers like it |
| Reception | Receives customers and visitors | Glass lodge or desk | Always one; a receptionist works here |
| Security room | Guards watch the site | Security video wall, glass lodge | For guards (or a guard booth at the gate) and a higher security level |
| NOC | Network operations centre: watches power, cooling and network 24/7 | NOC video wall, desks | For NOC operators; more traffic over the IX and peering, cheaper burst |
| SOC | Security operations centre against cyber attacks | SOC desks | For SOC analysts and the 24/7 SOC |
| R&D lab | Research | Lab benches, one researcher per bench | For the research tree |
| Warehouse store | Servers for your own hosting company, spare parts | Pallet racks (24 servers each), workbenches, pallet jacks, shredder | In a warehouse building |

{% include shot.html src="office" caption="Office rooms furnish themselves in a style. The inspector shows what the office still needs, for example a meeting room once you have six people." %}

> **Which rooms do I need first?** Data hall, loading dock, switchgear room, UPS room, meet-me room and a corridor in the datacenter, plus a director's office and an office for sales. Everything else comes when the game asks for it: the Overview shows advice when a room is missing.
{: .tip}

## Doors

| Door | Width | Use |
|---|---|---|
| Single | 1 | Offices, staff |
| Double | 2 | Halls; racks fit through |
| Fire | 1 | Between hall and UPS or battery room |
| Badge | 1 | Halls, MMR, customer areas; counts for the security level |
| Roll-up | 3 | Loading dock and warehouse; racks and trucks |
| Mantrap | 1 | Entrance to the secure area, after research |

Doors are closed and open by themselves when someone walks up to them.
