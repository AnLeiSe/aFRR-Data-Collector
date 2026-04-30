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
