export const state = {
  games: [],
  modes: {},
  gpu: null,
  spoof: null,
  paths: null,
  covers: {},
  section: 'framegen',
  busy: false,
};

export const $ = (selector, root = document) => root.querySelector(selector);
export const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

const stroke = 'fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"';
export const folderIcon = `<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false" ${stroke}><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/></svg>`;
export const trashIcon = `<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false" ${stroke}><path d="M3 6h18"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>`;

export const esc = (value) =>
  String(value ?? '').replace(/[&<>"']/g, (ch) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[ch]));

export const prettyPath = (value) => {
  const text = String(value ?? '');
  return text.length > 1 && text[1] === ':' ? text[0].toUpperCase() + text.slice(1) : text;
};

export const apiLabel = (value) => {
  const text = String(value ?? 'unknown');
  return text.toLowerCase() === 'unknown' ? '未知' : text.toUpperCase();
};

export const sourceLabel = (source) => ({
  steam: 'Steam', epic: 'Epic', xbox: 'Xbox', manual: '手动', loose: '散装',
}[source] || source || '未知');

export const stateLabel = (state) => ({
  installed: '已部署', outdated: '需更新', partial: '部分部署',
  conflict: '冲突', installedWithoutManifest: '旧版部署', notDeployed: '未部署',
}[state] || '未部署');

export const hagsLabel = (hags) => ({
  on: '已开启', off: '已关闭', pending: '待重启生效',
  unsupported: '不支持', unknown: '未知',
}[hags] || '未知');

export const hagsClass = (hags) =>
  (hags === 'on' ? 'ok' : (hags === 'off' || hags === 'pending' ? 'warn' : 'dim'));

export async function call(name, payload) {
  const reply = await window.fgm.call(name, payload);
  if (!reply.ok) {
    const error = new Error(reply.error?.message || reply.error?.code || '调用失败');
    error.code = reply.error?.code;
    throw error;
  }
  return reply.data;
}

let toastTimer = null;
export function toast(message, kind = '') {
  const node = $('#toast');
  node.textContent = message;
  node.className = `toast ${kind}`.trim();
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => node.classList.add('hidden'), 2600);
}

export function showProgress(title) {
  const node = $('#global-progress');
  node.classList.remove('hidden');
  $('.gp-phase', node).textContent = title;
  $('.gp-subject', node).textContent = '';
  $('.gp-percent', node).textContent = '…';
  $('.gp-track > i', node).style.width = '35%';
}

export function hideProgress() {
  $('#global-progress').classList.add('hidden');
}
