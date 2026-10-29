export default {
  async mount(root, ctx) {
    const { api, t, ui } = ctx;

    root.innerHTML = `
      <section class="page">
        <header class="page-head">
          <div><h1>${t('Logs.Title')}</h1><p>${t('Logs.Subtitle')}</p></div>
          <div class="head-actions">
            <button class="btn" data-action="open" type="button">${t('Logs.OpenFolder')}</button>
            <button class="btn primary" data-action="export" type="button">${t('Logs.Export')}</button>
          </div>
        </header>
        <div class="panel">
          <pre class="log-box" data-role="log"></pre>
        </div>
      </section>`;

    const q = (selector) => root.querySelector(selector);

    async function load() {
      q('[data-role="log"]').textContent = t('Logs.Status.Reading');
      try {
        const data = await api.request('logs.tail', { lines: 800 });
        q('[data-role="log"]').textContent = data.text || t('Logs.Status.Empty');
      } catch (error) {
        q('[data-role="log"]').textContent = error.message || '读取失败';
        ui.toast(error.message, 'error');
      }
    }

    const onClick = async (event) => {
      const button = event.target.closest('[data-action]');
      if (!button) return;
      if (button.dataset.action === 'open') {
        await api.request('logs.openFolder').catch((error) => ui.toast(error.message, 'error'));
        return;
      }
      if (button.dataset.action === 'export') {
        try {
          const data = await api.request('logs.export');
          ui.toast(t('Logs.Status.Exported').replace('{0}', data.path));
        } catch (error) {
          ui.toast(error.message, 'error');
        }
      }
    };
    root.addEventListener('click', onClick);

    await load();
    return () => root.removeEventListener('click', onClick);
  },
};