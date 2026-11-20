import {
  $, $$, apiLabel, call, esc, folderIcon, hagsClass, hagsLabel, hideProgress,
  prettyPath, showProgress, sourceLabel, state, stateLabel, toast, trashIcon,
} from './core.js';
import { t } from './l10n.js';
import { loadCovers } from './covers.js';
import { openDeployDialog, restoreGame } from './deploy.js';
import { showRemoved } from './removed.js';

export function renderFramegen() {
  const view = $('#view');
  view.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>${t('navFrameGeneration')}</h1>
          <div class="sub" data-role="status">${t('libraryStatusLoaded', { count: state.games.length })}</div>
        </div>
        <div class="lh-actions">
          <button class="btn" data-action="add" type="button">${t('addGame')}</button>
          <button class="btn" data-action="removed" type="button">${t('removedTitle')}</button>
          <button class="btn scan" data-action="scan" type="button">${t('scanGames')}</button>
        </div>
      </header>

      <main class="layout">
        <aside class="side">
          <section class="panel" data-role="gpu"></section>
          <section class="panel" data-role="spoof"></section>
          <section class="panel" data-role="assets"></section>
          <section class="panel">
            <div class="ctitle">${t('modeSourceTitle')}</div>
            <div class="chips"><span class="chip gpu-chip">${t('modeGpusFramegen')}</span></div>
            <a class="source-link" data-action="github" href="#">${t('modeSourceLink')}</a>
          </section>
        </aside>
        <section class="content">
          <div class="game-list" data-role="games"></div>
        </section>
      </main>
    </section>`;

  renderGpuPanel();
  renderSpoofPanel();
  renderAssetsPanel();
  renderGames();
  bindFramegenActions();
  requestAnimationFrame(() => {
    const side = $('.side');
    if (side) side.scrollTop = 0;
  });
}

function renderGpuPanel() {
  const gpu = state.gpu;
  const panel = $('[data-role="gpu"]');
  if (!gpu) {
    panel.innerHTML = `<div class="ctitle">${t('gpuPanelTitle')}</div><div class="hint">${t('libraryStatusLoading')}</div>`;
    return;
  }
  panel.innerHTML = `
    <div class="ctitle">${t('gpuPanelTitle')}</div>
    <div class="chips"><span class="chip gpu-chip" title="${esc(gpu.name)}">${esc(gpu.name)}</span></div>
    <div class="kv2"><span>${t('gpuDriverVersionLabel')}</span><b class="badge">${esc(gpu.driverVersion || '—')}</b></div>
    <div class="kv2"><span>${t('gpuHagsLabel')}</span><b class="badge ${hagsClass(gpu.hags)}">${esc(hagsLabel(gpu.hags))}</b></div>
    <button class="btn wide" data-action="open-hags" type="button">${t('gpuOpenHags')}</button>`;
}

function renderSpoofPanel() {
  const spoof = state.spoof;
  const panel = $('[data-role="spoof"]');
  if (!spoof) {
    panel.innerHTML = `<div class="ctitle">${t('gpuSpoofTitle')}</div><div class="hint">${t('libraryStatusLoading')}</div>`;
    return;
  }
  const adapter = (spoof.adapters || [])[0] || {};
  const applied = spoof.applied || null;
  const current = applied?.appliedName || spoof.liveName || adapter.displayName || '—';
  const driver = adapter.driverName || '—';
  const presets = spoof.presets || [];
  panel.innerHTML = `
    <div class="ctitle">${t('gpuSpoofTitle')}</div>
    <div class="kv2 spoof-name"><span>${t('gpuSpoofCurrent')}</span><b class="badge" title="${esc(current)}">${esc(current)}</b></div>
    <div class="kv2 spoof-name"><span>${t('gpuSpoofDriverRecord')}</span><b class="badge" title="${esc(driver)}">${esc(driver)}</b></div>
    <p class="hint">${t('gpuSpoofAdminWarning')}</p>
    <div class="spoof-picker">
      <select data-role="spoof-preset">${presets.map((preset) => `<option>${esc(preset)}</option>`).join('')}</select>
      <button class="btn primary wide" data-action="spoof-apply" type="button">${t('gpuSpoofApply')}</button>
    </div>
    <button class="btn wide" data-action="spoof-restore" type="button" ${applied ? '' : 'disabled'}>${t('gpuSpoofRestoreDriver')}</button>`;
}

function renderAssetsPanel() {
  const paths = state.paths || {};
  $('[data-role="assets"]').innerHTML = `
    <div class="ctitle">${t('assetsFolderTitle')}</div>
    <div class="hint">${t('assetsBundled')}</div>
    <div class="path">${esc(prettyPath(paths.bundledPacksDirectory))}</div>
    <button class="btn wide" data-action="open-bundled" type="button">${t('openFolder')}</button>
    <div class="hint">${t('assetsDownloaded')}</div>
    <div class="path">${esc(prettyPath(paths.packsDirectory))}</div>
    <button class="btn wide" data-action="open-data" type="button">${t('openFolder')}</button>`;
}

function cardMarkup(game, index) {
  const mode = state.modes[game.id] || {};
  const framegen = mode.framegen || 'notDeployed';
  const deployed = framegen !== 'notDeployed';
  const cover = state.covers[game.id];
  const initial = game.name ? game.name.slice(0, 1).toUpperCase() : '?';
  const stateText = stateLabel(framegen);
  const stateClass = framegen === 'installed' ? 'ok' : (framegen === 'notDeployed' ? 'dim' : 'warn');
  const primary = framegen === 'outdated' ? t('update') : (deployed ? t('restore') : t('deploy'));
  return `
    <article class="gcard" style="animation-delay:${Math.min(index * 35, 350)}ms">
      <div class="gtop">
        ${cover
          ? `<img class="thumb" data-cover="${esc(game.id)}" src="${esc(cover)}" alt="">`
          : `<div class="thumb ph2" data-cover="${esc(game.id)}">${esc(initial)}</div>`}
        <div class="ginfo">
          <div class="gname">${esc(game.name)}</div>
          <div class="badges">
            <span class="badge">${esc(sourceLabel(game.source))}</span>
            <span class="badge">${esc(apiLabel(game.graphicsApi))}</span>
            <span class="badge state ${stateClass}">${esc(stateText)}</span>
            ${mode.smoothMotion === 'on' ? '<span class="badge mode">SM</span>' : ''}
            ${mode.anr && mode.anr !== 'notDeployed' ? '<span class="badge mode">ANR</span>' : ''}
          </div>
        </div>
      </div>
      <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
      <div class="gactions">
        <button class="btn small ${deployed ? 'danger' : 'success'}" data-action="${deployed ? 'restore' : 'deploy'}" data-id="${esc(game.id)}" type="button">${esc(primary)}</button>
        <button class="btn small launch" data-action="launch" data-id="${esc(game.id)}" type="button">${t('launchGame')}</button>
        <span class="spacer"></span>
        <button class="btn small icon-only" data-action="folder" data-id="${esc(game.id)}" type="button" title="${t('openFolder')}" aria-label="打开文件夹">${folderIcon}</button>
        <button class="btn small danger icon-only" data-action="remove" data-id="${esc(game.id)}" type="button" title="${t('removeGame')}" aria-label="移除">${trashIcon}</button>
      </div>
    </article>`;
}

function renderGames() {
  const list = $('[data-role="games"]');
  if (!state.games.length) {
    list.innerHTML = '';
    $('[data-role="status"]').textContent = t('gameLibraryEmpty');
    return;
  }
  list.innerHTML = state.games.map(cardMarkup).join('');
  $('[data-role="status"]').textContent = `${t('libraryStatusLoaded', { count: state.games.length })}`;
}

function gameById(id) {
  return state.games.find((game) => game.id === id);
}

function bindFramegenActions() {
  const view = $('#view');
  view.onclick = async (event) => {
    const target = event.target.closest('[data-action]');
    if (!target) return;
    const action = target.dataset.action;
    const game = gameById(target.dataset.id);
    try {
      if (action === 'removed') {
        await showRemoved(refresh);
      } else if (action === 'scan') {
        await scan();
      } else if (action === 'add') {
        await addGame();
      } else if (action === 'launch' && game) {
        await call('launch', { gameId: game.id });
        toast(`已启动 ${game.name}`);
      } else if (action === 'folder' && game) {
        await call('openPath', { path: game.targetDirectory });
      } else if (action === 'library.remove' && game) {
        if (confirm(`从列表移除 ${game.name}？不会删除游戏文件。`)) {
          await call('library.remove', { gameId: game.id });
          await refresh();
        }
      } else if (action === 'deploy' && game) {
        openDeployDialog(game, refresh);
      } else if (action === 'restore' && game) {
        await restoreGame(game, refresh);
      } else if (action === 'open-hags') {
        await call('openHags');
      } else if (action === 'open-bundled') {
        await call('openFolder', { which: 'bundledPacks' });
      } else if (action === 'open-data') {
        await call('openFolder', { which: 'packs' });
      } else if (action === 'github') {
        event.preventDefault();
        window.fgm.openUrl('https://github.com/XiaoQAQ10086/FrameGen-Manager-Next');
      } else if (action === 'spoof-apply') {
        await applySpoof();
      } else if (action === 'spoof-restore') {
        await call('gpuSpoof.restore', { adapterIndex: 0, mode: 'backup' });
        toast('已还原驱动名称，重启后生效。');
        await refresh();
      }
    } catch (error) {
      toast(error.message, 'error');
    }
  };
}

async function scan() {
  showProgress('正在扫描游戏库');
  try {
    await call('library.scan');
    await refresh();
    toast('扫描完成');
  } catch (error) {
    toast(`扫描失败：${error.message}`, 'error');
  } finally {
    hideProgress();
  }
}

async function addGame() {
  const picked = await call('pickExecutable');
  if (picked.cancelled) return;
  await call('library.addManual', { path: picked.path });
  await refresh();
  toast('已添加游戏');
}

async function applySpoof() {
  const preset = $('[data-role="spoof-preset"]')?.value;
  if (!preset) return;
  if (!confirm(`把显卡型号伪装为 ${preset}？需要管理员权限，重启后生效。`)) return;
  await call('gpuSpoof.apply', { adapterIndex: 0, preset });
  toast('已写入，重启后生效。');
  await refresh();
}

export async function refresh() {
  const [gpu, modes, games, spoof] = await Promise.all([
    call('gpu'),
    call('gameModes', {}),
    call('library.list'),
    call('gpuSpoof.inspect'),
  ]);
  state.gpu = gpu.gpu;
  state.modes = Object.fromEntries((modes.modes || []).map((mode) => [mode.gameId, mode]));
  state.games = games.games || [];
  state.spoof = spoof;
  applyChrome();
  const languageSelect = $('#lang-select');
  if (languageSelect) languageSelect.value = state.settings.language || 'system';
  renderFramegen();
  const view = $('#view');
  if (view) view.scrollTop = 0;
  loadCovers();
}

$$('.nav-item').forEach((item) => item.addEventListener('click', () => selectSection(item.dataset.page)));
$('#lang-select').addEventListener('change', (event) => {
  const value = event.target.value;
  const language = value === 'english'
    ? 'english'
    : value === 'chineseSimplified'
      ? 'chineseSimplified'
      : (navigator.language || '').toLowerCase().startsWith('zh')
        ? 'chineseSimplified'
        : 'english';
  setLanguage(language);
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
