import { t } from './l10n.js';

export const state = {
  games: [],
  modes: {},
  gpu: null,
  spoof: null,
  paths: null,
  settings: {},
  covers: {},
  section: 'framegen',
  busy: false,
};

export const $ = (selector, root = document) => root.querySelector(selector);
export const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

const stroke = 'fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"';
export const folderIcon = `<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false" ${stroke}><path d="M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"/></svg>`;
export const trashIcon = `<svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true" focusable="false" ${stroke}><path d="M3 6h18"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/><path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/><path d="M10 11v6"/><path d="M14 11v6"/></svg>`;
export const githubIcon = `<svg class="github-icon" viewBox="0 0 16 16" width="16" height="16" aria-hidden="true" focusable="false" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8z"/></svg>`;

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
  return text.toLowerCase() === 'unknown' ? t('smUnknown') : text.toUpperCase();
};

export const sourceLabel = (source) => ({
  steam: t('sourceSteam'),
  epic: t('sourceEpic'),
  xbox: t('sourceXbox'),
  manual: t('sourceManual'),
  loose: t('sourceLoose'),
}[source] || source || t('smUnknown'));

export const stateLabel = (state) => ({
  installed: t('frameGenStateInstalled'),
  outdated: t('frameGenStateOutdated'),
  partial: t('frameGenStatePartial'),
  conflict: t('frameGenStateConflict'),
  installedWithoutManifest: t('frameGenStateNoManifest'),
  notDeployed: t('frameGenStateNotDeployed'),
}[state] || t('frameGenStateNotDeployed'));

export const hagsLabel = (hags) => ({
  on: t('gpuHagsOn'), off: t('gpuHagsOff'), pending: t('gpuHagsPending'),
  unsupported: t('gpuHagsUnsupported'), unknown: t('gpuHagsUnknown'),
}[hags] || t('gpuHagsUnknown'));

export const hagsClass = (hags) =>
  (hags === 'on' ? 'ok' : (hags === 'off' || hags === 'pending' ? 'warn' : 'dim'));

export function applyChrome() {
  document.querySelectorAll('[data-nav]').forEach((node) => {
    node.textContent = t(node.dataset.nav);
  });
  const language = $('#lang-select');
  if (language) {
    language.options[0].textContent = t('languageSystem');
    language.options[1].textContent = t('languageChinese');
    language.options[2].textContent = t('languageEnglish');
  }
}

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

let progressTimer = null;

export function showProgress(title) {
  const node = $('#global-progress');
  node.classList.remove('hidden');
  $('.gp-phase', node).textContent = title;
  $('.gp-subject', node).textContent = '';
  $('.gp-percent', node).textContent = '…';
  $('.gp-track > i', node).style.width = '35%';
  clearInterval(progressTimer);
  progressTimer = setInterval(async () => {
    try {
      const reply = await call('operation.progress');
      const progress = reply.progress || reply;
      const total = Number(progress.total || 0);
      const done = Number(progress.done || 0);
      const fraction = total > 0 ? Math.min(1, done / total) : 0;
      $('.gp-percent', node).textContent = total > 0
        ? `${Math.round(fraction * 100)}%`
        : '…';
      $('.gp-subject', node).textContent = total > 0 ? `${done} / ${total}` : '';
      $('.gp-track > i', node).style.width = total > 0
        ? `${Math.max(2, fraction * 100)}%`
        : '35%';
    } catch {}
  }, 300);
}

export function hideProgress() {
  clearInterval(progressTimer);
  progressTimer = null;
  $('#global-progress').classList.add('hidden');
}
