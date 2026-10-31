import { checkForUpdates, openCloudDownload } from '../update.js';

export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    const info = ctx.info || await api.request('app.info');
    root.innerHTML = `
      <section class="page about-page">
        <header class="page-head"><div><h1>${t('About.Title')}</h1><p>${t('About.Subtitle')}</p></div></header>
        <div class="panel form-panel">
          <div class="form-row"><div><h3>${t('About.Version')}</h3><p>v${info.version}</p></div></div>
          <div class="about-actions">
            <button class="btn success" data-action="check-update" type="button">${t('About.CheckUpdate')}</button>
            <button class="btn" data-action="cloud-download" type="button">${t('Update.Cloud')}</button>
          </div>
          <div class="form-row">
            <div><h3>GitHub</h3><p>${info.repository}</p></div>
            <button class="btn small" data-action="open-repo" type="button">${t('About.OpenGitHub')}</button>
          </div>
        </div>
        <div class="panel form-panel">
          <div class="ctitle">${t('About.Startup')}</div>
          <div class="form-row">
            <div><h3>${t('About.AutoScan')}</h3><p>${t('About.AutoScanHint')}</p></div>
            <label class="switch">
              <input type="checkbox" data-role="auto-scan" aria-label="${t('About.AutoScan')}"
                ${info.settings?.autoScanOnStartup ? 'checked' : ''}>
              <span></span>
            </label>
          </div>
        </div>
        <div class="panel form-panel">
          <div class="ctitle">${t('About.Feedback')}</div>
          <p class="hint">${t('About.FeedbackBody')}</p>
        </div>
      </section>`;

    const autoScan = root.querySelector('[data-role="auto-scan"]');
    const onToggle = async () => {
      const desired = autoScan.checked;
      autoScan.disabled = true;
      try {
        await api.request('app.saveSettings', {
          language: info.language === 'en' ? 'en' : 'zh',
          autoScanOnStartup: desired,
        });
        await ctx.refreshInfo();
        ui.toast(t('Settings.Status.Saved'));
      } catch (error) {
        autoScan.checked = !desired;
        ui.toast(error.message || error.code || t('Settings.Status.SaveFailed'), 'error');
      } finally {
        autoScan.disabled = false;
      }
    };
    autoScan.addEventListener('change', onToggle);

    const onClick = async (event) => {
      const button = event.target.closest('[data-action]');
      if (!button) return;
      if (button.dataset.action === 'check-update') {
        await checkForUpdates(ctx);
      }
      if (button.dataset.action === 'cloud-download') {
        openCloudDownload(ctx);
      }
      if (button.dataset.action === 'open-repo') {
        await api.request('app.openRepository').catch((error) => ui.toast(error.message, 'error'));
      }
    };
    root.addEventListener('click', onClick);
    return () => {
      root.removeEventListener('click', onClick);
      autoScan.removeEventListener('change', onToggle);
    };
  },
};
