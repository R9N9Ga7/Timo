const fs = require('fs');
const path = require('path');

async function ensureElectron() {
  const packageDir = path.dirname(require.resolve('electron/package.json'));
  const executable = path.join(packageDir, 'dist', 'electron.exe');

  if (!fs.existsSync(executable)) {
    const { downloadArtifact } = require('@electron/get');
    const extract = require('extract-zip');
    const { version } = require(path.join(packageDir, 'package.json'));
    const archive = await downloadArtifact({
      version,
      artifactName: 'electron',
      platform: 'win32',
      arch: process.arch,
      checksums: require(path.join(packageDir, 'checksums.json'))
    });
    await extract(archive, { dir: path.join(packageDir, 'dist') });
  }

  // Electron reads this value verbatim, so it must not contain a trailing newline.
  fs.writeFileSync(path.join(packageDir, 'path.txt'), 'electron.exe');
}

ensureElectron().catch(error => {
  console.error('Unable to prepare the Electron development binary.');
  console.error(error);
  process.exitCode = 1;
});
