# Rescue duel experiment

2026-11-19. The user explicitly requests more replayable HTML gameplay with varied decisions,
uncertainty, match-game satisfaction and a stronger rescue payoff. This is not Unity approval.

- Craft read: portrait casual rescue game, established mint/coral illustrated product language,
  variance 6; signature: a visible brain-board linking actual connected clears to animal reactions.
- Reuse the B layout's compact centered lower action bars and independent pins. Board taps are
  direct puzzle input; tactical cards use the existing cancellable 400 ms hold hook.
- Existing raster animal and parking-shed images only. No new art generation or Unity changes.
- Tokens: paper #f8fbfa, ink #293c37, green #337c62, coral #b86568; board categories add
  muted sky #537e9e and gold #a77b2a with icon/text redundancy. No generic hero or feature cards.
- Type: existing Microsoft YaHei/PingFang SC body; rounded YouYuan display where available;
  monospace only for reproducible encounter seeds. Letter spacing remains zero.
- Three comparable rule variants: intent read, true cascade, forecast environment events.
  Same seed preserves setup across variants; a new encounter changes seed, board, profile and draws.
- Randomness affects generated content, never a hidden success roll. Visible current groups,
  intention and event forecast determine the immediate result. Real chains are capped and logged.
- Goal completion is independent of further draws. No injury, death, economy or old-save writes.
- Native modal for rules/settings/history, visible focus, motion reduction, keyboard board and
  cards, no autoplay audio. All provisional rules and technical limits live in the implementation doc.

## Usable-height revision

The user reports ordinary screens feel too short. Keep the palette and rules; adapt to the usable
content box, not the physical screen. Use a viewport-constrained shell with named size-container queries after
safe-area padding. Merge title/mode selection into one 48px header; remove the always-on mode tabs
and experiment footer. At 650px usable height keep three compact action rows; below that use the
existing hand sheet. Below 560px combine needs with observation, keeping the 5x4 board at 44px cell
height. Show a fixed-height response title and compact gains; full narrative remains one tap away.
Modals scroll internally with their close header fixed. Preserve pins and current turn on resize.
Landscape uses the available width for observation/actions beside the board. Below the minimum
usable size allow shell scrolling rather than shrinking controls or silently clipping gameplay.

Latest follow-up: cap the portrait shell at 375x667 CSS pixels (ordinary 16:9 phone reference),
centered in the browser rather than stretched to desktop height. Smaller viewports still reflow.
Landscape caps at the rotated 667x375 size; dialogs use the shell's content dimensions, not the
desktop viewport. Browser regression asserts both caps, centering and dialog containment.
