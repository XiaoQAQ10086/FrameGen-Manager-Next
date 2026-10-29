let sequence = 0;
const pending = new Map();
const handlers = new Map();
const cache = new Map();
const cacheTtl = 120000;
const cacheable = new Set(['app.info', 'games.state', 'gpu.state', 'sm.state', 'dlss5.state']);
const invalidating = new Set([
  'app.saveSettings',
  'games.scan',
  'games.add',
  'games.remove',
  'games.restore',
  'framegen.deploy',
  'framegen.restore',
  'sm.enable',
  'sm.disable',
  'sm.restore',
  'dlss5.install',
  'dlss5.restore',
  'dlss5.uninstall',
  'gpu.spoof.apply',
  'gpu.spoof.restore',
]);

function post(payload) {
  if (!window.chrome?.webview) {
    throw new Error('WebView2 bridge is unavailable');
  }
  window.chrome.webview.postMessage(payload);
}

export function init() {
  window.chrome?.webview?.addEventListener('message', (event) => {
    const message = event.data;
    if (!message) return;

    if (message.event) {
      handlers.get(message.event)?.forEach((handler) => handler(message.data));
      return;
    }

    if (!message.id) return;
    const entry = pending.get(message.id);
    if (!entry) return;
    pending.delete(message.id);
    clearTimeout(entry.timer);
    if (message.ok) entry.resolve(message.data);
    else entry.reject({ code: message.error?.code, message: message.error?.message });
  });
}

export function invalidate() {
  cache.clear();
}

export function request(command, payload = {}, timeout = 300000) {
  if (invalidating.has(command)) {
    cache.clear();
  }

  const key = command === 'app.info' ? command : `${command}:${JSON.stringify(payload)}`;
  if (cacheable.has(command)) {
    const hit = cache.get(key);
    if (hit && Date.now() - hit.time < cacheTtl) {
      return hit.promise;
    }
    if (hit) cache.delete(key);
  }

  const id = `req-${++sequence}`;
  const promise = new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      pending.delete(id);
      reject({ code: 'Timeout', message: '操作超时' });
    }, timeout);
    pending.set(id, { resolve, reject, timer });
    try {
      post({ id, command, payload });
    } catch (error) {
      clearTimeout(timer);
      pending.delete(id);
      reject(error);
    }
  });

  if (!cacheable.has(command)) {
    return promise;
  }

  const guarded = promise.catch((error) => {
    cache.delete(key);
    throw error;
  });
  cache.set(key, { promise: guarded, time: Date.now() });
  return guarded;
}

export function on(eventName, handler) {
  if (!handlers.has(eventName)) handlers.set(eventName, new Set());
  handlers.get(eventName).add(handler);
  return () => handlers.get(eventName)?.delete(handler);
}
