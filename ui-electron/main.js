const { app, BrowserWindow, ipcMain, shell } = require('electron');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

const packaged = app.isPackaged;
const appRoot = packaged ? path.dirname(process.execPath) : path.resolve(__dirname, '..');
process.env.FGM_DATA_DIR ||= packaged
  ? path.join(path.dirname(process.execPath), 'userdata')
  : path.join(__dirname, 'data');
process.env.FGM_PACKS_DIR ||= packaged
  ? path.join(path.dirname(process.execPath), 'packs')
  : path.join(appRoot, 'packs');

const native = require(path.join(__dirname, 'native', 'fgm_node.node'));

function createWindow() {
  const window = new BrowserWindow({
    width: 1280,
    height: 850,
    minWidth: 1100,
    minHeight: 700,
    backgroundColor: '#F3F6FB',
    show: false,
    title: 'FrameGen Manager-Next',
    icon: path.join(__dirname, 'renderer', 'app.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      devTools: !packaged,
    },
  });
  window.setMenu(null);
  const query = process.env.FGM_PROTO_DEMO
    ? { demo: process.env.FGM_PROTO_DEMO }
    : undefined;
  window.loadFile(path.join(__dirname, 'renderer', 'index.html'), { query });
  window.webContents.on('console-message', (_event, level, message) => {
    console.log('renderer', level, message);
  });
  window.once('ready-to-show', () => window.show());
}

ipcMain.handle('fgm:call', async (_event, name, payload) => {
  try {
    if (name === 'version') {
      return { ok: true, data: native.version() };
    }
    const text = await native.invoke(name, payload || {});
    const data = JSON.parse(text);
    if (name === 'cover' && data.path) {
      data.url = pathToFileURL(data.path).href;
    }
    return { ok: true, data };
  } catch (error) {
    return {
      ok: false,
      error: { code: error?.code || 'BridgeError', message: String(error?.message || error) },
    };
  }
});

ipcMain.handle('fgm:openUrl', (_event, url) => shell.openExternal(String(url)));

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
