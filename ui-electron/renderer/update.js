import { $, call, esc, toast } from './core.js';
import { t } from './l10n.js';

const QUARK_URL = 'https://pan.quark.cn/s/5708e53de73b';
const BAIDU_URL = 'https://pan.baidu.com/s/1hL4B4WJEV0ui6hVqxiPvRg?pwd=1234';

let open = false;

function formatSpeed(bytesPerSecond) {
  if (!Number.isFinite(bytesPerSecond) || bytesPerSecond <= 0) return '';
  const mb = bytesPerSecond / 1024 / 1024;
  if (mb >= 1) return `${mb.toFixed(1)} MB/s`;
  return `${Math.max(1, Math.round(bytesPerSecond / 1024))} KB/s`;
}

export function showUpdateDialog(info) {
  if (open) return;
  open = true;

  const overlay = document.createElement('div');
  overlay.className = 'overlay';
  overlay.innerHTML = `
    <section class="modal update-modal" role="dialog" aria-modal="true">
      <header class="modal-head">
        <div>
          <h2>${t('updateTitle')}</h2>
          <p class="muted">${t('updateAvailable', { version: info.version })}</p>
        </div>
        <button class="icon-btn" data-update-close type="button">✕</button>
      </header>
      <div class="update-versions">
        <div><span>${t('updateCurrent')}</span><b>v${esc(info.currentVersion || '')}</b></div>
        <div><span>${t('updateLatestVersion')}</span><b class="update-new">v${esc(info.version || '')}</b></div>
      </div>
      <h3 class="update-notes-title">${t('updateNotes')}</h3>
      <pre class="update-notes">${esc((info.notes || '').trim() || t('updateNoNotes'))}</pre>
      <div class="update-progress hidden" data-role="update-progress">
        <div class="update-bar"><i data-role="update-bar"></i></div>
        <div class="update-meta">
          <span data-role="update-percent">0%</span>
          <span data-role="update-speed"></span>
        </div>
      </div>
      <footer class="modal-foot update-foot">
        <div class="foot-actions">
          <button class="btn" data-update-quark type="button">${t('updateQuark')}</button>
          <button class="btn" data-update-baidu type="button">${t('updateBaidu')}</button>
        </div>
        <div class="foot-actions">
          <button class="btn" data-update-cancel type="button">${t('updateModalLater')}</button>
          <button class="btn success" data-update-start type="button">${t('updateDownload')}</button>
        </div>
      </footer>
    </section>`;
  $('#modal-root').appendChild(overlay);

  const progress = $('[data-role="update-progress"]', overlay);
  const bar = $('[data-role="update-bar"]', overlay);
  const percent = $('[data-role="update-percent"]', overlay);
  const speedNode = $('[data-role="update-speed"]', overlay);
  const cancelButton = $('[data-update-cancel]', overlay);
  const startButton = $('[data-update-start]', overlay);
  const quarkButton = $('[data-update-quark]', overlay);
  const baiduButton = $('[data-update-baidu]', overlay);

  let downloading = false;
  let closed = false;
  let cancelled = false;
  let timer = null;
  let lastBytes = 0;
  let lastTime = 0;

  const stopPolling = () => {
    if (timer) clearInterval(timer);
    timer = null;
  };

  const close = () => {
    if (closed) return;
    if (downloading && !cancelled) {
      cancelled = true;
      call('operation.cancel').catch(() => {});
    }
    stopPolling();
    overlay.remove();
    closed = true;
    open = false;
  };

  const resetIdle = () => {
    downloading = false;
    startButton.disabled = false;
    startButton.textContent = t('updateDownload');
    cancelButton.textContent = t('updateModalLater');
    cancelButton.classList.remove('danger');
    quarkButton.disabled = false;
    baiduButton.disabled = false;
    stopPolling();
  };

  const poll = () => {
    lastBytes = 0;
    lastTime = performance.now();
    timer = setInterval(async () => {
      try {
        const state = await call('operation.progress');
        const done = Number(state.done || 0);
        const total = Number(state.total || 0);
        const now = performance.now();
        const elapsed = Math.max(0.2, (now - lastTime) / 1000);
        const speed = done > lastBytes ? (done - lastBytes) / elapsed : 0;
        lastBytes = done;
        lastTime = now;
        const fraction = total > 0 ? Math.min(1, done / total) : 0;
        bar.style.width = `${Math.max(1, fraction * 100)}%`;
        percent.textContent = total > 0 ? `${Math.round(fraction * 100)}%` : '…';
        speedNode.textContent = speed > 0 ? formatSpeed(speed) : '';
      } catch {}
    }, 200);
  };

  const closeButtons = overlay.querySelectorAll('[data-update-close]');
  closeButtons.forEach((button) => button.addEventListener('click', close));
  overlay.addEventListener('click', (event) => {
    if (event.target === overlay) close();
  });
  quarkButton.addEventListener('click', () => window.fgm.openUrl(QUARK_URL));
  baiduButton.addEventListener('click', () => window.fgm.openUrl(BAIDU_URL));

  cancelButton.addEventListener('click', async () => {
    if (downloading) {
      cancelled = true;
      cancelButton.textContent = t('updateCancelling');
      cancelButton.disabled = true;
      await call('operation.cancel').catch(() => {});
      close();
      toast(t('updateCancelled'));
      return;
    }
    close();
  });

  startButton.addEventListener('click', async () => {
    if (downloading) return;
    downloading = true;
    cancelled = false;
    startButton.disabled = true;
    startButton.textContent = t('updateDownloading');
    cancelButton.textContent = t('updateCancelDownload');
    cancelButton.classList.add('danger');
    quarkButton.disabled = true;
    baiduButton.disabled = true;
    progress.classList.remove('hidden');
    bar.style.width = '1%';
    percent.textContent = '0%';
    speedNode.textContent = '';
    poll();

    try {
      const reply = await call('update.download');
      if (closed) return;
      if (!reply.path) {
        resetIdle();
        progress.classList.add('hidden');
        toast(t('updateLatest'));
        return;
      }
      stopPolling();
      downloading = false;
      bar.style.width = '100%';
      percent.textContent = '100%';
      speedNode.textContent = '';
      cancelButton.textContent = t('updateClose');
      cancelButton.classList.remove('danger');
      if (reply.portable) {
        startButton.textContent = t('updateDone');
        startButton.classList.remove('success');
        toast(t('updatePortableHint'));
      } else {
        startButton.textContent = t('updateInstalling');
        await call('update.install', { path: reply.path });
        toast(t('updateDownloaded'));
      }
    } catch (error) {
      if (closed) return;
      if (cancelled || error.code === 'OperationCancelled') {
        close();
        toast(t('updateCancelled'));
        return;
      }
      resetIdle();
      toast(`${t('updateFailed', { code: error.code || '' })}: ${error.message}`, 'error');
    }
  });
}
