import { confirmDialog, escapeHtml as esc } from '../ui.js';
import { apiLabel, prettyPath, stateKeys } from '../labels.js';
import { createRemovedController, removedMarkup } from '../removed-modal.js';

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let games = [];
    let removed = [];
    let busy = false;
    let addingGame = false;
    let current = null;

    root.innerHTML = `
      <section class="page">
        <header class="listhead">
          <div class="lh-text">
            <h1>${t('Dlss5.Title')}</h1>
            <div class="sub" data-role="status">${t('Dlss5.Status.Checking')}</div>
          </div>
          <div class="lh-actions">
            <button class="btn" data-action="add" type="button">${t('Library.Add')}</button>
            <button class="btn" data-action="removed" type="button">${t('Library.Removed')}<span class="count" data-role="removed-count"></span></button>
            <button class="btn scan" data-action="scan" type="button">${t('Library.Scan')}</button>
          </div>
        </header>
        <div class="wall" data-role="games">
          <div class="tile skeleton-tile"></div>
          <div class="tile skeleton-tile"></div>
          <div class="tile skeleton-tile"></div>
        </div>
        <div class="empty hidden" data-role="empty"><p>${t('Dlss5.Empty')}</p></div>
      </section>
      ${removedMarkup(t)}
      <div class="overlay hidden" data-role="detail">
        <section class="modal dlss-modal" role="dialog" aria-modal="true">
          <header class="modal-head">
            <div><h2 data-role="detail-title"></h2><p class="muted" data-role="detail-path"></p></div>
            <button class="icon-btn" data-close-detail type="button">✕</button>
          </header>
          <div class="dlss-detail" data-role="detail-body"></div>
          <footer class="modal-foot">
            <span class="muted small">${t('Deploy.CloseGameHint')}</span>
            <div class="foot-actions">
              <button class="btn" data-action="detail-open" type="button">${t('Library.OpenFolder')}</button>
              <button class="btn danger" data-action="modal-uninstall" type="button">${t('Common.Uninstall')}</button>
              <button class="btn success" data-action="modal-install" type="button">${t('Common.Install')}</button>
            </div>
          </footer>
        </section>
      </div>`;

    const q = (selector) => root.querySelector(selector);
    const detail = q('[data-role="detail"]');
    const detailTitle = q('[data-role="detail-title"]');
    const detailPath = q('[data-role="detail-path"]');
    const detailBody = q('[data-role="detail-body"]');
    const removedController = createRemovedController(root, ctx, () => removed, load);

    function status(value) {
      q('[data-role="status"]').textContent = value;
    }

    function deployed(game) {
      return game.state === 'Installed' || game.state === 'Outdated' || game.state === 'Partial' || game.state === 'InstalledWithoutManifest';
    }

    function tile(game, index) {
      const media = game.cover
        ? `<img src="${game.cover}" alt="" loading="lazy" decoding="async">`
        : `<div class="ph">${esc(game.initial)}</div>`;
      return `
        <div class="tile${index < 12 ? '' : ' static'}" role="button" tabindex="0" data-action="open" data-id="${esc(game.id)}" style="--i:${index}">
          ${media}
          ${game.legacy ? `<span class="ribbon warn">${t('Dlss5.Legacy')}</span>` : ''}
          <span class="meta">
            <span class="name">${esc(game.name)}</span>
            <span class="tag">${esc(t('Dlss5.GraphicsApi'))}${esc(apiLabel(game.api))}</span>
            <span class="tag ${deployed(game) ? 'ok' : ''}">${esc(deployed(game) ? t('Dlss5.Deployed') : t('Dlss5.NotDeployed'))}</span>
          </span>
          <button class="tile-remove" data-action="remove" data-id="${esc(game.id)}" type="button" title="${esc(t('Common.Remove'))}" aria-label="${esc(t('Common.Remove'))}">🗑</button>
        </div>`;
    }

    function render() {
      const wall = q('[data-role="games"]');
      wall.innerHTML = games.map(tile).join('');
      q('[data-role="empty"]').classList.toggle('hidden', games.length > 0);
      wall.classList.toggle('hidden', games.length === 0);
    }

    function openDetail(game) {
      current = game;
      detailTitle.textContent = game.name;
      detailPath.textContent = prettyPath(game.targetDirectory);
      const targets = game.targets?.length
        ? game.targets
        : (game.renderExe ? [{ path: game.renderExe, name: game.renderExe.split('\\').pop() }] : []);
      const apiOptions = [
        ['DX12', 'DX12'],
        ['DX11', 'DX11'],
        ['DX10', 'DX10'],
        ['DX9', 'DX9'],
        ['VULKAN', 'Vulkan'],
        ['OPENGL', 'OpenGL'],
      ];
      const currentApi = game.api && game.api !== 'Unknown'
        ? game.api.replace('Direct3D', 'DX').toUpperCase()
        : 'DX12';
      const apiLabelText = (value, label) =>
        value === currentApi && game.api !== 'Unknown'
          ? `${label}${t('Dlss5.ApiAutoSuffix')}`
          : label;
      const manualTarget = '__manual__';
      detailBody.innerHTML = `
        <div class="badges">
          <span class="badge">${esc(apiLabel(game.api))}</span>
          <span class="badge ${deployed(game) ? 'ok' : 'dim'}">${esc(t(stateKeys[game.state] || 'State.NotDeployed'))}</span>
          ${game.legacy ? `<span class="badge warn">${esc(t('Dlss5.RequiresUninstall'))}</span>` : ''}
        </div>
        <div class="deploy-picker">
          <label>${t('Dlss5.Target')}<select data-role="detail-target">
            ${targets.map((target) => `<option value="${esc(target.path)}">${esc(target.name)}</option>`).join('')}
            <option value="${manualTarget}">${t('Deploy.ManualTarget')}</option>
          </select></label>
          <label>${t('Dlss5.GraphicsApiLabel')}<select data-role="detail-api">
            ${apiOptions.map(([value, label]) => `<option value="${value}">${esc(apiLabelText(value, label))}</option>`).join('')}
          </select></label>
        </div>
        <p class="hint">${esc(t('Dlss5.Subtitle'))}</p>
        <p class="hint hidden" data-role="vulkan-hint">${esc(t('Dlss5.VulkanOnlineHint'))}</p>
        <div class="install-progress hidden" data-role="install-progress">
          <div class="bar"><i data-role="install-bar"></i></div>
          <p class="hint" data-role="install-status"></p>
        </div>`;
      const targetSelect = detailBody.querySelector('[data-role="detail-target"]');
      const apiSelect = detailBody.querySelector('[data-role="detail-api"]');
      if (targetSelect && game.renderExe) {
        targetSelect.value = game.renderExe;
      }
      const vulkanHint = detailBody.querySelector('[data-role="vulkan-hint"]');
      const syncVulkanHint = () => {
        vulkanHint?.classList.toggle('hidden', apiSelect?.value !== 'VULKAN');
      };
      if (apiSelect) {
        apiSelect.value = apiOptions.some(([value]) => value === currentApi) ? currentApi : 'DX12';
        apiSelect.addEventListener('change', syncVulkanHint);
      }
      syncVulkanHint();

      const pickTarget = async () => {
        const previous = targets.find((target) => target.path === game.renderExe)?.path
          || targets[0]?.path
          || manualTarget;
        const picked = await api.request('games.pickExecutable', { id: game.id })
          .catch((error) => {
            ui.toast(error.message || error.code || '选择 EXE 失败', 'error');
            return null;
          });
        if (!picked?.path) {
          targetSelect.value = previous;
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
      };
      targetSelect?.addEventListener('change', () => {
        if (targetSelect.value === manualTarget) {
          void pickTarget();
        }
      });

      const installButton = q('[data-action="modal-install"]');
      const repairState = game.state === 'Outdated' || game.state === 'Partial' || game.state === 'Conflict';
      const installed = deployed(game) && !repairState;
      installButton.disabled = game.available === false || game.legacy || installed;
      installButton.textContent = t(installed
        ? 'State.Deployed'
        : repairState ? 'Common.Update' : 'Common.Install');

      const uninstallButton = q('[data-action="modal-uninstall"]');
      uninstallButton.disabled = !deployed(game) && !game.legacy && !game.leftovers;

      detail.classList.remove('hidden');
    }

    function closeDetail() {
      detail.classList.add('hidden');
      current = null;
    }

    async function load() {
      try {
        const state = await api.request('dlss5.state');
        games = state.games;
        removed = state.removed || [];
        render();
        removedController.render();
        status(t('Dlss5.Status.Checked').replace('{0}', games.length));
      } catch (error) {
        status(error.code || '加载失败');
        ui.toast(error.message, 'error');
      }
    }

    async function runDetailAction(command, game, extra = {}) {
      if (busy) return;
      busy = true;

      const box = detailBody.querySelector('[data-role="install-progress"]');
      const bar = detailBody.querySelector('[data-role="install-bar"]');
      const status = detailBody.querySelector('[data-role="install-status"]');
      const installButton = q('[data-action="modal-install"]');
      const uninstallButton = q('[data-action="modal-uninstall"]');
      const previousInstall = installButton.textContent;
      const previousUninstallDisabled = uninstallButton.disabled;

      box?.classList.remove('hidden', 'indeterminate');
      if (bar) bar.style.width = '0%';
      if (status) status.textContent = t('Progress.planning');
      installButton.disabled = true;
      uninstallButton.disabled = true;
      installButton.textContent = t('Common.Working');

      const unsubscribe = api.on('progress', (data) => {
        if (!box || !bar || !status) return;
        if (data.phase === 'installer') {
          box.classList.add('indeterminate');
          bar.style.width = '100%';
          status.textContent = t('Progress.installer');
          return;
        }

        box.classList.remove('indeterminate');
        const percent = data.total > 0 ? Math.min(100, (data.current * 100) / data.total) : 0;
        bar.style.width = `${percent}%`;
        const label = t(`Progress.${data.phase}`);
        status.textContent = data.total > 0
          ? `${label} ${data.current}/${data.total}`
          : label;
      });

      try {
        const state = await api.request(command, { id: game.id, ...extra });
        games = state.games;
        removed = state.removed || removed;
        render();
        removedController.render();
        closeDetail();
        ui.toast(t('Common.OperationDone'));
      } catch (error) {
        if (box) box.classList.remove('indeterminate');
        if (status) status.textContent = error.message || error.code || t('Progress.failed');
        installButton.textContent = previousInstall;
        installButton.disabled = false;
        uninstallButton.disabled = previousUninstallDisabled;
        ui.toast(error.message || error.code || t('Progress.failed'), 'error');
      } finally {
        unsubscribe();
        busy = false;
      }
    }

    const onClick = async (event) => {
      const button = event.target.closest('[data-action]');
      if (button) {
        const name = button.dataset.action;
        if (name === 'removed') {
          removedController.open();
          return;
        }
        if (name === 'add') {
          if (addingGame) return;
          addingGame = true;
          try {
            await api.request('games.add').catch((error) => ui.toast(error.message, 'error'));
            await load();
            ui.libraryChanged('dlss5');
          } finally {
            setTimeout(() => { addingGame = false; }, 700);
          }
          return;
        }
        if (name === 'scan') {
          button.classList.add('loading');
          try {
            await api.request('games.scan');
            await load();
            ui.libraryChanged('dlss5');
          } catch (error) {
            ui.toast(error.message, 'error');
          } finally {
            button.classList.remove('loading');
          }
          return;
        }
        if (name === 'open') {
          const game = games.find((item) => item.id === button.dataset.id);
          if (game) openDetail(game);
          return;
        }
        if (name === 'remove') {
          const game = games.find((item) => item.id === button.dataset.id);
          if (!game) return;
          if (!(await confirmDialog(
            t('Library.Dialog.RemoveTitle'),
            t('Library.Confirm.Remove').replace('{0}', game.name),
            t('Common.Remove')))) return;

          try {
            await api.request('games.remove', { id: game.id });
            await load();
            ui.libraryChanged('dlss5');
          } catch (error) {
            ui.toast(error.message, 'error');
          }
          return;
        }
        if (name === 'detail-open') {
          if (!current) return;
          await api.request('games.openFolder', { id: current.id })
            .catch((error) => ui.toast(error.message, 'error'));
          return;
        }
        if (name === 'modal-install' || name === 'modal-uninstall') {
          if (!current) return;
          const game = current;
          if (name === 'modal-install') {
            const target = detailBody.querySelector('[data-role="detail-target"]')?.value;
            if (!target || target === '__manual__') {
              ui.toast(t('Deploy.TargetRequired'), 'error');
              return;
            }
            const api = detailBody.querySelector('[data-role="detail-api"]')?.value || 'DX12';
            if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Install').replace('{0}', game.name).replace('{1}', prettyPath(target || game.targetDirectory)), t('Common.Install')))) return;
            return runDetailAction('dlss5.install', game, { target, api });
          }
          if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Uninstall').replace('{0}', game.name), t('Common.Uninstall')))) return;
          return runDetailAction('dlss5.uninstall', game);
        }
      }

      if (event.target === detail || event.target.closest('[data-close-detail]')) {
        closeDetail();
      }
    };
    root.addEventListener('click', onClick);

    const onKeyDown = (event) => {
      if (event.key !== 'Enter' && event.key !== ' ') return;
      if (event.target.closest('.tile-remove')) return;
      const tileElement = event.target.closest('.tile[data-action="open"]');
      if (!tileElement) return;
      event.preventDefault();
      const game = games.find((item) => item.id === tileElement.dataset.id);
      if (game) openDetail(game);
    };
    root.addEventListener('keydown', onKeyDown);

    const onLibraryChanged = (event) => {
      if (event.detail?.source === 'dlss5') return;
      load();
    };
    document.addEventListener('fgm:library-changed', onLibraryChanged);

    await load();
    return () => {
      document.removeEventListener('fgm:library-changed', onLibraryChanged);
      root.removeEventListener('click', onClick);
      root.removeEventListener('keydown', onKeyDown);
    };
  },
};
