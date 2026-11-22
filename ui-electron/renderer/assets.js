import { $, call, esc, hideProgress, showProgress, toast } from './core.js';
import { t } from './l10n.js';

function folderName(path) {
  const parts = String(path || '').split(/[\/]/).filter(Boolean);
  return parts.length ? parts[parts.length - 1] : path;
}

export function showAssetReport(report) {
  const cleared = report.cleared || [];
  const upgrades = report.upgrades || [];
  const failed = report.failed || [];
  if (!cleared.length && !upgrades.length && !failed.length) return false;
  const overlay = document.createElement('div');
  overlay.className = 'overlay';
  overlay.innerHTML = `
    <section class="modal confirm-modal asset-update-modal" role="dialog" aria-modal="true">
      <header class="modal-head"><h2>${t('assetUpdateTitle')}</h2></header>
      ${upgrades.length ? `<p class="hint">${t('assetUpdateUpgradeHint', { count: upgrades.length })}</p>
        <div class="asset-update-groups">
          ${upgrades.map((item) => `<div class="asset-update-group"><b>${esc(folderName(item.targetDirectory))}</b><span class="muted small">${esc(item.family)} · ${esc(item.oldVersion)} → ${esc(item.newVersion)}</span><button class="btn small success" data-asset-upgrade data-target="${esc(item.targetDirectory)}" type="button">${t('assetUpdateUpgrade')}</button></div>`).join('')}
        </div>` : ''}
      ${cleared.length ? `<p class="hint">${t('assetUpdateCleared', { count: cleared.length })}</p>
        <div class="asset-update-groups">
          ${cleared.map((item) => `<div class="asset-update-group"><b>${esc(folderName(item.targetDirectory))}</b><span class="muted small">${esc(item.family)} · ${esc(item.oldVersion)} → ${esc(item.newVersion)}</span></div>`).join('')}
        </div>` : ''}
      ${failed.length ? `<p class="hint danger">${t('assetUpdateFailed', { count: failed.length })}</p>
        <div class="asset-update-groups">
          ${failed.map((item) => `<div class="asset-update-group"><b>${esc(folderName(item.targetDirectory))}</b><span class="muted small">${esc(item.family)} · ${esc(item.code)} · ${esc(item.reason)}</span></div>`).join('')}
        </div>` : ''}
      <footer class="modal-foot">
        <span></span>
        <div class="foot-actions"><button class="btn success" data-asset-close type="button">${t('assetUpdateClose')}</button></div>
      </footer>
    </section>`;
  const close = () => overlay.remove();
  overlay.querySelector('[data-asset-close]').onclick = close;
  overlay.addEventListener('click', (event) => {
    if (event.target === overlay) close();
  });
  overlay.addEventListener('click', async (event) => {
    const button = event.target.closest('[data-asset-upgrade]');
    if (!button) return;
    button.disabled = true;
    showProgress(t('assetUpdateUpgrading'), false);
    try {
      const reply = await call('assets.upgrade', { targetDir: button.dataset.target });
      toast(reply.upgraded ? t('assetUpdateUpgraded') : t('assetUpdateLatest'));
      button.closest('.asset-update-group')?.remove();
    } catch (error) {
      toast(error.message, 'error');
      button.disabled = false;
    } finally {
      hideProgress();
    }
  });
  $('#modal-root').appendChild(overlay);
  return true;
}

export async function checkAssetUpdates() {
  try {
    const report = await call('assets.reconcile');
    return showAssetReport(report);
  } catch {
    return false;
  }
}
