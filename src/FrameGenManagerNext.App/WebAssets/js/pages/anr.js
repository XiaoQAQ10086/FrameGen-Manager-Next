import { escapeHtml as esc } from '../ui.js';
import { createRemovedController, removedMarkup } from '../removed-modal.js';
import { githubSource, modeSources } from '../mode-info.js';

const stateLabel = (state) => {
  if (state === 'Installed') return 'Anr.Installed';
  if (state === 'Partial' || state === 'Conflict') return 'Anr.Partial';
  return 'Anr.NotDeployed';
};

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    let state = { package: null, gpu: null, cloud: null, games: [], removed: [] };
    let removed = [];
    let busy = false;
    let updating = false;
    let updateChecked = false;
    let updateInfo = null;
    let updateDismissed = false;
    let unsubscribe = null;
    let unsubscribeUpdated = null;
    let removedController = null;

    root.innerHTML = `
      <section class="page anr-page">
        <header class="listhead">
          <div class="lh-text">
            <h1>${t('Anr.Title')} <span class="badge warn">${t('Anr.Experimental')}</span></h1>
            <div class="sub" data-role="status">${t('Anr.Subtitle')}</div>
          </div>
          <div class="lh-actions">
            <button class="btn" data-action="add" type="button">${t('Library.Add')}</button>
            <button class="btn" data-action="removed" type="button">${t('Library.Removed')}<span class="count" data-role="removed-count"></span></button>
            <button class="btn scan" data-action="scan" type="button">${t('Library.Scan')}</button>
          </div>
        </header>
        <section class="anr-update hidden" data-role="anr-update">
          <span class="anr-update-text" data-role="anr-update-text"></span>
          <button class="btn small primary" data-action="update" type="button">${t('Anr.UpdateNow')}</button>
          <button class="anr-update-close" data-action="update-later" type="button" aria-label="${t('Anr.UpdateLater')}">×</button>
        </section>
        <main class="layout">
          <aside class="side">
            <section class="panel">
              <div class="ctitle">${t('Anr.PackageTitle')} <span class="badge state dim" data-role="package-badge"></span></div>
              <div class="hint" data-role="package"></div>
              <div class="hint" data-role="gpu"></div>
              <button class="btn primary wide" data-action="download" type="button">${t('Anr.OnlineDownload')}</button>
              <button class="btn wide" data-action="import" type="button">${t('Anr.ImportZip')}</button>
              <div class="anr-cloud">
                <button class="btn" data-action="cloud-baidu" type="button">${t('Anr.DownloadBaidu')}</button>
                <button class="btn" data-action="cloud-quark" type="button">${t('Anr.DownloadQuark')}</button>
              </div>
              <div class="install-progress hidden" data-role="progress">
                <div class="bar"><i data-role="bar"></i></div>
                <p class="muted" data-role="progress-text"></p>
              </div>
            </section>
            <section class="panel">
              <div class="ctitle">${t('Anr.GpuTitle')}</div>
              <div class="anr-chips">
                <span class="chip">${t('Anr.GpuNvidia')}</span>
                <span class="chip">${t('Anr.GpuAmd')}</span>
                <span class="chip">${t('Anr.GpuIntel')}</span>
              </div>
              ${githubSource(t, modeSources.anr)}
            </section>
            <section class="panel">
              <div class="ctitle">${t('Anr.RequirementTitle')}</div>
              <ul class="anr-list">
                <li>${t('Anr.RequirementGame')}</li>
                <li>${t('Anr.RequirementRuntime')}</li>
                <li>${t('Anr.RequirementProxy')}</li>
              </ul>
            </section>
          </aside>
          <section class="content">
            <div class="game-list" data-role="games"></div>
            <div class="empty hidden" data-role="empty"><p>${t('Anr.Empty')}</p></div>
          </section>
        </main>
      </section>
      ${removedMarkup(t)}`;

    root.querySelector('.side').scrollTop = 0;
    const gamesHost = root.querySelector('[data-role="games"]');
    const empty = root.querySelector('[data-role="empty"]');
    const packageText = root.querySelector('[data-role="package"]');
    const gpuText = root.querySelector('[data-role="gpu"]');
    const status = root.querySelector('[data-role="status"]');
    const removedCount = root.querySelector('[data-role="removed-count"]');
    const progress = root.querySelector('[data-role="progress"]');
    const bar = root.querySelector('[data-role="bar"]');
    const progressText = root.querySelector('[data-role="progress-text"]');
    const updateBanner = root.querySelector('[data-role="anr-update"]');
    const updateText = root.querySelector('[data-role="anr-update-text"]');
    const packageBadge = root.querySelector('[data-role="package-badge"]');

    const updatePackageBadge = () => {
      let key = 'Anr.PackageReadyBadge';
      let tone = 'ok';
      if (!state.package) {
        key = 'Anr.PackageMissingBadge';
        tone = 'danger';
      } else if (updateInfo?.available) {
        key = 'Anr.HasUpdate';
        tone = 'warn';
      }
      packageBadge.textContent = t(key);
      packageBadge.className = `badge state ${tone}`;
    };

    const setBusy = (value) => {
      busy = value;
      root.querySelectorAll('[data-action="download"], [data-action="import"], [data-action="update"]')
        .forEach((button) => { button.disabled = value; });
    };

    const showProgress = (label) => {
      progress.classList.remove('hidden');
      progressText.textContent = label;
      bar.style.width = '0%';
    };

    const hideProgress = () => progress.classList.add('hidden');

    const hideUpdate = () => updateBanner.classList.add('hidden');

    const renderUpdate = () => {
      if (!updateInfo?.available || updateDismissed) {
        hideUpdate();
        updatePackageBadge();
        return;
      }
      updateText.textContent = t('Anr.UpdateAvailable')
        .replace('{0}', updateInfo.latest)
        .replace('{1}', updateInfo.current);
      updateBanner.classList.remove('hidden');
      updatePackageBadge();
    };

    const checkUpdate = async () => {
      if (updateChecked) return;
      updateChecked = true;
      try {
        const result = await api.request('anr.checkUpdate', {}, 60000);
        if (result?.available) {
          updateInfo = result;
          render();
          renderUpdate();
        }
      } catch {
        // Offline or rate-limited: keep the current package silently.
      }
    };

    const card = (game) => {
      const installed = game.state === 'Installed';
      const partial = game.state === 'Partial' || game.state === 'Conflict';
      const isAddon = state.package?.kind === 'Addon';
      const hasUpdate = (installed || partial) &&
        Boolean(state.package?.version && game.version) &&
        String(state.package.version).toLowerCase() !== String(game.version).toLowerCase();
      const reason = !game.renderExe
        ? t('Anr.NeedsExe')
        : !state.package
          ? t('Anr.NeedsPackage')
          : isAddon && !game.hasOptiScalerIni
            ? t('Anr.NeedsOptiScaler')
            : !isAddon && !game.proxy
              ? t('Anr.NeedsProxy')
              : !isAddon && game.hasOptiScaler && !installed && !partial
                ? t('Anr.OptiScalerConflict')
                : '';
      const action = installed || partial
        ? `<button class="btn small danger" data-action="restore" data-id="${esc(game.id)}" type="button">${t('Anr.Restore')}</button>`
        : state.package
          ? `<button class="btn small success" data-action="deploy" data-id="${esc(game.id)}" type="button"
              ${reason ? 'disabled' : ''}>${t('Anr.Deploy')}</button>`
          : '';
      return `
        <article class="gcard">
          <div class="gtop">
            ${game.cover
              ? `<img class="thumb" src="${game.cover}" alt="" loading="lazy" decoding="async">`
              : `<div class="thumb ph2">${esc(game.initial)}</div>`}
            <div class="ginfo">
              <div class="gname">${esc(game.name)}</div>
              <div class="badges">
                <span class="badge">${esc(game.launcher)}</span>
                <span class="badge">${esc(game.api)}</span>
                <span class="badge state ${hasUpdate ? 'warn' : installed ? 'ok' : partial ? 'warn' : 'dim'}">${t(hasUpdate ? 'Anr.HasUpdate' : stateLabel(game.state))}</span>
                ${(installed || partial) ? `<span class="badge mode">${esc(t('Mode.Badge.Anr'))}</span>` : ''}
                ${game.frameGenDeployed === true ? `<span class="badge mode">${esc(t('Mode.Badge.FrameGen'))}</span>` : ''}
                ${game.smoothMotionDeployed === true ? `<span class="badge mode">${esc(t('Mode.Badge.SmoothMotion'))}</span>` : ''}
                ${(installed || partial) && game.version ? `<span class="badge">${esc(game.version)}</span>` : ''}
                ${(installed || partial) && game.proxy ? `<span class="badge warn">${esc(t('Anr.ProxyLabel').replace('{0}', game.proxy))}</span>` : ''}
              </div>
            </div>
          </div>
          <div class="gpath" title="${esc(game.targetDirectory)}">${esc(game.targetDirectory)}</div>
          ${reason ? `<div class="hint">${esc(reason)}</div>` : ''}
          <div class="gactions">
            ${action}
            <button class="btn small" data-action="open" data-id="${esc(game.id)}" type="button">${t('Library.OpenFolder')}</button>
            <button class="btn small launch" data-action="launch" data-id="${esc(game.id)}" type="button" ${game.launchMode === 'none' ? 'disabled' : ''} ${game.launchMode === 'none' ? `title="${esc(t('Library.LaunchUnsupported'))}"` : ''}>${t('Library.Launch')}</button>
          </div>
        </article>`;
    };

    const render = () => {
      packageText.classList.toggle('danger', !state.package);
      const kind = state.package?.kind === 'Addon' ? ` · ${t('Anr.KindAddon')}` : '';
      packageText.textContent = state.package
        ? t('Anr.PackageVersion').replace('{0}', state.package.version) + kind
        : t('Anr.PackageMissing');
      updatePackageBadge();
      const gpuName = state.gpu?.name || t('Common.Unknown');
      const driver = state.gpu?.driver ? ` · ${state.gpu.driver}` : '';
      gpuText.textContent = t('Anr.CurrentGpu').replace('{0}', gpuName + driver);
      gamesHost.innerHTML = state.games.map(card).join('');
      empty.classList.toggle('hidden', state.games.length > 0);
      status.textContent = t('Anr.GamesLoaded').replace('{0}', String(state.games.length));
      removedCount.textContent = removed.length > 0 ? ` (${removed.length})` : '';
    };

    const load = async () => {
      setBusy(true);
      try {
        state = await api.request('anr.state');
        removed = state.removed || [];
        render();
        removedController?.render();
      } catch (error) {
        ui.toast(ui.errorText(error, t('Anr.NeedsPackage')), 'error');
      } finally {
        setBusy(false);
      }
    };

    const reloadAfterRemoved = async () => {
      await load();
      ui.libraryChanged('anr');
    };

    removedController = createRemovedController(root, ctx, () => removed, reloadAfterRemoved);

    const run = async (action, label, success) => {
      if (busy) return false;
      setBusy(true);
      showProgress(label);
      try {
        const result = await action();
        if (result) {
          state = result;
          removed = state.removed || removed;
          render();
          removedController.render();
        }
        if (success) ui.toast(success);
        return true;
      } catch (error) {
        ui.toast(ui.errorText(error, label), 'error');
        return false;
      } finally {
        setBusy(false);
        hideProgress();
      }
    };

    root.addEventListener('click', async (event) => {
      const button = event.target.closest('[data-action]');
      if (!button) return;
      const action = button.dataset.action;
      if (action === 'scan') {
        button.classList.add('loading');
        try {
          await api.request('games.scan');
          await load();
          ui.libraryChanged('anr');
        } catch (error) {
          ui.toast(ui.errorText(error, t('Library.Scan')), 'error');
        } finally {
          button.classList.remove('loading');
          ui.progressDone();
        }
        return;
      }
      if (action === 'add') {
        try {
          await api.request('games.add');
          await load();
          ui.libraryChanged('anr');
        } catch (error) {
          ui.toast(ui.errorText(error, t('Library.Add')), 'error');
        }
        return;
      }
      if (action === 'removed') {
        removedController.open();
        return;
      }
      if (action === 'update-later') {
        updateDismissed = true;
        hideUpdate();
        updatePackageBadge();
        return;
      }
      if (action === 'update') {
        updating = true;
        await run(
          () => api.request('anr.update', {}, 3600000),
          t('Anr.Updating'),
          t('Anr.Updated'),
        );
        updating = false;
        updateInfo = null;
        updateDismissed = false;
        hideUpdate();
        updatePackageBadge();
        return;
      }
      if (action === 'cloud-baidu' || action === 'cloud-quark') {
        const url = action === 'cloud-baidu' ? state.cloud?.baidu : state.cloud?.quark;
        const label = action === 'cloud-baidu' ? t('Anr.DownloadBaidu') : t('Anr.DownloadQuark');
        if (!url) {
          ui.toast(label, 'error');
          return;
        }
        await api.request('updates.openPage', { url })
          .catch((error) => ui.toast(ui.errorText(error, label), 'error'));
        return;
      }
      if (action === 'download') {
        await run(
          () => api.request('anr.download', {}, 600000),
          t('Anr.Downloading'),
          t('Common.OperationDone'),
        );
        return;
      }
      if (action === 'import') {
        await run(
          () => api.request('anr.import', {}, 600000),
          t('Anr.Importing'),
          t('Common.OperationDone'),
        );
        return;
      }
      const game = state.games.find((item) => item.id === button.dataset.id);
      if (!game) return;
      if (action === 'launch') {
        try {
          await api.request('games.launch', { id: game.id }, 20000);
          ui.toast(t('Library.Launching').replace('{0}', game.name));
        } catch (error) {
          ui.toast(
            error?.code === 'LaunchUnsupported'
              ? t('Library.LaunchUnsupported')
              : t('Library.LaunchFailed'),
            'error',
          );
        }
        return;
      }
      if (action === 'open') {
        await api.request('games.openFolder', { id: game.id })
          .catch((error) => ui.toast(ui.errorText(error, t('Library.OpenFolder')), 'error'));
        return;
      }
      if (action === 'deploy') {
        const ok = await run(
          () => api.request('anr.deploy', { id: game.id, proxy: game.proxy }, 600000),
          t('Anr.Deploy'),
          t('Anr.DeployedToast'),
        );
        if (ok) ui.libraryChanged('anr');
        return;
      }
      if (action === 'restore') {
        const ok = await run(
          () => api.request('anr.restore', { id: game.id }, 600000),
          t('Anr.Restore'),
          t('Anr.RestoredToast'),
        );
        if (ok) ui.libraryChanged('anr');
      }
    });

    const onLibraryChanged = (event) => {
      if (event.detail?.source === 'anr') return;
      load();
    };
    document.addEventListener('fgm:library-changed', onLibraryChanged);

    const onPageShown = (event) => {
      if (event.detail?.page === 'anr') load();
    };
    document.addEventListener('fgm:page-shown', onPageShown);

    unsubscribe = api.on('anr.progress', (data) => {
      const percent = Math.max(0, Math.min(100, Number(data.percent) || 0));
      const label = updating ? t('Anr.Updating') : t('Anr.Downloading');
      const speed = Number(data.speed) || 0;
      const speedText = speed >= 1048576
        ? ` · ${(speed / 1048576).toFixed(1)} MB/s`
        : speed >= 1024
          ? ` · ${(speed / 1024).toFixed(0)} KB/s`
          : '';
      progress.classList.remove('hidden');
      bar.style.width = `${percent}%`;
      progressText.textContent = `${label} ${percent.toFixed(0)}%${speedText}`;
    });

    unsubscribeUpdated = api.on('anr.updated', (data) => {
      if (Number(data.updated) > 0) {
        ui.toast(t('Anr.UpdatedGames').replace('{0}', String(data.updated)));
      }
      if (Number(data.failed) > 0) {
        ui.toast(t('Anr.UpdateFailedGames').replace('{0}', String(data.failed)), 'error');
      }
    });

    await load();
    void checkUpdate();
    return () => {
      unsubscribe?.();
      unsubscribe = null;
      unsubscribeUpdated?.();
      unsubscribeUpdated = null;
      document.removeEventListener('fgm:library-changed', onLibraryChanged);
      document.removeEventListener('fgm:page-shown', onPageShown);
    };
  },
};
