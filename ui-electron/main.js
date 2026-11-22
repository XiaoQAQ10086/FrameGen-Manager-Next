const { app, BrowserWindow, ipcMain, screen, shell } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { performance } = require('node:perf_hooks');

app.commandLine.appendSwitch('js-flags', '--max-semi-space-size=8');

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

const perfEnabled = process.env.FGM_PERF === '1';
const perfPath = () => path.join(process.env.FGM_DATA_DIR, 'logs', 'perf.log');

function writePerf(source, name, at, detail) {
  if (!perfEnabled) return;
  try {
    fs.mkdirSync(path.dirname(perfPath()), { recursive: true });
    const entry = { source, name, at: Math.round(Number(at) * 1000) / 1000, wall: Date.now() };
    if (detail && Object.keys(detail).length) entry.detail = detail;
    fs.appendFileSync(perfPath(), `${JSON.stringify(entry)}
`);
  } catch {}
}

writePerf('main', 'process-start', 0);

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
      spellcheck: false,
      webgl: false,
      devTools: !packaged,
    },
  });
  window.setMenu(null);
  applyWindowCorners(window);
  const query = {};
  if (process.env.FGM_PROTO_DEMO) query.demo = process.env.FGM_PROTO_DEMO;
  if (process.env.FGM_PROTO_PAGE) query.page = process.env.FGM_PROTO_PAGE;
  if (process.env.FGM_PROTO_LANG) query.lang = process.env.FGM_PROTO_LANG;
  if (perfEnabled) query.perf = '1';
  if (process.env.FGM_PERF_NAV === '1') query.perfnav = '1';
  writePerf('main', 'window-created', performance.now());
  window.loadFile(path.join(__dirname, 'renderer', 'index.html'), { query });
  window.webContents.on('dom-ready', () => writePerf('main', 'dom-ready', performance.now()));
  window.webContents.on('did-finish-load', () => writePerf('main', 'did-finish-load', performance.now()));
  window.webContents.on('console-message', (_event, level, message) => {
    console.log('renderer', level, message);
  });
  window.once('ready-to-show', () => {
    writePerf('main', 'ready-to-show', performance.now());
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

ipcMain.on('fgm:perf', (_event, entry) => {
  if (entry && typeof entry.name === 'string') writePerf('renderer', entry.name, entry.at, entry.detail);
});

ipcMain.handle('fgm:call', async (_event, name, payload) => {
  const callStart = perfEnabled ? performance.now() : 0;
  let callOk = true;
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
    callOk = false;
    return {
      ok: false,
      error: { code: error?.code || 'BridgeError', message: String(error?.message || error) },
    };
  } finally {
    if (perfEnabled) writePerf('ipc', name, performance.now() - callStart, { ok: callOk });
  }
});


const FEEDBACK_API = 'https://laobiao-feedback.pages.dev/api/feedback';
const FEEDBACK_QUERY = 'https://laobiao-feedback.pages.dev/api/feedback/query';
const feedbackHistoryPath = () => path.join(process.env.FGM_DATA_DIR, 'state', 'feedback-history.json');
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

function feedbackKey() {
  if (process.env.FGM_FEEDBACK_KEY) return process.env.FGM_FEEDBACK_KEY;
  try {
    return JSON.parse(fs.readFileSync(path.join(__dirname, 'feedback.local.json'), 'utf8')).uploadKey || '';
  } catch {
    return '';
  }
}

function loadFeedbackHistory() {
  try {
    const items = JSON.parse(fs.readFileSync(feedbackHistoryPath(), 'utf8'));
    return Array.isArray(items) ? items : [];
  } catch {
    return [];
  }
}

function saveFeedbackHistory(items) {
  const trimmed = items.slice(0, 30);
  fs.mkdirSync(path.dirname(feedbackHistoryPath()), { recursive: true });
  fs.writeFileSync(feedbackHistoryPath(), JSON.stringify(trimmed, null, 2));
}

function sanitizedHistory() {
  return loadFeedbackHistory().map(({ id, time, summary, type, statusText, reply, lastReadReply, lastReadStatus }) => {
    const text = statusText || '待处理';
    const unread = (reply && reply !== (lastReadReply || ''))
      || (text === '已完成' && lastReadStatus !== '已完成');
    return {
      id, time, summary, type, statusText: text, reply: reply || '', unread: Boolean(unread),
    };
  });
}

ipcMain.handle('fgm:feedbackSubmit', async (_event, payload = {}) => {
  const key = feedbackKey();
  if (!key) {
    return { ok: false, error: { code: 'unauthorized', message: '客户端缺少反馈密钥，请更新软件。' } };
  }
  const description = String(payload.description || '').trim();
  if (!description) {
    return { ok: false, error: { code: 'description_required', message: '请填写详细说明。' } };
  }
  if (description.length > 5000) {
    return { ok: false, error: { code: 'description_too_long', message: '详细说明不能超过 5000 字符。' } };
  }
  const type = payload.type === 'suggestion' ? 'suggestion' : 'issue';
  const contact = String(payload.contact || '').slice(0, 200);
  const meta = JSON.stringify(payload.meta || {}).slice(0, 4000);
  let logBuffer = null;
  let logName = '';
  if (payload.logPath) {
    try {
      logBuffer = fs.readFileSync(String(payload.logPath));
      if (logBuffer.length > 2 * 1024 * 1024) {
        return { ok: false, error: { code: 'log_too_large', message: '日志压缩包超过 2MB。' } };
      }
      logName = path.basename(String(payload.logPath));
    } catch {
      return { ok: false, error: { code: 'log_missing', message: '日志压缩包读取失败，请重试。' } };
    }
  }

  for (let attempt = 0; attempt < 3; attempt += 1) {
    try {
      const form = new FormData();
      form.set('type', type);
      form.set('description', description);
      form.set('app', 'FrameGen Manager Next');
      form.set('version', String(payload.version || ''));
      form.set('contact', contact);
      form.set('meta', meta);
      if (logBuffer) {
        form.set('log', new Blob([logBuffer], { type: 'application/zip' }), logName);
      }
      const response = await fetch(FEEDBACK_API, {
        method: 'POST',
        headers: { 'x-upload-key': key },
        body: form,
        signal: AbortSignal.timeout(90000),
      });
      const data = await response.json().catch(() => ({}));
      if (response.ok && data.ok) {
        const items = loadFeedbackHistory();
        items.unshift({
          id: data.id,
          key: data.key,
          time: Date.now(),
          type,
          summary: description.slice(0, 160),
          statusText: data.statusText || '待处理',
          reply: '',
        });
        saveFeedbackHistory(items);
        return { ok: true, id: data.id, statusText: data.statusText || '待处理' };
      }
      if (response.status >= 500 && attempt < 2) {
        await sleep(attempt === 0 ? 2000 : 5000);
        continue;
      }
      if (response.status === 401) {
        return { ok: false, error: { code: 'unauthorized', message: '客户端版本过旧，请下载新版后再反馈。' } };
      }
      return { ok: false, error: { code: data.error || 'upload_failed', message: `上传失败（HTTP ${response.status}）。` } };
    } catch (error) {
      if (attempt < 2) {
        await sleep(attempt === 0 ? 2000 : 5000);
        continue;
      }
      return { ok: false, error: { code: 'network', message: `网络错误：${error.message || error}` } };
    }
  }
  return { ok: false, error: { code: 'upload_failed', message: '上传失败，请稍后重试。' } };
});

ipcMain.handle('fgm:feedbackHistory', async (_event, payload = {}) => {
  const items = loadFeedbackHistory();
  if (items.length && feedbackKey()) {
    try {
      const response = await fetch(FEEDBACK_QUERY, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(items.map((item) => ({ id: item.id, k: item.key }))),
        signal: AbortSignal.timeout(20000),
      });
      const data = await response.json().catch(() => ({}));
      if (data.ok && Array.isArray(data.items)) {
        const byId = new Map(data.items.filter((item) => item.ok).map((item) => [item.id, item]));
        for (const item of items) {
          const remote = byId.get(item.id);
          if (remote) {
            item.statusText = remote.statusText || item.statusText;
            item.reply = remote.reply || '';
          }
        }
        saveFeedbackHistory(items);
      }
    } catch {
      // Offline: keep the local snapshot.
    }
  }
  if (payload.markRead) {
    for (const item of items) {
      item.lastReadReply = item.reply || '';
      item.lastReadStatus = item.statusText || '';
    }
    saveFeedbackHistory(items);
  }
  const history = sanitizedHistory();
  return { ok: true, items: history, hasUnread: history.some((item) => item.unread) };
});

ipcMain.handle('fgm:feedbackDelete', (_event, id) => {
  saveFeedbackHistory(loadFeedbackHistory().filter((item) => item.id !== String(id)));
  return { ok: true };
});

ipcMain.handle('fgm:openUrl', (_event, url) => shell.openExternal(String(url)));

ipcMain.handle('fgm:quit', () => app.quit());

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
