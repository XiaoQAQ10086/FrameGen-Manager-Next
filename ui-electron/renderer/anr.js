import {
  $, call, esc, folderIcon, hideProgress, prettyPath, showProgress, state, toast, trashIcon,
} from './core.js';
import { t } from './l10n.js';
import { showRemoved } from './removed.js';

let packageInfo = null;
let modes = {};

export async function renderAnr(root) {
  root.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>${t('navAnr')} <span class="badge warn">${t('anrExperimental')}</span></h1>
          <div class="sub" data-role="status">${t('anrChecked', { count: state.games.length })}</div>
        </div>
        <div class="lh-actions">
          <button class="btn" data-action="add" type="button">${t('addGame')}</button>
          <button class="btn" data-action="removed" type="button">${t('removedTitle')}</button>
          <button class="btn scan" data-action="scan" type="button">${t('scanGames')}</button>
        </div>
      </header>
      <main class="layout">
        <aside class="side">
          <section class="panel" data-role="package"></section>
          <section class="panel">
            <div class="ctitle">${t('modeSourceTitle')}</div>
            <div class="chips"><span class="chip gpu-chip">${t('modeGpusFramegen')}</span></div>
            <a class="source-link" data-action="github" href="#">${t('modeSourceLink')}</a>
          </section>
          <section class="panel">
            <div class="ctitle">${t('anrRequirementsTitle')}</div>
            <p class="hint">• ${t('anrRequirement1')}</p>
            <p class="hint">• ${t('anrRequirement2')}</p>
            <p class="hint">• ${t('anrRequirement3')}</p>
          </section>
        </aside>
        <section class="content">
          <div class="game-list" data-role="games">
            <div class="gcard skeleton-card"><div class="sk-cover"></div><div class="sk-body"><i></i><i></i><i></i></div></div>
          </div>
        </section>
      </main>
    </section>`;

  await load();
  renderPackage(root);
  renderGames(root);
  bind(root);
}

async function load() {
  try {
    const [current, modeReply] = await Promise.all([
      call('anr.current'),
      call('gameModes', {}),
    ]);
    packageInfo = current.package || null;
    modes = Object.fromEntries((modeReply.modes || []).map((mode) => [mode.gameId, mode]));
  } catch (error) {
    toast(error.message, 'error');
  }
}

function renderPackage(root) {
  const gpu = state.gpu;
  $('[data-role="package"]', root).innerHTML = `
    <div class="ctitle">${t('anrPackageTitle')}</div>
    <div class="chips"><span class="chip ${packageInfo ? 'ok' : 'dim'}">${packageInfo ? t('anrPackageReady') : t('anrNotDownloaded')}</span></div>
    <p class="hint">${packageInfo
      ? `${t('anrPackageVersion', { version: packageInfo.version })}${packageInfo.kind === 'addon' ? t('anrPackageAddon') : ''}`
      : t('anrPackageMissing')}</p>
    <p class="hint">${gpu ? t('anrCurrentGpu', { name: gpu.name, driver: gpu.driverVersion }) : t('gpuMissing')}</p>
    <button class="btn primary wide" data-action="download" type="button">${t('anrDownloadOnline')}</button>
    <button class="btn wide" data-action="import" type="button">${t('anrImportZip')}</button>
    <div class="anr-cloud">
      <button class="btn" data-action="baidu" type="button">${t('anrBaidu')}</button>
      <button class="btn" data-action="quark" type="button">${t('anrQuark')}</button>
    </div>`;
}

function renderGames(root) {
  const list = $('[data-role="games"]', root);
  if (!state.games.length) {
    list.innerHTML = '';
    return;
  }
  list.innerHTML = state.games.map((game, index) => cardMarkup(game, index)).join('');
}

function cardMarkup(game, index) {
  const anr = (modes[game.id] || {}).anr || 'notDeployed';
  const deployed = anr !== 'notDeployed';
  const cover = state.covers[game.id];
  return `
    <article class="gcard" style="animation-delay:${Math.min(index * 35, 350)}ms">
      <div class="gtop">
        ${cover ? `<img class="thumb" src="${esc(cover)}" alt="">` : `<div class="thumb ph2">${esc(game.name.slice(0, 1))}</div>`}
        <div class="ginfo">
          <div class="gname">${esc(game.name)}</div>
          <div class="badges">
            <span class="badge ${deployed ? (anr === 'needsRepair' ? 'warn' : 'ok') : 'dim'}">${esc(anrLabel(anr))}</span>
          </div>
        </div>
      </div>
      <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
      <div class="gactions">
        ${deployed
          ? `<button class="btn small danger" data-action="restore" data-id="${esc(game.id)}" type="button">${t('restore')}</button>`
          : `<button class="btn small success" data-action="deploy" data-id="${esc(game.id)}" type="button" ${packageInfo ? '' : 'disabled'}>${t('deploy')}</button>`}
        <button class="btn small launch" data-action="launch" data-id="${esc(game.id)}" type="button">${t('launchGame')}</button>
        <span class="spacer"></span>
        <button class="btn small icon-only" data-action="folder" data-id="${esc(game.id)}" type="button" title="${esc(t('openFolder'))}">${folderIcon}</button>
        <button class="btn small danger icon-only" data-action="remove" data-id="${esc(game.id)}" type="button" title="${esc(t('removeGame'))}">${trashIcon}</button>
      </div>
    </article>`;
}

function anrLabel(anr) {
  return {
    installed: t('anrStateDeployed'),
    needsRepair: t('anrStateNeedsRepair'),
  }[anr] || t('anrStateNotDeployed');
}

function bind(root) {
  root.onclick = async (event) => {
    const target = event.target.closest('[data-action]');
    if (!target) return;
    const action = target.dataset.action;
    const game = state.games.find((item) => item.id === target.dataset.id);
    try {
      if (action === 'removed') {
        await showRemoved(async () => {
          state.games = (await call('library.list')).games || [];
          await load();
          renderGames(root);
        });
      } else if (action === 'download') {
        showProgress(t('anrDownloadOnline'));
        try {
          packageInfo = (await call('anr.download', {})).package || null;
          toast(t('anrImported'));
        } finally {
          hideProgress();
        }
        renderPackage(root);
        renderGames(root);
      } else if (action === 'import') {
        const reply = await call('anr.import', {});
        if (!reply.cancelled) {
          packageInfo = reply.package || null;
          renderPackage(root);
          renderGames(root);
          toast(t('anrImported'));
        }
      } else if (action === 'deploy' && game) {
        showProgress(t('deploy'));
        try {
          const reply = await call('anr.deploy', {
            targetDir: game.targetDirectory,
            exePath: game.renderExe,
          });
          await load();
          renderGames(root);
          toast(t('anrDeployDone', { proxy: reply.proxyModule || '—' }));
        } finally {
          hideProgress();
        }
      } else if (action === 'restore' && game) {
        showProgress(t('restore'));
        try {
          await call('anr.restore', { targetDir: game.targetDirectory });
          await load();
          renderGames(root);
          toast(t('anrRestored'));
        } finally {
          hideProgress();
        }
      } else if (action === 'launch' && game) {
        await call('launch', { gameId: game.id });
      } else if (action === 'folder' && game) {
        await call('openPath', { path: game.targetDirectory });
      } else if (action === 'remove' && game) {
        if (confirm(`${t('removeGame')} ${game.name}?`)) {
          await call('library.remove', { gameId: game.id });
          state.games = state.games.filter((item) => item.id !== game.id);
          renderGames(root);
        }
      } else if (action === 'scan') {
        await call('library.scan');
        state.games = (await call('library.list')).games || [];
        await load();
        renderGames(root);
      } else if (action === 'add') {
        const picked = await call('pickExecutable');
        if (!picked.cancelled) {
          await call('library.addManual', { path: picked.path });
          state.games = (await call('library.list')).games || [];
          await load();
          renderGames(root);
        }
      } else if (action === 'baidu') {
        window.fgm.openUrl('https://pan.baidu.com/s/1DWG8f4767zpjD0Rk4HBX2w?pwd=1234');
      } else if (action === 'quark') {
        window.fgm.openUrl('https://pan.quark.cn/s/5708e53de73b');
      } else if (action === 'github') {
        event.preventDefault();
        window.fgm.openUrl('https://github.com/apex-compute/ANR');
      }
    } catch (error) {
      toast(error.message, 'error');
    }
  };
}
