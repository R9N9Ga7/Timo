const { contextBridge } = require('electron');

const readArg = name => {
  const prefix = `--${name}=`;
  return process.argv.find(value => value.startsWith(prefix))?.slice(prefix.length) ?? '';
};

contextBridge.exposeInMainWorld('timoDesktop', {
  apiUrl: readArg('timo-api-url'),
  apiToken: readArg('timo-api-token'),
  platform: process.platform
});
