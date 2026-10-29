import * as api from './api.js';
import * as i18n from './i18n.js';
import * as ui from './ui.js';
import framegen from './pages/framegen.js';
import smoothMotion from './pages/smooth-motion.js';
import dlss5 from './pages/dlss5.js';
import logs from './pages/logs.js';
import about from './pages/about.js';
import donate from './pages/donate.js';
import { checkForUpdates } from './update.js';

const pages = {
  framegen,
  'smooth-motion': smoothMotion,
  dlss5,
  logs,
  about,
  donate,
};

const state = {
  info: null,
  current: 'framegen',
  mounted: new Map(),
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
  requestAnimationFrame(updateIndicator);
}

function updateIndicator() {
  const nav = document.getElementById('nav');
  const active = nav?.querySelector('.nav-item.active');
  const indicator = nav?.querySelector('.nav-indicator');
  if (!nav || !active || !indicator) return;
  const navRect = nav.getBoundingClientRect();
  const rect = active.getBoundingClientRect();
  indicator.style.left = `${rect.left - navRect.left}px`;
  indicator.style.width = `${rect.width}px`;
}

async function loadInfo() {
  const info = await api.request('app.info');
  state.info = info;
  i18n.setStrings(info.strings, info.language);
  const version = document.getElementById('app-version');
  if (version) version.textContent = `v${info.version}`;
  const language = document.getElementById('lang-select');
  if (language) language.value = info.language === 'en' ? 'en' : 'zh';
  updateSidebar();
  return info;
}

async function changeLanguage(language) {
  try {
    const info = await api.request('app.saveSettings', { language });
    state.info = info;
    i18n.setStrings(info.strings, info.language);
    resetPages();
    await navigate(state.current, false);
  } catch (error) {
    ui.toast(error.message || error.code || '语言切换失败', 'error');
  }
}

function resetPages() {
  for (const entry of state.mounted.values()) {
    entry.cleanup?.();
    entry.host.remove();
  }
  state.mounted.clear();
}

async function navigate(pageName, push = true) {
  const name = pages[pageName] ? pageName : 'framegen';
  state.current = name;
  updateSidebar();

  const view = document.getElementById('view');
  view.querySelector('.boot')?.remove();
  let entry = state.mounted.get(name);

  if (!entry) {
    const host = document.createElement('div');
    host.className = 'page-host hidden';
    view.appendChild(host);
    entry = { host, cleanup: null };
    state.mounted.set(name, entry);
    // Do not await: the page shell (and its skeleton) is written synchronously by
    // mount, so the tab appears immediately and data fills in afterwards.
    let mounting;
    try {
      mounting = pages[name].mount(host, context);
    } catch (error) {
      mounting = Promise.reject(error);
    }
    Promise.resolve(mounting)
      .then((cleanup) => { entry.cleanup = cleanup; })
      .catch((error) => {
        host.innerHTML = `<div class="panel"><h2>${ui.escapeHtml(i18n.t('Common.Unknown'))}</h2><p class="muted">${ui.escapeHtml(error.message || error)}</p></div>`;
      });
  }

  for (const [key, item] of state.mounted) {
    item.host.classList.toggle('hidden', key !== name);
  }
  entry.host.animate(
    [
      { opacity: 0, transform: 'translateY(8px)' },
      { opacity: 1, transform: 'none' },
    ],
    { duration: 180, easing: 'cubic-bezier(.2,.9,.3,1)' },
  );
  document.dispatchEvent(new CustomEvent('fgm:page-shown', { detail: { page: name } }));

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
  document.getElementById('lang-select')?.addEventListener('change', (event) => {
    changeLanguage(event.target.value);
  });

  try {
    await loadInfo();
    const initial = location.hash.replace('#', '') || 'framegen';
    await navigate(pages[initial] ? initial : 'framegen', false);
    setTimeout(() => { checkForUpdates(context, { silent: true }); }, 1500);
  } catch (error) {
    const view = document.getElementById('view');
    if (view) {
      view.innerHTML = `<div class="boot"><div class="boot-title">界面加载失败</div><div class="boot-sub">${ui.escapeHtml(error.message || String(error))}</div></div>`;
    }
    ui.toast(`初始化失败：${error.message || error}`, 'error');
  }
}

document.addEventListener('contextmenu', (event) => event.preventDefault());
window.addEventListener('resize', updateIndicator);
bootstrap();
