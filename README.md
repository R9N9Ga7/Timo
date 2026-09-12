# Timo

Timo is a local-first routine tracker built with Electron, React, ASP.NET Core, and SQLite. It tracks recurring tasks against time goals and reports progress by category.

## Development

Requirements: Node.js 24+, npm 11+, and .NET SDK 9.

```powershell
npm.cmd install
npm.cmd run dev
```

The Electron process starts the API automatically. The development database is stored under `./.timo-data`; packaged builds use the operating system's application-data folder.

Run checks with `npm.cmd test`, and create a Windows installer with `npm.cmd run build`.
