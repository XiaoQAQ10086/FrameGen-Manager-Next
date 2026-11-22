import {
  $, $$, apiLabel, applyChrome, call, confirmAction, esc, folderIcon, githubIcon, hagsClass, hagsLabel,
  hideProgress, prettyPath, proxyOwnerLabel, showProgress, sourceLabel, state, stateLabel, toast, trashIcon,
} from './core.js';
import { t } from './l10n.js';
import { loadCovers } from './covers.js';
import { openDeployDialog, restoreGame } from './deploy.js';
import { showRemoved } from './removed.js';

export function renderFramegen({ preserveScroll = false, root = null } = {}) {
  if (state.section !== 'framegen') return;
  const view = root || $('.page-host') || $('#view');
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
            <a class="source-link" data-action="github" href="#">${githubIcon}<span>${t('modeSourceLink')}</span></a>
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
  bindFramegenActions(view);
  view.dataset.ready = '1';
  if (!preserveScroll) {
    requestAnimationFrame(() => {
      const side = $('.side');
      if (side) side.scrollTop = 0;
    });
  }
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
    <div class="kv2 equal-values"><span>${t('gpuDriverVersionLabel')}</span><b class="badge">${esc(gpu.driverVersion || '—')}</b></div>
    <div class="kv2 equal-values"><span>${t('gpuHagsLabel')}</span><b class="badge ${hagsClass(gpu.hags)}">${esc(hagsLabel(gpu.hags))}</b></div>
    <button class="btn wide" data-action="open-hags" type="button">${t('gpuOpenHags')}</button>
    ${state.rtss ? `<p class="hint danger">${esc(t('rtssConflict'))}</p>` : ''}`;
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
  const assetVersion = mode.framegenVersion || mode.framegenPackVersion;
  const stateBadge = deployed && assetVersion
    ? `${stateText} ${t('assetVersionLabel')}：${assetVersion}`
    : stateText;
  const smOn = mode.smoothMotion === 'on';
  const smBlocked = smOn && !deployed;
  const anrOn = mode.anr && mode.anr !== 'notDeployed';
  const route = String(state.gpu?.route || '').toLowerCase();
  const gpuPending = !state.gpu;
  const gpuBlocked = !gpuPending && !['sm86', 'sm75', 'notneeded'].includes(route);
  const modesPending = !state.modesReady;
  const blocked = smBlocked || (anrOn && !deployed) || (gpuBlocked && !deployed)
    || ((gpuPending || modesPending) && !deployed);
  const blockedTitle = smBlocked ? t('smConflict')
    : (anrOn ? t('anrConflict')
      : (gpuPending || modesPending ? t('libraryStatusLoading') : t('gpuIncompatible')));
  const occupied = (mode.proxies || []).filter((entry) => !entry.ownedByFramegen);
  return `
    <article class="gcard" style="animation-delay:${Math.min(index * 35, 350)}ms">
      <div class="gtop">
        ${cover
          ? `<img class="thumb" data-cover="${esc(game.id)}" src="${esc(cover)}" alt="">`
          : `<div class="thumb ph2" data-cover="${esc(game.id)}">${esc(initial)}</div>`}
        <div class="ginfo">
          <div class="gname" title="${esc(game.name)}">${esc(game.name)}</div>
          <div class="badges">
            <span class="badge">${esc(sourceLabel(game.source))}</span>
            <span class="badge">${esc(apiLabel(game.graphicsApi))}</span>
            ${deployed
              ? `<span class="badge state ${stateClass}">${esc(stateBadge)}</span>`
              : (modesPending ? `<span class="badge dim">${esc(t('libraryStatusLoading'))}</span>` : '')}
            ${smOn ? `<span class="badge danger">${esc(t('smConflict'))}</span>` : ''}
            ${occupied.map((entry) => `<span class="badge warn">${esc(entry.module)} · ${esc(proxyOwnerLabel(entry.owner))}</span>`).join('')}
            ${anrOn ? `<span class="badge danger">${esc(t('anrConflict'))}</span>` : ''}
            ${gpuBlocked ? `<span class="badge danger">${esc(t('gpuIncompatible'))}</span>` : ''}
          </div>
        </div>
      </div>
      <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
      <div class="gactions">
        <button class="btn small ${deployed ? 'danger' : 'success'}" data-action="${deployed ? 'restore' : 'deploy'}" data-id="${esc(game.id)}" type="button" ${blocked ? 'disabled' : ''} ${blocked ? `title="${esc(blockedTitle)}"` : ''}>${esc(primary)}</button>
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

function bindFramegenActions(view = $('.page-host') || $('#view')) {
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
      } else if (action === 'remove' && game) {
        const ok = await confirmAction({
          title: t('removeGame'),
          body: t('removeGameConfirm', { game: game.name }),
          confirmLabel: t('removeGame'),
          cancelLabel: t('cancel'),
          danger: true,
        });
        if (!ok) return;
        await call('library.remove', { gameId: game.id });
        await refresh();
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
        window.fgm.openUrl('https://github.com/sdli1995/dlssg_for_sm86');
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
  showProgress(t('scanPhase_steam'), false);
  $('.gp-subject').textContent = t('scanProgressDetail', {
    done: 0,
    total: 4,
    found: 0,
    remaining: 4,
  });
  $('.gp-percent').textContent = '0%';
  $('.gp-track > i').style.width = '2%';
  try {
    const reply = await call('library.scan');
    const count = Array.isArray(reply.games) ? reply.games.length : state.games.length;
    $('.gp-phase').textContent = t('scanPhase_finalizing');
    $('.gp-subject').textContent = t('scanProgressDetail', {
      done: 4,
      total: 4,
      found: count,
      remaining: 0,
    });
    $('.gp-percent').textContent = '100%';
    $('.gp-track > i').style.width = '100%';
    await refresh();
    toast('扫描完成');
  } catch (error) {
    toast(`扫描失败：${error.message}`, 'error');
  } finally {
    hideProgress();
  }
}

async function addGame() {
  const picked = await call('pickExecutable', { title: t('pickExeTitle') });
  if (picked.cancelled) return;
  await call('library.addManual', { path: picked.path });
  await refresh();
  toast('已添加游戏');
}

async function applySpoof() {
  const preset = $('[data-role="spoof-preset"]')?.value;
  if (!preset) return;
  if (!(await confirmAction({
    title: t('gpuSpoofApply'),
    body: `${t('gpuSpoofConfirm', { name: preset })} ${t('gpuSpoofAdminWarning')}`,
    confirmLabel: t('gpuSpoofApply'),
    cancelLabel: t('cancel'),
    danger: true,
  }))) return;
  await call('gpuSpoof.apply', { adapterIndex: 0, preset });
  toast('已写入，重启后生效。');
  await refresh();
}

export async function refresh() {
  if (state.section !== 'framegen') return;
  const [gpu, modes, games, spoof] = await Promise.all([
    call('gpu'),
    call('gameModes', {}),
    call('library.list'),
    call('gpuSpoof.inspect'),
  ]);
  state.gpu = gpu.gpu;
  state.rtss = Boolean(gpu.rtss);
  state.modes = Object.fromEntries((modes.modes || []).map((mode) => [mode.gameId, mode]));
  state.modesReady = true;
  state.games = games.games || [];
  state.spoof = spoof;
  applyChrome();
  const languageSelect = $('#lang-select');
  if (languageSelect) languageSelect.value = state.settings.language || 'system';
  const view = $('.page-host') || $('#view');
  const list = $('[data-role="games"]');
  const side = $('.side');
  const scroll = {
    view: view?.scrollTop || 0,
    list: list?.scrollTop || 0,
    side: side?.scrollTop || 0,
  };
  renderFramegen({ preserveScroll: true, root: view });
  requestAnimationFrame(() => {
    const nextView = $('#view');
    const nextList = $('[data-role="games"]');
    const nextSide = $('.side');
    if (nextView) nextView.scrollTop = scroll.view;
    if (nextList) nextList.scrollTop = scroll.list;
    if (nextSide) nextSide.scrollTop = scroll.side;
  });
  loadCovers();
}
