```
============================================================
 KIMI
 Code Lines · Code Mass · Growth Tracker
============================================================
```

Kimi is a small tool that answers a simple question:

  What did your time turn into?

Point it at a file or a directory.
It measures the code, and over time, it remembers how it grows.

No accounts. No cloud. No noise.
Just you and your machine, keeping an honest record.

------------------------------------------------------------
 PHILOSOPHY
------------------------------------------------------------

We tend to remember how long we worked.

We rarely remember what that work became.

Kimi keeps a quiet ledger:

  how much code exists
  how it is structured
  how it changes over time

It pairs naturally with:

  Gravity → where your time went
  Pulse   → what you did on the daily
  Kimi    → what your work became

------------------------------------------------------------
 INSTALL
------------------------------------------------------------

Build:

Build:

  dotnet publish -c Release -r linux-x64 \ --self-contained true /p:PublishSingleFile=true

Install:

  mkdir -p ~/.local/bin
  cp bin/Release/net*/linux-x64/publish/Kimi ~/.local/bin/kimi
  chmod +x ~/.local/bin/kimi

Now you can run:

  kimi
Install:

  mkdir -p ~/.local/bin
  cp bin/Release/net*/linux-x64/publish/Kimi ~/.local/bin/kimi
  chmod +x ~/.local/bin/kimi

Now you can run:

  kimi

------------------------------------------------------------
 BASIC USAGE
------------------------------------------------------------

Count a file:

  kimi Program.cs lines

Count a directory:

  kimi ~/Documents/scripts/Pulse lines

Full report:

  kimi ~/Documents/scripts/Pulse lines report

Scan and record a snapshot:

  kimi scan ~/Documents/scripts/Pulse

------------------------------------------------------------
 HISTORY
------------------------------------------------------------

Over time, Kimi builds a simple picture of your work.

Hourly (today):

  kimi Program.cs lines day

Monthly (daily view):

  kimi ~/Documents/scripts/Pulse lines month

Specific month:

  kimi ~/Documents/scripts/Pulse lines month 2026-04

Year view:

  kimi ~/Documents/scripts/Pulse lines year

------------------------------------------------------------
 TRACKING (OPTIONAL)
------------------------------------------------------------

You can ask Kimi to watch certain paths.

Track:

  kimi track ~/Documents/scripts/Pulse
  
  kimi track ~/Documents/scripts/gravity

List tracked:

  kimi tracked

Run continuously:

  kimi daemon

Install as a user service:

  kimi install-service
  systemctl --user enable --now kimid

------------------------------------------------------------
 OVERVIEW
------------------------------------------------------------

Top projects:

  kimi top

Monthly summary:

  kimi month

Yearly summary:

  kimi year

History for a path:

  kimi history ~/Documents/scripts/Pulse

Status:

  kimi status

Storage locations:

  kimi where

------------------------------------------------------------
 SAMPLE OUTPUT
------------------------------------------------------------

  Pulse — Directory Lines

  Total lines:        1,398
  Code lines:         1,132
  Blank lines:          140
  Comment lines:         96
  Files:                  3

  Top files:
    Program.cs         1,246
    README.md            140
    Pulse.csproj          12

------------------------------------------------------------
 GROWTH
------------------------------------------------------------

  April 2026 — Kimi Growth

  You wrote a total of 10,002 lines of code this month.

  That is real progress.

  Top growth:
    Pulse          +3,200 lines
    Gravity        +2,880 lines

------------------------------------------------------------
 STORAGE
------------------------------------------------------------

  ~/.config/kimi/config.json

  ~/.local/share/kimi/snapshots.jsonl
  
  ~/.local/share/kimi/state.json

Everything stays on your machine.

------------------------------------------------------------
 DESIGN
------------------------------------------------------------

Kimi is built to be:

  fast      — streams files, scans in parallel
  
  simple    — no heavy parsing, no unnecessary state
  
  local     — your data never leaves your system
  
  honest    — it reports what exists, 



------------------------------------------------------------
 COMMAND SUMMARY
------------------------------------------------------------

  kimi <file> lines
  kimi <directory> lines
  kimi <path> lines report
  kimi <path> lines day
  kimi <path> lines month
  kimi <path> lines year

  kimi scan <path>

  kimi track <path>
  kimi untrack <path>
  kimi tracked

  kimi top
  kimi month
  kimi year
  kimi history <path>

  kimi daemon
  kimi install-service
  kimi uninstall-service

  kimi status
  kimi where

------------------------------------------------------------
 FINAL
------------------------------------------------------------

You don’t need a dashboard to know you’ve been working.

But a few lines never hurt. 
