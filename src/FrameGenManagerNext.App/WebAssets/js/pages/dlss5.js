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
        <button class="tile" data-action="open" data-id="${esc(game.id)}" type="button" style="--i:${index}">
          ${media}
          ${game.legacy ? `<span class="ribbon warn">${t('Dlss5.Legacy')}</span>` : ''}
          <span class="meta">
            <span class="name">${esc(game.name)}</span>
            <span class="tag">${esc(t('Dlss5.GraphicsApi'))}${esc(apiLabel(game.api))}</span>
            <span class="tag ${deployed(game) ? 'ok' : ''}">${esc(deployed(game) ? t('Dlss5.Deployed') : t('Dlss5.NotDeployed'))}</span>
          </span>
        </button>`;
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
        ['auto', t('Dlss5.ApiAuto')],
        ['DX12', 'DX12'],
        ['DX11', 'DX11'],
        ['DX10', 'DX10'],
        ['DX9', 'DX9'],
        ['VULKAN', 'Vulkan'],
        ['OPENGL', 'OpenGL'],
      ];
      const currentApi = game.api && game.api !== 'Unknown'
        ? game.api.replace('Direct3D', 'DX').toUpperCase()
        : 'auto';
      detailBody.innerHTML = `
        <div class="badges">
          <span class="badge">${esc(apiLabel(game.api))}</span>
          <span class="badge ${deployed(game) ? 'ok' : 'dim'}">${esc(t(stateKeys[game.state] || 'State.NotDeployed'))}</span>
          <span class="badge ${game.reShade ? 'ok' : 'dim'}">${esc(t('Dlss5.ReShade'))} ${game.reShade ? '✓' : '—'}</span>
          ${game.legacy ? `<span class="badge warn">${esc(t('Dlss5.RequiresUninstall'))}</span>` : ''}
        </div>
        <div class="deploy-picker">
          <label>${t('Dlss5.Target')}<select data-role="detail-target">
            ${targets.map((target) => `<option value="${esc(target.path)}">${esc(target.name)}</option>`).join('')}
          </select></label>
          <label>${t('Dlss5.GraphicsApiLabel')}<select data-role="detail-api">
            ${apiOptions.map(([value, label]) => `<option value="${value}">${label}</option>`).join('')}
          </select></label>
        </div>
        <p class="hint">${esc(t('Dlss5.Subtitle'))}</p>`;
      const targetSelect = detailBody.querySelector('[data-role="detail-target"]');
      const apiSelect = detailBody.querySelector('[data-role="detail-api"]');
      if (targetSelect && game.renderExe) {
        targetSelect.value = game.renderExe;
      }
      if (apiSelect) {
        apiSelect.value = apiOptions.some(([value]) => value === currentApi) ? currentApi : 'auto';
      }
      q('[data-action="modal-install"]').disabled = game.available === false || game.legacy || targets.length === 0;
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

    async function action(command, id, extra = {}) {
      if (busy) return;
      busy = true;
      try {
        const state = await api.request(command, { id, ...extra });
        games = state.games;
        removed = state.removed || removed;
        render();
        removedController.render();
      } catch (error) {
        ui.toast(error.message, 'error');
      } finally {
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
        if (name === 'modal-install' || name === 'modal-uninstall') {
          if (!current) return;
          const game = current;
          if (name === 'modal-install') {
            const target = detailBody.querySelector('[data-role="detail-target"]')?.value;
            const api = detailBody.querySelector('[data-role="detail-api"]')?.value || 'auto';
            if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Install').replace('{0}', game.name).replace('{1}', prettyPath(target || game.targetDirectory)), t('Common.Install')))) return;
            closeDetail();
            return action('dlss5.install', game.id, { target, api });
          }
          if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Uninstall').replace('{0}', game.name), t('Common.Uninstall')))) return;
          closeDetail();
          return action('dlss5.uninstall', game.id);
        }
      }

      if (event.target === detail || event.target.closest('[data-close-detail]')) {
        closeDetail();
      }
    };
    root.addEventListener('click', onClick);

    const onLibraryChanged = (event) => {
      if (event.detail?.source === 'dlss5') return;
      load();
    };
    document.addEventListener('fgm:library-changed', onLibraryChanged);

    await load();
    return () => {
      document.removeEventListener('fgm:library-changed', onLibraryChanged);
      root.removeEventListener('click', onClick);
    };
  },
};
