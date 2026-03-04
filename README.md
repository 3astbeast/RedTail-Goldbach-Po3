<p align="center">
  <img src="https://avatars.githubusercontent.com/u/209633456?v=4" width="160" alt="RedTail Indicators Logo"/>
</p>

<h1 align="center">RedTail Goldbach Po3 Levels</h1>

<p align="center">
  <b>A price level indicator for NinjaTrader 8 based on Goldbach number theory applied to Power of 3 ranges.</b><br>
  Dynamic and fixed modes with auto-settlement detection, premium/discount shading, and advanced level types.
</p>

<p align="center">
  <a href="https://buymeacoffee.com/dmwyzlxstj">
    <img src="https://img.shields.io/badge/☕_Buy_Me_a_Coffee-FFDD00?style=flat-square&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/3astbeast/RedTail-Goldbach-Po3/refs/heads/main/Screenshot%202026-03-03%20130031.png" width="800" alt="RedTail Goldbach Po3 Screenshot"/>
</p>

---

## Overview

RedTail Goldbach Po3 Levels maps a grid of price levels based on Goldbach number theory within a configurable Power of 3 (Po3) range. The range is centered on the settlement price (dynamic mode) or a fixed price, and levels are drawn at Goldbach-significant percentages within that range. The result is a structured set of support/resistance levels that define premium/discount zones, equilibrium, stop run targets, and standard deviation intervals — all calculated from a mathematically grounded framework.

---

## Modes

**Dynamic Mode** (recommended for intraday) — Centers the Po3 range on the settlement price, recalculating at each new session. The settlement price is either auto-detected based on the instrument or manually specified.

**Fixed Mode** — Centers the range on a manually entered price. Useful for anchoring levels to a specific reference point like a key high/low.

**Manual FIX Price** — Optionally override the settlement detection with a specific price. An offset in ticks can be added on top.

---

## Po3 Range Sizes

The range defines how wide the level grid extends above and below the center price. Available sizes follow the Power of 3 sequence:

3, 9, 27, 81, 243, 729, 2,187, 6,561, 19,683, 59,049, 177,147, 531,441, or Custom.

The right size depends on the instrument and timeframe. For example, NQ intraday might use 729 while ES might use 243. The Auto PO3 feature can recommend the optimal range based on the instrument's Average Daily Range.

---

## Level Types

### Goldbach Levels (Premium & Discount)
The core levels derived from Goldbach number theory. Discount levels sit below equilibrium (0, 3, 11, 17, 29, 41, 47) and premium levels sit above (53, 59, 71, 83, 89, 97, 100). Each level is labeled with its corresponding market structure concept (High, Rejection, Order Block, FVG, Liquidity Void, Breaker, Mitigation).

### Equilibrium
The 50% midpoint of the range — the dividing line between premium and discount territory.

### Non-Goldbach Levels
Semi-prime levels (23, 35, 65, 77) that fall between the Goldbach levels. Disabled by default. These can explain price reactions at levels that aren't pure Goldbach numbers.

### Midpoint Levels
CE/MT (Central Equilibrium / Mean Threshold) levels drawn between adjacent Goldbach levels for finer granularity. Disabled by default.

### Inverted Goldbach Levels
Levels at 14, 32, 38, 56, 74, 79, 92, 95, and 98 — the inverse of the standard Goldbach set. Disabled by default. These can explain erratic price behavior at range extremes.

### Stop Run Levels
Projected levels outside the range boundaries where stop runs are likely to target. Configurable distance based on sub-Po3 sizes: PO3÷9, PO3÷27, or PO3÷81.

### Standard Deviation Levels
Evenly-spaced levels extending from settlement at a configurable point interval (3, 9, 27, or 81 points). Useful for tracking standard deviation moves from the session anchor.

---

## Premium / Discount Area Shading

Optional background shading that visually separates the premium zone (above equilibrium) from the discount zone (below equilibrium). Makes it immediately clear which side of fair value price is trading in.

---

## Settlement Detection

The indicator auto-detects the correct settlement time for each instrument:

- **Equity Indices** (NQ, ES, YM, RTY) — 4:00 PM ET
- **Gold** (GC) — 1:30 PM ET
- **Silver** (SI) — 1:25 PM ET
- **Crude Oil** (CL) — 2:30 PM ET
- **Natural Gas** (NG) — 2:30 PM ET
- **Copper** (HG) — 1:00 PM ET
- **Currency Futures** (6E, 6B, 6J, 6C) — 4:00 PM ET / 5:00 PM ET
- **Bonds** (ZB, ZN, ZF, ZT) — 2:00 PM ET

Auto-detection can be disabled for manual override of settlement hour, minute, and timezone.

---

## Auto PO3 Calculation

When enabled, the indicator calculates the Average Daily Range over a configurable lookback period and recommends the optimal Po3 range size for the instrument. The recommendation is based on finding the Po3 value that best contains the typical daily range. An on-chart info box displays the current ADR, recommended Po3, active Po3, settlement price, and range boundaries.

---

## Level Merging

When multiple levels land close together, they can be merged to reduce visual clutter. Configurable threshold in ticks — levels within this distance are combined into a single line. Auto-scaling option adjusts the threshold based on the Po3 range size.

---

## Advanced Features

**Po3 DR Shift** — Half-shift dealing range for alignment with other methodologies. Set to 0 for no shift or 0.5 for a half-range offset.

**Hidden Range** — Shows intermediate range boundaries calculated as the midpoint between the current Po3 and the next Po3 size up.

---

## Line Styles

Every level type has independent visual controls:

- FIX Price, Premium, Discount, Boundary, Non-Goldbach, Midpoint, Inverted Goldbach, Stop Run, and STDV levels each have their own color, line width, and dash style
- Configurable line extension (left and right)
- Label font size and optional price display
- Info box position (Top Left / Top Right / Bottom Left / Bottom Right) and font size

---

## Installation

1. Download the .cs file from the indicator's repository
2. Copy the .cs to documents\Ninja Trader 8\bin\custom\indicators
3. Open Ninja Trader (if not already open) 
4. In control center, go to New --> Ninja Script Editor
5. Expand the Indicator Tree, find your new indicator, double click to open it
6. At the top of the Editor window, click the "Compile" button
7. That's it!

> **Note:** This indicator adds a secondary 1-minute data series for precise settlement detection. NinjaTrader will automatically load the required data when the indicator is applied.

---

## Part of the RedTail Indicators Suite

This indicator is part of the [RedTail Indicators](https://github.com/3astbeast/RedTailIndicators) collection — free NinjaTrader 8 tools built for futures traders who demand precision.

---

<p align="center">
  <a href="https://buymeacoffee.com/dmwyzlxstj">
    <img src="https://img.shields.io/badge/☕_Buy_Me_a_Coffee-Support_My_Work-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me a Coffee"/>
  </a>
</p>
