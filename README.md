# Renko Zone Reversal — Equal High / Low for NinjaTrader 8

**A personal C# project focused on turning an observed pattern into explicit rules, state transitions and testable behavior.**

This indicator creates support and resistance zones from reversal bars, tracks subsequent visits, and emits a bar-close signal when a revisit meets configurable conditions. A zero-width zone requires an exact level test (*Equal High / Equal Low*); a positive width allows a price tolerance.

**Status: functional prototype.** The user confirmed that the script works in NinjaTrader 8. Trading experiments produced mixed results and were reported as generally losing over the observed period. No reproducible performance dataset is included, and no profitability claim is made.

## Why this belongs in my cybersecurity learning portfolio

I developed this project with AI assistance alongside my cybersecurity training. It is a software development project, not a security tool, penetration test or security audit.

Its relevance is the engineering process: clarifying an ambiguous requirement, defining states and boundaries, working with C#, and checking expected behavior against tests. These practices are transferable to code analysis and tool development. They do not, by themselves, establish competence in security assessment.

| Area | Evidence available in this repository |
|---|---|
| Requirements | Explicit definitions of touches, visits, breakouts, expiration and retests |
| State management | Rules for enabling, signaling, invalidating and rearming a zone |
| Separation of concerns | A platform-independent rules engine and an NT8 adapter in one importable file |
| Verification | Boundary, transition, repeatability and duplicate-signal tests |
| Documentation | Setup, parameters, integration contract and limitations |
| Critical evaluation | Functional correctness distinguished from trading performance |

## My contribution and AI assistance

I defined the use case, supplied chart examples, refined the rules and exceptions, and tested the script in my NinjaTrader environment. My feedback shaped the specification and identified limitations of the trading idea.

**The code, tests and documentation were produced with Codex assistance.** This repository represents an AI-assisted project, not a claim that every implementation detail was written independently. Generated tests do not replace independent code review or validation against real platform behavior.

## The problem

A support or resistance line is easy to describe visually, but automation requires decisions that a chart reader might leave implicit:

- Do several nearby candles represent one visit or multiple tests?
- How far must price move away before a new visit is allowed?
- Does a wick beyond a level have the same meaning as a close beyond it?
- Should a broken level disappear or switch roles?
- When can the same zone emit another signal?

The goal is to make those decisions explicit while retaining an entry candidate on the reversal bar's close, without waiting for additional confirmation bars.

## Features

- Automatic levels based on reversal-bar extremes.
- Symmetric zones with configurable total width, including zero and odd tick counts.
- Separate maximum wick-overshoot tolerance.
- Configurable departure distance and consecutive closes to distinguish visits.
- Minimum and maximum spacing between visits.
- One signal per visit, with subsequent visits allowed after rearming.
- Expiration based on the most recent touch.
- Optional role reversal and retest after a breakout.
- Active zone shading; a center line appears only after a signal.
- A native numeric `Signal` plot intended for readers such as Predator X.

The indicator does not submit orders or manage positions, stops or profit targets. It does not use RSI or ADX filters.

## Rules

### 1. Level creation

| Reversal | New level |
|---|---|
| Red bar immediately after a green bar | Resistance at the **red bar's High** |
| Green bar immediately after a red bar | Support at the **green bar's Low** |

Red means `Close < Open`; green means `Close > Open`. Chart colors assigned by another indicator are ignored. A doji is neither color, although it can still touch a zone.

The creation bar produces **no signal**. The level's center stays fixed. A new reference point inside an existing active zone does not create another zone. Zones can nevertheless overlap when their centers fall outside existing zones.

### 2. Width and tolerance

For level `L`, total width `W`, and the instrument's `TickSize`:

```text
Lower boundary = L - W * TickSize / 2
Upper boundary = L + W * TickSize / 2
```

A width of 10 ticks means 5 ticks on each side. A width of 1 tick means 0.5 tick on each side; boundaries are not rounded to tradable ticks.

Wick overshoot applies to the breakout boundary: the upper boundary for resistance and the lower boundary for support. With zero overshoot, the relevant extreme must stay inside the zone, including its boundaries.

### 3. Visits and spacing

A touch is an intersection between a bar's Low–High range and the zone. It updates the most recent touch even if no signal is issued, the bar is a doji, or its wick exceeds the signal allowance.

A new visit requires the configured number of consecutive closes sufficiently far away on the rejection side: below resistance or above support. Wicks may still touch during this departure phase; departure is evaluated using closes.

- A close that is too near resets the consecutive departure counter.
- With zero departure distance, the close must still be strictly outside the zone.
- The creation bar can supply the first qualifying departure close.
- After departure is confirmed, the next contact starts a new visit.

Spacing counts bars **strictly between** the previous last touch and the new visit's first contact. A touch at bar 100 followed by contact at bar 106 means 5 intervening bars.

Minimum and maximum bounds are inclusive. Eligibility is fixed at the first contact. Waiting inside the zone cannot make an early visit eligible. A reversal can occur later during an eligible visit, but its own bar must touch the zone.

A zone expires when spacing since the last touch exceeds the maximum. An overdue touch cannot revive it. Zones do not automatically reset at session boundaries.

### 4. Signal conditions

| Condition | Short | Long |
|---|---|---|
| Zone | Active resistance | Active support |
| Visit | Eligible, no previous signal in this visit | Eligible, no previous signal in this visit |
| Reversal | Red immediately after green | Green immediately after red |
| Contact | The reversal bar itself touches the zone | The reversal bar itself touches the zone |
| Wick | High <= upper boundary + allowed overshoot | Low >= lower boundary - allowed overshoot |
| Close | Close <= upper boundary | Close >= lower boundary |

The close can be inside the zone or on the rejection side. An excessive wick rejects that bar's signal without deleting the level. A close beyond the breakout boundary always invalidates rejection, regardless of wick tolerance.

The signal is recorded on the closed reversal bar. Another eligible visit can signal on the same level, but the same visit cannot signal twice.

### 5. Breakout and optional retest

A close strictly above resistance or strictly below support breaks that level. A close exactly on its boundary does not.

- **Retest disabled:** the broken zone is removed. It is not immediately recreated with the opposite role inside its former boundaries on the same bar.
- **Retest enabled:** the zone changes role and color while retaining its center and width. Departure and visit state are reset. The breakout bar cannot emit a rejection signal for that zone; its close may count as the first departure close in the new role.

Spacing remains anchored to the last actual touch, including the breakout bar if it intersects the zone. A gap beyond the zone without contact does not update that date. Repeated role changes are possible with retests enabled.

## Parameters

| Display name | Default | Purpose |
|---|---:|---|
| TOTAL zone width (ticks) | 10 | Full width of the shaded band |
| Maximum wick overshoot (ticks) | 0 | Allowance beyond the breakout boundary |
| Departure distance (ticks) | 2 | Distance from the zone boundary to rearm |
| Consecutive departure closes | 2 | Required consecutive qualifying closes |
| Minimum bars BETWEEN touches | 3 | Minimum intervening bars for a new visit |
| Maximum bars BETWEEN touches | 100 | Maximum spacing and expiration threshold |
| Retest after breakout | Off | Allow role reversal after a breakout |
| Zone opacity (%) | 15 | Shading intensity |
| Arrow offset (ticks) | 3 | Vertical display offset, not a signal delay |

Support/long and resistance/short colors are configurable. Minimum spacing must not exceed maximum spacing. At least one departure close is required. Defaults are starting values, not optimized trading recommendations.

## Installation

Requirements: Windows and NinjaTrader 8. Predator X is optional for viewing the indicator and requires a separate installation to consume signals.

1. Download the [NT8 import archive](dist/fiRenkoZoneReversal-NT8.zip).
2. In the NT8 Control Center, select **Tools > Import > NinjaScript…**.
3. Select the ZIP without extracting it.
4. Add **FreeIndicators > fiRenkoZoneReversal** to a chart.
5. Configure zones, visits and optional retests.

This English edition keeps the same class name, internal property names, defaults and `Signal` plot as the earlier French UI edition. Only display text and messages were translated. It updates that indicator rather than installing a second independent version; NT8 may prompt to overwrite it.

The code consumes the chart's OHLC data and has no direct dependency on a particular Renko vendor. Compatibility with every custom bar type has not been established. The archive declares NT8 version 8.1.6.3 in its metadata; this is not a compatibility matrix.

### Visual behavior

Active zones appear as shaded bands. A center line begins at the first signal in the current role. Drawings extend one bar slot to the right so they are visible on their creation bar; this does not use future data.

Expiration or removal deletes the band and center line but preserves historical arrows and plot values. A role change removes the old line and waits for a new signal. At zero width, the band has zero height and is not visible before the signal line appears.

### Predator X interface

The public native NT8 plot **Signal** is transparent but visible in the Data Box:

| Value | Event |
|---:|---|
| +1 | Long signal |
| -1 | Short signal |
| 0 | No signal |

In Predator X, use **Plot Mode**, select this indicator's `Signal` plot, and associate +1 with long entries and -1 with short entries. Keep **Scan Current Bar disabled** for this bar-close logic. Avoid changing the indicator label after configuring the plot selector.

This contract follows the [TradeSaber guide](https://tradesaber.com/automating-signals/). The repository does not provide Predator X or an automated test of its order execution. Plot values and the NT8 adapter are tested separately.

## Architecture

The [indicator source](src/fiRenkoZoneReversal.cs) contains all components in one file for straightforward import:

| Component | Responsibility |
|---|---|
| `fiRenkoZoneReversal` | NT8 integration, tick conversion, plot and drawings |
| `RzrEngine` | Sequential processing of closed bars |
| `RzrZone` | Level, role, last touch and visit state |
| `RzrSettings` | Engine configuration |
| `RzrResult` | Signal and removed-zone identifiers |

The engine has no platform API calls. Tests extract it from the same source file rather than maintaining a separate handwritten copy.

## Tests and validation

### Run the tests

On Windows with PowerShell and the .NET Framework 4.x C# compiler:

```powershell
.\tests\Run-Tests.ps1
```

The runner builds temporary executables under `.build/`, without modifying NinjaTrader. It does not require NT8 or Predator X. Follow your machine's script-execution policy if local PowerShell scripts are restricted.

- [ZoneTests.cs](tests/ZoneTests.cs): creation without signals, fixed centers, widths 0/1/3/10, boundary contacts, accumulation, expiration, breakouts, optional retests and rearming. Symmetry and repeatability checks cover 4,800 synthetic OHLC bars.
- [ZoneAdapterTests.cs](tests/ZoneAdapterTests.cs): the actual indicator adapter running against a simulated host, checking its plot, reset behavior, tick conversion and drawing requests. This does not reproduce the native NT8 graphics engine.

### Validation status

| Check | Scope and status |
|---|---|
| Compilation against installed NT8 assemblies | Passed during development; proprietary assemblies are not distributed |
| Engine and simulated-adapter tests | Passed on synthetic data |
| ZIP/source consistency | Packaged source matches repository source |
| English localization | Only an explicit set of UI/message string literals changed; non-string source is unchanged |
| Operation in NT8 | Confirmed by the user for the original UI edition; not an exhaustive qualification campaign |
| English UI in the native application | Not visually verified in this delivery |
| Live Predator X integration | No complete end-to-end validation evidence included |
| Trading profitability | Not demonstrated; user experiments reported generally losing results |

## Limitations and lessons

- Correct implementation does not establish a profitable trading edge.
- Turning the visual idea into rules required explicit decisions about total width, wicks versus closes, contacts versus visits, and rejection versus breakout.
- Available chart history determines the initial levels. Reloading data or changing parameters recalculates the history.
- Custom Renko implementations may construct or revise bars differently. This indicator does not correct their construction mechanisms.
- Calculation requires `OnBarClose`. If a parent script imposes intrabar calculation, the adapter emits no signal.
- A rewind of already-processed bar indices requests a reload through an error; incremental state reconstruction is not implemented.
- There is no explicit cap on active zones or historical arrows. Performance on very long histories has not been quantified.
- Targeted tests are not a formal proof, exhaustive coverage or security audit.

The experiments did not establish that an RSI or ADX filter improves this approach. Adding later confirmation also changes the entry-to-stop distance. A useful next step would be measuring outcomes by context rather than asserting that another filter solves the problem.

## Possible next steps — not implemented

- Export signal acceptance and rejection reasons.
- Measure performance with large histories and many zones.
- Document a reproducible simulation campaign with settings and outcomes.
- Independently validate the Predator X connection.

## Repository layout

```text
.
├── README.md
├── .gitignore
├── src/
│   └── fiRenkoZoneReversal.cs
├── dist/
│   └── fiRenkoZoneReversal-NT8.zip
└── tests/
    ├── Run-Tests.ps1
    ├── ZoneTests.cs
    └── ZoneAdapterTests.cs
```

No proprietary NT8, TradeSaber or Renko-vendor binaries, license keys or account data are included. The test-host classes are local simulation scaffolding, not third-party platform components.

## Origins, references and licensing

The work began with an RSI-divergence indicator review and evolved into this separate zone/visit design. The original RSI indicator is not included. The technical name and `FreeIndicators` namespace are retained to match the previously distributed version; they do not imply affiliation.

- [NinjaScript import — NinjaTrader](https://ninjatrader.com/support/helpguides/nt8/import.htm)
- [NT8 plots — AddPlot](https://ninjatrader.com/support/helpguides/nt8/addplot.htm)
- [Plot automation — TradeSaber](https://tradesaber.com/automating-signals/)
- [Historical/live discrepancies — NinjaTrader](https://ninjatrader.com/support/helpguides/nt8/discrepancies_real-time_vs_bac.htm)

No reuse license has been assigned to this version. This should not be described as a release under an open-source license. NinjaTrader and TradeSaber are third-party products; no affiliation or vendor endorsement is claimed.
