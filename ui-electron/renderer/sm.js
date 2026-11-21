import {
  $, call, esc, folderIcon, githubIcon, hideProgress, prettyPath, showProgress, state, toast, trashIcon,
} from './core.js';
import { t } from './l10n.js';
import { showRemoved } from './removed.js';

let statuses = null;
let modes = {};
let serial = 0;
let cachedKey = '';
let cachedAt = 0;

export async function renderSm(root) {
  const current = ++serial;
  root.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>${t('navSmoothMotion')}</h1>
          <div class="sub" data-role="status">${t('libraryStatusLoading')}</div>
        </div>
        <div class="lh-actions">
          <button class="btn" data-action="add" type="button">${t('addGame')}</button>
          <button class="btn" data-action="removed" type="button">${t('removedTitle')}</button>
          <button class="btn scan" data-action="scan" type="button">${t('scanGames')}</button>
        </div>
      </header>
      <main class="layout">
        <aside class="side">
          <section class="panel" data-role="compat"></section>
          <section class="panel">
            <div class="ctitle">${t('modeSourceTitle')}</div>
            <div class="chips"><span class="chip gpu-chip">${t('modeGpusFramegen')}</span></div>
            <a class="source-link" data-action="github" href="#">${githubIcon}<span>${t('modeSourceLink')}</span></a>
          </section>
          <section class="panel">
            <div class="ctitle">${t('smNotesTitle')}</div>
            <p class="hint">${t('smNotesBody')}</p>
          </section>
        </aside>
        <section class="content">
          <div class="game-list" data-role="games">
            <div class="gcard skeleton-card"><div class="sk-cover"></div><div class="sk-body"><i></i><i></i><i></i></div></div>
          </div>
        </section>
      </main>
    </section>`;

  renderCompat($('[data-role="compat"]', root));
  const key = state.games.map((game) => game.renderExe || game.id).join('|');
  const cacheHit = statuses && cachedKey === key && current === serial && state.section === 'smooth-motion';
  if (cacheHit) {
    renderGames(root);
    bind(root);
    if (Date.now() - cachedAt < 10000) return;
    load().then(() => {
      if (current === serial && state.section === 'smooth-motion') renderGames(root);
    });
    return;
  }
  await load();
  cachedKey = key;
  if (current !== serial || state.section !== 'smooth-motion') return;
  renderGames(root);
  bind(root);
}

function renderCompat(panel) {
  const gpu = state.gpu;
  panel.innerHTML = `
    <div class="ctitle">${t('smCompatibilityTitle')}</div>
    <div class="chips"><span class="chip ${gpu ? 'ok' : 'dim'}">${gpu ? t('smReady') : t('gpuMissing')}</span></div>
    <div class="kv2"><span>${t('smGpuLabel')}</span><b class="badge">${esc(gpu?.name || '—')}</b></div>
    <div class="kv2"><span>${t('gpuDriverVersionLabel')}</span><b class="badge">${esc(gpu?.driverVersion || '—')}</b></div>
    <div class="kv2"><span>${t('smMinimum')}</span><b class="badge">616.64</b></div>
    <p class="hint">${gpu ? t('smDriverOk') : t('gpuMissing')}</p>`;
}

async function load() {
  const paths = state.games
    .map((game) => game.renderExe)
    .filter((value) => typeof value === 'string' && value.length > 0);
  try {
    const [many, modeReply] = await Promise.all([
      call('sm.inspectMany', { exePaths: paths }),
      call('gameModes', {}),
    ]);
    statuses = many.states || [];
    cachedAt = Date.now();
    modes = Object.fromEntries((modeReply.modes || []).map((mode) => [mode.gameId, mode]));
  } catch (error) {
    toast(`${t('smOperationFailed')}: ${error.message}`, 'error');
  }
}

function statusFor(game) {
  const index = state.games
    .filter((item) => typeof item.renderExe === 'string' && item.renderExe.length > 0)
    .findIndex((item) => item.id === game.id);
  return statuses[index] || {};
}

function renderGames(root) {
  if (state.section !== 'smooth-motion') return;
  const list = $('[data-role="games"]', root);
  if (!state.games.length) {
    list.innerHTML = '';
    $('[data-role="status"]', root).textContent = t('gameLibraryEmpty');
    return;
  }
  list.innerHTML = state.games.map((game, index) => cardMarkup(game, index)).join('');
  $('[data-role="status"]', root).textContent = t('smChecked', { count: state.games.length });
}

function cardMarkup(game, index) {
  const status = statusFor(game);
  const mode = modes[game.id] || {};
  const component = mode.smoothMotionComponent || 'notDeployed';
  const enabled = status.enabled === true;
  const framegenOn = (mode.framegen || 'notDeployed') !== 'notDeployed';
  const outdated = component === 'outdated';
  const blocked = framegenOn && !enabled;
  const cover = state.covers[game.id];
  const initial = game.name ? game.name.slice(0, 1) : '?';
  return `
    <article class="gcard" style="animation-delay:${Math.min(index * 35, 350)}ms">
      <div class="gtop">
        ${cover ? `<img class="thumb" src="${esc(cover)}" alt="">` : `<div class="thumb ph2">${esc(initial)}</div>`}
        <div class="ginfo">
          <div class="gname">${esc(game.name)}</div>
          <div class="badges">
            <span class="badge ${component === 'installed' ? 'ok' : (component === 'notDeployed' ? 'dim' : 'warn')}">${esc(componentLabel(component))}</span>
            ${enabled ? `<span class="badge ok">${t('smDriverOn')}</span>` : `<span class="badge dim">${t('smDriverOff')}</span>`}
            ${framegenOn ? `<span class="badge danger">${t('smConflict')}</span>` : ''}
          </div>
        </div>
      </div>
      <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
      ${status.detail ? `<div class="hint">${esc(status.detail)}</div>` : ''}
      <div class="gactions">
        <button class="btn small ${enabled ? 'danger' : 'success'}" data-action="${enabled ? 'disable' : 'enable'}" data-id="${esc(game.id)}" type="button" ${blocked ? 'disabled' : ''} ${blocked ? `title="${esc(t('smConflict'))}"` : ''}>${enabled ? t('smDisable') : (outdated ? t('smUpdate') : t('smEnable'))}</button>
        <button class="btn small launch" data-action="launch" data-id="${esc(game.id)}" type="button">${t('launchGame')}</button>
        <span class="spacer"></span>
        <button class="btn small icon-only" data-action="folder" data-id="${esc(game.id)}" type="button" title="${esc(t('openFolder'))}">${folderIcon}</button>
        <button class="btn small danger icon-only" data-action="remove" data-id="${esc(game.id)}" type="button" title="${esc(t('removeGame'))}">${trashIcon}</button>
      </div>
    </article>`;
}

function componentLabel(component) {
  return {
    installed: t('smComponentDeployed'),
    outdated: t('smComponentOutdated'),
    partial: t('smComponentPartial'),
    conflict: t('smComponentConflict'),
    installedWithoutManifest: t('smComponentPresent'),
  }[component] || t('smComponentNotDeployed');
}

function bind(root) {
  root.onclick = async (event) => {
    const target = event.target.closest('[data-action]');
    if (!target) return;
    const action = target.dataset.action;
    const game = state.games.find((item) => item.id === target.dataset.id);
    try {
      if (action === 'removed') {
        await showRemoved(async () => {
          state.games = (await call('library.list')).games || [];
          await load();
          renderGames(root);
        });
      } else if (action === 'enable' && game) {
        showProgress(t('smEnable'));
        try {
          await call('sm.enable', {
            game: game.name,
            exePath: game.renderExe,
            targetDir: game.targetDirectory,
          });
          await load();
          renderGames(root);
          toast(t('smEnabledDone'));
        } finally {
          hideProgress();
        }
      } else if (action === 'disable' && game) {
        showProgress(t('smDisable'));
        try {
          await call('sm.disable', {
            game: game.name,
            exePath: game.renderExe,
            targetDir: game.targetDirectory,
          });
          await load();
          renderGames(root);
          toast(t('smDisabledDone'));
        } finally {
          hideProgress();
        }
      } else if (action === 'launch' && game) {
        await call('launch', { gameId: game.id });
        toast(`${t('launchGame')}: ${game.name}`);
      } else if (action === 'folder' && game) {
        await call('openPath', { path: game.targetDirectory });
      } else if (action === 'remove' && game) {
        if (confirm(`${t('removeGame')} ${game.name}?`)) {
          await call('library.remove', { gameId: game.id });
          state.games = state.games.filter((item) => item.id !== game.id);
          await load();
          renderGames(root);
        }
      } else if (action === 'scan') {
        await call('library.scan');
        const reply = await call('library.list');
        state.games = reply.games || [];
        await load();
        renderGames(root);
      } else if (action === 'add') {
        const picked = await call('pickExecutable');
        if (!picked.cancelled) {
          await call('library.addManual', { path: picked.path });
          const reply = await call('library.list');
          state.games = reply.games || [];
          await load();
          renderGames(root);
        }
      } else if (action === 'github') {
        event.preventDefault();
        window.fgm.openUrl('https://github.com/apex-compute/ANR');
      }
    } catch (error) {
      toast(error.message, 'error');
    }
  };
}
