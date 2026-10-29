let sequence = 0;
const pending = new Map();
const handlers = new Map();

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

export function request(command, payload = {}, timeout = 300000) {
  const id = `req-${++sequence}`;
  return new Promise((resolve, reject) => {
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
}

export function on(eventName, handler) {
  if (!handlers.has(eventName)) handlers.set(eventName, new Set());
  handlers.get(eventName).add(handler);
  return () => handlers.get(eventName)?.delete(handler);
}
