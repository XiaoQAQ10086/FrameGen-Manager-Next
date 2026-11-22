import {
  $, apiLabel, call, confirmAction, esc, folderIcon, githubIcon, hideProgress, prettyPath, proxyOwnerLabel,
  showProgress, sourceLabel, state,
  syncRemovedCount,
  toast, trashIcon,
} from './core.js';
import { t } from './l10n.js';
import { loadCovers } from './covers.js';
import { showRemoved } from './removed.js';

let packageInfo = null;
let modes = {};
let serial = 0;

export async function renderAnr(root) {
  const current = ++serial;
  root.innerHTML = `
    <section class="page">
      <header class="listhead">
        <div class="lh-text">
          <h1>${t('navAnr')}</h1>
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
            <div class="chips"><span class="chip gpu-chip wrap">${t('modeGpusAnr')}</span></div>
            <a class="source-link" data-action="github" href="#">${githubIcon}<span>${t('modeSourceLink')}</span></a>
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
  if (current !== serial || state.section !== 'anr') return;
  renderPackage(root);
  renderGames(root);
  bind(root);
  syncRemovedCount(root);
  root.dataset.ready = '1';
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
  if (state.section !== 'anr') return;
  const gpu = state.gpu;
  $('[data-role="package"]', root).innerHTML = `
    <div class="ctitle">${t('anrPackageTitle')}</div>
    <div class="chips"><span class="chip ${packageInfo ? 'ok' : 'dim'}">${packageInfo ? t('anrPackageReady') : t('anrNotDownloaded')}</span></div>
    <p class="hint">${packageInfo
      ? `${t('anrPackageVersion', { version: packageInfo.version })}${packageInfo.kind === 'addon' ? t('anrPackageAddon') : t('anrPackageFull')}`
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
  if (state.section !== 'anr') return;
  const list = $('[data-role="games"]', root);
  if (!state.games.length) {
    list.innerHTML = '';
    return;
  }
  list.innerHTML = state.games.map((game, index) => cardMarkup(game, index)).join('');
  loadCovers();
}

function cardMarkup(game, index) {
  const mode = modes[game.id] || {};
  const anr = mode.anr || 'notDeployed';
  const deployed = anr !== 'notDeployed';
  const route = String(state.gpu?.route || '').toLowerCase();
  const gpuBlocked = Boolean(state.gpu) && !['sm86', 'sm75', 'notneeded', 'gtx16', 'unsupported'].includes(route);
  const framegenOn = (mode.framegen || 'notDeployed') !== 'notDeployed';
  const smOn = mode.smoothMotion === 'on'
    || (mode.smoothMotionComponent && mode.smoothMotionComponent !== 'notDeployed');
  const conflict = framegenOn || smOn;
  const blocked = !packageInfo || gpuBlocked || (conflict && !deployed);
  const blockedTitle = framegenOn ? t('framegenConflict')
    : (smOn ? t('smConflict') : t('gpuIncompatible'));
  const anrVersion = mode.anrVersion || mode.anrPackVersion;
  const occupied = (mode.proxies || []).filter((entry) => entry.owner !== 'anr');
  const cover = state.covers[game.id];
  return `
    <article class="gcard" style="animation-delay:${Math.min(index * 35, 350)}ms">
      <div class="gtop">
        ${cover
          ? `<img class="thumb" data-cover="${esc(game.id)}" src="${esc(cover)}" alt="" decoding="async">`
          : `<div class="thumb ph2" data-cover="${esc(game.id)}">${esc(game.name.slice(0, 1))}</div>`}
        <div class="ginfo">
          <div class="gname" title="${esc(game.name)}">${esc(game.name)}</div>
          <div class="badges">
            <span class="badge">${esc(sourceLabel(game.source))}</span>
            <span class="badge">${esc(apiLabel(game.graphicsApi))}</span>
            ${deployed ? `<span class="badge ${anr === 'needsRepair' ? 'warn' : 'ok'}">${esc(`${anrLabel(anr)}${anrVersion ? ` ${t('assetVersionLabel')}：${anrVersion}` : ''}`)}</span>` : ''}
            ${framegenOn ? `<span class="badge danger">${esc(t('framegenConflict'))}</span>` : ''}
            ${smOn ? `<span class="badge danger">${esc(t('smConflict'))}</span>` : ''}
            ${gpuBlocked ? `<span class="badge danger">${esc(t('gpuIncompatible'))}</span>` : ''}
            ${occupied.map((entry) => `<span class="badge warn">${esc(entry.module)} · ${esc(proxyOwnerLabel(entry.owner))}</span>`).join('')}
          </div>
        </div>
      </div>
      <div class="gpath" title="${esc(prettyPath(game.targetDirectory))}">${esc(prettyPath(game.targetDirectory))}</div>
      <div class="gactions">
        ${deployed
          ? `<button class="btn small danger" data-action="restore" data-id="${esc(game.id)}" type="button">${t('restore')}</button>`
          : `<button class="btn small success" data-action="deploy" data-id="${esc(game.id)}" type="button" ${blocked ? 'disabled' : ''} ${blocked ? `title="${esc(blockedTitle)}"` : ''}>${t('deploy')}</button>`}
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

async function chooseProxy(game) {
  let entries = [];
  try {
    entries = (await call('proxyStatus', { targetDir: game.targetDirectory })).proxies || [];
  } catch {}
  const owners = new Map(entries.map((entry) => [String(entry.module).toLowerCase(), entry.owner]));
  const choices = ['dxgi.dll', 'winmm.dll', 'version.dll'].map((name) => {
    const owner = owners.get(name);
    return { name, owner, disabled: Boolean(owner), label: owner ? `${name} · ${proxyOwnerLabel(owner)}` : name };
  });
  const free = choices.find((choice) => !choice.disabled);
  if (!free) {
    toast(t('anrProxyHint'), 'error');
    return null;
  }
  return new Promise((resolve) => {
    const overlay = document.createElement('div');
    overlay.className = 'overlay';
    overlay.innerHTML = `
      <section class="modal deploy-modal" role="dialog" aria-modal="true">
        <header class="modal-head"><div><h2>${esc(game.name)}</h2><p class="muted">${t('optionProxySlot')}</p></div></header>
        <p class="hint">${t('anrProxyHint')}</p>
        <div class="options"><label><span>${t('optionProxySlot')}</span><select data-anr-proxy>
          ${choices.map((choice) => `<option value="${choice.name}" ${choice.disabled ? 'disabled' : ''} ${choice.name === free.name ? 'selected' : ''}>${esc(choice.label)}</option>`).join('')}
        </select></label></div>
        <footer class="modal-foot"><span></span><div class="foot-actions">
          <button class="btn" data-anr-cancel type="button">${t('cancel')}</button>
          <button class="btn success" data-anr-go type="button">${t('deploy')}</button>
        </div></footer>
      </section>`;
    const close = (value) => { overlay.remove(); resolve(value); };
    overlay.querySelector('[data-anr-cancel]').onclick = () => close(null);
    overlay.querySelector('[data-anr-go]').onclick = () => close(overlay.querySelector('[data-anr-proxy]').value);
    overlay.addEventListener('click', (event) => { if (event.target === overlay) close(null); });
    document.querySelector('#modal-root').appendChild(overlay);
  });
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
        const proxy = packageInfo?.kind === 'addon' ? null : await chooseProxy(game);
        if (packageInfo?.kind !== 'addon' && !proxy) return;
        showProgress(t('deploy'));
        try {
          const reply = await call('anr.deploy', {
            targetDir: game.targetDirectory,
            exePath: game.renderExe,
            proxy,
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
        const ok = await confirmAction({
          title: t('removeGame'),
          body: t('removeGameConfirm', { game: game.name }),
          confirmLabel: t('removeGame'),
          cancelLabel: t('cancel'),
          danger: true,
        });
        if (!ok) return;
        await call('library.remove', { gameId: game.id });
        state.games = state.games.filter((item) => item.id !== game.id);
        renderGames(root);
        syncRemovedCount(root);
      } else if (action === 'scan') {
        await call('library.scan');
        state.games = (await call('library.list')).games || [];
        await load();
        renderGames(root);
      } else if (action === 'add') {
        const picked = await call('pickExecutable', { title: t('pickExeTitle') });
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
