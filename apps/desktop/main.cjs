const { app, BrowserWindow } = require('electron');
const { spawn } = require('child_process');
const crypto = require('crypto');
const net = require('net');
const path = require('path');

let apiProcess;

function freePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.unref();
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const port = server.address().port;
      server.close(() => resolve(port));
    });
  });
}

async function waitForApi(url) {
  for (let attempt = 0; attempt < 80; attempt += 1) {
    try {
      const response = await fetch(`${url}/health`);
      if (response.ok) return;
    } catch { /* API is still starting. */ }
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error('The Timo API did not start.');
}

async function startApi() {
  const port = await freePort();
  const url = `http://127.0.0.1:${port}`;
  const token = crypto.randomBytes(32).toString('hex');
  const isDev = !app.isPackaged;
  const command = isDev ? 'dotnet' : path.join(process.resourcesPath, 'api', 'Timo.Api.exe');
  const args = isDev
    ? ['run', '--project', path.join(__dirname, '..', '..', 'src', 'Timo.Api', 'Timo.Api.csproj'), '--no-launch-profile', '--urls', url]
    : ['--urls', url];

  apiProcess = spawn(command, args, {
    windowsHide: true,
    stdio: isDev ? 'inherit' : 'ignore',
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: isDev ? 'Development' : 'Production',
      TIMO_API_TOKEN: token,
      TIMO_DATA_DIR: isDev ? path.join(__dirname, '..', '..', '.timo-data') : app.getPath('userData')
    }
  });
  apiProcess.once('exit', code => {
    if (code && !app.isQuitting) console.error(`Timo API exited with code ${code}`);
  });
  await waitForApi(url);
  return { url, token };
}

async function createWindow() {
  const connection = await startApi();
  const window = new BrowserWindow({
    width: 1440,
    height: 940,
    minWidth: 980,
    minHeight: 700,
    backgroundColor: '#f6f4ee',
    icon: app.isPackaged ? path.join(process.resourcesPath, 'logo.png') : path.join(__dirname, '..', '..', 'build', 'logo.png'),
    titleBarStyle: 'hiddenInset',
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      additionalArguments: [
        `--timo-api-url=${connection.url}`,
        `--timo-api-token=${connection.token}`
      ]
    }
  });

  if (!app.isPackaged) await window.loadURL('http://127.0.0.1:5173');
  else await window.loadFile(path.join(__dirname, '..', 'web', 'dist', 'index.html'));
}

app.whenReady().then(createWindow).catch(error => {
  console.error(error);
  app.quit();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

app.on('before-quit', () => {
  app.isQuitting = true;
  if (apiProcess && !apiProcess.killed) apiProcess.kill();
});
