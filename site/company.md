---
title: Company and customers
kicker: Run
lead: You start as the owner. Hire a CEO and directors, and they run the company for you. Sales brings in customers, you decide what to sell where.
description: The organisation, customers, contracts, tenders and money in Datacenter Architect.
---

## The organisation

Everything starts with a **director's office** with a desk and a chair. Then you can hire a **CEO** under **Company › Organisation** (<kbd>F2</kbd>). Each director and manager unlocks something and needs a furnished office of their own.

| Role | Needs first | Unlocks |
|---|---|---|
| CEO | – | Everything else |
| CFO | CEO | Loans, investors, going public |
| HR manager | CEO | Hiring beyond the starting crew (4 builders, 2 fitters, 1 engineer) |
| Sales director | CEO | Customers. Sales people (one desk each) bring more and bigger requests |
| Legal counsel | Sales | Bidding on tenders |
| Customer success | Sales | Happier customers, more renewals |
| Marketing | Sales | More requests, good reviews count more |
| Procurement | CFO | Cheaper energy and carriers |
| Operations manager | CEO + HR | Faster crews; unlocks the managers below |
| Facility manager | Operations | Grid upgrades to 10, 20 and 50 MVA; power and cooling staff |
| Network manager | Operations | Carriers, IX, peering; network staff |
| Security manager | Operations | Guards, security level, demanding customers |
| Project manager | Operations | Builders and fitters work faster |
| Office manager | Operations | Renting out empty offices |
| Sustainability | Operations | ESG |
| Compliance officer | CEO | Certificates |
| R&D manager | CEO | Research |
| Hosting director | CEO + Sales | Your own hosting company |
| International director | CEO + CFO | Sites in other countries |

Every role has a hiring fee, a monthly salary and two trainings that make them better. With **all unlocks** in creative mode the whole tree is open.

{% include shot.html src="company" caption="Company › Organisation: the tree of directors and managers, with hiring and training." %}

## Customers

Your **sales director** brings in requests (up to eight at a time, each valid for 12 to 22 days). Open **Customers › Sales** (<kbd>F3</kbd>) to see them. Each request shows its requirements with ✓ (met), ! (warning) or ✕ (not met). You can sign as soon as there is no ✕ left.

{% include shot.html src="customers" caption="Customers › Sales: incoming requests with their requirements, size, products and value." %}

### Customer types

Startups, small businesses, hosting companies, web hosting, video streaming, social media, crypto mining, cloud services, municipalities, ministries, and through tenders banks, hyperscalers and AI companies. Each type has its own size, products, traffic per rack, number of carriers, and a chance of asking for 32 A, 2N, green power, security or certificates. Bigger and more demanding customers come with a higher **reputation**.

### What you sell: hall zones

Per data hall you choose what you sell (in the hall inspector or Company › Sales):

| Zone | Product | Price per month |
|---|---|---|
| Full racks | 16 A (3.7 kW) or 32 A (7.4 kW) rack | € 780 / € 1,250 |
| Colocation per U | Units of a rack, 42 U per rack | € 42 per U |
| Cages | Several racks for one customer, inside one hall | +15% |
| Private hall | A whole hall for one customer | +10% |
| Own cloud | Racks for your own hosting company | – |

2N racks earn 10% more, and each customer type has its own price factor. You can also choose **which customer types a hall takes**, for example one hall only for banks and government and another for AI companies. Changing it never evicts anyone.

### Typical requirements

| Requirement | How to meet it |
|---|---|
| Free racks, units, cage or hall | Enough live, running, online racks in a hall with the right zone and customer types |
| A/B power | Two or more PDUs, or a busbar, with room for the biggest to fail. See [Power]({{ '/power/' | relative_url }}) |
| 32 A | A/B power and hot/cold aisles |
| 2N UPS | UPS capacity at least twice the IT load |
| Generators N+1 | One generator more than needed (a warning, not a blocker) |
| Bandwidth and carriers | Unsold transit capacity and enough carriers |
| Green energy | A green energy contract |
| Security | Security manager and a minimum security level |
| Certificates | ISO 27001, Tier III, PCI DSS, government seal or green label |

## Contracts

After signing, the customer gets fixed racks. Every day the game measures which share of those racks runs and is online; rent is paid in proportion. Downtime costs an **SLA penalty** and satisfaction. At the end of the contract a happy customer (60 or more) renews; an unhappy one leaves, and below 15 they leave straight away. Big customers move in with a convoy of moving trucks.

## Tenders

About once a month a big customer issues a **tender**: 24 to 200 racks, its own cage or hall, 2N, several carriers, 36 to 60 months. With legal counsel you bid 85, 100 or 115% of the asking price. After 10 days the customer decides; a lower price, better reputation and already meeting the requirements help, and AI rivals bid too. Won? Then you deliver within 90 to 180 days, or the contract lapses with a penalty and lost reputation.

## Money

- **Finances** (click the balance at the top): income and costs per month per line, and the result.
- **Loans**: up to € 2 million plus 24 months of revenue, at 6.5% (less with CFO training and good reputation), paid back over 60 months.
- **Investors**: sell 10% of the company at a time; you always keep at least 51%. Later you can **go public**.
- **Bankruptcy**: 30 days in the red and the game is over. Not possible with unlimited money.
