import { $, call, esc, hideProgress, prettyPath, showProgress, state, toast } from './core.js';
import { t } from './l10n.js';
import { showRemoved } from './removed.js';

let inspections = {};
let selected = null;
let detail = null;

export async function renderDlss5(root) {
  root.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>${t('navDlss5')}</h1>
          <div class="sub" data-role="status">${t('dlss5Checked', { count: state.games.length })}</div>
        </div>
        <div class="lh-actions">
          <button class="btn" data-action="add" type="button">${t('addGame')}</button>
          <button class="btn" data-action="removed" type="button">${t('removedTitle')}</button>
          <button class="btn scan" data-action="scan" type="button">${t('scanGames')}</button>
        </div>
      </header>
      <main class="layout">
        <aside class="side">
          <section class="panel">
            <div class="ctitle">${t('modeSourceTitle')}</div>
            <div class="chips"><span class="chip gpu-chip">${t('modeGpusFramegen')}</span></div>
            <a class="source-link" data-action="github" href="#">${t('modeSourceLink')}</a>
          </section>
        </aside>
        <section class="content">
          <div class="wall" data-role="games">
            <div class="tile skeleton-tile"></div>
            <div class="tile skeleton-tile"></div>
          </div>
        </section>
      </main>
    </section>`;

  await load();
  renderWall(root);
  bind(root);
}

async function load() {
  const results = await Promise.all(
    state.games.map(async (game) => {
      try {
        const reply = await call('dlss5.inspect', { targetDir: game.targetDirectory });
        return [game.id, reply];
      } catch {
        return [game.id, null];
      }
    }),
  );
  inspections = Object.fromEntries(results);
}

function deployed(reply) {
  return reply && ['installed', 'outdated', 'partial', 'conflict', 'installedWithoutManifest'].includes(reply.state);
}

function renderWall(root) {
  const wall = $('[data-role="games"]', root);
  if (!state.games.length) {
    wall.innerHTML = '';
    return;
  }
  wall.innerHTML = state.games
    .map((game, index) => {
      const reply = inspections[game.id];
      const cover = state.covers[game.id];
      const legacy = reply?.legacyFiles?.length > 0;
      return `
        <div class="tile" role="button" tabindex="0" data-action="open" data-id="${esc(game.id)}" style="--i:${index}">
          ${cover ? `<img src="${esc(cover)}" alt="" loading="lazy" decoding="async">` : `<div class="ph">${esc(game.name.slice(0, 1))}</div>`}
          ${legacy ? `<span class="ribbon warn">${t('dlss5LegacyRibbon')}</span>` : ''}
          <span class="meta">
            <span class="name">${esc(game.name)}</span>
            <span class="tag">${esc(t('dlss5ApiLabel', { api: String(game.graphicsApi || 'unknown').toUpperCase() }))}</span>
            <span class="tag ${deployed(reply) ? 'ok' : ''}">${esc(deployed(reply) ? t('dlss5Deployed') : t('dlss5NotDeployed'))}</span>
          </span>
        </div>`;
    })
    .join('');
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
          renderWall(root);
        });
      } else if (action === 'open' && game) {
        openDetail(root, game);
      } else if (action === 'github') {
        event.preventDefault();
        window.fgm.openUrl('https://github.com/apex-compute/DLSS5');
      } else if (action === 'scan') {
        await call('library.scan');
        state.games = (await call('library.list')).games || [];
        await load();
        renderWall(root);
      } else if (action === 'add') {
        const picked = await call('pickExecutable');
        if (!picked.cancelled) {
          await call('library.addManual', { path: picked.path });
          state.games = (await call('library.list')).games || [];
          await load();
          renderWall(root);
        }
      } else if (action === 'install' && selected) {
        await install(root, selected);
      } else if (action === 'uninstall' && selected) {
        await uninstall(root, selected);
      } else if (action === 'close-detail') {
        detail?.remove();
        detail = null;
      } else if (action === 'open-folder' && selected) {
        await call('openPath', { path: selected.targetDirectory });
      }
    } catch (error) {
      toast(error.message, 'error');
    }
  };
}

function openDetail(root, game) {
  selected = game;
  const reply = inspections[game.id] || {};
  detail?.remove();
  detail = document.createElement('div');
  detail.className = 'overlay';
  detail.innerHTML = `
    <section class="modal dlss-modal" role="dialog" aria-modal="true">
      <header class="modal-head">
        <div><h2>${esc(game.name)}</h2><p class="muted">${esc(prettyPath(game.targetDirectory))}</p></div>
        <button class="icon-btn" data-action="close-detail" type="button">✕</button>
      </header>
      <div class="dlss-detail">
        <div class="kv2"><span>${t('deployGraphicsApi')}</span><b class="badge">${esc(String(game.graphicsApi || 'unknown').toUpperCase())}</b></div>
        <div class="kv2"><span>${t('dlss5OverlayKey')}</span><b class="badge">${reply.hotkeyVk ?? '-'}</b></div>
        <p class="hint">${t('dlss5DetailSubtitle')}</p>
      </div>
      <footer class="modal-foot">
        <span class="muted small">${t('deployCloseGameHint')}</span>
        <div class="foot-actions">
          <button class="btn" data-action="open-folder" type="button">${t('openFolder')}</button>
          <button class="btn danger" data-action="uninstall" type="button">${t('dlss5Uninstall')}</button>
          <button class="btn success" data-action="install" type="button">${t('dlss5Install')}</button>
        </div>
      </footer>
    </section>`;
  $('#modal-root').appendChild(detail);
  detail.onclick = (event) => {
    if (event.target === detail) {
      detail.remove();
      detail = null;
    }
  };
}

async function install(root, game) {
  if (!confirm(t('dlss5ConfirmBody', { game: game.name }))) return;
  const api = String(game.graphicsApi || 'dx12').toLowerCase();
  showProgress(t('dlss5Install'));
  try {
    const reply = await call('dlss5.install', {
      exePath: game.renderExe,
      targetDir: game.targetDirectory,
      api: api === 'unknown' ? 'dx12' : api,
    });
    detail?.remove();
    detail = null;
    await load();
    renderWall(root);
    toast(t('dlss5Complete', { proxy: reply.proxyModule || '—' }));
  } finally {
    hideProgress();
  }
}

async function uninstall(root, game) {
  if (!confirm(t('dlss5UninstallConfirm', { game: game.name }))) return;
  showProgress(t('dlss5Uninstall'));
  try {
    const reply = await call('dlss5.uninstall', { targetDir: game.targetDirectory });
    detail?.remove();
    detail = null;
    await load();
    renderWall(root);
    toast(
      reply.conflicts?.length
        ? t('dlss5UninstallConflicts', { count: reply.conflicts.length })
        : t('dlss5Uninstalled'),
    );
  } finally {
    hideProgress();
  }
}
