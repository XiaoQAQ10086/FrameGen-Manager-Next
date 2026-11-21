import { $, $$, applyChrome, call, state, toast } from './core.js';
import { setLanguage } from './l10n.js';
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

enhanceSelects();
observeSelects();

function selectSection(section) {
  state.section = section;
  $$('.nav-item').forEach((item) => item.classList.toggle('active', item.dataset.page === section));
  moveIndicator();
  if (section === 'framegen') {
    renderFramegen();
  } else if (section === 'smooth-motion') {
    renderSm($('#view'));
  } else if (section === 'dlss5') {
    renderDlss5($('#view'));
  } else if (section === 'anr') {
    renderAnr($('#view'));
  } else if (section === 'logs') {
    renderLogs($('#view'));
  } else if (section === 'about') {
    renderAbout($('#view'));
  } else if (section === 'donate') {
    renderDonate($('#view'));
  }
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

async function boot() {
  try {
    const version = await call('version');
    $('#app-version').textContent = `v${version}`;
  } catch {}
  try {
    const [paths, gpu, modes, games, spoof, settingsReply] = await Promise.all([
      call('paths'),
      call('gpu'),
      call('gameModes', {}),
      call('library.list'),
      call('gpuSpoof.inspect'),
      call('settings.get'),
    ]);
    state.settings = settingsReply.settings || {};
    const language = state.settings.language || 'system';
    setLanguage(language);
    state.paths = paths;
    state.gpu = gpu.gpu;
    state.modes = Object.fromEntries((modes.modes || []).map((mode) => [mode.gameId, mode]));
    state.games = games.games || [];
    state.spoof = spoof;
  } catch (error) {
    console.error('boot failed', error);
    toast(`加载失败：${error.message}`, 'error');
  }
  applyChrome();
  const languageSelect = $('#lang-select');
  if (languageSelect) languageSelect.value = state.settings.language || 'system';
  renderFramegen();
  moveIndicator();
  const view = $('#view');
  if (view) view.scrollTop = 0;
  loadCovers();
  window.addEventListener('resize', moveIndicator);
document.addEventListener('click', async (event) => {
  if (!event.target.closest('[data-action="cancel-op"]')) return;
  try {
    await call('operation.cancel');
    event.target.textContent = '取消中…';
  } catch (error) {
    toast(error.message, 'error');
  }
});
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
});
