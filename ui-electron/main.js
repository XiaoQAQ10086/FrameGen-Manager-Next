const { app, BrowserWindow, ipcMain, screen, shell } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

const packaged = app.isPackaged;
if (!packaged) app.commandLine.appendSwitch('force-renderer-accessibility');
const appRoot = packaged ? path.dirname(process.execPath) : path.resolve(__dirname, '..');
process.env.FGM_DATA_DIR ||= packaged
  ? path.join(path.dirname(process.execPath), 'userdata')
  : path.join(__dirname, 'data');
process.env.FGM_PACKS_DIR ||= packaged
  ? path.join(path.dirname(process.execPath), 'packs')
  : path.join(appRoot, 'packs');

const native = require(path.join(__dirname, 'native', 'fgm_node.node'));

const windowStatePath = () => path.join(process.env.FGM_DATA_DIR, 'config', 'window.json');

function savedBounds() {
  try {
    return JSON.parse(fs.readFileSync(windowStatePath(), 'utf8'));
  } catch {
    return null;
  }
}

function visibleBounds(bounds) {
  if (!bounds || !Number.isFinite(bounds.x) || !Number.isFinite(bounds.y)) return null;
  const width = Math.max(1100, Number(bounds.width) || 1280);
  const height = Math.max(700, Number(bounds.height) || 850);
  const visible = screen.getAllDisplays().some((display) => {
    const area = display.workArea;
    return bounds.x + 80 < area.x + area.width
      && bounds.x + width - 80 > area.x
      && bounds.y + 40 < area.y + area.height
      && bounds.y + height - 40 > area.y;
  });
  return visible ? { x: bounds.x, y: bounds.y, width, height } : null;
}

function saveBounds(window) {
  if (window.isMinimized() || window.isMaximized()) return;
  const bounds = window.getBounds();
  try {
    fs.mkdirSync(path.dirname(windowStatePath()), { recursive: true });
    fs.writeFileSync(windowStatePath(), JSON.stringify(bounds));
  } catch {}
}

function createWindow() {
  const bounds = visibleBounds(savedBounds());
  const window = new BrowserWindow({
    width: bounds?.width || 1280,
    height: bounds?.height || 850,
    ...(bounds ? { x: bounds.x, y: bounds.y } : {}),
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
  const query = {};
  if (process.env.FGM_PROTO_DEMO) query.demo = process.env.FGM_PROTO_DEMO;
  if (process.env.FGM_PROTO_PAGE) query.page = process.env.FGM_PROTO_PAGE;
  if (process.env.FGM_PROTO_LANG) query.lang = process.env.FGM_PROTO_LANG;
  window.loadFile(path.join(__dirname, 'renderer', 'index.html'), { query });
  window.webContents.on('console-message', (_event, level, message) => {
    console.log('renderer', level, message);
  });
  window.once('ready-to-show', () => {
    if (!bounds) window.center();
    window.show();
  });
  let saveTimer = null;
  const scheduleSave = () => {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => saveBounds(window), 800);
  };
  window.on('resize', scheduleSave);
  window.on('move', scheduleSave);
  window.on('close', () => saveBounds(window));
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
