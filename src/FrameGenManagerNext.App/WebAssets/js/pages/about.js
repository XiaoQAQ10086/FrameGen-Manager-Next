export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;
    const info = ctx.info || await api.request('app.info');
    root.innerHTML = `
      <section class="page">
        <header class="page-head"><div><h1>${t('About.Title')}</h1><p>${t('About.Subtitle')}</p></div></header>
        <div class="panel form-panel">
          <div class="form-row"><div><h3>${t('About.Product')}</h3><p>${info.product}</p></div></div>
          <div class="form-row"><div><h3>${t('About.Version')}</h3><p>v${info.version}</p></div></div>
          <div class="form-row"><div><h3>${t('About.Runtime')}</h3><p>${info.runtime}</p></div></div>
          <div class="form-row"><div><h3>${t('About.Portable')}</h3><p>${info.portable ? t('About.PortableYes') : t('About.PortableNo')}</p></div></div>
          <div class="path-block"><code>${info.dataRoot}</code><button class="btn small" data-action="open-data" type="button">${t('About.OpenData')}</button></div>
          <div class="form-row">
            <div><h3>GitHub</h3><p>${info.repository}</p></div>
            <button class="btn small" data-action="open-repo" type="button">${t('About.OpenGitHub')}</button>
          </div>
        </div>
      </section>`;

    const onClick = async (event) => {
      const button = event.target.closest('[data-action]');
      if (!button) return;
      if (button.dataset.action === 'open-data') {
        await api.request('app.openData').catch((error) => ui.toast(error.message, 'error'));
      }
      if (button.dataset.action === 'open-repo') {
        await api.request('app.openRepository').catch((error) => ui.toast(error.message, 'error'));
      }
    };
    root.addEventListener('click', onClick);
    return () => root.removeEventListener('click', onClick);
  },
};