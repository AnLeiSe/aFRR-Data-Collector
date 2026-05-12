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

## Build one-file portable Windows EXE (no DLL folder)

If your goal is **one single EXE file** that already contains .NET runtime + app dependencies, use `dotnet publish` (not `dotnet build`):

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

The EXE is written to:

```
bin/Release/net8.0-windows/win-x64/publish/AfrrCollector.exe
```

After publishing, that `publish` directory should contain just the EXE for this project.

Notes:
- `afrr-data.db` is intentionally **not embedded** into the EXE. It is created next to the EXE on first run so your data persists in a portable way.
- If you run `dotnet build`, you will still see many files; that is normal. Use `dotnet publish` for the distributable artifact.
