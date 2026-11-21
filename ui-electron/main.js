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
const normalBounds = new WeakMap();
const windowRounding = new WeakMap();

function savedBounds() {
  try {
    return JSON.parse(fs.readFileSync(windowStatePath(), 'utf8'));
  } catch {
    return null;
  }
}

function visibleBounds(bounds) {
  if (!bounds || !Number.isFinite(bounds.x) || !Number.isFinite(bounds.y)) return null;
  const width = Math.max(1360, Number(bounds.width) || 1360);
  const height = Math.max(820, Number(bounds.height) || 880);
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
  if (normalBounds.has(window) || window.isMinimized() || window.isMaximized()) return;
  const bounds = window.getBounds();
  try {
    fs.mkdirSync(path.dirname(windowStatePath()), { recursive: true });
    fs.writeFileSync(windowStatePath(), JSON.stringify(bounds));
  } catch {}
}

function applyWindowCorners(window) {
  if (process.platform !== 'win32' || typeof native.setRoundedCorners !== 'function') return;
  const handle = window.getNativeWindowHandle();
  const hwnd = Number(handle.readBigUInt64LE(0));
  let mode = windowRounding.get(window);
  if (!mode) {
    mode = native.setRoundedCorners(hwnd) ? 'dwm' : 'region';
    windowRounding.set(window, mode);
  }
  if (mode === 'dwm') return;
  if (normalBounds.has(window)) native.clearRoundedRegion(hwnd);
  else native.setRoundedRegion(hwnd, 12);
}

function createWindow() {
  const bounds = visibleBounds(savedBounds());
  const window = new BrowserWindow({
    width: bounds?.width || 1360,
    height: bounds?.height || 880,
    ...(bounds ? { x: bounds.x, y: bounds.y } : {}),
    minWidth: 1360,
    minHeight: 820,
    backgroundColor: '#F3F6FB',
    frame: false,
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
  applyWindowCorners(window);
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
    applyWindowCorners(window);
    window.show();
  });
  let saveTimer = null;
  const scheduleSave = () => {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => saveBounds(window), 800);
  };
  window.on('resize', () => {
    scheduleSave();
    applyWindowCorners(window);
  });
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
    if (data && data.ok === false) {
      return {
        ok: false,
        error: {
          code: data.code || 'BridgeError',
          message: data.message || data.code || 'operation failed',
          context: data.context || [],
        },
      };
    }
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

ipcMain.handle('fgm:window', (event, action) => {
  const window = BrowserWindow.fromWebContents(event.sender);
  if (!window) return;
  if (action === 'minimize') {
    window.minimize();
  } else if (action === 'maximize') {
    if (normalBounds.has(window)) {
      window.setBounds(normalBounds.get(window));
      normalBounds.delete(window);
      applyWindowCorners(window);
    } else {
      normalBounds.set(window, window.getBounds());
      window.setBounds(screen.getDisplayMatching(window.getBounds()).workArea);
      applyWindowCorners(window);
    }
  } else if (action === 'unmaximize') {
    if (normalBounds.has(window)) {
      window.setBounds(normalBounds.get(window));
      normalBounds.delete(window);
      applyWindowCorners(window);
    }
  } else if (action === 'close') {
    window.close();
  }
});

app.whenReady().then(() => {
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
