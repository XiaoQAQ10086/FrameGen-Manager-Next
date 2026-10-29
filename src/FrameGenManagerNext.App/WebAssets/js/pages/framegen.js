import { escapeHtml as esc, confirmDialog } from '../ui.js';
import { prettyPath } from '../labels.js';

const launcherKeys = {
  Steam: 'Common.Steam',
  Epic: 'Common.Epic',
  WeGame: 'Common.WeGame',
  Loose: 'Common.Loose',
  Manual: 'Common.Manual',
};

const apiKeys = {
  Direct3D12: 'DX12',
  Direct3D11: 'DX11',
  Direct3D10: 'DX10',
  Direct3D9: 'DX9',
  Vulkan: 'Vulkan',
  OpenGl: 'OpenGL',
  Unknown: '未知',
};

const stateKeys = {
  Installed: 'State.Installed',
  Outdated: 'State.Outdated',
  Partial: 'State.Partial',
  Conflict: 'State.Conflict',
  InstalledWithoutManifest: 'State.InstalledWithoutManifest',
  NotDeployed: 'State.NotDeployed',
};

const routeKeys = {
  Sm86: 'Gpu.Route.Sm86',
  Sm75: 'Gpu.Route.Sm75',
  Gtx16: 'Gpu.Route.Gtx16',
  NoTensorCore: 'Gpu.Route.NoTensorCore',
  NotNeeded: 'Gpu.Route.NotNeeded',
  Unsupported: 'Gpu.Route.Unsupported',
  Unknown: 'Gpu.Route.Unknown',
};

const hagsKeys = {
  on: 'Gpu.Hags.On',
  off: 'Gpu.Hags.Off',
  unknown: 'Gpu.Hags.Unknown',
};

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let games = [];
    let removed = [];
    let gpu = null;
    let options = null;
    let busy = false;
    let current = null;

    root.innerHTML = `
      <section class="page">
        <header class="listhead">
          <div class="lh-text">
            <h1>${esc(t('Library.Title'))}</h1>
            <div class="sub" data-role="status">${esc(t('Library.Status.Loading'))}</div>
          </div>
          <div class="lh-actions">
            <button class="btn" data-action="add" type="button">${esc(t('Library.Add'))}</button>
            <button class="btn" data-action="removed" type="button">${esc(t('Library.Removed'))}<span class="count" data-role="removed-count"></span></button>
            <button class="btn hidden" data-action="cancel" type="button" disabled>${esc(t('Common.Cancel'))}</button>
            <button class="btn scan" data-action="scan" type="button">${esc(t('Library.Scan'))}</button>
          </div>
        </header>
        <div class="notice hidden" data-role="notice"></div>
        <main class="layout">
          <aside class="side">
            <section class="panel" data-role="gpu">
              <div class="ctitle">${esc(t('Gpu.SettingsTitle'))}</div>
              <div class="hint">${esc(t('Gpu.Status.Reading'))}</div>
            </section>
            <section class="panel">
              <div class="ctitle">${esc(t('Gpu.SpoofTitle'))}</div>
              <div data-role="spoof"><div class="hint">${esc(t('Gpu.Status.Reading'))}</div></div>
            </section>
            <section class="panel">
              <div class="ctitle">${esc(t('Library.AssetsFolder'))}</div>
              <div class="path" data-role="data-path"></div>
              <button class="btn wide" data-action="open-data" type="button">${esc(t('Library.OpenFolder'))}</button>
            </section>
          </aside>
          <section class="content">
            <div class="game-list" data-role="games">
              <div class="gcard skeleton-card">
                <div class="sk-cover"></div>
                <div class="sk-body"><i></i><i></i><i></i></div>
              </div>
              <div class="gcard skeleton-card">
                <div class="sk-cover"></div>
                <div class="sk-body"><i></i><i></i><i></i></div>
              </div>
              <div class="gcard skeleton-card">
                <div class="sk-cover"></div>
                <div class="sk-body"><i></i><i></i><i></i></div>
              </div>
            </div>
            <div class="empty hidden" data-role="empty">
              <p>${esc(t('Library.Empty'))}</p>
              <button class="btn scan" data-action="scan" type="button">${esc(t('Library.Scan'))}</button>
            </div>
          </section>
        </main>
      </section>
      <div class="overlay hidden" data-role="overlay">
        <section class="modal" role="dialog" aria-modal="true">
          <header class="modal-head">
            <div><h2 data-role="modal-title"></h2><p class="muted" data-role="modal-path"></p></div>
            <button class="icon-btn" data-close type="button">✕</button>
          </header>
          <p class="hint">${esc(t('Deploy.DialogHint'))}</p>
          <div class="options" data-role="options"></div>
          <footer class="modal-foot">
            <span class="muted small">${esc(t('Deploy.CloseGameHint'))}</span>
            <div class="foot-actions">
              <button class="btn ghost" data-close type="button">${esc(t('Common.Cancel'))}</button>
              <button class="btn" data-action="modal-restore" type="button">${esc(t('Common.Restore'))}</button>
              <button class="btn primary" data-action="modal-deploy" type="button">${esc(t('Common.Deploy'))}</button>
            </div>
          </footer>
        </section>
      </div>
      <div class="overlay hidden" data-role="removed-overlay">
        <section class="modal removed-modal" role="dialog" aria-modal="true">
          <header class="modal-head">
            <div>
              <h2>${esc(t('Library.RemovedTitle'))}</h2>
              <p class="muted">${esc(t('Library.RemovedHint'))}</p>
            </div>
            <button class="icon-btn" data-close-removed type="button">✕</button>
          </header>
          <div class="removed-list" data-role="removed-list"></div>
        </section>
      </div>`;

    const q = (selector) => root.querySelector(selector);
    const overlay = q('[data-role="overlay"]');
    const removedOverlay = q('[data-role="removed-overlay"]');
    const removedList = q('[data-role="removed-list"]');
    const removedCount = q('[data-role="removed-count"]');
    const modalTitle = q('[data-role="modal-title"]');
    const modalPath = q('[data-role="modal-path"]');
    const optionsRoot = q('[data-role="options"]');
    const dataPath = q('[data-role="data-path"]');
    if (dataPath) dataPath.textContent = prettyPath(ctx.info?.bundledPacksDirectory || ctx.info?.packsDirectory || ctx.info?.dataRoot || '');

    function setBusy(value) {
      busy = value;
      root.querySelectorAll('[data-action="scan"], [data-action="add"]').forEach((button) => {
        button.disabled = value;
      });
    }

    function status(message) {
      q('[data-role="status"]').textContent = message;
    }

    function notice(message) {
      const node = q('[data-role="notice"]');
      node.textContent = message || '';
      node.classList.toggle('hidden', !message);
    }

    function renderGpu() {
      const g = gpu.gpu;
      const s = gpu.spoof;
      const hagsClass = g.hags === 'on' ? 'ok' : (g.hags === 'off' ? 'warn' : 'dim');
      q('[data-role="gpu"]').innerHTML = `
        <div class="ctitle">${esc(t('Gpu.SettingsTitle'))}</div>
        <div class="chips">
          <span class="chip gpu-chip" title="${esc(g.name)}">${esc(g.name)}</span>
        </div>
        <div class="kv2"><span>${esc(t('Gpu.DriverVersion'))}</span><b class="badge">${esc(g.driver || t('Gpu.Status.UnknownDriver'))}</b></div>
        <div class="kv2"><span>${esc(t('Gpu.Hags'))}</span><b class="badge ${hagsClass}">${esc(t(hagsKeys[g.hags] || 'Gpu.Hags.Unknown'))}</b></div>
        <button class="btn wide" data-action="open-hags" type="button">${esc(t('Gpu.GoSettings'))}</button>`;
      q('[data-role="spoof"]').innerHTML = `
        <div class="kv2 stacked"><span>${esc(t('Gpu.Card'))}</span><b class="badge" title="${esc(s.current || '—')}">${esc(s.current || '—')}</b></div>
        <div class="kv2 stacked"><span>${esc(t('Gpu.DriverNameLabel'))}</span><b class="badge" title="${esc(s.driver || '—')}">${esc(s.driver || '—')}</b></div>
        <p class="hint">${esc(t('Gpu.Spoof.Warning'))}</p>
        <div class="row">
          <select data-role="spoof-preset">${s.presets.map((preset) => `<option>${esc(preset)}</option>`).join('')}</select>
          <button class="btn primary small" data-action="spoof-apply" type="button">${esc(t('Gpu.ApplySpoof'))}</button>
        </div>
        <button class="btn wide" data-action="spoof-driver" type="button" ${s.spoofed ? '' : 'disabled'}>${esc(t('Gpu.RestoreDriver'))}</button>`;
    }

    function renderGames() {
      const list = q('[data-role="games"]');
      list.innerHTML = games.map((game, index) => `
        <article class="gcard" style="animation-delay:${Math.min(index * 35, 350)}ms">
          <div class="gtop">
            ${game.cover ? `<img class="thumb" src="${game.cover}" alt="">` : `<div class="thumb ph2">${esc(game.initial)}</div>`}
            <div class="ginfo">
              <div class="gname">${esc(game.name)}</div>
              <div class="badges">
                <span class="badge">${esc(t(launcherKeys[game.launcher] || 'Common.Unknown'))}</span>
                <span class="badge">${esc(apiKeys[game.api] || game.api || '未知')}</span>
                <span class="badge ${game.state === 'Installed' ? 'ok' : (game.state === 'Partial' || game.state === 'Conflict' ? 'warn' : 'dim')}">${esc(t(stateKeys[game.state] || 'State.NotDeployed'))}</span>
              </div>
            </div>
          </div>
          <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
          <div class="gactions">
            <button class="btn small primary" data-action="deploy" data-id="${esc(game.id)}" type="button">${esc(t('Common.Deploy'))}</button>
            <button class="btn small" data-action="restore" data-id="${esc(game.id)}" type="button">${esc(t('Common.Restore'))}</button>
            <button class="btn small" data-action="open" data-id="${esc(game.id)}" type="button">${esc(t('Library.OpenFolder'))}</button>
            <button class="btn small danger" data-action="remove" data-id="${esc(game.id)}" type="button">${esc(t('Common.Remove'))}</button>
          </div>
        </article>`).join('');
      q('[data-role="empty"]').classList.toggle('hidden', games.length > 0);
      list.classList.toggle('hidden', games.length === 0);
      updateRemovedCount();
    }

    function updateRemovedCount() {
      removedCount.textContent = removed.length > 0 ? ` (${removed.length})` : '';
    }

    function renderRemoved() {
      removedList.innerHTML = removed.length > 0
        ? removed.map((game) => `
          <div class="removed-row">
            <div class="removed-initial">${esc(game.initial)}</div>
            <div class="removed-info">
              <div class="removed-name">${esc(game.name)}</div>
              <div class="game-path" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
              <div class="chips">
                <span class="chip">${esc(t(launcherKeys[game.launcher] || 'Common.Unknown'))}</span>
                <span class="chip">${esc(apiKeys[game.api] || game.api || '未知')}</span>
              </div>
            </div>
            <button class="btn small primary" data-action="restore-removed" data-id="${esc(game.id)}" type="button">${esc(t('Library.RestoreRemoved'))}</button>
          </div>`).join('')
        : `<p class="muted removed-empty">${esc(t('Library.RemovedEmpty'))}</p>`;
      updateRemovedCount();
    }

    function openRemoved() {
      renderRemoved();
      removedOverlay.classList.remove('hidden');
    }

    function closeRemoved() {
      removedOverlay.classList.add('hidden');
    }

    function openModal(game) {
      current = game;
      modalTitle.textContent = game.name;
      modalPath.textContent = prettyPath(game.targetDirectory);
      optionsRoot.innerHTML = optionFields();
      overlay.classList.remove('hidden');
      syncEngine();
    }

    function closeModal() {
      overlay.classList.add('hidden');
      current = null;
    }

    function select(id, values, selected) {
      return `<select data-option="${id}">${values.map((value) => `<option value="${esc(value)}" ${String(value) === String(selected) ? 'selected' : ''}>${esc(value)}</option>`).join('')}</select>`;
    }

    function optionFields() {
      return `
        <label>${esc(t('Library.Proxy'))}${select('proxySlot', options.proxySlots, 'version.dll')}</label>
        <label>${esc(t('Library.Optimized'))}${select('optimized', options.optimizedLevels, 1)}</label>
        <label>${esc(t('Library.Frames'))}${select('frames', options.frameMultipliers, 3)}</label>
        <label>${esc(t('Library.Preset'))}${select('preset', options.presets, 'A')}</label>
        <label>${esc(t('Library.Engine'))}${select('engine', options.engines, 'DLSS')}</label>
        <label>${esc(t('Library.SuperResolution'))}${select('superResolutionMode', options.superResolutionModes, 'Off')}</label>
        <label class="xess">${esc(t('Library.XeSsFlow'))}${select('xeSsFlow', options.xeSsFlowResolutions, 'Middle')}</label>
        <label class="xess">${esc(t('Library.XeSsHudless'))}${select('xeSsHudlessValues', options.xeSsHudlessValues, 'Auto')}</label>`;
    }

    function syncEngine() {
      const engine = optionsRoot.querySelector('[data-option="engine"]')?.value;
      optionsRoot.querySelectorAll('.xess').forEach((label) => label.classList.toggle('hidden', engine !== 'XeSS'));
    }

    function readOptions() {
      const get = (name) => optionsRoot.querySelector(`[data-option="${name}"]`)?.value;
      return {
        proxySlot: get('proxySlot'),
        optimized: Number(get('optimized')),
        frames: Number(get('frames')),
        preset: get('preset'),
        engine: get('engine'),
        superResolutionMode: get('superResolutionMode'),
        xeSsFlow: get('xeSsFlow'),
        xeSsHudless: get('xeSsHudless'),
      };
    }

    async function load() {
      // Let the game list paint as soon as its data arrives; GPU probing may take
      // longer because it queries the driver.
      const gpuPromise = api.request('gpu.state').catch((error) => {
        ui.toast(error.message || error.code || 'GPU 状态读取失败', 'error');
        return null;
      });

      try {
        const gamesState = await api.request('games.state');
        options = gamesState.options;
        games = gamesState.games;
        removed = gamesState.removed || [];
        renderGames();
        renderRemoved();
        status(t('Library.Status.Loaded').replace('{0}', games.length));
        notice(gamesState.warning);

        const gpuState = await gpuPromise;
        if (gpuState) {
          gpu = gpuState;
          renderGpu();
        }
      } catch (error) {
        games = [];
        removed = [];
        renderGames();
        status(error.code || '加载失败');
        notice(error.message);
      }
    }

    async function run(action, successText) {
      if (busy) return;
      setBusy(true);
      try {
        const next = await action();
        if (next?.games) {
          games = next.games;
          removed = next.removed || removed;
          options = next.options || options;
          renderGames();
          renderRemoved();
          status(t('Library.Status.Loaded').replace('{0}', games.length));
        }
        if (next?.options) options = next.options;
        if (successText) ui.toast(successText);
      } catch (error) {
        ui.toast(error.message || error.code || '操作失败', 'error');
      } finally {
        setBusy(false);
      }
    }

    async function scan() {
      const buttons = root.querySelectorAll('[data-action="scan"]');
      const cancel = q('[data-action="cancel"]');
      buttons.forEach((button) => button.classList.add('loading'));
      cancel.classList.remove('hidden');
      cancel.disabled = false;
      try {
        await run(() => api.request('games.scan'), null);
      } finally {
        buttons.forEach((button) => button.classList.remove('loading'));
        cancel.classList.add('hidden');
        cancel.disabled = true;
      }
    }

    const onRootClick = async (event) => {
      const target = event.target.closest('[data-action]');
      if (!target) return;
      const action = target.dataset.action;
      const id = target.dataset.id;

      if (action === 'scan') return scan();
      if (action === 'add') {
        return run(() => api.request('games.add'), null);
      }
      if (action === 'cancel') {
        await api.request('games.cancelScan').catch(() => {});
        return;
      }
      if (action === 'removed') {
        openRemoved();
        return;
      }
      if (action === 'restore-removed') {
        const game = removed.find((item) => item.id === id);
        if (!game) return;
        await run(
          () => api.request('games.restore', { id }),
          t('Library.Status.Restored').replace('{0}', game.name),
        );
        return;
      }
      if (action === 'open-hags') {
        await api.request('settings.openHags').catch(() => {});
        return;
      }
      if (action === 'open-data') {
        await api.request('settings.openFolder', { which: 'bundledPacks' })
          .catch((error) => ui.toast(error.message, 'error'));
        return;
      }
      if (action === 'spoof-apply') {
        const preset = q('[data-role="spoof-preset"]').value;
        if (!confirm(t('Gpu.Spoof.ConfirmApply').replace('{0}', preset).replace('{1}', t('Gpu.Spoof.Warning')))) return;
        try {
          gpu = await api.request('gpu.spoof.apply', { preset });
          renderGpu();
          ui.toast(t('Gpu.Spoof.Done'));
        } catch (error) {
          ui.toast(error.message, 'error');
        }
        return;
      }
      if (action === 'spoof-driver') {
        if (!confirm(t('Gpu.Spoof.ConfirmDriver'))) return;
        try {
          gpu = await api.request('gpu.spoof.restore', { target: 'driver' });
          renderGpu();
          ui.toast(t('Gpu.Spoof.Done'));
        } catch (error) {
          ui.toast(error.message, 'error');
        }
        return;
      }
      if (action === 'deploy') {
        const game = games.find((item) => item.id === id);
        if (game) openModal(game);
        return;
      }
      if (action === 'restore') {
        return run(() => api.request('framegen.restore', { id }), t('Library.Status.RestoreDone').replace('{0}', id));
      }
      if (action === 'open') {
        await api.request('games.openFolder', { id }).catch((error) => ui.toast(error.message, 'error'));
        return;
      }
      if (action === 'remove') {
        const game = games.find((item) => item.id === id);
        if (!game || !(await confirmDialog(t('Library.Dialog.RemoveTitle'), t('Library.Confirm.Remove').replace('{0}', game.name), t('Common.Remove')))) return;
        await run(() => api.request('games.remove', { id }), null);
        return;
      }
      if (action === 'modal-deploy') {
        if (!current) return;
        const game = current;
        closeModal();
        await run(() => api.request('framegen.deploy', { id: game.id, options: readOptions() }), t('Library.Status.DeployDone').replace('{0}', game.name));
        return;
      }
      if (action === 'modal-restore') {
        if (!current) return;
        const game = current;
        closeModal();
        await run(() => api.request('framegen.restore', { id: game.id }), t('Library.Status.RestoreDone').replace('{0}', game.name));
      }
    };
    root.addEventListener('click', onRootClick);

    const onOptionsChange = (event) => {
      if (event.target.matches('[data-option="engine"]')) syncEngine();
    };
    optionsRoot.addEventListener('change', onOptionsChange);
    overlay.querySelectorAll('[data-close]').forEach((button) => button.addEventListener('click', closeModal));
    overlay.addEventListener('click', (event) => {
      if (event.target === overlay) closeModal();
    });
    removedOverlay.querySelectorAll('[data-close-removed]').forEach((button) => {
      button.addEventListener('click', closeRemoved);
    });
    removedOverlay.addEventListener('click', (event) => {
      if (event.target === removedOverlay) closeRemoved();
    });

    await load();
    return () => {
      closeModal();
      closeRemoved();
      root.removeEventListener('click', onRootClick);
      optionsRoot.removeEventListener('change', onOptionsChange);
    };
  },
};
