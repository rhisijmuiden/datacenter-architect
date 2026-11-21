---
title: Tips and tricks
kicker: Help
lead: Things that make building easier, keep your racks online and save money. Collected from playtests.
description: Tips and tricks for Datacenter Architect.
---

## Building

- **Start small, grow later.** A building of about 20 × 14 m is plenty for the first customers. You can always add a building, a basement or an extra floor.
- **Plan first with <kbd>B</kbd>.** Plan mode lets you draw a whole new building with rooms, racks and equipment for free, see the total cost, and build it in one go when the money is there.
- **Undo anything** with <kbd>Ctrl</kbd>+<kbd>Z</kbd>; the money comes back.
- **One wide drag fills a hall.** The rack tool makes pairs of rows with cold and hot aisles by itself. Hold the drag and watch the count, kW and cost in the ghost.
- **Leave aisles along the walls.** Every rack needs a free cell in front and behind. A row pushed against a wall stays unreachable.
- **Keep a corridor from the dock to every hall**, with double doors. Racks travel by trolley.
- **Put heavy plant in the basement.** Transformers, switchgear, UPS and batteries underground keep the ground floor free for halls.
- **Several roll-up doors = several trucks at once.** Each roll-up door gets its own loading bay.
- **Chillers on the roof** save space on the ground. Solar panels and terrace sets can go on roofs too.
- **Roads of 6 m or wider** carry two-way traffic. A narrow driveway works, but trucks wait for each other.
- <kbd>T</kbd> hides the roofs, <kbd>Page Up</kbd>/<kbd>Page Down</kbd> switch floor, <kbd>Q</kbd>/<kbd>E</kbd> turn the camera by 90°.

## Keeping racks online

- **Look at the bars at the top.** Power, backup, cooling and network show load against capacity. If one gets close to full, act before it is.
- **Use the overlays.** <kbd>P</kbd> power, <kbd>H</kbd> cooling, <kbd>N</kbd> network. Red shows where it is stuck.
- **Follow the advice on the Overview** (<kbd>F1</kbd>). Every piece of advice has a button that opens the screen that fixes it.
- **Order the grid upgrade early.** It takes 30 to 120 days, plus the queue with grid congestion on. Ask for it when you are at about 60% of the current connection.
- **Never forget the switchgear.** Without it there is no ATS, so the generators cannot take over in an outage.
- **Count diesel in hours, not litres.** The power screen shows fuel hours; aim for a full day at least.
- **A spare of everything.** One extra UPS, one extra CRAH per hall, one extra generator, a second carrier, a second spine. It is the difference between a message and an outage.

## Redundancy in one minute

| You want | Do this |
|---|---|
| **N+1** on a link | Units for the load, plus one. The game removes the biggest unit and checks the rest still carries everything |
| **2N** on a link | Total capacity at least twice the load, with at least two units |
| **A/B** in a hall | Two or more PDUs or a busbar, so the hall survives losing its biggest feed |
| Generators N+1 | One generator more than the site needs (IT × PUE + other) |
| Network redundancy | Two carriers, two fiber pits on different street pits, two core routers, two spines per hall |

Full explanation with examples on [Power and redundancy]({{ '/power/' | relative_url }}).

## Money

- **Sign customers that fit.** A request with only ! and ✓ can be signed; a ✕ means you would break a promise from day one.
- **Watch the SLA.** Downtime costs a penalty and satisfaction. A customer below 15 leaves at once.
- **Containment pays for itself.** Hot/cold aisles bring the PUE from 1.65 to 1.50 for € 120 per m²; the energy bill drops straight away.
- **Procurement manager**: 5% off energy and 10% off carriers, more with training.
- **IX and peering** carry part of your traffic for free or cheap; less transit to pay.
- **Remote hands** service plan earns extra per rented rack.
- **Rent out empty offices** with an office manager.
- **Fixed or variable energy?** Fixed is calm; variable follows the market and can be cheaper with batteries that trade for you.

## Staff

- **Do not hire individuals; set policies.** If a department is short, raise its staffing level to generous or add a service plan.
- **A canteen, a coffee machine and parking** keep people happy and stop them leaving.
- **Cleaners** (one per two halls) make hall equipment last 40% longer.
- **A NOC operator on shift** means faults are seen at once instead of after two hours.
- **A fire safety officer** prevents the gas system from taking a hall down for four hours.

## Just enjoying it

- Press <kbd>F</kbd> and walk through your own halls at night.
- Press <kbd>F9</kbd>, turn on the cinematic camera and let it float.
- Switch day/night and weather on or off with the sun and cloud buttons, any time.
- Watch the timelapse of your campus growing under Progress › Timelapse.
