import { confirmDialog } from '../ui.js';
import { apiLabel, stateKeys } from '../labels.js';

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let games = [];
    let busy = false;

    root.innerHTML = `
      <section class="page">
        <header class="page-head">
          <div><h1>${t('Dlss5.Title')}</h1><p>${t('Dlss5.Subtitle')}</p></div>
          <div class="head-actions">
            <button class="btn" data-action="add" type="button">${t('Library.Add')}</button>
            <button class="btn primary" data-action="refresh" type="button">${t('Common.RefreshState')}</button>
          </div>
        </header>
        <div class="statusline"><span data-role="status">${t('Dlss5.Status.Checking')}</span></div>
        <div class="dlss-grid" data-role="games"></div>
        <div class="empty hidden" data-role="empty"><p>${t('Dlss5.Empty')}</p></div>
      </section>`;

    const q = (selector) => root.querySelector(selector);

    function render() {
      q('[data-role="games"]').innerHTML = games.map((game, index) => `
        <article class="game-card dlss-card" style="animation-delay:${Math.min(index * 26, 160)}ms">
          <div class="cover-fallback">${(game.name || '?')[0].toUpperCase()}</div>
          <span></span>
          <div class="game-main">
            <div class="game-info">
              <div class="game-name">${game.name}</div>
              <div class="game-path" title="${game.targetDirectory}">${game.targetDirectory}</div>
              <div class="chips">
                <span class="chip">${apiLabel(game.api)}</span>
                <span class="chip ${game.state === 'Installed' ? 'ok' : (game.state === 'Partial' || game.state === 'Conflict' ? 'warn' : '')}">${t(stateKeys[game.state] || 'State.NotDeployed')}</span>
                <span class="chip">${game.reShade ? 'ReShade ✓' : 'ReShade —'}</span>
                ${game.legacy ? `<span class="chip warn">${t('Dlss5.Legacy')}</span>` : ''}
              </div>
            </div>
            <div class="actions">
              <button class="btn small primary" data-action="install" data-id="${game.id}" type="button" ${game.available === false || game.legacy ? 'disabled' : ''}>${t('Common.Install')}</button>
              <button class="btn small" data-action="restore" data-id="${game.id}" type="button">${t('Common.Restore')}</button>
              <button class="btn small ghost" data-action="uninstall" data-id="${game.id}" type="button">${t('Common.Uninstall')}</button>
            </div>
          </div>
        </article>`).join('');
      q('[data-role="empty"]').classList.toggle('hidden', games.length > 0);
      q('[data-role="games"]').classList.toggle('hidden', games.length === 0);
    }

    async function load() {
      try {
        const state = await api.request('dlss5.state');
        games = state.games;
        render();
        q('[data-role="status"]').textContent = t('Dlss5.Status.Checked').replace('{0}', games.length);
      } catch (error) {
        q('[data-role="status"]').textContent = error.code || '加载失败';
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
      if (!button) return;
      const name = button.dataset.action;
      if (name === 'refresh') return load();
      if (name === 'add') {
        await api.request('games.add').catch((error) => ui.toast(error.message, 'error'));
        return load();
      }
      const game = games.find((item) => item.id === button.dataset.id);
      if (!game) return;
      if (name === 'install') {
        if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Install').replace('{0}', game.name).replace('{1}', game.targetDirectory), t('Common.Install')))) return;
        return action('dlss5.install', game.id);
      }
      if (name === 'restore') {
        if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Restore').replace('{0}', game.name), t('Common.Restore')))) return;
        return action('dlss5.restore', game.id);
      }
      if (name === 'uninstall') {
        if (!(await confirmDialog(t('Dlss5.Title'), t('Dlss5.Confirm.Uninstall').replace('{0}', game.name), t('Common.Uninstall')))) return;
        return action('dlss5.uninstall', game.id);
      }
    };
    root.addEventListener('click', onClick);

    await load();
    return () => root.removeEventListener('click', onClick);
  },
};