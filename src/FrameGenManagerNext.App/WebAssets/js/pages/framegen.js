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

const deployedStates = new Set(['Installed', 'Outdated', 'Partial', 'Conflict']);

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

let autoScanStarted = false;

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let games = [];
    let removed = [];
    let gpu = null;
    let options = null;
    let busy = false;
    let addingGame = false;
    let current = null;
    let unsupportedGpuShown = sessionStorage.getItem('fgm.gpuUnsupportedShown') === '1';

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
        <div class="notice info hidden" data-role="asset-hint"></div>
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
        <section class="modal deploy-modal" role="dialog" aria-modal="true">
          <header class="modal-head">
            <div class="modal-game-head">
              <div class="modal-cover" data-role="modal-cover"></div>
              <div><h2 data-role="modal-title"></h2><p class="muted" data-role="modal-path"></p></div>
            </div>
            <button class="icon-btn" data-close type="button">✕</button>
          </header>
          <div class="deploy-meta" data-role="deploy-meta"></div>
          <p class="hint">${esc(t('Deploy.DialogHint'))}</p>
          <div class="options" data-role="options"></div>
          <p class="option-note hidden" data-role="proxy-note"></p>
          <p class="option-note hidden" data-role="dlss-note">${esc(t('Deploy.DlssOnlyHint'))}</p>
          <footer class="modal-foot">
            <span class="muted small">${esc(t('Deploy.CloseGameHint'))}</span>
            <div class="foot-actions">
              <button class="btn" data-action="modal-restore" type="button" disabled>${esc(t('Common.Restore'))}</button>
              <button class="btn success" data-action="modal-deploy" type="button">${esc(t('Common.Deploy'))}</button>
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
    const deployModal = overlay.querySelector('.modal');
    const removedOverlay = q('[data-role="removed-overlay"]');
    const removedList = q('[data-role="removed-list"]');
    const removedCount = q('[data-role="removed-count"]');
    const modalTitle = q('[data-role="modal-title"]');
    const modalPath = q('[data-role="modal-path"]');
    const modalCover = q('[data-role="modal-cover"]');
    const deployMeta = q('[data-role="deploy-meta"]');
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

    function promptRestart() {
      return new Promise((resolve) => {
        const dialog = ui.modal(`
          <header class="modal-head"><div><h2>${esc(t('Gpu.Spoof.RestartTitle'))}</h2></div></header>
          <p class="hint">${esc(t('Gpu.Spoof.RestartMessage'))}</p>
          <footer class="modal-foot">
            <span></span>
            <div class="foot-actions">
              <button class="btn" data-later type="button">${esc(t('Gpu.Spoof.RestartLater'))}</button>
              <button class="btn success" data-restart type="button">${esc(t('Gpu.Spoof.RestartNow'))}</button>
            </div>
          </footer>`);
        dialog.root.querySelector('[data-later]').addEventListener('click', () => {
          dialog.close();
          resolve(false);
        });
        dialog.root.querySelector('[data-restart]').addEventListener('click', () => {
          dialog.close();
          resolve(true);
        });
        const overlay = dialog.root.closest('.overlay');
        overlay?.addEventListener('click', (event) => {
          if (event.target === overlay) resolve(false);
        });
      });
    }

    function checkGpuSupport() {
      if (unsupportedGpuShown || !gpu?.gpu?.route) return;
      const route = gpu.gpu.route;
      if (route === 'Sm86' || route === 'Sm75' || route === 'Unknown') return;
      unsupportedGpuShown = true;
      sessionStorage.setItem('fgm.gpuUnsupportedShown', '1');
      const dialog = ui.modal(`
        <header class="modal-head"><div><h2>${esc(t('Gpu.UnsupportedTitle'))}</h2></div></header>
        <p class="hint">${esc(t('Gpu.UnsupportedMessage'))}</p>
        <footer class="modal-foot">
          <span></span>
          <button class="btn" data-gpu-ok type="button">${esc(t('Common.GotIt'))}</button>
        </footer>`);
      dialog.root.querySelector('[data-gpu-ok]').addEventListener('click', () => dialog.close());
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
        <div class="kv2 spoof-name"><span>${esc(t('Gpu.Card'))}</span><b class="badge" title="${esc(s.current || '—')}">${esc(s.current || '—')}</b></div>
        <div class="kv2 spoof-name"><span>${esc(t('Gpu.DriverNameLabel'))}</span><b class="badge" title="${esc(s.driver || '—')}">${esc(s.driver || '—')}</b></div>
        <p class="hint">${esc(t('Gpu.Spoof.Warning'))}</p>
        <div class="spoof-picker">
          <select data-role="spoof-preset">${s.presets.map((preset) => `<option>${esc(preset)}</option>`).join('')}</select>
          <button class="btn primary wide" data-action="spoof-apply" type="button">${esc(t('Gpu.ApplySpoof'))}</button>
        </div>
        <button class="btn wide" data-action="spoof-driver" type="button" ${s.spoofed ? '' : 'disabled'}>${esc(t('Gpu.RestoreDriver'))}</button>`;
    }

    const renderChunkSize = 40;
    let renderedCount = 0;

    function cardMarkup(game, index) {
      const legacy = game.state === 'InstalledWithoutManifest';
      const restorable = ['Installed', 'Partial', 'Conflict'].includes(game.state);
      const smConflict = game.smoothMotionOn === true;
      const primaryAction = legacy ? 'clean' : restorable ? 'restore' : 'deploy';
      const primaryLabel = legacy
        ? t('Common.Clean')
        : restorable
          ? t('Common.Restore')
          : game.state === 'Outdated'
            ? t('Common.Update')
            : t('Common.Deploy');
      const blocked = smConflict && !restorable && !legacy;
      const stateText = game.state === 'Installed'
        ? (game.packVersion
            ? t('State.DeployedVersion').replace('{0}', game.packVersion)
            : t('State.Deployed'))
        : game.legacyTool
          ? t('State.LegacyDeployed')
          : t(stateKeys[game.state] || 'State.NotDeployed');
      return `
        <article class="gcard${index < 12 ? '' : ' static'}" style="animation-delay:${Math.min(index * 35, 350)}ms">
          <div class="gtop">
            ${game.cover ? `<img class="thumb" src="${game.cover}" alt="" loading="lazy" decoding="async">` : `<div class="thumb ph2">${esc(game.initial)}</div>`}
            <div class="ginfo">
              <div class="gname">${esc(game.name)}</div>
              <div class="badges">
                <span class="badge">${esc(t(launcherKeys[game.launcher] || 'Common.Unknown'))}</span>
                <span class="badge">${esc(apiKeys[game.api] || game.api || '未知')}</span>
                <span class="badge ${game.state === 'Installed' ? 'ok' : (game.state === 'Partial' || game.state === 'Conflict' || legacy ? 'warn' : 'dim')}">${esc(stateText)}</span>
                ${smConflict ? `<span class="badge danger">${esc(t('Conflict.SmoothMotionOn'))}</span>` : ''}
              </div>
            </div>
          </div>
          <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
          <div class="gactions">
            <button class="btn small ${restorable || legacy ? 'danger' : 'success'}" data-action="${primaryAction}" data-id="${esc(game.id)}" type="button" ${blocked ? 'disabled' : ''} ${blocked ? `title="${esc(t('Conflict.SmoothMotionOn'))}"` : ''}>${esc(primaryLabel)}</button>
            <button class="btn small" data-action="open" data-id="${esc(game.id)}" type="button">${esc(t('Library.OpenFolder'))}</button>
            <span class="spacer"></span>
            <button class="btn small danger icon-only" data-action="remove" data-id="${esc(game.id)}" type="button" title="${esc(t('Common.Remove'))}" aria-label="${esc(t('Common.Remove'))}">🗑</button>
          </div>
        </article>`;
    }

    function appendGames() {
      const list = q('[data-role="games"]');
      if (renderedCount >= games.length) return;
      const end = Math.min(games.length, renderedCount + renderChunkSize);
      list.insertAdjacentHTML(
        'beforeend',
        games.slice(renderedCount, end).map((game, offset) => cardMarkup(game, renderedCount + offset)).join(''),
      );
      renderedCount = end;
    }

    function renderGames() {
      const list = q('[data-role="games"]');
      list.innerHTML = '';
      list.scrollTop = 0;
      renderedCount = 0;
      appendGames();
      q('[data-role="empty"]').classList.toggle('hidden', games.length > 0);
      list.classList.toggle('hidden', games.length === 0);
      const outdated = games.filter((game) => game.state === 'Outdated').length;
      const assetHint = q('[data-role="asset-hint"]');
      assetHint.textContent = outdated > 0
        ? t('Library.AssetUpdateHint').replace('{0}', outdated)
        : '';
      assetHint.classList.toggle('hidden', outdated === 0);
      updateRemovedCount();
    }

    function updateRemovedCount() {
      removedCount.textContent = removed.length > 0 ? ` (${removed.length})` : '';
    }

    function renderRemoved() {
      removedList.innerHTML = removed.length > 0
        ? removed.map((game) => `
          <div class="removed-row">
            ${game.cover
              ? `<img class="removed-cover" src="${game.cover}" alt="" loading="lazy" decoding="async">`
              : `<div class="removed-initial">${esc(game.initial)}</div>`}
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

    const manualTarget = '__manual__';

    function openModal(game) {
      current = game;
      modalTitle.textContent = game.name;
      modalPath.textContent = prettyPath(game.targetDirectory);
      modalCover.innerHTML = game.cover
        ? `<img src="${game.cover}" alt="" loading="lazy" decoding="async">`
        : `<span>${esc(game.initial)}</span>`;
      const targets = game.targets?.length
        ? game.targets
        : (game.renderExe ? [{ path: game.renderExe, name: game.renderExe.split('\\').pop() }] : []);
      deployMeta.innerHTML = `
        <label class="deploy-target-label">
          <span>${esc(t('Dlss5.Target'))}</span>
          <div class="target-field">
            <select data-role="modal-target">
              ${targets.map((target) => `<option value="${esc(target.path)}">${esc(target.name)}</option>`).join('')}
              <option value="${manualTarget}">${esc(t('Deploy.ManualTarget'))}</option>
            </select>
            <div class="target-hint" data-role="target-hint"></div>
          </div>
        </label>
        <div class="kv2 inline">
          <span>${esc(t('Dlss5.GraphicsApiLabel'))}</span>
          <b class="api-value">${esc(apiKeys[game.api] || game.api || t('Common.Unknown'))}</b>
        </div>
        ${game.state === 'InstalledWithoutManifest'
          ? `<p class="asset-warning">${esc(t('Deploy.LegacyFilesWarning'))}</p>`
          : ''}`;
      const targetSelect = deployMeta.querySelector('[data-role="modal-target"]');
      const targetHint = deployMeta.querySelector('[data-role="target-hint"]');
      const restoreButton = deployModal.querySelector('[data-action="modal-restore"]');
      const samePath = (left, right) => String(left).toLowerCase() === String(right).toLowerCase();
      const sameDirectory = (left, right) => samePath(
        String(left).replace(/\//g, '\\').replace(/\\+$/, ''),
        String(right).replace(/\//g, '\\').replace(/\\+$/, ''));

      function paintRestoreState() {
        const selected = targetSelect.value;
        if (!selected || selected === manualTarget) {
          restoreButton.disabled = true;
          return;
        }

        const separator = selected.lastIndexOf('\\');
        const directory = separator > 0 ? selected.slice(0, separator) : '';
        restoreButton.disabled = !deployedStates.has(game.state) ||
          !sameDirectory(directory, game.targetDirectory);
      }

      function paintTargetHint() {
        targetHint.textContent = targetSelect.value && targetSelect.value !== manualTarget
          ? t('Deploy.TargetHint').replace('{0}', prettyPath(targetSelect.value))
          : '';
      }

      const initialTarget = targets.find((target) => samePath(target.path, game.renderExe))?.path
        || targets[0]?.path
        || manualTarget;
      targetSelect.value = initialTarget;

      const pickTarget = async () => {
        const previous = targetSelect.value === manualTarget ? manualTarget : targetSelect.value;
        const picked = await api.request('games.pickExecutable', { id: game.id })
          .catch((error) => {
            ui.toast(error.message || error.code || '选择 EXE 失败', 'error');
            return null;
          });
        if (!picked?.path) {
          targetSelect.value = previous;
          paintTargetHint();
          paintRestoreState();
          return;
        }

        let option = targetSelect.querySelector('option[data-picked="1"]');
        if (!option) {
          option = document.createElement('option');
          option.dataset.picked = '1';
          targetSelect.appendChild(option);
        }
        option.value = picked.path;
        option.textContent = picked.name;
        targetSelect.value = picked.path;
        paintTargetHint();
        paintRestoreState();
      };

      targetSelect.addEventListener('change', () => {
        if (targetSelect.value === manualTarget) {
          void pickTarget();
        } else {
          paintTargetHint();
          paintRestoreState();
        }
      });

      paintTargetHint();
      paintRestoreState();
      optionsRoot.innerHTML = optionFields();
      applyOptionValues(game.options);
      overlay.classList.remove('hidden');
      syncEngine();
      void applyProxyStatus(game.id);
      if (initialTarget === manualTarget) {
        void pickTarget();
      }
    }

    async function applyProxyStatus(gameId) {
      const note = q('[data-role="proxy-note"]');
      const selectControl = optionsRoot.querySelector('[data-option="proxySlot"]');
      if (!note || !selectControl) return;
      const status = await api.request('framegen.proxyStatus', { id: gameId }).catch(() => null);
      const occupied = status?.reShadeProxy;
      if (!occupied) {
        note.classList.add('hidden');
        return;
      }

      const option = Array.from(selectControl.options).find((item) => item.value === occupied);
      if (option) {
        option.disabled = true;
        option.textContent = `${option.textContent}${t('Deploy.ProxyOccupied')}`;
      }
      if (selectControl.value === occupied) {
        const fallback = Array.from(selectControl.options).find((item) => !item.disabled);
        if (fallback) selectControl.value = fallback.value;
      }

      note.textContent = t('Deploy.ProxyOccupiedHint').replace('{0}', occupied);
      note.classList.remove('hidden');
    }

    function closeModal() {
      overlay.classList.add('hidden');
      current = null;
    }

    const optimizedLabel = (value) => t(`Deploy.Optimized${value}`);
    const frameLabel = (value) => t(`Deploy.Frames${value}`);

    function select(id, values, selected, labelOf = (value) => String(value)) {
      return `<select data-option="${id}">${values.map((value) => `<option value="${esc(value)}" ${String(value) === String(selected) ? 'selected' : ''}>${esc(labelOf(value))}</option>`).join('')}</select>`;
    }

    function optionFields() {
      return `
        <label>${esc(t('Library.Proxy'))}${select('proxySlot', options.proxySlots, 'version.dll')}</label>
        <label>${esc(t('Library.Optimized'))}${select('optimized', options.optimizedLevels, 1, optimizedLabel)}</label>
        <label>${esc(t('Library.Frames'))}${select('frames', options.frameMultipliers, 3, frameLabel)}</label>
        <label>${esc(t('Library.Preset'))}${select('preset', options.presets, 'A')}</label>
        <label>${esc(t('Library.Engine'))}${select('engine', options.engines, 'DLSS')}</label>
        <label>${esc(t('Library.SuperResolution'))}${select('superResolutionMode', options.superResolutionModes, 'Off')}</label>
        <label class="xess">${esc(t('Library.XeSsFlow'))}${select('xeSsFlow', options.xeSsFlowResolutions, 'Middle')}</label>
        <label class="xess">${esc(t('Library.XeSsHudless'))}${select('xeSsHudless', options.xeSsHudlessValues, 'Auto')}</label>`;
    }

    function applyOptionValues(values) {
      if (!values) return;
      Object.entries(values).forEach(([key, value]) => {
        if (value === null || value === undefined || value === '') return;
        const control = optionsRoot.querySelector(`[data-option="${key}"]`);
        if (!control) return;
        const text = String(value);
        if (Array.from(control.options).some((option) => option.value === text)) {
          control.value = text;
        }
      });
    }

    function syncEngine() {
      const engine = optionsRoot.querySelector('[data-option="engine"]')?.value;
      const xess = engine === 'XeSS';
      optionsRoot.querySelectorAll('.xess').forEach((label) => label.classList.toggle('hidden', !xess));

      const optimized = optionsRoot.querySelector('[data-option="optimized"]');
      const preset = optionsRoot.querySelector('[data-option="preset"]');
      if (optimized) optimized.disabled = xess;
      if (preset) preset.disabled = xess;
      q('[data-role="dlss-note"]')?.classList.toggle('hidden', !xess);

      // XeSS serves 2X/3X/4X only; keep 6X out of the list instead of silently
      // deploying a different value than the one shown.
      const frames = optionsRoot.querySelector('[data-option="frames"]');
      if (!frames) return;
      const current = Number(frames.value) || 3;
      const allowed = (options.frameMultipliers || [1, 2, 3, 5]).filter((value) => !xess || value <= 3);
      const next = allowed.includes(current) ? current : 3;
      frames.innerHTML = allowed
        .map((value) => `<option value="${esc(value)}">${esc(frameLabel(value))}</option>`)
        .join('');
      frames.value = String(next);
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

    const snapshotKey = 'fgm.framegen.snapshot';

    function readSnapshot() {
      try {
        return JSON.parse(localStorage.getItem(snapshotKey) || 'null');
      } catch {
        return null;
      }
    }

    function saveSnapshot() {
      try {
        localStorage.setItem(snapshotKey, JSON.stringify({ games, removed, options }));
      } catch {
        // A full quota should never break the page.
      }
    }

    function applySnapshot() {
      const snapshot = readSnapshot();
      if (!snapshot) return false;
      if (!games.length && Array.isArray(snapshot.games)) {
        games = snapshot.games;
        removed = snapshot.removed || [];
        options = snapshot.options || null;
        renderGames();
        renderRemoved();
        status(t('Library.Status.Loaded').replace('{0}', games.length));
      }
      return games.length > 0;
    }

    async function load() {
      applySnapshot();
      // The GPU card has its own request and paints as soon as it resolves, so a
      // large or slow library scan never holds it back.
      const gpuPromise = api.request('gpu.state').catch((error) => {
        ui.toast(error.message || error.code || 'GPU 状态读取失败', 'error');
        return null;
      });
      gpuPromise.then((gpuState) => {
        if (!gpuState || !root.isConnected) return;
        gpu = gpuState;
        renderGpu();
        checkGpuSupport();
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
        saveSnapshot();
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
          saveSnapshot();
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
        ui.libraryChanged('framegen');
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
        if (addingGame) return;
        addingGame = true;
        try {
          await run(() => api.request('games.add'), null);
          ui.libraryChanged('framegen');
        } finally {
          setTimeout(() => { addingGame = false; }, 700);
        }
        return;
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
          if (await promptRestart()) {
            await api.request('app.restartComputer')
              .catch((error) => ui.toast(error.message, 'error'));
          }
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
        const game = games.find((item) => item.id === id);
        await run(
          () => api.request('framegen.restore', { id }),
          t('Library.Status.RestoreDone').replace('{0}', game?.name || id),
        );
        api.invalidate();
        ui.libraryChanged('framegen');
        return;
      }
      if (action === 'clean') {
        const game = games.find((item) => item.id === id);
        if (!game) return;
        const confirmed = await confirmDialog(
          t('Common.Clean'),
          t('Library.Confirm.Clean').replace('{0}', prettyPath(game.targetDirectory)),
          t('Common.Clean'),
        );
        if (!confirmed) return;
        await run(
          () => api.request('framegen.clean', { id }),
          t('Library.Status.CleanDone').replace('{0}', game.name),
        );
        api.invalidate();
        ui.libraryChanged('framegen');
        return;
      }
      if (action === 'open') {
        await api.request('games.openFolder', { id }).catch((error) => ui.toast(error.message, 'error'));
        return;
      }
      if (action === 'remove') {
        const game = games.find((item) => item.id === id);
        if (!game || !(await confirmDialog(t('Library.Dialog.RemoveTitle'), t('Library.Confirm.Remove').replace('{0}', game.name), t('Common.Remove')))) return;
        await run(() => api.request('games.remove', { id }), null);
        ui.libraryChanged('framegen');
        return;
      }
      if (action === 'modal-deploy') {
        if (!current) return;
        if (current.state === 'InstalledWithoutManifest') {
          ui.toast(t('Deploy.LegacyFilesWarning'), 'error');
          return;
        }
        if (current.smoothMotionOn === true) {
          ui.toast(t('Conflict.SmoothMotionOn'), 'error');
          return;
        }
        const game = current;
        const target = deployMeta.querySelector('[data-role="modal-target"]')?.value;
        if (!target || target === manualTarget) {
          ui.toast(t('Deploy.TargetRequired'), 'error');
          return;
        }
        const options = readOptions();
        closeModal();
        await run(
          () => api.request('framegen.deploy', { id: game.id, target, options }),
          t('Library.Status.DeployDone').replace('{0}', game.name),
        );
        api.invalidate();
        ui.libraryChanged('framegen');
        return;
      }
      if (action === 'modal-restore') {
        if (!current) return;
        const game = current;
        const target = deployMeta.querySelector('[data-role="modal-target"]')?.value;
        if (!target || target === manualTarget) {
          ui.toast(t('Deploy.TargetRequired'), 'error');
          return;
        }
        closeModal();
        await run(
          () => api.request('framegen.restore', { id: game.id, target }),
          t('Library.Status.RestoreDone').replace('{0}', game.name),
        );
        api.invalidate();
        ui.libraryChanged('framegen');
      }
    };
    root.addEventListener('click', onRootClick);

    q('[data-role="games"]').addEventListener('scroll', () => {
      const list = q('[data-role="games"]');
      if (list.scrollTop + list.clientHeight >= list.scrollHeight - 400) {
        appendGames();
      }
    }, { passive: true });

    const onLibraryChanged = (event) => {
      if (event.detail?.source === 'framegen') return;
      load();
    };
    document.addEventListener('fgm:library-changed', onLibraryChanged);

    const onOptionsChange = (event) => {
      if (event.target.matches('[data-option="engine"]')) syncEngine();
    };
    optionsRoot.addEventListener('change', onOptionsChange);

    let scrollTimer = null;
    const onModalScroll = () => {
      deployModal.classList.add('scrolling');
      clearTimeout(scrollTimer);
      scrollTimer = setTimeout(() => deployModal.classList.remove('scrolling'), 650);
    };
    deployModal.addEventListener('scroll', onModalScroll, { passive: true });

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
    if (!autoScanStarted && ctx.startupAutoScan) {
      autoScanStarted = true;
      void scan();
    }
    return () => {
      closeModal();
      closeRemoved();
      clearTimeout(scrollTimer);
      deployModal.removeEventListener('scroll', onModalScroll);
      document.removeEventListener('fgm:library-changed', onLibraryChanged);
      root.removeEventListener('click', onRootClick);
      optionsRoot.removeEventListener('change', onOptionsChange);
    };
  },
};
