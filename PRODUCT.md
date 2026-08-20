# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

**Clients (primary, no account).** Mixed traffic: regulars of the shop rebooking from a
WhatsApp message or the Instagram bio link, and new clients who land cold and must be
convinced before they book. Mobile-first in both cases. A regular wants the shortest path
from link to confirmed slot; a newcomer needs to see the shop is real and worth the trip
before entering the wizard. Neither ever creates an account — identity per booking is
name + WhatsApp phone, and the only handle on an existing appointment is the opaque
`AccessToken` link.

**Barbers (secondary, authenticated).** Working the floor between cuts. They open the
dashboard on a phone, glance at what's next, and mark appointments completed or cancelled
with hands that are busy. Scanability beats density.

**Admin (secondary, authenticated).** Manages every barber's agenda, the service catalog,
and the WhatsApp/notification configuration.

## Product Purpose

Replace WhatsApp back-and-forth as the booking mechanism for one physical barbershop.
A client picks barber, service(s) and slot themselves; the barber's agenda fills without
anyone typing "tem horário amanhã?". Success is a booking completed without a single
message exchanged, and a barber whose day is visible at a glance.

## Positioning

Booking with **no account, ever**. No signup wall, no password, no app install — a link,
four steps, done. The appointment is owned by a token URL the client keeps, which is also
where they cancel and later leave a review. A neighboring product that requires
registration cannot copy this without becoming a different product.

## Operating Context

- Traffic arrives from Instagram bio and WhatsApp links — external, mobile, one tap in.
- Booking is a 4-step wizard at `/agendar`: service(s) → barber → slot → name + phone.
- Total appointment duration is the **sum** of the selected services' durations, so slot
  availability shifts with the cart.
- Post-booking life happens entirely at `/agendamento/[token]`: view, cancel (only while
  `Accepted` and more than 2h out), and review (only once `Completed`).
- Notifications go out over email and/or WhatsApp (Evolution API), configured at runtime
  by the admin, sent asynchronously off the request.
- Barbers work the dashboard mid-shift, on a phone, standing.

## Capabilities and Constraints

- All UI copy is Brazilian Portuguese. Dates `DD/MM/YYYY`, currency `R$ X,XX`.
- Six seeded global services (Corte, Fade, Barba, Sobrancelha, Hidratação, Pigmentação) —
  the catalog is shop-wide, not per barber.
- Appointments are auto-confirmed (`Accepted`) at creation; there is no approval step.
- Anti-spam is real and user-visible: 5 bookings/hour per IP, 3/hour per phone. The UI has
  to explain a refusal that is not the client's fault.
- One barber cannot hold two overlapping `Accepted` appointments; a DB constraint on
  `(BarberId, ScheduledAt)` can reject a booking after the client hit submit.
- No client accounts exist and none are planned — any design assuming "my bookings",
  history, or a logged-in client is out of bounds.
- Single-shop today: one location, real barbers, one service catalog.

## Brand Commitments

Name: **O Imperador Barber Shop**. Existing tokens (`frontend/src/app/globals.css`):
gold `#C9A84C` / `#E8C96A` / `#A8872E`, black `#0D0D0D` / `#1A1A1A`, white `#F5F5F5`;
Montserrat headings, Inter body. Logo at `frontend/public/logo.png`.

## Evidence on Hand

**Logo only.** `frontend/public/logo.png` is the single real asset.

Not available and **must not be fabricated**: shop photography, haircut/work photos,
barber portraits or bios, client testimonials, ratings, review counts, awards, years in
business, client numbers, address, hours, prices beyond the seeded catalog. A landing page
must persuade a cold visitor without inventing any of these — ask for them or design
around their absence.

Real barbers and real reviews do exist in the running system as data; their content comes
from the API at runtime, never from hardcoded copy.

## Product Principles

1. **No account, no friction.** Never introduce a step that implies an identity the client
   does not have.
2. **The link is the account.** The token URL must survive being pasted into WhatsApp and
   opened weeks later on a cheap phone.
3. **Mobile is the product, not a breakpoint.** Clients and barbers both arrive on phones.
4. **Say what the system did.** Rate limits, slot collisions, and the 2-hour cancel window
   are refusals users can't predict — every one gets a plain-Portuguese explanation.
5. **Don't invent the shop.** Absent proof is designed around, never filled with plausible
   copy.

## Accessibility & Inclusion

No product-specific standard established. Baseline assumption from the audience: low-end
Android phones, one-handed use, outdoor/indoor glare against a near-black background.
