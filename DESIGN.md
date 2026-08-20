---
name: O Imperador Barber Shop
description: A near-black barbershop interface lit by a single warm gold, where the client is attended to rather than processed.
colors:
  brand-gold: "#C9A84C"
  brand-gold-light: "#E8C96A"
  brand-gold-dark: "#A8872E"
  brand-black: "#0D0D0D"
  brand-black-soft: "#1A1A1A"
  brand-white: "#F5F5F5"
  status-confirmed: "#4ADE80"
  status-cancelled: "#9CA3AF"
  danger: "#DC2626"
  danger-text: "#F87171"
typography:
  display:
    fontFamily: "Montserrat, system-ui, sans-serif"
    fontSize: "clamp(3rem, 8vw, 4.5rem)"
    fontWeight: 900
    lineHeight: 1.25
    letterSpacing: "-0.025em"
  headline:
    fontFamily: "Montserrat, system-ui, sans-serif"
    fontSize: "1.875rem"
    fontWeight: 900
    lineHeight: 1.2
    letterSpacing: "normal"
  title:
    fontFamily: "Montserrat, system-ui, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 700
    lineHeight: 1.4
    letterSpacing: "normal"
  body:
    fontFamily: "Inter, system-ui, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.625
    letterSpacing: "normal"
  label:
    fontFamily: "Montserrat, system-ui, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "0.1em"
  wordmark:
    fontFamily: "Montserrat, system-ui, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 900
    lineHeight: 1
    letterSpacing: "0.1em"
rounded:
  md: "6px"
  lg: "8px"
  xl: "12px"
  2xl: "16px"
  full: "9999px"
spacing:
  xs: "8px"
  sm: "12px"
  md: "16px"
  lg: "24px"
  xl: "32px"
  2xl: "48px"
  section: "80px"
components:
  button-primary:
    backgroundColor: "{colors.brand-gold}"
    textColor: "{colors.brand-black}"
    rounded: "{rounded.md}"
    padding: "10px 20px"
  button-primary-hover:
    backgroundColor: "{colors.brand-gold-light}"
    textColor: "{colors.brand-black}"
  button-primary-active:
    backgroundColor: "{colors.brand-gold-dark}"
    textColor: "{colors.brand-black}"
  button-secondary:
    backgroundColor: "transparent"
    textColor: "{colors.brand-gold}"
    rounded: "{rounded.md}"
    padding: "10px 20px"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.brand-white}"
    rounded: "{rounded.md}"
    padding: "10px 20px"
  button-danger:
    backgroundColor: "{colors.danger}"
    textColor: "#FFFFFF"
    rounded: "{rounded.md}"
    padding: "10px 20px"
  input:
    backgroundColor: "{colors.brand-black-soft}"
    textColor: "{colors.brand-white}"
    rounded: "{rounded.md}"
    padding: "10px 12px"
  card:
    backgroundColor: "{colors.brand-black-soft}"
    textColor: "{colors.brand-white}"
    rounded: "{rounded.xl}"
    padding: "16px"
  chip-slot:
    backgroundColor: "transparent"
    textColor: "{colors.brand-white}"
    rounded: "{rounded.lg}"
    padding: "8px 12px"
  chip-slot-selected:
    backgroundColor: "{colors.brand-gold}"
    textColor: "{colors.brand-black}"
    rounded: "{rounded.lg}"
    padding: "8px 12px"
  badge-status:
    backgroundColor: "transparent"
    textColor: "{colors.brand-white}"
    rounded: "{rounded.full}"
    padding: "2px 10px"
  nav-item-active:
    backgroundColor: "transparent"
    textColor: "{colors.brand-gold}"
    rounded: "{rounded.lg}"
    padding: "10px 16px"
---

# Design System: O Imperador Barber Shop

## Overview

**Creative North Star: "The Gilded Chair"**

The barber's chair as a throne. The room is nearly black — `#0D0D0D` everywhere, with cards a
half-step lighter at `#1A1A1A` — and a single warm gold falls on the thing being attended to:
the selected service, the chosen time, the price, the confirmation. Nothing else glows. The
interface is ceremony without stuffiness: the client is being attended to, not processed through
a form.

The system is built almost entirely from two moves. First, **tonal layering**: black, black-soft,
and a 1px `rgba(245,245,245,0.1)` hairline separate every surface from the one behind it.
Second, **the gold tint ladder**: the same `#C9A84C` at 5% / 10% / 15% / 20% / 30% opacity does
the work that a second and third accent color would do elsewhere — hover washes, selected states,
badge fills, focus rings, decorative rings on the hero. Gold at full strength is reserved for
things that are true right now: a price, a selected slot, the primary button, the wordmark.

Density is generous rather than efficient. Sections breathe at 80px; cards pad at 16–48px; text
runs at `1.625` line-height. This is a mobile-first product opened one-handed from a WhatsApp
link, so tap targets are large (40px slot chips, 44px+ buttons) and the layout is a single column
until `sm`, with the barber and admin tools widening into two-column and sidebar shells at `lg`.

**Key Characteristics:**
- Near-black canvas, gold as the only accent hue
- Hairline borders (`1px` white @ 10–20%) instead of shadows
- Montserrat 900 with tight tracking for identity; Inter for everything read
- Wide-tracked uppercase micro-labels (`0.1em`–`0.4em`) as the recurring formal gesture
- State expressed as a gold tint wash, never as a color change to a different hue
- Portuguese-only copy; `R$` and `DD/MM` formatting are part of the visual rhythm

## Colors

A one-hue system: a warm, slightly desaturated gold against a near-black neutral ramp, with
functional greens and reds admitted only for status.

### Primary
- **Warm Lamplight** (`#C9A84C`): the single warm light source in a dark room. Primary buttons,
  prices, selected slots, active nav, the wordmark, star fills, chart bars. Everything else in the
  palette is either the room or the tint of this light.
- **Lamplight Flare** (`#E8C96A`): the hover brightening of the same source — primary button hover,
  wordmark hover, scrollbar thumb hover. Never used at rest.
- **Lamplight Ember** (`#A8872E`): the pressed state, the light dimming under a finger. Active
  state only.

### Neutral
- **Shop Black** (`#0D0D0D`): the room. Page background, header background at 95% opacity with
  backdrop blur, nested surfaces inside an already-soft card.
- **Chair Leather** (`#1A1A1A`): every raised surface — cards, inputs, modals, the admin sidebar,
  the sticky booking total. Exactly one step off the background, never more.
- **Bone White** (`#F5F5F5`): all readable text. Its opacity ladder is the entire secondary type
  scale — `/80` labels, `/70` sidebar links, `/60` body prose, `/50` hints and metadata, `/40`
  footer legal, `/30` placeholders and disabled cues, `/20` and `/10` borders and empty stars.

### Tertiary (status only)
- **Confirmed Green** (`#4ADE80` at 100% text / 20% fill): `Accepted` appointments.
- **Cancelled Gray** (`#9CA3AF` at 100% text / 20% fill): `Cancelled` appointments.
- **Alert Red** (`#DC2626` buttons / `#F87171` text / `#EF4444` input borders): destructive actions,
  field errors, `role="alert"` messages. Completed appointments deliberately use gold, not green —
  completion is the shop's win, and it earns the accent.

### Named Rules
**The One Light Rule.** There is one accent hue in this product. Any new color must be a status
signal (green/gray/red) or a tint of `#C9A84C`. A second decorative hue is a defect.

**The Tint Ladder Rule.** Emphasis below "primary" is expressed as `brand-gold` at 5/10/15/20/30%
opacity over black — never as a new solid color. Hover is `/10`, selected fill is `/10` with a
solid `#C9A84C` border, active nav is `/15`, badge fill is `/20`, decorative borders are `/30`
and below.

**The Opacity Type Scale Rule.** Secondary text is `brand-white` at a documented opacity step, not
a gray. `/60` for prose, `/50` for metadata, `/30` for placeholders. Never introduce `#9CA3AF` as
body text.

## Typography

**Display Font:** Montserrat (with `system-ui, sans-serif`) — loaded via `next/font/google`, `display: swap`
**Body Font:** Inter (with `system-ui, sans-serif`)

**Character:** Montserrat at weight 900 with negative tracking is a barbershop sign — geometric,
symmetrical, unapologetically loud at hero size. Inter underneath it is the receipt: neutral,
legible at 12px on a cheap phone, invisible by design. Every heading level is Montserrat via a
global `h1–h6` rule; anything that isn't a heading is Inter unless it is a wordmark or a label.

### Hierarchy
- **Display** (900, `clamp(3rem, 8vw, 4.5rem)`, `1.25`, `-0.025em`): the hero name only. One per page,
  and only on the landing surface.
- **Headline** (900, `1.875rem`, `1.2`): section headings and CTA blocks.
- **Title** (700, `1.25rem`): modal titles, card headings, dashboard section titles.
- **Body** (Inter 400, `1rem`, `1.625`): all prose. Constrained to `max-w-xl` (36rem) in hero copy;
  never runs the full 80rem container width.
- **Label** (Montserrat 600, `0.75rem`, `0.1em`, uppercase): eyebrow badges, admin section labels,
  add-on prompts, scroll cues.
- **Wordmark** (900, `1.25rem`, `0.1em` over a `0.55rem` / `0.35em` subline): "O IMPERADOR" above
  "BARBER SHOP", stacked and optically centered. Header and footer only.

### Named Rules
**The Wide-Track Rule.** Uppercase is always tracked out — `0.1em` for labels, `0.35em`–`0.4em` for
the wordmark subline and the hero's "BARBER SHOP". Untracked uppercase reads as shouting; tracked
uppercase reads as engraving.

**The Two Voices Rule.** Montserrat states, Inter explains. If a string is longer than five words
and isn't a heading, it is Inter.

## Layout

A centered `max-w-7xl` (80rem) container with `px-4` rising to `px-6` at `sm` governs the public
surfaces; marketing sections stack at `py-20` (80px). Vertical rhythm inside components runs on a
4px base: `gap-2` (8px) between chips, `gap-3`/`gap-4` (12/16px) between list rows, `p-4` (16px)
inside cards, `p-6`–`p-8` (24–32px) inside feature cards and the sidebar, `p-12` (48px) inside the
CTA block.

Breakpoints are Tailwind defaults, and only two are load-bearing: `sm` (640px) turns stacked
button pairs into rows and single columns into 3-up grids; `lg` (1024px) turns the admin drawer
into a static 16rem sidebar and puts the booking calendar beside its slot grid. Between them
nothing changes — the design is a phone layout that widens twice.

The app shell is a flex column with `min-h-screen`: sticky blurred header, `flex-1` main, footer.
Admin nests a second shell inside it — a fixed off-canvas `w-64` sidebar with a `bg-black/60`
scrim below `lg`, plus its own sticky mobile bar carrying the drawer toggle. The booking total
inside the service picker is `sticky bottom-0`, so the running price stays glued to the thumb
while the list scrolls.

**The Thumb Zone Rule.** Anything a client must tap on a phone — slot chips, service rows, primary
CTAs, the running total — is at least 40px tall and reachable in the lower half of the viewport.

## Elevation & Depth

**Current state (and a known gap).** The implemented system is flat: depth comes from the two-step
tonal ramp (`#0D0D0D` → `#1A1A1A`) plus a `1px rgba(245,245,245,0.1)` hairline, with `backdrop-blur-sm`
on the sticky header and modal scrim. The only real shadow in the codebase is `shadow-2xl` on the
modal dialog.

That flatness is **not** ratified as an invariant. A proper shadow scale is wanted and does not
exist yet; until it is defined, do not invent per-component shadows ad hoc — that is how a system
ends up with six unrelated blurs. Extend the scale deliberately in one pass.

### Shadow Vocabulary
- **Overlay** (`box-shadow: 0 25px 50px -12px rgba(0,0,0,0.25)` — Tailwind `shadow-2xl`): modal dialog
  only, above a `bg-black/70 backdrop-blur-sm` scrim.
- **Resting / hover / raised scale**: `[to be resolved]` — confirmed as a gap, not a prohibition.

## Shapes

Rounded, never pill-shaped, except for status badges. The radius ladder maps to surface size:
`6px` (`rounded-md`) for controls — buttons, inputs, icon buttons; `8px` (`rounded-lg`) for slot
chips, add-on rows, and nav items; `12px` (`rounded-xl`) for cards, service rows, and the sticky
total; `16px` (`rounded-2xl`) for the full-width CTA block; fully round only for status badges and
the hero eyebrow.

Borders are the primary structural device: `1px` at `white/10` for resting surfaces, `white/20` for
input strokes and unselected chips, `brand-gold/20`–`/30` for gold-framed containers, and solid
`brand-gold` for anything selected. The landing hero adds two concentric 400px/600px circles at
`brand-gold/5`–`/8` — a barber-pole halo behind the logo, decorative and `aria-hidden`.

**The Hairline Rule.** Every raised surface is separated from what's behind it by exactly one
1px hairline. Two nested borders, or a border plus a shadow, is over-drawing.

## Components

### Buttons
- **Shape:** gently rounded (`6px`), inline-flex, `gap-2` for an optional leading spinner, `transition-colors duration-150`.
- **Sizes:** sm `12px/6px` at `0.875rem`, md `20px/10px` at `1rem`, lg `28px/14px` at `1.125rem`.
- **Primary:** solid `#C9A84C` on `#0D0D0D` text, weight 600. Hover lightens to `#E8C96A`, active
  darkens to `#A8872E`. The only solid-gold element on most screens.
- **Secondary:** `1px #C9A84C` border, gold text, transparent fill; hover `gold/10`, active `gold/20`.
- **Ghost:** bone-white text, no border; hover `white/10`.
- **Danger:** solid `#DC2626`, white text; hover `#EF4444`.
- **Focus:** `2px` gold ring with a `2px` offset in `brand-black` — the offset is what keeps the ring
  legible against a near-black page.
- **Loading:** `isLoading` disables the button and prepends a 16px spinning arc at `opacity-25/75`;
  the label stays. Never swap the label for a spinner.

### Chips (time slots)
- **Style:** `8px` radius, `1px white/20` border, transparent fill, `0.875rem` weight 500, laid out
  `grid-cols-3` rising to `sm:grid-cols-4` with `gap-2`.
- **Hover:** border to `gold/50`, fill to `gold/10`.
- **Selected:** solid `#C9A84C` fill with `#0D0D0D` text — the inversion is the confirmation.
- **Semantics:** the grid is a `listbox`, each chip an `option` with `aria-selected`.

### Cards / Containers
- **Corner Style:** `12px` for content cards, `16px` for the CTA block.
- **Background:** `#1A1A1A` on the `#0D0D0D` page; nested rows inside a selected card drop back to
  `#0D0D0D` to stay one step apart.
- **Shadow Strategy:** none — see Elevation & Depth.
- **Border:** `1px white/10` at rest; `gold/30` on hover for interactive cards; solid `#C9A84C` with a
  `gold/10` fill when selected.
- **Internal Padding:** `16px` for list rows, `32px` for feature cards, `48px` for the CTA block.

### Inputs / Fields
- **Style:** `#1A1A1A` fill, `1px white/20` border, `6px` radius, `12px/10px` padding, placeholder at
  `white/30`.
- **Label:** `0.875rem` weight 500 at `white/80`, stacked above with `gap-1`.
- **Focus:** border shifts to `#C9A84C` with a matching `1px` ring; no glow, no lift.
- **Error:** border `#EF4444`, message below in `0.75rem` `#F87171` with `role="alert"`, wired through
  `aria-describedby` / `aria-invalid`.
- **Hint:** `0.75rem` at `white/50`, replaced by the error when one exists — never both at once.
- **Checkbox:** native input with `accent-color: #C9A84C`, 20px in service rows.

### Navigation
- **Public header:** sticky, `bg-brand-black/95` with `backdrop-blur-sm`, `1px white/10` bottom border,
  stacked wordmark left, action buttons right. "Agendar" is always a primary button — booking is
  never a text link.
- **Admin sidebar:** `w-64`, `#1A1A1A`, `1px white/10` right border, logo + "ADMINISTRADOR" label on
  top, links at `8px` radius. Active is `gold/15` fill with gold weight-600 text and
  `aria-current="page"`; inactive is `white/70` moving to gold on hover. Off-canvas below `lg`
  with a `200ms ease-out` translate and a `black/60` scrim.

### Status Badge
Pill (`9999px`), `10px/2px` padding, `0.75rem` weight 600, always a 20%-opacity fill of its own text
color: green for Confirmado, gray for Cancelado, **gold for Concluído**. The label is Portuguese and
comes from a single `statusConfig` map — status colors are never written inline.

### Hero Hair Rain (signature)

A `<canvas>` behind the landing hero renders golden hair strands falling through the
lamplight — the instant after the clippers pass. Strands are tapered quadratic curves in the
three golds, in three depth layers (far = thin, slow, dim; near = thick, fast, bright), with
a slow tumble and sinusoidal sway. A radial `destination-out` mask erases whatever falls
behind the hero text, so legibility never depends on where a strand happens to land.

Density scales with viewport area (26–84 strands), DPR is capped at 2, and the loop stops
when the hero scrolls offscreen or the tab hides. Under `prefers-reduced-motion` it paints
one static scatter and never starts the loop — the composition survives, the movement does
not.

**The One Cut Rule.** The hero is the only place motion is authored. Everything else in the
product animates to explain state, never to decorate.

### Snip CTA (signature)

The primary landing CTA carries two coupled effects: a warm radial highlight that tracks the
pointer across the gold (the room's light following the hand), and a burst of strands
released from the exact point of contact on click — pressing the button *is* the cut.
Keyboard activation releases the burst from the button's center. Both are suppressed under
reduced motion; the color feedback stays.

### Star Rating
Gold-filled stars against `white/20` empties, `aria-label`ed as "Avaliação: N de 5". The input
variant is 32px buttons that scale to `1.1` on hover with a hover-preview fill; the display variant
rounds to the nearest whole star. Both are gold — ratings are earned light.

## Do's and Don'ts

### Do:
- **Do** express every non-primary emphasis as a `brand-gold` opacity tint (`/5` `/10` `/15` `/20` `/30`)
  over black, per The Tint Ladder Rule.
- **Do** separate surfaces with exactly one `1px` hairline: `white/10` at rest, `white/20` for input
  strokes, solid `#C9A84C` when selected.
- **Do** give every focusable control the `2px` gold ring with `2px` `brand-black` offset — on a
  near-black page a ring without an offset disappears.
- **Do** pair Montserrat 900 with tracked-out uppercase for identity, and set everything readable in
  Inter at `1.625` line-height.
- **Do** keep tap targets ≥40px and keep the running total `sticky bottom-0` — this is a phone
  product opened from a WhatsApp link.
- **Do** route status labels and colors through `statusConfig`, and keep Concluído gold.
- **Do** write all UI copy in Brazilian Portuguese, with `R$ X,XX` and `DD/MM/YYYY`.

### Don't:
- **Don't** introduce a second decorative hue. Green, gray and red exist for status; everything else
  is gold or a neutral.
- **Don't** use a gray hex for secondary text — use `brand-white` at an opacity step.
- **Don't** add per-component shadows before the elevation scale is defined; the only shipped shadow
  is the modal's.
- **Don't** stack a border and a shadow on the same surface, or nest two bordered containers.
- **Don't** use solid `#C9A84C` for anything that isn't currently true — not for decoration, not for
  large fills, not for headings. Gold marks the selected, the primary, and the priced.
- **Don't** use `#E8C96A` or `#A8872E` at rest; they exist only as hover and active states.
- **Don't** put the display size on any surface but the landing hero, and never twice on one page.
- **Don't** replace a button's label with a spinner — set `isLoading` and keep the words.
