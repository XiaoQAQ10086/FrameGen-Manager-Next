import { confirmDialog, escapeHtml as esc } from '../ui.js';
import { apiLabel, prettyPath, stateKeys } from '../labels.js';

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let games = [];
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
              <button class="btn" data-action="modal-restore" type="button">${t('Common.Restore')}</button>
              <button class="btn danger" data-action="modal-uninstall" type="button">${t('Common.Uninstall')}</button>
              <button class="btn primary" data-action="modal-install" type="button">${t('Common.Install')}</button>
            </div>
          </footer>
        </section>
      </div>`;

    const q = (selector) => root.querySelector(selector);
    const detail = q('[data-role="detail"]');
    const detailTitle = q('[data-role="detail-title"]');
    const detailPath = q('[data-role="detail-path"]');
    const detailBody = q('[data-role="detail-body"]');

    function status(value) {
      q('[data-role="status"]').textContent = value;
    }

    function deployed(game) {
      return game.state === 'Installed' || game.state === 'Outdated' || game.state === 'Partial' || game.state === 'InstalledWithoutManifest';
    }

    function tile(game, index) {
      const media = game.cover
        ? `<img src="${game.cover}" alt="">`
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
      detailBody.innerHTML = `
        <div class="badges">
          <span class="badge">${esc(apiLabel(game.api))}</span>
          <span class="badge ${deployed(game) ? 'ok' : 'dim'}">${esc(t(stateKeys[game.state] || 'State.NotDeployed'))}</span>
          <span class="badge ${game.reShade ? 'ok' : 'dim'}">${esc(t('Dlss5.ReShade'))} ${game.reShade ? '✓' : '—'}</span>
          ${game.legacy ? `<span class="badge warn">${esc(t('Dlss5.RequiresUninstall'))}</span>` : ''}
        </div>
        <p class="hint">${esc(t('Dlss5.Subtitle'))}</p>`;
      q('[data-action="modal-install"]').disabled = game.available === false || game.legacy;
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
        render();
        status(t('Dlss5.Status.Checked').replace('{0}', games.length));
      } catch (error) {
        status(error.code || '加载失败');
        ui.toast(error.message, 'error');
      }
    }

    async function action(command, id) {
      if (busy) return;
      busy = true;
      try {
        const state = await api.request(command, { id });
        games = state.games;
        render();
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
        if (name === 'modal-install' || name === 'modal-restore' || name === 'modal-uninstall') {
          if (!current) return;
          const game = current;
          if (name === 'modal-install') {
            if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Install').replace('{0}', game.name).replace('{1}', game.targetDirectory), t('Common.Install')))) return;
            closeDetail();
            return action('dlss5.install', game.id);
          }
          if (name === 'modal-restore') {
            if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Restore').replace('{0}', game.name), t('Common.Restore')))) return;
            closeDetail();
            return action('dlss5.restore', game.id);
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
