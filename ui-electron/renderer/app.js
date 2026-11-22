import { $, $$, applyChrome, call, state, toast } from './core.js';
import { setLanguage, t } from './l10n.js';
import { renderSm } from './sm.js';
import { renderDlss5 } from './dlss5.js';
import { renderAnr } from './anr.js';
import { renderLogs } from './logs.js';
import { renderAbout } from './about.js';
import { renderDonate } from './donate.js';
import { renderFramegen, refresh } from './framegen.js';
import { loadCovers } from './covers.js';
import { openDeployDialog } from './deploy.js';
import { autoCheckUpdate } from './update.js';
import { checkAssetUpdates } from './assets.js';
import { enhanceSelects, observeSelects } from './select.js';
import { perfMark } from './perf.js';
import { refreshFeedbackBadge } from './feedback.js';

perfMark('renderer-start');
window.addEventListener('error', (event) => {
  perfMark('renderer-error', { message: String(event.message || '') });
});
window.addEventListener('unhandledrejection', (event) => {
  const reason = event.reason;
  perfMark('renderer-rejection', { message: String(reason?.message || reason || '') });
});
enhanceSelects();
observeSelects();

const hosts = new Map();
const scrollMemory = new Map();
const PAGE_TTL_MS = 5000;
let currentHost = null;

function hostFor(section) {
  let host = hosts.get(section);
  if (!host) {
    host = document.createElement('section');
    host.className = 'page-host';
    host.dataset.section = section;
    hosts.set(section, host);
  }
  return host;
}

function renderSection(section, host) {
  let result;
  if (section === 'framegen') {
    result = renderFramegen({ root: host });
  } else if (section === 'smooth-motion') {
    result = renderSm(host);
  } else if (section === 'dlss5') {
    result = renderDlss5(host);
  } else if (section === 'anr') {
    result = renderAnr(host);
  } else if (section === 'logs') {
    result = renderLogs(host);
  } else if (section === 'about') {
    result = renderAbout(host);
  } else if (section === 'donate') {
    result = renderDonate(host);
  }
  if (result?.then) {
    result.then(() => perfMark('page-ready', { section })).catch(() => {});
  } else {
    perfMark('page-ready', { section });
  }
}

function selectSection(section) {
  const switchStart = performance.now();
  perfMark('page-switch-start', { section });
  const view = $('#view');
  if (currentHost && currentHost.dataset.section !== section) {
    scrollMemory.set(currentHost.dataset.section, {
      view: view.scrollTop,
      list: currentHost.querySelector('.game-list')?.scrollTop || 0,
      side: currentHost.querySelector('.side')?.scrollTop || 0,
    });
    currentHost.remove();
  } else if (currentHost) {
    scrollMemory.set(section, {
      view: view.scrollTop,
      list: currentHost.querySelector('.game-list')?.scrollTop || 0,
      side: currentHost.querySelector('.side')?.scrollTop || 0,
    });
  }
  state.section = section;
  const host = hostFor(section);
  if (!host.isConnected) view.appendChild(host);
  currentHost = host;
  $$('.nav-item').forEach((item) => item.classList.toggle('active', item.dataset.page === section));
  moveIndicator();
  const renderedAt = Number(host.dataset.renderedAt || 0);
  if (!host.dataset.rendered || host.dataset.ready !== '1' || Date.now() - renderedAt > PAGE_TTL_MS) {
    host.dataset.rendered = '1';
    host.dataset.ready = '';
    host.dataset.renderedAt = String(Date.now());
    renderSection(section, host);
  }
  perfMark('page-switch-sync', { section, ms: performance.now() - switchStart });
  requestAnimationFrame(() => {
    const scroll = scrollMemory.get(section);
    view.scrollTop = scroll?.view || 0;
    const list = host.querySelector('.game-list');
    const side = host.querySelector('.side');
    if (list) list.scrollTop = scroll?.list || 0;
    if (side) side.scrollTop = scroll?.side || 0;
    perfMark('page-switch-end', { section, ms: performance.now() - switchStart });
  });
}

function moveIndicator() {
  const nav = $('#nav');
  const active = $('.nav-item.active', nav);
  const indicator = $('.nav-indicator', nav);
  if (!active || !indicator) return;
  indicator.style.left = `${active.offsetLeft}px`;
  indicator.style.width = `${active.offsetWidth}px`;
  indicator.style.top = `${active.offsetTop}px`;
  indicator.style.bottom = 'auto';
  indicator.style.height = `${active.offsetHeight}px`;
}

function renderBootShell() {
  const host = hostFor('framegen');
  const view = $('#view');
  view.replaceChildren(host);
  currentHost = host;
  host.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>${t('navFrameGeneration')}</h1>
          <div class="sub">${t('libraryStatusLoading')}</div>
        </div>
      </header>
      <main class="layout">
        <aside class="side">
          <section class="panel"><div class="hint">${t('libraryStatusLoading')}</div></section>
        </aside>
        <section class="content">
          <div class="game-list">
            <div class="gcard skeleton-card"><div class="sk-cover"></div><div class="sk-body"><i></i><i></i><i></i></div></div>
            <div class="gcard skeleton-card"><div class="sk-cover"></div><div class="sk-body"><i></i><i></i><i></i></div></div>
            <div class="gcard skeleton-card"><div class="sk-cover"></div><div class="sk-body"><i></i><i></i><i></i></div></div>
          </div>
        </section>
      </main>
    </section>`;
  perfMark('shell-rendered');
}

async function boot() {
  renderBootShell();
  const modesPromise = call('gameModes', {})
    .then((modes) => {
      state.modes = Object.fromEntries((modes.modes || []).map((mode) => [mode.gameId, mode]));
      state.modesReady = true;
      perfMark('modes-ready', { games: Object.keys(state.modes).length });
      if (state.section === 'framegen') {
        renderFramegen({ preserveScroll: true, root: hostFor('framegen') });
      } else {
        const host = hosts.get('framegen');
        if (host) host.dataset.renderedAt = '0';
      }
      loadCovers();
    })
    .catch((error) => toast(`加载失败：${error.message}`, 'error'));
  try {
    const version = await call('version');
    $('#app-version').textContent = `v${version}`;
  } catch {}
  try {
    const [paths, gpu, games, spoof, settingsReply] = await Promise.all([
      call('paths'),
      call('gpu'),
      call('library.list'),
      call('gpuSpoof.inspect'),
      call('settings.get'),
    ]);
    state.settings = settingsReply.settings || {};
    const language = state.settings.language || 'system';
    setLanguage(language);
    state.paths = paths;
    state.gpu = gpu.gpu;
    state.rtss = Boolean(gpu.rtss);
    state.games = games.games || [];
    state.spoof = spoof;
    perfMark('data-ready', { games: state.games.length });
  } catch (error) {
    console.error('boot failed', error);
    toast(`加载失败：${error.message}`, 'error');
  }
  applyChrome();
  const languageSelect = $('#lang-select');
  if (languageSelect) languageSelect.value = state.settings.language || 'system';
  const host = hostFor('framegen');
  host.dataset.rendered = '1';
  host.dataset.renderedAt = String(Date.now());
  renderFramegen({ root: host });
  perfMark('first-page-rendered');
  moveIndicator();
  const view = $('#view');
  if (view) view.scrollTop = 0;
  loadCovers();
  window.addEventListener('resize', moveIndicator);
  modesPromise.catch(() => {});
}


$$('.nav-item').forEach((item) => item.addEventListener('click', () => selectSection(item.dataset.page)));
$('.window-controls')?.addEventListener('click', (event) => {
  const button = event.target.closest('[data-window]');
  if (!button) return;
  window.fgm.windowControl(button.dataset.window);
});
$('.topbar')?.addEventListener('dblclick', (event) => {
  if (event.target.closest('button, select, .lang-box, .window-controls')) return;
  window.fgm.windowControl('maximize');
});
$('#lang-select').addEventListener('change', (event) => {
  const value = event.target.value;
  setLanguage(value);
  state.settings = { ...state.settings, language: value };
  call('settings.set', state.settings).catch(() => {});
  applyChrome();
  selectSection(state.section);
});
document.addEventListener('click', async (event) => {
  if (!event.target.closest('[data-action="cancel-op"]')) return;
  try {
    await call('operation.cancel');
    event.target.textContent = '取消中…';
  } catch (error) {
    toast(error.message, 'error');
  }
});

setTimeout(() => refreshFeedbackBadge(), 1500);
setTimeout(async () => {
  await checkAssetUpdates();
  setTimeout(() => autoCheckUpdate(), 1200);
}, 1200);
boot().then(() => {
  const params = new URLSearchParams(location.search);
  const page = params.get('page');
  const lang = params.get('lang');
  if (lang) {
    setLanguage(lang);
    applyChrome();
  }
  if (page) selectSection(page);
  if (params.get('demo') === 'deploy' && state.games.length) {
    openDeployDialog(state.games[0], refresh);
  }
  if (params.get('perf') === '1' && params.get('perfnav') === '1') {
    const pages = ['smooth-motion', 'dlss5', 'anr', 'logs', 'about', 'donate', 'framegen'];
    setTimeout(async () => {
      for (const section of pages) {
        selectSection(section);
        await new Promise((resolve) => setTimeout(resolve, 900));
      }
      perfMark('perf-nav-done');
    }, 1200);
  }
});
