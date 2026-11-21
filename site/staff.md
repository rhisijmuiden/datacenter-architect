---
title: Staff and incidents
kicker: Run
lead: Managers hire, roster and train their own people. You set the policies and service plans; the team keeps the site running day and night.
description: Staff, policies, maintenance, faults and events in Datacenter Architect.
---

## A team that runs itself

Open **Staff** (<kbd>F5</kbd>). You never pick candidates or fill rosters. Every department gets an **advice** (for example one cooling technician per 20 cooling units, one electrical engineer per 30 parts of the power chain, one support engineer per 150 racks) and its manager hires up to that advice by themselves, with a name, a level (junior, medior, senior) and sometimes a trait (fast, precise, night owl, loyal).

| Department (manager) | Roles |
|---|---|
| Facility | Electrical engineer, cooling technician, generator mechanic, fire safety officer |
| Network | Network engineer, NOC operator |
| Operations | Support engineer, remote hands, receptionist, warehouse worker, cleaner, canteen worker, gardener, IT helpdesk |
| Security | Guard, SOC analyst |
| Construction crew | Builders, fitters, engineers (rented through a contractor) |

Some roles need a room: NOC operators a NOC, guards a security room or guard booth, the receptionist a reception, canteen staff a canteen, researchers a lab, SOC analysts a SOC.

{% include shot.html src="staff" caption="Staff › Team: every department with its advice, the people on staff, their level and shift." %}

## Policies and service plans

Under **Staff › Policy** you steer the team without touching individuals:

| Policy | Choices |
|---|---|
| Staffing | Tight (75%), normal, generous (125% and every shift covered) |
| Pay | −10%, market, +15% |
| Training budget | € 0 to € 20,000 per month; lifts people a level (junior 0.8×, medior 1×, senior 1.25× speed) |
| Preventive maintenance | Light, normal, thorough |

**Service plans**: 24/7 customer support (happier customers, reputation), 24/7 NOC (every fault seen at once) and a remote hands service (extra income per rented rack).

Shifts run day (07–15), evening (15–23) and night (23–07). 24/7 roles are spread over the shifts by themselves; night owls take the nights.

## Happiness

Staff care about pay, a **canteen** (better with a canteen worker), a coffee or vending machine, **parking** for half of them, enough desks, an IT helpdesk from twelve office people, the training budget and some green around the site. Lots of work and night shifts count against it. Below 30% people may leave; the manager replaces them.

## Wear, faults and repairs

- Racks, CRAH units, chillers, cooling towers, generators, UPS units, batteries, PDUs, transformers, switchgear, routers, spines, ODFs and fiber pits **wear** a little every day. New equipment does not break; from about 5% wear the chance rises quickly.
- Technicians keep things in shape. Without **cleaners** (one per two halls) hall equipment wears 40% faster.
- A **broken** part stops working at once, and the power chain, cooling or network notices it. Without a NOC operator on duty someone notices after two hours. A free technician on shift walks over and repairs it; you can watch them work.
- No one on staff for the job? After 12 hours an **external mechanic** comes (with an orange helmet, at a cost). You can also call one straight away.
- **Spare parts** (server, cooling, electrical, UPS batteries, optics) are ordered automatically when stock drops to the minimum. A repair without a part takes three times as long.
- UPS units and batteries last five years, then they are replaced at half the price.

{% include shot.html src="incidents" caption="Staff › Faults and events: open faults with who is on the way, and the log of events." %}

## Events

With **random events** on (new game setting):

| Event | What happens | Be ready with |
|---|---|---|
| City power outage | 2 to 8 hours without grid | Generators behind switchgear, diesel, a generator mechanic |
| Heatwave | 20% less heat rejection, cooling wears faster | Spare cooling capacity |
| Cable cut | A digger cuts a fiber route | Two fiber pits on different street pits |
| Fire alarm | Without a fire safety officer the gas system goes off: hall down for 4 hours, € 35,000 | A fire safety officer |
| Customer bankruptcy | A customer disappears | Spread over many customers |
| Press visit | Reputation +2 or −1 | A tidy, well run site |

In creative mode **no breakdowns** switches off wear faults and cable cuts.

{% include shot.html src="staff-site" caption="During a city outage: the generators run, a technician walks to a broken CRAH unit and the inspector shows its state." %}
