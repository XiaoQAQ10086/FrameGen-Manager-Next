import * as api from './api.js';
import * as i18n from './i18n.js';
import * as ui from './ui.js';
import framegen from './pages/framegen.js';
import smoothMotion from './pages/smooth-motion.js';
import dlss5 from './pages/dlss5.js';
import logs from './pages/logs.js';
import settings from './pages/settings.js';
import about from './pages/about.js';

const pages = {
  framegen,
  'smooth-motion': smoothMotion,
  dlss5,
  logs,
  settings,
  about,
};

const state = {
  info: null,
  current: 'framegen',
  cleanup: null,
};

const context = {
  api,
  i18n,
  ui,
  t: i18n.t,
  get info() { return state.info; },
  refreshInfo: loadInfo,
  navigate,
};

function updateSidebar() {
  document.querySelectorAll('.nav-item').forEach((item) => {
    item.classList.toggle('active', item.dataset.page === state.current);
  });
  document.querySelectorAll('[data-nav-label]').forEach((element) => {
    element.textContent = i18n.t(element.dataset.navLabel);
  });
  document.querySelectorAll('[data-i18n]').forEach((element) => {
    element.textContent = i18n.t(element.dataset.i18n);
  });
}

async function loadInfo() {
  const info = await api.request('app.info');
  state.info = info;
  i18n.setStrings(info.strings, info.language);
  const version = document.getElementById('app-version');
  if (version) version.textContent = `v${info.version}`;
  updateSidebar();
  return info;
}

async function navigate(pageName, push = true) {
  const page = pages[pageName] ?? pages.framegen;
  state.current = pages[pageName] ? pageName : 'framegen';
  state.cleanup?.();
  state.cleanup = null;

  const view = document.getElementById('view');
  view.innerHTML = '';
  view.scrollTop = 0;
  updateSidebar();

  try {
    state.cleanup = await page.mount(view, context);
  } catch (error) {
    view.innerHTML = `<div class="panel"><h2>${ui.escapeHtml(i18n.t('Common.Unknown'))}</h2><p class="muted">${ui.escapeHtml(error.message || error)}</p></div>`;
  }

  if (push) {
    const hash = state.current === 'framegen' ? '' : `#${state.current}`;
    history.replaceState(null, '', `${location.pathname}${hash}`);
  }
}

function setupNavigation() {
  document.getElementById('nav')?.addEventListener('click', (event) => {
    const item = event.target.closest('[data-page]');
    if (!item) return;
    navigate(item.dataset.page);
  });
}

async function bootstrap() {
  api.init();
  api.on('progress', (data) => {
    ui.progress(i18n.t(`Progress.${data.phase}`), data.subject, data.current, data.total);
  });
  api.on('toast', (data) => ui.toast(data.message, data.level));
  setupNavigation();

  try {
    await loadInfo();
    const initial = location.hash.replace('#', '') || 'framegen';
    await navigate(pages[initial] ? initial : 'framegen', false);
  } catch (error) {
    ui.toast(`初始化失败：${error.message || error}`, 'error');
  }
}

window.addEventListener('resize', () => { /* reserved for responsive layout hooks */ });
bootstrap();
