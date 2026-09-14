# TradeBook — UI and UX Design Specification

Version 0.1 · 15 September 2026

## 1. Who this is for and what it has to do

One screen, used for hours at a time, by someone who is not looking at it
continuously. A trader glances at TradeBook between other things, and in that
glance has to answer three questions without reading carefully:

1. Am I long or short, and by how much?
2. Am I up or down?
3. Did anything just change?

Everything below serves those three questions. The design problem is not
navigation or onboarding; it is **numeric legibility under motion**. Numbers move
several times a second. A minus sign that can be misread, a column that shifts
width as digits change, or a highlight that draws the eye to the wrong row are
all functional failures, not cosmetic ones.

The secondary user is operations, who reads the same screen slowly and in full.
Nothing is hidden from them behind a hover.

## 2. Principles

**Direction is never carried by colour alone.** Every long or short, gain or
loss, is signalled three ways at once: colour, an explicit sign on the number,
and a word or glyph. Around one in twelve men has some form of colour vision
deficiency, and red-green is the affected pair. A design that fails for them on a
trading screen is a design that loses money.

**Digits never move.** All numeric columns use tabular figures and are right
aligned on the decimal. A price going from 9.99 to 10.00 must not shift the
column. This is why the typeface choice below is a functional decision.

**One moment of motion.** A value that changes flashes its cell briefly, tinted
by direction. That is the entire motion vocabulary. No hover transitions on rows,
no entrance animations, no skeleton shimmer. In a room where everything moves,
motion only means something if it is rare.

**Density is a feature.** This is not a marketing page and should not be laid out
like one. Rows are tight, padding is small, and the screen shows as much as it
can while staying readable. Whitespace is spent on separating zones, not on
padding individual rows.

**The screen opens on the data.** No dashboard landing, no summary cards, no
welcome state. Signing in puts the trader in the positions grid.

## 3. Visual direction

### Typeface

**IBM Plex Sans**, one family throughout, at four weights (400, 500, 600, 700).

Chosen for its numerals rather than its personality: Plex has true tabular
figures, a slashed zero, and a one with a foot serif, so 0/O and 1/l/I never
collide in a price. Its engineered, slightly mechanical letterforms suit an
instrument panel. Using a single family and carrying hierarchy on weight and
size keeps the interface quiet, which matters when the data is loud.

Numerals are set with `font-variant-numeric: tabular-nums slashed-zero`
everywhere a number appears, including inside prose.

Type scale, 1.2 ratio from a 13px base, which is deliberately small — this is a
data tool, not a document:

| Role | Size | Weight | Notes |
|---|---|---|---|
| Grid numeric | 14px | 500 | Tabular, right aligned |
| Grid text | 13px | 400 | |
| Column header | 12px | 600 | Sentence case, not caps |
| Section title | 15px | 600 | |
| Account name | 18px | 600 | |
| Headline figure | 28px | 700 | Total unrealised only |
| Helper and meta | 12px | 400 | Dimmed |

### Colour

Dark by default. The screen is watched for a full session, and a dark surface
keeps the bright elements — the numbers — as the only things competing for
attention. A light theme is specified in section 9 for print and screenshots.

The base is a blue-leaning graphite rather than a neutral near-black, so that the
red and teal sit on it without vibrating.

| Token | Hex | Use |
|---|---|---|
| `--surface-0` | `#10131A` | Page background |
| `--surface-1` | `#171B24` | Panels, grid background |
| `--surface-2` | `#1F2430` | Row hover, input fields, headers |
| `--line` | `#2C3340` | Borders and rules |
| `--text` | `#E3E7EE` | Primary text and numbers |
| `--text-dim` | `#8B94A5` | Labels, metadata, inactive |
| `--gain` | `#3FB6A8` | Positive P&L, long positions |
| `--loss` | `#D9544D` | Negative P&L, short positions |
| `--accent` | `#C9A227` | Submit action, focus ring |

The accent is brass, and it is deliberately outside the gain/loss pair. On a
trading screen the interactive colour must never be confusable with a P&L
colour, or a focused button reads as a profit. Brass also carries the right
connotation for a ticket: confirm, commit, stamp.

Red and teal are used at around 70% saturation. Fully saturated red and green on
a dark background produce chromatic aberration at small sizes and are tiring
across a session.

## 4. Layout

Three zones, fixed, no page scroll. The grids scroll internally so the headers
and the ticket stay put.

```
┌────────────┬─────────────────────────────────────────┬──────────────┐
│            │  Equities Desk 1              +1,284.50 │              │
│  ACCOUNTS  │  ─────────────────────────────────────  │   NEW TRADE  │
│            │  POSITIONS                              │              │
│ ▸ Desk 1   │  Symbol  Net   Avg    Last   Unrl   Rlsd│  Instrument  │
│   Desk 2   │  AAPL   +500  10.80  11.02  +110  +220  │  [        ]  │
│   Prop A   │  MSFT   -200   9.00   8.50  +100  -500  │              │
│            │  TSLA      0      —  42.10     —   +80  │  Side        │
│            │                                         │  [Buy][Sell] │
│            │  ─────────────────────────────────────  │              │
│            │  BLOTTER                     Today ▾    │  Quantity    │
│            │  Time    Sym  Side  Qty   Price   Ref   │  [        ]  │
│            │  09:31   AAPL Buy   300   10.00  OMS-1  │              │
│            │  09:48   AAPL Buy   200   12.00  OMS-2  │  Price       │
│            │  10:02   AAPL Sell  100   13.00  OMS-3  │  [        ]  │
│            │                                         │              │
│            │                                         │ [ Book trade]│
└────────────┴─────────────────────────────────────────┴──────────────┘
   200px                    flexible                        320px
```

Positions sit above the blotter because they answer the glance question.
The blotter is the evidence underneath, read deliberately rather than scanned.

The ticket is a permanent right rail rather than a modal. A modal would cover the
positions at exactly the moment the trader most wants to see them, and booking a
trade is the screen's most frequent action, so it should never need opening.

## 5. The positions grid

The primary surface. Columns, left to right:

| Column | Alignment | Treatment |
|---|---|---|
| Symbol | Left | 600 weight |
| Direction | Left | Word: `Long`, `Short`, or `Flat`, coloured |
| Net quantity | Right | Signed, always explicit `+` or `−` |
| Average cost | Right | 4 decimal places |
| Last price | Right | Flashes on change |
| Unrealised | Right | Signed, coloured, flashes on change |
| Realised | Right | Signed, coloured |

Notes that matter:

- The sign is always printed, including the plus. A number that only sometimes
  carries a sign forces the reader to check whether the sign is missing or
  negative.
- The minus is a true minus (`−`, U+2212), not a hyphen. Hyphens are too short
  to read as a sign at 14px and sit at the wrong height.
- Flat positions stay visible by default with an em dash in the price-derived
  columns, so a trader can see that a holding was closed rather than wondering
  whether it vanished. A filter hides them.
- Row height 32px. Zebra striping is not used; a single hairline rule between
  rows is quieter and does not compete with the tick flash.
- The account's total unrealised P&L appears once, large, in the header. It is
  the only headline figure on the screen.

## 6. Motion: the tick flash

One rule. When a cell's value changes, its background is tinted for 400ms and
fades out: teal for an increase, red for a decrease. The text colour does not
change during the flash, so the number stays readable throughout.

Two constraints:

- Only `Last price` and `Unrealised` flash. If every cell flashed, the flash
  would carry no information.
- Under `prefers-reduced-motion: reduce`, the flash is replaced by a small
  direction triangle that appears beside the number for the same duration and
  then disappears. The information is preserved; the animation is not.

## 7. The trade ticket

Fields in the order a trader thinks about them: instrument, side, quantity,
price. Account is inherited from the selection in the left rail and shown as
static text, not a dropdown, because picking the wrong account is the most
expensive mistake available on this screen.

- Side is a two-button segmented control, not a dropdown. It is a binary choice
  and must be visible without opening anything. The selected side takes the
  gain or loss colour as a fill, so the ticket itself signals direction.
- Quantity and price are numeric inputs with tabular figures and no spinner
  arrows. Spinners invite accidental increments.
- The submit button reads **Book trade**, and on success the confirmation reads
  **Trade booked**. The verb does not change between the action and its result.
- Below the button, a live preview line shows the resulting position: "Position
  becomes 500 long at 10.80". This is the single most useful element in the
  ticket, because it catches a wrong side or a wrong quantity before submission
  rather than after.

### Validation and errors

Validation runs on blur, not on every keystroke, so a half-typed number is never
marked wrong. Errors appear beneath the field in plain language naming the field
and the rule: "Quantity must be greater than zero." No apology, no exclamation
mark, no generic "Something went wrong".

Server refusals map to the same pattern:

| Condition | Message |
|---|---|
| Not entitled to the account | You do not have access to this account. |
| Instrument inactive | This instrument is no longer tradeable. |
| Concurrency retries exhausted | The position changed while this was submitted. Book it again. |

## 8. States

| State | Treatment |
|---|---|
| Loading | The grid frame and column headers render immediately; rows show a single dimmed line reading "Loading positions". No skeleton shimmer. |
| Empty account | "No positions in this account. Book a trade to open one." The ticket is already visible, so the empty state does not need a button. |
| Connection lost | A single amber bar across the top: "Live prices disconnected. Reconnecting." Numbers stay on screen, dimmed to `--text-dim`, because stale data clearly marked is more useful than a blank grid. |
| Reconnected | The bar turns brass and reads "Prices live" for two seconds, then disappears. |
| Signed out | Full-screen panel with the TradeBook mark and a single **Sign in** button. No form; authentication happens at the identity provider. |

## 9. Light theme

Same structure, remapped tokens. For screenshots, printing and anyone who
prefers it.

| Token | Dark | Light |
|---|---|---|
| `--surface-0` | `#10131A` | `#F7F8FA` |
| `--surface-1` | `#171B24` | `#FFFFFF` |
| `--surface-2` | `#1F2430` | `#EDF0F4` |
| `--line` | `#2C3340` | `#D8DEE7` |
| `--text` | `#E3E7EE` | `#1A1F29` |
| `--text-dim` | `#8B94A5` | `#697386` |
| `--gain` | `#3FB6A8` | `#0F7F73` |
| `--loss` | `#D9544D` | `#B3352E` |
| `--accent` | `#C9A227` | `#8A6F14` |

Gain, loss and accent darken in the light theme to hold contrast against white.
Both themes meet WCAG AA for body text and AA Large for the headline figure.

## 10. Accessibility

- Every interactive element has a visible focus ring: 2px `--accent` with a 2px
  offset. The ring is never removed, only restyled.
- Full keyboard path: Tab moves account rail → positions → blotter → ticket.
  Arrow keys move within a grid. Enter on a position row pre-fills the ticket
  with that instrument.
- The positions grid is a real `<table>` with `<th scope="col">`, not a div
  grid, so screen readers announce column context.
- Live regions: the connection bar is `role="status"`. Individual price ticks are
  **not** announced — a screen reader reading every tick would be unusable. The
  position summary is announced on change instead, debounced to once every five
  seconds.
- Colour is never the sole carrier of meaning, per section 2.
- Target size for the side buttons and submit is at least 36px high.

## 11. Responsive behaviour

This is a desktop tool and should say so rather than pretending otherwise.

- **Above 1280px**: the three-zone layout above.
- **1024–1280px**: the account rail collapses to a dropdown in the header.
- **768–1024px**: the ticket moves below the positions grid, full width. The
  blotter moves to a tab beside Positions.
- **Below 768px**: positions only, as a stacked card per instrument showing
  symbol, direction, net quantity and unrealised. The ticket is reachable from a
  single fixed button. Booking a trade on a phone is supported but not optimised,
  and that is a deliberate scope decision rather than an omission.

## 12. What this is not

Recorded so the absences are decisions:

- No charts. A price chart would be the obvious addition and adds nothing to the
  three questions in section 1.
- No dark/light toggle in the first build. Dark is the default and the light
  tokens exist; the toggle is trivial to add later.
- No customisable column order, saved layouts or view presets.
- No notifications, alerts or sound.
- No multi-account aggregated view. One account at a time keeps the headline
  figure unambiguous.

## 13. Handoff

The prototype in `prototypes/blotter.html` implements this specification with
mock data and a simulated price feed. It is a single self-contained file and is
intended to become `wwwroot/index.html`, with the mock feed replaced by the
SignalR client and the mock arrays replaced by REST responses.

Tokens live in `:root` as CSS custom properties matching the names in this
document, so a change here is a one-line change there.
