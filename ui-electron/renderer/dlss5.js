import { $, call, esc, githubIcon, hideProgress, prettyPath, showProgress, state, toast } from './core.js';
import { t } from './l10n.js';
import { showRemoved } from './removed.js';

let inspections = {};
let selected = null;
let detail = null;
let detailApi = 'auto';
let detailProxy = 'auto';
let recording = false;
let serial = 0;

export async function renderDlss5(root) {
  const current = ++serial;
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
            <a class="source-link" data-action="github" href="#">${githubIcon}<span>${t('modeSourceLink')}</span></a>
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
  if (current !== serial || state.section !== 'dlss5') return;
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
  if (state.section !== 'dlss5') return;
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
      } else if (action === 'record-hotkey' && selected) {
        await recordHotkey(selected);
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
  detailApi = 'auto';
  detailProxy = 'auto';
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
        <div class="options">
          <label><span>${t('deployGraphicsApi')}</span><div class="select-wrap"><select data-detail="api">
            <option value="auto">${esc(t('dlss5ApiAuto', { api: String(game.graphicsApi || 'dx12').toUpperCase() }))}</option>
            <option value="dx12">DX12</option><option value="dx11">DX11</option><option value="dx10">DX10</option>
            <option value="dx9">DX9</option><option value="vulkan">Vulkan</option><option value="opengl">OpenGL</option>
          </select></div></label>
          <label><span>${t('optionProxySlot')}</span><div class="select-wrap"><select data-detail="proxy">
            <option value="auto">${esc(t('dlss5ProxyAuto', { proxy: autoProxy(game) }))}</option>
            <option value="dxgi.dll">dxgi.dll</option><option value="d3d12.dll">d3d12.dll</option>
            <option value="d3d11.dll">d3d11.dll</option><option value="d3d10.dll">d3d10.dll</option>
            <option value="d3d9.dll">d3d9.dll</option><option value="opengl32.dll">opengl32.dll</option>
          </select></div></label>
        </div>
        <div class="kv2 hotkey-row"><span>${t('dlss5OverlayKey')}</span>
          <div class="hotkey-controls">
            <b class="badge">${esc(hotkeyLabel(reply.hotkeyVk))}</b>
            <button class="btn small" data-action="record-hotkey" type="button">${t('dlss5RecordHotkey')}</button>
          </div>
        </div>
        <p class="hint">${t('dlss5DetailSubtitle')}</p>
      </div>
      <footer class="modal-foot">
        <span class="muted small">${t('deployCloseGameHint')}</span>
        <div class="foot-actions">
          <button class="btn" data-action="open-folder" type="button">${t('openFolder')}</button>
          <button class="btn danger" data-action="uninstall" type="button" ${deployed(reply) ? '' : 'disabled'}>${t('dlss5Uninstall')}</button>
          <button class="btn success" data-action="install" type="button">${t('dlss5Install')}</button>
        </div>
      </footer>
    </section>`;
  $('#modal-root').appendChild(detail);
  const close = () => {
    detail?.remove();
    detail = null;
  };
  $('[data-action="close-detail"]', detail).onclick = close;
  $('[data-action="open-folder"]', detail).onclick = () => call('openPath', { path: game.targetDirectory });
  $('[data-action="record-hotkey"]', detail).onclick = () => recordHotkey(game);
  $('[data-action="install"]', detail).onclick = () => install(root, game);
  $('[data-action="uninstall"]', detail).onclick = () => uninstall(root, game);
  detail.onchange = (event) => {
    if (event.target.dataset.detail === 'api') detailApi = event.target.value;
    if (event.target.dataset.detail === 'proxy') detailProxy = event.target.value;
  };
  detail.onclick = (event) => {
    if (event.target === detail) {
      detail.remove();
      detail = null;
    }
  };
}

function autoProxy(game) {
  const api = String(game.graphicsApi || '').toLowerCase();
  if (api === 'vulkan') return t('optionOff');
  if (api === 'dx9') return 'd3d9.dll';
  if (api === 'opengl') return 'opengl32.dll';
  return 'dxgi.dll';
}

function resolveApi(game) {
  return detailApi === 'auto'
    ? (String(game.graphicsApi || 'dx12').toLowerCase() === 'unknown'
        ? 'dx12'
        : String(game.graphicsApi).toLowerCase())
    : detailApi;
}

async function install(root, game) {
  if (!confirm(t('dlss5ConfirmBody', { game: game.name }))) return;
  const api = resolveApi(game);
  const proxy = detailProxy === 'auto' ? undefined : detailProxy;
  showProgress(t('dlss5Install'));
  try {
    const reply = await call('dlss5.install', {
      exePath: game.renderExe,
      targetDir: game.targetDirectory,
      api,
      proxy,
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


async function recordHotkey(game) {
  if (recording) return;
  recording = true;
  toast(t('dlss5RecordHotkey'));
  const handler = async (event) => {
    event.preventDefault();
    window.removeEventListener('keydown', handler, true);
    recording = false;
    const virtualKey = keyToVirtualKey(event);
    if (!virtualKey) {
      toast(t('smUnknown'), 'warn');
      return;
    }
    try {
      const reply = await call('dlss5.setHotkey', {
        targetDir: game.targetDirectory,
        virtualKey,
        exePath: game.renderExe,
      });
      if (reply.deferred) toast(t('dlss5HotkeyDeferred'));
      await load();
      renderWall(document.querySelector('#view'));
      detail?.remove();
      detail = null;
    } catch (error) {
      toast(error.message, 'error');
    }
  };
  window.addEventListener('keydown', handler, true);
}

function hotkeyLabel(vk) {
  const value = Number(vk);
  if (!Number.isFinite(value) || value <= 0) return 'Home';
  const names = {
    0x08: 'Backspace', 0x09: 'Tab', 0x0d: 'Enter', 0x1b: 'Esc', 0x20: 'Space',
    0x21: 'PageUp', 0x22: 'PageDown', 0x23: 'End', 0x24: 'Home', 0x25: 'Left',
    0x26: 'Up', 0x27: 'Right', 0x28: 'Down', 0x2d: 'Insert', 0x2e: 'Delete',
  };
  if (names[value]) return names[value];
  if (value >= 0x70 && value <= 0x7b) return `F${value - 0x6f}`;
  if ((value >= 65 && value <= 90) || (value >= 48 && value <= 57)) return String.fromCharCode(value);
  return `VK ${value}`;
}

function keyToVirtualKey(event) {
  const key = event.key;
  if (/^[a-zA-Z]$/.test(key)) return key.toUpperCase().charCodeAt(0);
  if (/^[0-9]$/.test(key)) return key.charCodeAt(0);
  if (/^F([1-9]|1[0-2])$/.test(key)) return 0x70 + Number(key.slice(1)) - 1;
  return { Escape: 0x1b, ' ': 0x20, Tab: 0x09, Enter: 0x0d }[key] || null;
}
