import { confirmDialog, escapeHtml as esc } from '../ui.js';
import { componentKeys, prettyPath } from '../labels.js';
import { createRemovedController, removedMarkup } from '../removed-modal.js';

const gateKeys = {
  Supported: 'Gpu.Gate.Supported',
  DriverTooOld: 'Gpu.Gate.DriverTooOld',
  GpuNotSupported: 'Gpu.Gate.GpuNotSupported',
  GpuNotNeeded: 'Gpu.Gate.GpuNotNeeded',
  Unknown: 'Gpu.Gate.Unknown',
};

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let games = [];
    let removed = [];
    let gpu = null;
    let busy = false;
    let addingGame = false;

    root.innerHTML = `
      <section class="page">
        <header class="listhead">
          <div class="lh-text">
            <h1>${t('SmoothMotion.Title')}</h1>
            <div class="sub" data-role="status">${t('SmoothMotion.Status.Checking')}</div>
          </div>
          <div class="lh-actions">
            <button class="btn" data-action="add" type="button">${t('Library.Add')}</button>
            <button class="btn" data-action="removed" type="button">${t('Library.Removed')}<span class="count" data-role="removed-count"></span></button>
            <button class="btn scan" data-action="scan" type="button">${t('Library.Scan')}</button>
          </div>
        </header>
        <main class="layout">
          <aside class="side">
            <section class="panel">
              <div class="ctitle">${t('SmoothMotion.Applicability')}</div>
              <div class="chips"><span class="chip dim" data-role="applicability">${t('Common.Checking')}</span></div>
              <div class="kv2"><span>${t('Gpu.Name')}</span><b class="badge" data-role="gpu"></b></div>
              <div class="kv2"><span>${t('Gpu.DriverVersion')}</span><b class="badge" data-role="driver"></b></div>
              <div class="kv2"><span>${t('SmoothMotion.MinDriver')}</span><b class="badge" data-role="min-driver">616.64</b></div>
              <p class="hint" data-role="note">${t('SmoothMotion.Status.Checking')}</p>
            </section>
            <section class="panel">
              <div class="ctitle">${t('SmoothMotion.Notes')}</div>
              <p class="hint">${t('SmoothMotion.Subtitle')}</p>
            </section>
          </aside>
          <section class="content">
            <div class="game-list" data-role="games">
              <div class="gcard skeleton-card"><div class="sk-cover"></div><div class="sk-body"><i></i><i></i><i></i></div></div>
              <div class="gcard skeleton-card"><div class="sk-cover"></div><div class="sk-body"><i></i><i></i><i></i></div></div>
            </div>
            <div class="empty hidden" data-role="empty"><p>${t('SmoothMotion.Empty')}</p></div>
          </section>
        </main>
      </section>
      ${removedMarkup(t)}`;

    const q = (selector) => root.querySelector(selector);
    const removedController = createRemovedController(root, ctx, () => removed, load);

    function status(value) {
      q('[data-role="status"]').textContent = value;
    }

    function renderGpu() {
      if (!gpu) return;
      const gate = gateKeys[gpu.gate] || 'Gpu.Gate.Unknown';
      const supported = gpu.gate === 'Supported';
      const applicability = q('[data-role="applicability"]');
      applicability.textContent = supported ? t('SmoothMotion.CanDeploy') : t('SmoothMotion.NotApplicable');
      applicability.className = `chip ${supported ? 'ok' : 'dim'}`;
      q('[data-role="gpu"]').textContent = gpu.name || '—';
      q('[data-role="driver"]').textContent = gpu.driver || t('Gpu.Status.UnknownDriver');
      q('[data-role="min-driver"]').textContent = gpu.minDriver || '616.64';
      q('[data-role="note"]').textContent = supported ? t('SmoothMotion.Ready') : t(gate);
    }

    function render() {
      const list = q('[data-role="games"]');
      list.innerHTML = games.map((game, index) => {
        const outdated = game.component === 'Outdated';
        const toggleAction = outdated || game.enabled !== true ? 'enable' : 'disable';
        const toggleLabel = outdated
          ? t('Common.Update')
          : game.enabled === true
            ? t('SmoothMotion.Disable')
            : t('SmoothMotion.Enable');
        return `
        <article class="gcard${index < 12 ? '' : ' static'}" style="animation-delay:${Math.min(index * 35, 350)}ms">
          <div class="gtop">
            ${game.cover ? `<img class="thumb" src="${game.cover}" alt="" loading="lazy" decoding="async">` : `<div class="thumb ph2">${esc(game.initial)}</div>`}
            <div class="ginfo">
              <div class="gname">${esc(game.name)}</div>
              <div class="badges">
                <span class="badge ${game.component === 'Installed' ? 'ok' : (game.component === 'Partial' || game.component === 'Conflict' ? 'warn' : 'dim')}">${esc(t(componentKeys[game.component] || 'State.NotDeployed'))}</span>
                <span class="badge ${game.enabled === true ? 'ok' : 'dim'}">${esc(game.enabled === true ? t('SmoothMotion.Driver.On') : game.enabled === false ? t('SmoothMotion.Driver.Off') : t('SmoothMotion.Driver.Unknown'))}</span>
              </div>
            </div>
          </div>
          <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
          ${game.detail ? `<div class="hint">${esc(game.detail)}</div>` : ''}
          <div class="gactions end">
            <button class="btn small" data-action="open" data-id="${game.id}" type="button">${t('Library.OpenFolder')}</button>
            <button class="btn small ${toggleAction === 'enable' ? 'success' : 'danger'}" data-action="${toggleAction}" data-id="${game.id}" type="button" ${game.available === false ? 'disabled' : ''}>${toggleLabel}</button>
            <button class="btn small danger icon-only" data-action="remove" data-id="${game.id}" type="button" title="${esc(t('Common.Remove'))}" aria-label="${esc(t('Common.Remove'))}">🗑</button>
          </div>
        </article>`;
      }).join('');
      q('[data-role="empty"]').classList.toggle('hidden', games.length > 0);
      list.classList.toggle('hidden', games.length === 0);
    }

    async function load() {
      try {
        const state = await api.request('sm.state');
        gpu = state.gpu;
        games = state.games;
        removed = state.removed || [];
        renderGpu();
        render();
        removedController.render();
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
        removed = state.removed || removed;
        gpu = state.gpu || gpu;
        renderGpu();
        render();
        removedController.render();
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
      if (actionName === 'removed') {
        removedController.open();
        return;
      }
      if (actionName === 'add') {
        if (addingGame) return;
        addingGame = true;
        try {
          await api.request('games.add').catch((error) => ui.toast(error.message, 'error'));
          await load();
          ui.libraryChanged('smooth-motion');
        } finally {
          setTimeout(() => { addingGame = false; }, 700);
        }
        return;
      }
      if (actionName === 'scan') {
        button.classList.add('loading');
        try {
          await api.request('games.scan');
          await load();
          ui.libraryChanged('smooth-motion');
        } catch (error) {
          ui.toast(error.message, 'error');
        } finally {
          button.classList.remove('loading');
        }
        return;
      }
      const game = games.find((item) => item.id === button.dataset.id);
      if (!game) return;
      if (actionName === 'open') {
        await api.request('games.openFolder', { id: game.id })
          .catch((error) => ui.toast(error.message, 'error'));
        return;
      }
      if (actionName === 'enable') {
        return action('sm.enable', game.id);
      }
      if (actionName === 'restore') {
        return action('sm.restore', game.id);
      }
      if (actionName === 'disable') {
        return action('sm.disable', game.id);
      }
      if (actionName === 'remove') {
        if (!(await confirmDialog(
          t('Library.Dialog.RemoveTitle'),
          t('Library.Confirm.Remove').replace('{0}', game.name),
          t('Common.Remove')))) return;

        try {
          await api.request('games.remove', { id: game.id });
          await load();
          ui.libraryChanged('smooth-motion');
        } catch (error) {
          ui.toast(error.message, 'error');
        }
      }
    };
    root.addEventListener('click', onClick);

    const onLibraryChanged = (event) => {
      if (event.detail?.source === 'smooth-motion') return;
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
