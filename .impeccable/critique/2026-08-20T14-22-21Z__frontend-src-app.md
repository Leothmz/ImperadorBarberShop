---
target: client journey end-to-end (landing, booking wizard, manage page)
total_score: 16
max_score: 40
na_heuristics: 
p0_count: 2
p1_count: 3
timestamp: 2026-08-20T14-22-21Z
slug: frontend-src-app
---
Method: dual-agent (A: design review, isolated · B: detector + browser evidence, isolated). Target: frontend/src/app — landing → booking wizard → manage page as one journey. No ignore list.

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 2 | Submit teleports to the manage page with no confirmation moment (agendar/page.tsx:83); slot grid never states which services/duration filter it |
| 2 | Match System / Real World | 3 | PT-BR, R$, DD/MM às HH:mm, "Deseja adicionar?" ring true; "Novo Agendamento" is admin language |
| 3 | User Control and Freedom | 1 | Irreversible cancel fires on one tap, no confirm (ManageAppointmentView.tsx:73); wizard state is pure useState, reload = restart |
| 4 | Consistency and Standards | 2 | shadow-[0_0_20px_rgba(201,168,76,0.2)] on a bordered card (BarberPicker.tsx:57); manage page re-implements AppointmentCard; focus rings only on Button |
| 5 | Error Prevention | 1 | No type="tel" on the phone field; changing services never clears selectedSlot (agendar/page.tsx:35-49) |
| 6 | Recognition Rather Than Recall | 2 | Step labels are hidden sm:block — on mobile the stepper is four naked digits (confirmed in screenshot); barber never re-shown on steps 2-3 |
| 7 | Flexibility and Efficiency | 1 | Zero fast path for the regular: no deep-link prefill, no "repetir último", no remembered phone, no keyboard nav on the slot listbox |
| 8 | Aesthetic and Minimalist Design | 3 | Token discipline is real and the summary card is handsome; undone by emoji cards and staff CTAs eating half the hero |
| 9 | Error Recovery | 1 | One sentence for 429, 409 and 500 alike (agendar/page.tsx:222); cancel failure renders nothing |
| 10 | Help and Documentation | 0 | No address, hours, phone, WhatsApp, FAQ; the 2-hour cancel rule is disclosed nowhere before booking |
| **Total** | | **16/40** | **Poor — structurally sound system, unfinished product** |

## Design Specificity Verdict

The system is authored; the composition is not. Genuinely this shop: the slot chip inverting to solid gold at the moment of highest stakes (SlotPicker.tsx:150-152), the stacked signage wordmark, "Deseja adicionar?" written as the barber's spoken question (ServicePicker.tsx:91), the notes placeholder "Ex: Prefiro o corte mais curto nas laterais...".

Category-interchangeable: the emoji feature trio (scissors/calendar/star) in three identical bordered cards is a 2019 template; the hero paragraph ships unchanged for a nail salon; the wizard chrome is every Stripe-checkout clone. Two of those cards assert what PRODUCT.md forbids — "anos de experiência" claims tenure the shop hasn't stated, "Avaliações reais de clientes" advertises reviews the page never displays.

Sharpest finding: real specificity was available for free and is thrown away. Barber.photoUrl and Service.photoUrl exist in the types and render in the admin panel, but the public surface shows a letter in a circle. useBarberReviews and barbersApi.getReviews are fully wired with zero call sites in any .tsx.

Deterministic scan: detect.mjs over all 7 targets — exit 0, zero findings. Verified as a real clean pass, not a targeting failure. Limitation documented: .tsx routes through the regex engine only, and the five page-level analyzers (flat-type-hierarchy, monotonous-spacing, dark-glow, buzzwords) are gated on isFullPage(), which no Next component satisfies. No rule in that path checks emoji-as-icon. Clean exit means "no rule fired," not "the design was checked."

Browser evidence: six full-page screenshots at 390x844 and 1440x900. No horizontal scroll anywhere. Five sub-44px interactive elements on every route, all in the header. Two agent readings corrected: (1) the reported "duplicate" a/button pairs are Link-wrapping-Button nesting in Header.tsx:31-33, producing invalid a > button; (2) the reported infinite spinner on an invalid token is a retry-window artifact (QueryProvider sets retry: 1) — the weaker finding stands: loading is unbounded, no skeleton, no timeout copy.

Not found by either agent: on mobile the hero h1 wraps "O" onto its own line in white above the gold "IMPERADOR", reading as a stray dot. The hero states the shop name three times (logo image, h1, tracked subline) under a header that states it a fourth. No user-visible overlay was produced; screenshots are the fallback signal.

## Overall Impression

A well-built machine with no manners. The engineering is honest — code comments record real bugs found by real use. The design system is disciplined enough that the product feels coherent. But the journey has no author: it collects a phone number without saying why, produces the client's only handle on their appointment and never tells them to keep it, and ends both exits in a 14px sentence and a wall.

Biggest opportunity: the access-token link is the product's entire positioning — "the link is the account" — and the interface treats it as a redirect target.

## What's Working

1. Token discipline that actually holds. Six brand tokens in globals.css; the #F5F5F5 opacity ladder (/80 labels, /60 prose, /50 metadata) applied consistently across four unrelated components. Status color centralized in statusConfig.ts, never inline.
2. The step-4 summary is the right answer to the riskiest screen (BookingConfirmation.tsx:72-112): barber, itemized prices, duration, date/time, gold-ruled total in one card. Recognition over recall exactly where money gets committed.
3. Code comments prove the flow was driven, not just written — four separate comments document a bug and its reasoning.

## Priority Issues

[P0] Every post-submit refusal collapses into one wrong sentence. "Erro ao criar agendamento. Tente novamente." covers 429, 409 and 500 alike; useCreateAppointment never inspects the error. These are the two refusals a client cannot predict and cannot fix by retrying — telling a rate-limited user to try again guarantees a second failure; telling a collision victim to try again loops them. Both go back to WhatsApp, the behavior this product exists to eliminate. Fix: branch on error.response.status. 409 -> "Esse horário acabou de ser reservado. Escolha outro:" + button back to step 3 with a slot refetch. 429 -> "Você já fez alguns agendamentos na última hora." + WhatsApp link. Never "novamente" on a 429. Command: $impeccable harden

[P0] The booking->manage seam has no confirmation and never tells the client to keep the link. agendar/page.tsx:83 pushes straight to the token URL; the manage view renders identically whether the booking happened two seconds or two months ago. The token URL is the client's only handle, and the system never delivers it — the "created" notification goes to the barber, and the form collects no email. Close the tab and the appointment becomes uncancellable and unreviewable. Fix: a ?novo=1 flag rendering a distinct success header — gold check, "Agendamento confirmado", "Guarde este link — é por aqui que você cancela ou avalia depois" — plus copy/navigator.share and an .ics. Queue the link to the client's WhatsApp on creation. Command: $impeccable onboard

[P1] The landing page spends half its conversion surface on a staff door and shows none of the real proof it already has. "Área do Barbeiro" gets the same size="lg" min-w-[200px] as "Agendar agora" in hero and closing CTA, plus a third staff entry in the header. The scroll cue promises "Conheça nossos barbeiros" and delivers three emoji cards. Nearly all landing traffic is clients from an Instagram bio. Fix: demote both staff buttons to a footer text link; make the scroll cue land on a real barbers strip from useBarbers(), each card deep-linking into the wizard with that barber preselected; delete the "Qualidade Garantida" card and lift "sem cadastro" into the hero. Command: $impeccable bolder

[P1] Cancel is irreversible, one-tap, unconfirmed, silent on failure, and a dead end on success. cancelAppointment.mutate() fires directly from a danger button; isError is never rendered; success drops the user at one grey sentence. The Modal primitive exists and is unused. The disabled variant stuffs the explanation into the button label at disabled:opacity-50 — red on near-black, unfocusable. Fix: wrap in the existing Modal; render the error state; on success show "Agendamento cancelado" + primary "Agendar novo horário"; when !canCancel, drop the disabled button and show explanatory text plus a WhatsApp link. Command: $impeccable harden

[P1] The wizard is hostile to the exact device it targets. (a) all state is useState with no URL/sessionStorage mirror — backgrounding the tab restarts at step 1, browser Back exits the wizard; (b) the phone field has no type="tel"/inputMode/autoComplete, so Android opens QWERTY for eleven digits; (c) canConfirm only disables the button — Input supports error and hint, neither is passed, so a bad number gives a dead gold button with no reason; (d) toggleService/toggleAddon never clear selectedSlot, so adding "Barba" after picking a time submits a slot sized for the old duration, landing back in the P0 generic error. Fix: mirror state to sessionStorage and the step to the URL; add input attributes; pass hint + dirty-field error; call setSelectedSlot(null) from both toggles. Command: $impeccable adapt

## Persona Red Flags

Rodrigo, 24 — cold visitor, Moto G, 4G, walking, Instagram bio link. Superlatives with nothing behind them; no shop photo, address or hours. The scroll cue promising barbers is hidden sm:flex, invisible on his phone; scrolling yields emoji rendered as saturated multicolor Samsung glyphs against the black, breaking the one-hue rule. A card claims "Avaliações reais de clientes" on a page showing zero reviews. Taps "Agendar agora" and must choose between two people rendered as letters in circles, one reading "Sem avaliações", with no "tanto faz" escape. Outdoors, calendar disabled days at white/20 (~1.7:1) and placeholders at white/30 (~2.3:1) are invisible.

Carlos, 41 — regular, "Corte + Barba com o João" every three weeks, wants out in 30 seconds. Header "Agendar" measures 82x32px, under the 40px his own design system mandates. Four steps from zero every time: no deep link, no "repetir último agendamento", nothing remembered. Re-types eleven digits on QWERTY every booking. Slot chips ~36px in a 3-column grid. "Próximo" is not sticky.

Fernanda, 33 — booked 08:00 tomorrow; at 07:05 her kid spikes a fever. canCancel is false; she gets a half-opacity red button reading "Cancelamento indisponível (menos de 2h)" — a rule disclosed nowhere in /agendar. No phone, WhatsApp or barber contact anywhere; the footer is a wordmark and a copyright. Options: no-show, hunt Instagram, or message the barber directly. Had she caught it at 05:30, one tap would have destroyed the booking with no confirm, and a failed request would have shown nothing.

## Minor Observations

- role="listitem" on a button element (BarberPicker.tsx:51) overrides the button role; TalkBack announces list items, not controls, and aria-pressed is discarded.
- Link wrapping Button in the header produces a > button — invalid interactive nesting.
- The slot grid declares role="listbox"/option with no roving tabindex and no arrow keys.
- No focus-visible styling on barber cards, service rows, or slot chips — only Button has the ring, so DESIGN.md's focus rule is unimplemented outside it.
- Correction to DESIGN.md: the modal's shadow-2xl is not the only shadow — BarberPicker.tsx:57 carries a hand-rolled gold glow that stacks a shadow on a border, violating the Hairline Rule. The rule is right; the inventory was incomplete.
- Same control, two fills: booking textarea uses bg-brand-black-soft, review textarea bg-brand-black.
- BookingConfirmation is not a form element — Enter does nothing on either field.
- Hero uses min-h-[85vh]; mobile browsers measure vh against the largest viewport, pushing the CTA below the fold on first paint. Use svh/dvh.
- public/logo.png is 1.1 MB and priority on the LCP element, for an audience on 4G.
- The manage page cannot know a review was submitted — reviewSubmitted is local state and the DTO carries no review field, so a reload re-offers the form and the second submit surfaces as "Erro ao enviar avaliação", which is a lie.
- PRODUCT.md documents the wizard as service -> barber -> slot; the code ships barber -> service -> slot. Reconcile deliberately.
- Footer copyright at white/40 on #0D0D0D ~3.5:1, below AA.

## Questions to Consider

1. What if the token link were treated as the product rather than a redirect target — pushed to WhatsApp on creation, carrying a countdown, an .ics, and a share button?
2. Why does the client choose a barber before choosing a haircut? PRODUCT.md already specifies the other order.
3. The shop has no photography, but it has real barbers, ratings and reviews behind three unused endpoints. Why is the landing page arguing with adjectives instead of showing them?
4. If both exits from this product are a 14px sentence and a wall, where is the next booking supposed to come from?
5. What if one persistent line — "João · Corte + Barba · 50 min · R$ 60,00" — rode from step 2 through submit?
