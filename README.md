# Timo

Timo is a local-first routine tracker built with Electron, React, ASP.NET Core, and SQLite. It tracks recurring tasks against time goals and reports progress by category.

## Development

Requirements: Node.js 24+, npm 11+, and .NET SDK 9.

```powershell
npm.cmd install
npm.cmd run dev
```

The Electron process starts the API automatically. The development database is stored under `./.timo-data`; packaged builds use the operating system's application-data folder.

## Demo data

With the development app stopped, generate one year of deterministic demo activity:

```powershell
npm.cmd run seed
```

This creates a `Timo Demo` profile without changing other profiles. Run `npm.cmd run seed:replace` to replace only that generated profile. The generator also accepts `--days 90` and `--data-dir C:\path\to\data` after an extra `--`, for example `npm.cmd run seed -- --days 90`.

Run checks with `npm.cmd test`, and create a Windows installer with `npm.cmd run build`.
