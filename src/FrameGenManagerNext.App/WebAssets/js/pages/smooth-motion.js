import { confirmDialog } from '../ui.js';
import { apiLabel, componentKeys, routeKeys } from '../labels.js';

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let games = [];
    let busy = false;

    root.innerHTML = `
      <section class="page">
        <header class="page-head">
          <div><h1>${t('SmoothMotion.Title')}</h1><p>${t('SmoothMotion.Subtitle')}</p></div>
          <div class="head-actions">
            <button class="btn" data-action="add" type="button">${t('Library.Add')}</button>
            <button class="btn primary" data-action="refresh" type="button">${t('Common.RefreshState')}</button>
          </div>
        </header>
        <div class="statusline"><span data-role="status">${t('SmoothMotion.Status.Checking')}</span></div>
        <div class="game-list" data-role="games"></div>
        <div class="empty hidden" data-role="empty"><p>${t('SmoothMotion.Empty')}</p></div>
      </section>`;

    const q = (selector) => root.querySelector(selector);

    function status(value) {
      q('[data-role="status"]').textContent = value;
    }

    function render() {
      q('[data-role="games"]').innerHTML = games.map((game, index) => `
        <article class="game-card sm-card" style="animation-delay:${Math.min(index * 26, 160)}ms">
          <div class="cover-fallback">${(game.name || '?')[0].toUpperCase()}</div>
          <span></span>
          <div class="game-main">
            <div class="game-info">
              <div class="game-name">${game.name}</div>
              <div class="game-path" title="${game.targetDirectory}">${game.targetDirectory}</div>
              <div class="chips">
                <span class="chip">${t(routeKeys[game.route] || 'Gpu.Route.Unknown')}</span>
                <span class="chip ${game.component === 'Installed' ? 'ok' : (game.component === 'Partial' || game.component === 'Conflict' ? 'warn' : '')}">${t(componentKeys[game.component] || 'State.NotDeployed')}</span>
                <span class="chip">${game.enabled === true ? t('SmoothMotion.Driver.On') : game.enabled === false ? t('SmoothMotion.Driver.Off') : t('SmoothMotion.Driver.Unknown')}</span>
              </div>
              ${game.detail ? `<div class="component">${game.detail}</div>` : ''}
            </div>
            <div class="actions">
              <button class="btn small primary" data-action="enable" data-id="${game.id}" type="button" ${game.available === false || game.enabled === true ? 'disabled' : ''}>${t('SmoothMotion.Enable')}</button>
              <button class="btn small" data-action="disable" data-id="${game.id}" type="button" ${game.available === false || game.enabled !== true ? 'disabled' : ''}>${t('SmoothMotion.Disable')}</button>
              <button class="btn small ghost" data-action="restore" data-id="${game.id}" type="button">${t('Common.Restore')}</button>
            </div>
          </div>
        </article>`).join('');
      q('[data-role="empty"]').classList.toggle('hidden', games.length > 0);
      q('[data-role="games"]').classList.toggle('hidden', games.length === 0);
    }

    async function load() {
      try {
        const state = await api.request('sm.state');
        games = state.games;
        render();
        status(t('SmoothMotion.Status.Checked').replace('{0}', games.length));
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
        ui.toast(t('Common.OperationDone'));
      } catch (error) {
        ui.toast(error.message, 'error');
      } finally {
        busy = false;
      }
    }

    const onClick = async (event) => {
      const button = event.target.closest('[data-action]');
      if (!button) return;
      const actionName = button.dataset.action;
      if (actionName === 'refresh') return load();
      if (actionName === 'add') {
        await api.request('games.add').catch((error) => ui.toast(error.message, 'error'));
        return load();
      }
      const game = games.find((item) => item.id === button.dataset.id);
      if (!game) return;
      if (actionName === 'enable') {
        if (!(await confirmDialog(t('SmoothMotion.Title'), t('SmoothMotion.Confirm.Enable').replace('{0}', game.name).replace('{1}', game.targetDirectory), t('SmoothMotion.Enable')))) return;
        return action('sm.enable', game.id);
      }
      if (actionName === 'disable') {
        if (!(await confirmDialog(t('SmoothMotion.Title'), t('SmoothMotion.Confirm.Disable').replace('{0}', game.name), t('SmoothMotion.Disable')))) return;
        return action('sm.disable', game.id);
      }
      if (actionName === 'restore') {
        if (!(await confirmDialog(t('SmoothMotion.Title'), t('SmoothMotion.Confirm.Restore').replace('{0}', game.name), t('Common.Restore')))) return;
        return action('sm.restore', game.id);
      }
    };
    root.addEventListener('click', onClick);

    await load();
    return () => root.removeEventListener('click', onClick);
  },
};