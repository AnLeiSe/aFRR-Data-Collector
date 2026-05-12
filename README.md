# aFRR Data Collector (C#/WPF)

This desktop app fetches historic **accepted balancing capacity bids** (aFRR/SECONDARY) from NUCS and summarizes them into:

- Day
- Time
- Total MW
- Price Avg
- Price Min
- Price Max
- Volume (`Total MW * Price Avg`)

It also renders a **daily line chart** for traded volume across the selected period.

## Features

- Multi-select regions: `DK1, DK2, FI, NO1, NO2, NO3, NO4, NO5, SE1, SE2, SE3, SE4`
- Date range (`From` / `To`)
- Direction selector (`Up` / `Down`)
- Iterates each day and maps selections into NUCS query parameters
- Aggregates accepted bids per hour for each day

## Run

```bash
dotnet restore
dotnet run
```

## Notes

- The parser targets table rows in the NUCS HTML response and extracts the time plus the two right-most numeric values as `MW` and `Price`.
- If NUCS changes its table structure, parsing logic in `NucsAfrrService.ParseRows` may need adjustment.

## Build one-file portable Windows EXE (exactly 1 file output)

You are currently looking at `bin/Release/net8.0-windows/win-x64/` in your screenshot.
That folder is an intermediate/runtime build folder and can contain many files.

To get a **single distributable EXE only**, publish to a dedicated output folder:

```bash
dotnet publish ./aFRR-Data-Collector.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o ./dist-single
```

Then distribute only:

```text
dist-single/AfrrCollector.exe
```

`dist-single` should contain just that EXE for this project (no .pdb, no extra DLL files).

Notes:
- `afrr-data.db` is intentionally **not embedded** into the EXE. It is created next to the EXE on first run so data persists in portable mode.
- Do **not** distribute `bin/Release/.../win-x64/`; distribute only the custom publish output folder (`dist-single`).
