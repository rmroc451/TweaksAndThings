# Freight, industries, held deliveries and pickup penalties

This guide explains the accounting around simulated interchange work. Native waybills and industry demand remain central: the mod schedules physical deliveries, tracks deferred work and adds explicit incentives or missed-pickup consequences.

Related guides: [interchange operation](INTERCHANGE-SERVICES.md), [switch-list and car tools](PLAYER-TOOLS.md), [timetable controls](TIMETABLE-AND-TIME.md), and [troubleshooting](TROUBLESHOOTING.md).

## Held delivery versus delinquent pickup

| Situation | Held delivery | Delinquent pickup |
| --- | --- | --- |
| Car's intended direction | Into your railroad for an industry | Out through an interchange or off-map loader |
| What went wrong | Destination/spawn capacity prevented its planned delivery | A reserved pickup was absent from its recorded pickup span |
| What remains saved | Order, destination, type/load, car identity when available, and bonus timing | Car identity, waybill identity, pickup spans and missed-attempt history |
| Player's next job | Receive it on a later service and deliver it to its industry promptly | Return it to its proper pickup span before the next attempt |
| Financial effect | A declining delivery premium | First warning; second miss forfeits/reclaims that car's delivery payout |

The two lists describe different work. A yard-capacity deferral does not by itself penalize an outbound car, and putting a delinquent pickup into an industry does not satisfy its interchange pickup requirement.

## Held deliveries prevent duplicate industry orders

When planned deliveries cannot fit, they remain reserved instead of disappearing from demand accounting. Reservations survive saves and are restored to native pending orders before industries ask for replacement cars. Available storage, pending waybilled deliveries and held orders therefore contribute to covered demand.

Before departure, cars can be removed from the planned consist until the native destination fitter accepts what remains. After departure, a newly full yard can cause physical inbound cars to be stored off-map through the native bardo backend. Their identity, load and waybill are retained for a later delivery.

Use **Held deliveries** in either traffic view to inspect the railroad-wide list. The individual **Interchanges** sections show work associated with that interchange. The industry's **Company → Locations** panel shows its missed scheduled deliveries and what was ordered. Once a held car is dispatched, its inspector shows the countdown and payout near its destination information.

Descriptions include the intended car type/load and destination where known. Undispatched payout amounts are estimates; spawning a physical car with a native waybill updates the estimate to its actual amount. Remaining-time summaries can round to the nearest hour and display approximately “~hh remaining”; use the deadline and detailed countdown when a delivery is close to expiry.

## The declining delivery premium

Defaults are a four-game-hour delivery window after the next interchange service and a 100% starting premium. “100% premium” means one extra base payout, so the total starts at twice the normal base payout. It does not mean a flat $100 charge or two extra payouts.

For base payout B, premium fraction P, start S and deadline D, the extra amount declines with the remaining fraction of the window:

~~~text
extra premium = B × P × clamp((D − current game time) / (D − S), 0, 1)
total before separate native adjustments = B + extra premium
~~~

Example with a $1,000 base payout, 100% premium and four-hour window:

| Time elapsed after the bonus starts | Extra premium | Base plus premium |
| --- | ---: | ---: |
| 0 hours | $1,000 | $2,000 |
| 1 hour | $750 | $1,750 |
| 2 hours | $500 | $1,500 |
| 3 hours | $250 | $1,250 |
| 4 hours or later | $0 | $1,000 |

The premium is earned by delivering to the industry's waybill destination. Receiving the car at the interchange is not that final delivery. Native contract bonuses and condition fines remain separate, so actual accounting may differ from the simple example.

Configure **Held delivery deadline (hours)** and **Held delivery premium (%)** under traffic Settings. The current UI permits 1–72 hours and 0–100% premium. Timewarp and simulation fast-forward advance game time and therefore reduce the bonus. Reloading, another capacity deferral, or another missed service does not restart the original bonus clock. An earlier extra service can advance the start rather than provide a fresh later deadline.

## Reserved pickups and missed attempts

An incoming service records eligible pickup cars when it dispatches. The record includes the waybill's destination and creation identity and the pickup span IDs. At arrival, the game checks that the same reserved work is still present. This helps distinguish a moved car from a later unrelated waybill using the same car.

The first missed pickup sends a telegraph warning. Return the car to its recorded pickup track before another service tries to collect it. On the second missed attempt, that car's delivery payout is forfeited. If it was already credited, the mod reclaims that credited amount through the balance ledger. Saved history avoids repeatedly reclaiming an already forfeited bill on each refresh or reload.

Use the timetable's **Delinquent pickups** tab or the traffic view's identically named tab. The list covers all train crews, not just the currently selected crew. Rows show car identity, return interchange, payout status and an estimate until the next pickup attempt. Locate actions help find the car or interchange; **+ List** adds the car to your current crew's switch list. The console command **/npcPickups** also opens the listing.

The countdown is an estimate derived from active progress or the next native service time. A blocked route or full train can delay the actual attempt. Intentionally excluded excess pickups are left for another service without a missed-pickup penalty. New cars arriving while a service is en route may be accepted within its 90% power and passing-length limits.

## Purchased coal and other off-map loader cars

Player-owned cars waybilled to an interchange's off-map industry loader are eligible for that loader's pickup tracks. This includes purchased coal cars waiting to be taken off-map. Their loader destination need not match the ordinary interchange export destination; the mod records both correctly.

Native filling charges and return scheduling continue to apply. A normal owned car without a qualifying loader destination is only exported under the existing sell-waybill rule. If an expected pickup is missing from a train's count, inspect its exact waybill destination, its pickup span and whether another active pool service has already reserved it.

## Order pulpwood explicitly

Industries with an active pulpwood unloader show **Order pulpwood from interchange** in their industry screen. Choose 1–50 requested cars and review the estimated ordering fee. The host places the order; multiplayer clients are directed to ask the host.

The requested count is limited to unmet demand. Existing storage, cars already on order and restored held deliveries count toward that demand. If those already cover the industry, no extra order is placed. An enabled interchange capable of serving the industry is required.

The additional fee defaults to 10% of load value and is configurable through **Pulpwood ordering fee (%)**. The implementation values the load using the greater of its defined purchase cost and pay-per-quantity value. Booking uses nominal car capacity; fulfillment adjusts the charge to the actual car capacity. For a $1,000 estimated load value and a 10% setting, the added ordering fee is $100 per car, before ordinary native costs.

Explicit orders enter the native interchange backend and use normal waybills, delivery charges, yard capacity and service availability. They are not an instant placement action. They work with either interchange mode's backend; simulated mode physically carries fitting orders on a service train. A pulpwood definition with no positive value cannot support a nonzero calculated ordering fee and produces an explanatory message.

## Switch-list accounting

The car inspector's **To Switch List** action includes ordinary cars, unwaybilled stock and locomotives/tenders with repair or overhaul destinations. An unwaybilled entry is shown as **No waybill / Unassigned**. A repair work order shows its repair destination even when the car also has a freight waybill.

Adding a car to the switch list is a planning action; it does not manufacture a waybill, change the car's destination or guarantee payment. The host verifies visible additions before confirmation. Clients can show a pending request while awaiting the host's result.

For a grouped breakdown, open **Waybill summary** from a caboose inspector or the switch list's tools menu. It shows counts, loaded/empty cars, tonnage and length by destination and current location. Its **Current switch list** tab follows your current crew, unlike the railroad-wide delinquent list.

If an order, premium or penalty looks incorrect, capture the car's ID, destination, waybill and ledger entry together with **Write debug snapshot**. Follow [the bug-report checklist](TROUBLESHOOTING.md), then [open or search a GitHub issue](https://github.com/rmroc451/TweaksAndThings/issues).
