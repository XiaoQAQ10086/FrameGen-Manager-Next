import { escapeHtml as esc } from './ui.js';

let checking = false;
let dialogOpen = false;

function formatBytes(value) {
  const size = Number(value) || 0;
  if (size >= 1024 * 1024 * 1024) return `${(size / 1024 / 1024 / 1024).toFixed(2)} GB`;
  if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`;
  if (size >= 1024) return `${(size / 1024).toFixed(0)} KB`;
  return `${size} B`;
}

function formatSpeed(value) {
  return `${formatBytes(value)}/s`;
}

export async function checkForUpdates(ctx, options = {}) {
  const { silent = false } = options;
  if (checking || dialogOpen) return;
  checking = true;
  try {
    const info = await ctx.api.request('updates.check', {}, 60000);
    if (!info?.available) {
      if (!silent) {
        ctx.ui.toast(info?.error ? `${ctx.t('Update.CheckFailed')}: ${info.error}` : ctx.t('Update.UpToDate'), info?.error ? 'error' : '');
      }
      return;
    }
    showUpdateDialog(ctx, info);
  } catch (error) {
    if (!silent) ctx.ui.toast(error.message || error.code || ctx.t('Update.CheckFailed'), 'error');
  } finally {
    checking = false;
  }
}

export function openCloudDownload(ctx) {
  const url = ctx.info?.netdiskUrl;
  ctx.api.request('updates.openPage', { url }).catch((error) => {
    ctx.ui.toast(error.message || error.code || ctx.t('Update.OpenFailed'), 'error');
  });
}

function showUpdateDialog(ctx, info) {
  if (dialogOpen) return;
  dialogOpen = true;

  const root = document.getElementById('modal-root');
  const overlay = document.createElement('div');
  overlay.className = 'overlay update-overlay';
  overlay.innerHTML = `
    <section class="modal update-modal" role="dialog" aria-modal="true">
      <header class="modal-head">
        <div>
          <h2>${esc(ctx.t('Update.Available'))}</h2>
          <p class="muted">${esc(ctx.t('Update.Subtitle'))}</p>
        </div>
        <button class="icon-btn" data-update-close type="button">✕</button>
      </header>
      <div class="update-versions">
        <div><span>${esc(ctx.t('Update.Current'))}</span><b>v${esc(info.currentVersion)}</b></div>
        <div><span>${esc(ctx.t('Update.LatestVersion'))}</span><b class="update-new">v${esc(info.latestVersion)}</b></div>
      </div>
      <h3 class="update-notes-title">${esc(ctx.t('Update.Notes'))}</h3>
      <pre class="update-notes">${esc(info.notes?.trim() || ctx.t('Update.NoNotes'))}</pre>
      <div class="update-progress hidden" data-role="update-progress">
        <div class="update-bar"><i data-role="update-bar"></i></div>
        <div class="update-meta">
          <span data-role="update-percent">0%</span>
          <span data-role="update-speed"></span>
        </div>
      </div>
      <footer class="modal-foot update-foot">
        <button class="btn" data-update-cloud type="button">${esc(ctx.t('Update.Cloud'))}</button>
        <div class="foot-actions">
          <button class="btn ghost" data-update-close type="button">${esc(ctx.t('Update.Later'))}</button>
          <button class="btn success" data-update-start type="button">${esc(ctx.t('Update.Install'))}</button>
        </div>
      </footer>
    </section>`;
  root.appendChild(overlay);

  const progress = overlay.querySelector('[data-role="update-progress"]');
  const bar = overlay.querySelector('[data-role="update-bar"]');
  const percent = overlay.querySelector('[data-role="update-percent"]');
  const speed = overlay.querySelector('[data-role="update-speed"]');
  const startButton = overlay.querySelector('[data-update-start]');
  const laterButton = overlay.querySelector('.update-foot [data-update-close]');
  const cloudButton = overlay.querySelector('[data-update-cloud]');
  const closeButtons = overlay.querySelectorAll('[data-update-close]');
  let downloading = false;
  let unsubscribe = null;

  const close = () => {
    if (downloading) {
      ctx.api.request('updates.cancel').catch(() => {});
    }
    unsubscribe?.();
    unsubscribe = null;
    overlay.remove();
    dialogOpen = false;
  };

  closeButtons.forEach((button) => button.addEventListener('click', close));

  overlay.querySelector('[data-update-cloud]').addEventListener('click', () => {
    ctx.api.request('updates.openPage', { url: info.cloudUrl || info.htmlUrl }).catch((error) => {
      ctx.ui.toast(error.message || error.code || ctx.t('Update.OpenFailed'), 'error');
    });
  });

  startButton.addEventListener('click', async () => {
    if (downloading) return;
    if (!info.downloadUrl) {
      await ctx.api.request('updates.openPage', { url: info.htmlUrl }).catch(() => {});
      close();
      return;
    }

    downloading = true;
    startButton.disabled = true;
    startButton.textContent = ctx.t('Update.Downloading');
    laterButton.textContent = ctx.t('Update.Cancel');
    laterButton.classList.add('danger');
    cloudButton.disabled = true;
    progress.classList.remove('hidden');
    unsubscribe = ctx.api.on('update.progress', (data) => {
      const value = Math.max(0, Math.min(100, Number(data.percent) || 0));
      bar.style.width = `${value}%`;
      percent.textContent = `${value.toFixed(0)}%`;
      speed.textContent = data.speed ? formatSpeed(data.speed) : '';
    });

    try {
      const result = await ctx.api.request(
        'updates.download',
        { url: info.downloadUrl, fileName: info.assetName, version: info.latestVersion },
        3600000,
      );
      if (result?.cancelled) return;
      downloading = false;
      startButton.textContent = ctx.t('Update.Done');
      startButton.classList.remove('success');
      startButton.disabled = true;
      laterButton.textContent = ctx.t('Update.Close');
      laterButton.classList.remove('danger');
      closeButtons.forEach((button) => {
        if (button.classList.contains('btn')) button.textContent = ctx.t('Update.Close');
      });
      ctx.ui.toast(ctx.t('Update.Downloaded').replace('{0}', result.path || info.assetName));
      unsubscribe?.();
      unsubscribe = null;
    } catch (error) {
      downloading = false;
      startButton.disabled = false;
      startButton.textContent = ctx.t('Update.Install');
      laterButton.textContent = ctx.t('Update.Later');
      laterButton.classList.remove('danger');
      cloudButton.disabled = false;
      ctx.ui.toast(error.message || error.code || ctx.t('Update.DownloadFailed'), 'error');
    }
  });
}
