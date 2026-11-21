---
title: Network and fiber
kicker: Systems
color: fiber
lead: Customers pay for racks that are online. Bring fiber onto your site, sign carriers, and build the path from the street pit to every rack row.
description: Network and fiber in Datacenter Architect, carriers, the internet exchange, peering, core routers and spine switches.
---

## The path to the internet

<ul class="chain fiber">
<li><b>Street pit</b>A, B or C in the pavement</li>
<li><b>Your fiber pit</b>on the plot edge</li>
<li><b>ODF</b>patch frame in the MMR</li>
<li><b>Core router</b>in the MMR</li>
<li><b>Spine switch</b>in every hall</li>
<li><b>Rack</b>online</li>
</ul>

A rack is **online** when it has a port on a spine switch in its own hall, that spine has a port on a core router, and the core reaches a carrier, the internet exchange or a peer through the ODFs in the meet-me room. Racks that run but are offline earn no rent.

| Equipment | Where | Ports | Capacity |
|---|---|---|---|
| Fiber pit | Outside, near the road | 4 subducts (8 and 12 after upgrades) | 144 fibers per cable |
| ODF (patch frame) | Meet-me room | 48 | Every carrier and cross-connect takes a port |
| Core router | Meet-me room | 32 | 400 Gbit/s |
| Spine switch | Data hall | 48 | 400 Gbit/s |

{% include shot.html src="network-overlay" caption="Network overlay (N): orange lines and dots run from the street pits through your fiber pit and ducts to the meet-me room, the core routers, the spines and the rows. The busier, the faster the dots; red means full or offline." %}

## Fiber pits and routes

Three carrier pits lie in the pavement of the public road: **A**, **B** and **C**. Your own fiber pit connects to the nearest one with a duct. Every carrier, IX port and dark fiber takes one **subduct** in your pit; upgrade the pit for more.

Two pits that connect to **different street pits** give you **two separate routes**. If a digger cuts one cable, the other keeps you online.

## Carriers, the IX and peering

Under **Infrastructure › Network** (<kbd>F4</kbd>) you sign contracts. A port takes 20 days to deliver (an upgrade 10).

| Carrier | Pit | Price per Mbit | Character |
|---|---|---|---|
| Lowlands Transit | A | € 0.32 | Tier 1 |
| Kanaal Net | A | € 0.29 | Regional |
| NordFiber | B | € 0.26 | Budget |
| Eurolink | C | € 0.38 | Premium |
| Polder-IX | C | port only | Internet exchange |

- You order a **port** (1G, 10G, 100G or 400G) with a **commit**: you pay the port and the commit every month. From a 10G commit you get 15% off, from 100G 30%.
- **Burst**: traffic above the commit is billed at the 95th percentile, at 1.5 × the price (1.2 × with a NOC). The busiest 5% of hours do not count.
- The **internet exchange** costs only the port and takes about a quarter of your traffic for free, until the port is full.
- **Peering**: with AI rivals switched on, rival datacenters ask to peer over dark fiber. Some rent a rack in your MMR and pay you, bigger ones ask a monthly fee. A peer takes 7 to 10% of your traffic.

Traffic goes to the IX and peers first and the rest over transit. Each online rack does about 0.25 Gbit/s, quiet in the morning and busiest around 20:00. If traffic does not fit, customers pay less rent for the part they lose.

{% include shot.html src="network-panel" caption="Infrastructure › Network: carriers, ports and commits, the IX, peering requests and warnings for single points of failure." %}

## Network redundancy

The network screen shows N, N+1 or 2N per link and warns about **single points of failure**:

- only **one carrier** – sign a second one (customers often ask for two or more carriers);
- only **one route** – a second fiber pit on another street pit;
- only **one meet-me room** – a second MMR on the other side of the building;
- only **one core router** – add a second;
- a **hall with one spine** – add a second spine to that hall.

## Cross-connects and the NOC

- **Cross-connects**: the more networks in your MMR, the more customers order a cross-connect from their rack to a carrier. An engineer patches it at an ODF (a free port is needed). Each one brings € 250 once and € 60 a month.
- **NOC**: a NOC room with a **video wall** watches the network. More traffic goes over the IX and peering, burst gets cheaper and you get a warning when transit is over 85% at peak time. With the 24/7 NOC service plan every fault is seen right away.

## Network checklist

1. Fiber pit near the road, a meet-me room with ODFs and a core router.
2. A spine switch in every hall (48 racks per spine, two for redundancy).
3. Two carriers on two routes, plus the IX.
4. Commit a bit above your average traffic; burst handles the peaks.
5. Upgrade ports before the NOC warns you at 85%.
