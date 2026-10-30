import { escapeHtml as esc, modal } from './ui.js';

const familyKeys = {
  FrameGen: 'Assets.FrameGen',
  SmoothMotion: 'Assets.SmoothMotion',
  Dlss5: 'Assets.Dlss5',
};

let currentReport = null;
let checked = false;

export async function checkAssetUpdates(ctx, options = {}) {
  if (checked) return;
  checked = true;

  try {
    const report = await ctx.api.request('assets.check', {}, 120000);
    if (!report?.totalGames) {
      currentReport = null;
      hideBanner();
      return;
    }

    currentReport = report;
    showBanner(ctx);
    if (!document.querySelector('.overlay')) {
      openPrompt(ctx);
    }
  } catch (error) {
    checked = false;
    if (!options.silent) {
      ctx.ui.toast(error.message || error.code || '检测资产更新失败', 'error');
    }
  }
}

export function refreshAssetBanner(ctx) {
  if (currentReport?.totalGames) {
    showBanner(ctx);
  }
}

function familyName(ctx, family) {
  return ctx.t(familyKeys[family] || 'Common.Unknown');
}

function showBanner(ctx) {
  if (!currentReport?.totalGames) return;

  let node = document.getElementById('asset-banner');
  if (!node) {
    node = document.createElement('div');
    node.id = 'asset-banner';
    const view = document.getElementById('view');
    view?.parentNode?.insertBefore(node, view);
  }

  node.className = 'asset-banner';
  node.innerHTML = `
    <span class="asset-banner-text">${esc(ctx.t('Assets.Banner').replace('{0}', currentReport.totalGames))}</span>
    <button class="btn small" type="button" data-banner="review">${esc(ctx.t('Assets.Handle'))}</button>
    <button class="asset-banner-close" type="button" data-banner="dismiss" aria-label="${esc(ctx.t('Assets.BannerClose'))}">×</button>`;
  node.onclick = (event) => {
    const button = event.target.closest('[data-banner]');
    if (!button) return;
    if (button.dataset.banner === 'review') {
      openPrompt(ctx);
    } else {
      node.classList.add('hidden');
    }
  };
}

function hideBanner() {
  document.getElementById('asset-banner')?.classList.add('hidden');
}

function openPrompt(ctx) {
  if (!currentReport?.totalGames || document.querySelector('[data-role="asset-prompt"]')) return;

  const rows = currentReport.groups.map((group) => `
    <div class="asset-row">
      <span>${esc(familyName(ctx, group.family))}</span>
      <b>${esc(ctx.t('Assets.GameCount').replace('{0}', group.games.length))}</b>
    </div>`).join('');

  const dialog = modal(`
    <header class="modal-head">
      <div><h2>${esc(ctx.t('Assets.Title'))}</h2><p class="muted">${esc(ctx.t('Assets.Subtitle'))}</p></div>
      <button class="icon-btn" data-close type="button">✕</button>
    </header>
    <div class="asset-list">${rows}</div>
    <p class="asset-warning">${esc(ctx.t('Assets.Warning'))}</p>
    <footer class="modal-foot">
      <span></span>
      <div class="foot-actions">
        <button class="btn ghost" data-later type="button">${esc(ctx.t('Assets.Later'))}</button>
        <button class="btn danger" data-clear type="button">${esc(ctx.t('Assets.Clear'))}</button>
      </div>
    </footer>`);
  dialog.root.classList.add('asset-modal');
  dialog.root.dataset.role = 'asset-prompt';

  const close = () => {
    dialog.close();
  };

  dialog.root.querySelector('[data-later]').addEventListener('click', close);
  dialog.root.querySelector('[data-clear]').addEventListener('click', () => {
    close();
    runClear(ctx);
  });
  dialog.root.querySelector('[data-close]').addEventListener('click', close);
  dialog.root.closest('.overlay').addEventListener('click', (event) => {
    if (event.target === event.currentTarget) close();
  });
}

async function runClear(ctx) {
  const dialog = modal(`
    <header class="modal-head"><div><h2>${esc(ctx.t('Assets.Title'))}</h2></div></header>
    <p class="asset-status" data-role="asset-status">${esc(ctx.t('Assets.Status'))}</p>
    <div class="asset-progress"><i data-role="asset-bar"></i></div>
    <footer class="modal-foot"><span></span><div class="foot-actions">
      <button class="btn" data-cancel-clear type="button">${esc(ctx.t('Assets.Cancel'))}</button>
    </div></footer>`);
  dialog.root.classList.add('asset-modal');

  const status = dialog.root.querySelector('[data-role="asset-status"]');
  const bar = dialog.root.querySelector('[data-role="asset-bar"]');
  const cancelButton = dialog.root.querySelector('[data-cancel-clear]');

  const off = ctx.api.on('asset-progress', (update) => {
    if (update.phase === 'done') return;
    const label = familyName(ctx, update.family);
    status.textContent = ctx.t('Assets.Clearing')
      .replace('{0}', label)
      .replace('{1}', update.game || '')
      .replace('{2}', update.current)
      .replace('{3}', update.total);
    if (update.total > 0) {
      bar.style.width = `${Math.min(100, (update.current * 100) / update.total)}%`;
    }
  });

  cancelButton.addEventListener('click', () => {
    cancelButton.disabled = true;
    ctx.api.request('assets.cancel').catch(() => {});
  });

  try {
    const result = await ctx.api.request('assets.clear', {}, 1800000);
    off();
    dialog.close();
    currentReport = null;
    hideBanner();
    ctx.api.invalidate();
    ctx.ui.libraryChanged('assets');
    showResult(ctx, result);
  } catch (error) {
    off();
    dialog.close();
    ctx.ui.toast(error.message || error.code || ctx.t('Assets.FailedStatus'), 'error');
  }
}

function failureReason(ctx, code) {
  const key = `Assets.Fail.${code}`;
  const text = ctx.t(key);
  return text === key ? ctx.t('Assets.Fail.Generic') : text;
}

function showResult(ctx, result) {
  const failures = result?.failures || [];
  const cleared = result?.cleared || 0;
  const doneText = ctx.t('Assets.Done').replace('{0}', cleared);
  const title = result?.cancelled
    ? ctx.t('Assets.Cancelled').replace('{0}', cleared)
    : failures.length
      ? ctx.t('Assets.FailedStatus')
      : doneText;
  const summary = failures.length ? `<p class="asset-status">${esc(doneText)}</p>` : '';

  const failureRows = failures.map((failure, index) => `
    <div class="asset-failure" data-failure="${index}">
      <div class="asset-failure-main">
        <b>${esc(failure.gameName)}</b>
        <span>${esc(familyName(ctx, failure.family))} · ${esc(failure.code)}</span>
        <p>${esc(failureReason(ctx, failure.code))}</p>
        <code>${esc(failure.targetDirectory)}</code>
      </div>
      <button class="btn small" data-open-failure="${index}" type="button">${esc(ctx.t('Assets.OpenFolder'))}</button>
    </div>`).join('');

  const dialog = modal(`
    <header class="modal-head"><div><h2>${esc(title)}</h2></div></header>
    ${summary}
    ${failures.length > 0 ? `
      <h3 class="asset-failed-title">${esc(ctx.t('Assets.FailedTitle'))}</h3>
      <div class="asset-failures">${failureRows}</div>
      <p class="asset-warning">${esc(ctx.t('Assets.ManualHint'))}</p>` : ''}
    <footer class="modal-foot"><span class="muted small">${esc(ctx.t('Assets.Subtitle'))}</span>
      <div class="foot-actions"><button class="btn primary" data-result-close type="button">${esc(ctx.t('Assets.GotIt'))}</button></div>
    </footer>`);
  dialog.root.classList.add('asset-modal');

  dialog.root.querySelector('[data-result-close]').addEventListener('click', dialog.close);
  dialog.root.querySelectorAll('[data-open-failure]').forEach((button) => {
    button.addEventListener('click', () => {
      const failure = failures[Number(button.dataset.openFailure)];
      if (!failure) return;
      ctx.api.request('games.openFolder', { id: failure.gameId })
        .catch((error) => ctx.ui.toast(error.message || error.code, 'error'));
    });
  });
}
