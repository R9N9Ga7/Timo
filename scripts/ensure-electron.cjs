const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

async function ensureElectron() {
  const packageDir = path.dirname(require.resolve('electron/package.json'));
  const executable = path.join(packageDir, 'dist', 'electron.exe');

  if (!fs.existsSync(executable)) {
    const { downloadArtifact } = require('@electron/get');
    const { version } = require(path.join(packageDir, 'package.json'));
    const archive = await downloadArtifact({
      version,
      artifactName: 'electron',
      platform: 'win32',
      arch: process.arch,
      checksums: require(path.join(packageDir, 'checksums.json'))
    });
    const temporaryDirectory = fs.mkdtempSync(path.join(packageDir, '.dist-'));

    try {
      // extract-zip can stall or let Node exit early under newer Node releases.
      // PowerShell's synchronous archive extraction keeps installation alive
      // until the full Electron archive has been unpacked.
      execFileSync('powershell.exe', [
        '-NoProfile',
        '-NonInteractive',
        '-Command',
        '& { param($archive, $destination) Expand-Archive -LiteralPath $archive -DestinationPath $destination -Force }',
        archive,
        temporaryDirectory
      ], {
        stdio: 'inherit'
      });
      if (!fs.existsSync(path.join(temporaryDirectory, 'electron.exe'))) {
        throw new Error('The Electron archive did not contain electron.exe.');
      }

      const distDirectory = path.join(packageDir, 'dist');
      fs.rmSync(distDirectory, { recursive: true, force: true });
      fs.renameSync(temporaryDirectory, distDirectory);
    } finally {
      fs.rmSync(temporaryDirectory, { recursive: true, force: true });
    }
  }

  // Electron reads this value verbatim, so it must not contain a trailing newline.
  fs.writeFileSync(path.join(packageDir, 'path.txt'), 'electron.exe');
}

ensureElectron().catch(error => {
  console.error('Unable to prepare the Electron development binary.');
  console.error(error);
  process.exitCode = 1;
});
