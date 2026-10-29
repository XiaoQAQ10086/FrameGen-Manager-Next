import { escapeHtml as esc, confirmDialog } from '../ui.js';

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

const gateKeys = {
  Supported: 'Gpu.Gate.Supported',
  DriverTooOld: 'Gpu.Gate.DriverTooOld',
  GpuNotSupported: 'Gpu.Gate.GpuNotSupported',
  GpuNotNeeded: 'Gpu.Gate.GpuNotNeeded',
  Unknown: 'Gpu.Gate.Unknown',
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
    let gpu = null;
    let spoof = null;
    let options = null;
    let busy = false;
    let current = null;

    root.innerHTML = `
      <section class="page">
        <header class="page-head">
          <div>
            <h1>${esc(t('Library.Title'))}</h1>
            <p>${esc(t('Library.Subtitle'))}</p>
          </div>
          <div class="head-actions">
            <button class="btn" data-action="add" type="button">${esc(t('Library.Add'))}</button>
            <button class="btn" data-action="cancel" type="button" disabled>${esc(t('Common.Cancel'))}</button>
            <button class="btn scan" data-action="scan" type="button">${esc(t('Library.Scan'))}</button>
          </div>
        </header>
        <div class="notice hidden" data-role="notice"></div>
        <main class="layout">
          <aside class="side">
            <section class="panel" data-role="gpu"></section>
            <section class="panel spoof">
              <details>
                <summary>${esc(t('Gpu.SpoofTitle'))}</summary>
                <div data-role="spoof"></div>
              </details>
            </section>
          </aside>
          <section class="content">
            <div class="statusline">
              <span data-role="status">${esc(t('Library.Status.Loading'))}</span>
              <div class="progress"><i data-role="page-progress"></i></div>
            </div>
            <div class="game-list" data-role="games"></div>
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
      </div>`;

    const q = (selector) => root.querySelector(selector);
    const overlay = q('[data-role="overlay"]');
    const modalTitle = q('[data-role="modal-title"]');
    const modalPath = q('[data-role="modal-path"]');
    const optionsRoot = q('[data-role="options"]');

    function setBusy(value) {
      busy = value;
      root.querySelectorAll('[data-action="scan"], [data-action="add"]').forEach((button) => {
        button.disabled = value;
      });
      q('[data-action="cancel"]').disabled = !value;
    }

    function status(message) {
      q('[data-role="status"]').textContent = message;
    }

    function progress(value) {
      q('[data-role="page-progress"]').style.width = `${Math.max(0, Math.min(100, value))}%`;
    }

    function notice(message) {
      const node = q('[data-role="notice"]');
      node.textContent = message || '';
      node.classList.toggle('hidden', !message);
    }

    function renderGpu() {
      const g = gpu.gpu;
      const s = gpu.spoof;
      q('[data-role="gpu"]').innerHTML = `
        <div class="panel-head"><div><h2>${esc(t('Gpu.Title'))}</h2><p class="muted">${esc(t('Gpu.Status.Refreshed'))}</p></div>
          <button class="btn small" data-action="refresh-gpu" type="button">${esc(t('Common.Refresh'))}</button></div>
        <div class="gpu-name">${esc(g.name)}</div>
        <div class="kv"><span>${esc(t('Gpu.DriverVersion'))}</span><b>${esc(g.driver || t('Gpu.Status.UnknownDriver'))}</b></div>
        <div class="gate-row">
          <div><span class="muted small">${esc(t('Gpu.FrameGeneration'))}</span><b>${esc(t(gateKeys[g.frameGeneration] || 'Gpu.Gate.Unknown'))}</b></div>
          <div><span class="muted small">${esc(t('Gpu.SmoothMotion'))}</span><b>${esc(t(gateKeys[g.smoothMotion] || 'Gpu.Gate.Unknown'))}</b></div>
        </div>
        <div class="kv"><span>${esc(t('Gpu.Route'))}</span><b>${esc(t(routeKeys[g.route] || 'Gpu.Route.Unknown'))}</b></div>
        <div class="hags">
          <div class="kv"><span>${esc(t('Gpu.Hags'))}</span><b>${esc(t(hagsKeys[g.hags] || 'Gpu.Hags.Unknown'))}</b></div>
          <button class="btn small ghost" data-action="open-hags" type="button">${esc(t('Settings.OpenHags'))}</button>
        </div>`;
      q('[data-role="spoof"]').innerHTML = `
        <div class="kv"><span>${esc(t('Gpu.Card'))}</span><b>${esc(s.current || '—')}</b></div>
        <div class="kv"><span>${esc(t('Gpu.DriverNameLabel'))}</span><b>${esc(s.driver || '—')}</b></div>
        <p class="muted small">${esc(t('Gpu.Spoof.Warning'))}</p>
        <select data-role="spoof-preset">${s.presets.map((preset) => `<option>${esc(preset)}</option>`).join('')}</select>
        <div class="actions" style="margin-top:10px;flex-wrap:wrap;gap:6px">
          <button class="btn primary small" data-action="spoof-apply" type="button">${esc(t('Gpu.ApplySpoof'))}</button>
          <button class="btn small" data-action="spoof-driver" type="button">${esc(t('Gpu.RestoreDriver'))}</button>
          <button class="btn ghost small" data-action="spoof-original" type="button" ${s.hasOriginal ? '' : 'disabled'}>${esc(t('Gpu.RestoreOriginal'))}</button>
        </div>`;
    }

    function renderGames() {
      const list = q('[data-role="games"]');
      list.innerHTML = games.map((game, index) => `
        <article class="game-card" style="animation-delay:${Math.min(index * 26, 160)}ms">
          ${game.cover ? `<img class="cover" src="${game.cover}" alt="">` : `<div class="cover-fallback">${esc(game.initial)}</div>`}
          <span></span>
          <div class="game-main">
            <div class="game-info">
              <div class="game-name">${esc(game.name)}</div>
              <div class="game-path" title="${esc(game.targetDirectory)}">${esc(game.targetDirectory)}</div>
              <div class="chips">
                <span class="chip">${esc(t(launcherKeys[game.launcher] || 'Common.Unknown'))}</span>
                <span class="chip">${esc(apiKeys[game.api] || game.api || '未知')}</span>
                <span class="chip ${game.state === 'Installed' ? 'ok' : (game.state === 'Partial' || game.state === 'Conflict' ? 'warn' : '')}">${esc(t(stateKeys[game.state] || 'State.NotDeployed'))}</span>
              </div>
            </div>
            <div class="actions">
              <button class="btn small" data-action="deploy" data-id="${esc(game.id)}" type="button">${esc(t('Common.Deploy'))}</button>
              <button class="btn small ghost" data-action="restore" data-id="${esc(game.id)}" type="button">${esc(t('Common.Restore'))}</button>
              <button class="btn small ghost" data-action="open" data-id="${esc(game.id)}" type="button">${esc(t('Common.Open'))}</button>
              <button class="btn small ghost" data-action="remove" data-id="${esc(game.id)}" type="button">${esc(t('Common.Remove'))}</button>
            </div>
          </div>
        </article>`).join('');
      q('[data-role="empty"]').classList.toggle('hidden', games.length > 0);
      list.classList.toggle('hidden', games.length === 0);
    }

    function openModal(game) {
      current = game;
      modalTitle.textContent = game.name;
      modalPath.textContent = game.targetDirectory;
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
      try {
        const [gpuState, gamesState] = await Promise.all([
          api.request('gpu.state'),
          api.request('games.state'),
        ]);
        gpu = gpuState;
        options = gamesState.options;
        games = gamesState.games;
        renderGpu();
        renderGames();
        status(t('Library.Status.Loaded').replace('{0}', games.length));
        notice(gamesState.warning);
      } catch (error) {
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
          options = next.options || options;
          renderGames();
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
      buttons.forEach((button) => button.classList.add('loading'));
      try {
        await run(() => api.request('games.scan'), null);
      } finally {
        buttons.forEach((button) => button.classList.remove('loading'));
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
      if (action === 'refresh-gpu') {
        try {
          gpu = await api.request('gpu.state');
          renderGpu();
        } catch (error) {
          ui.toast(error.message, 'error');
        }
        return;
      }
      if (action === 'open-hags') {
        await api.request('settings.openHags').catch(() => {});
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
      if (action === 'spoof-driver' || action === 'spoof-original') {
        const targetName = action === 'spoof-driver' ? 'driver' : 'original';
        if (!confirm(t('Gpu.Spoof.ConfirmDriver'))) return;
        try {
          gpu = await api.request('gpu.spoof.restore', { target: targetName });
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

    await load();
    return () => {
      closeModal();
      root.removeEventListener('click', onRootClick);
      optionsRoot.removeEventListener('change', onOptionsChange);
    };
  },
};
