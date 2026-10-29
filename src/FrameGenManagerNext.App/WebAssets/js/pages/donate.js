export default {
  async mount(root, ctx) {
    const { t } = ctx;
    root.innerHTML = `
      <section class="page">
        <header class="page-head">
          <div><h1>${t('Donate.Title')}</h1><p>${t('Donate.Subtitle')}</p></div>
        </header>
        <div class="donate">
          <figure class="qr">
            <img src="img/wechat.png" alt="${t('Donate.WeChat')}" />
            <figcaption>${t('Donate.WeChat')}</figcaption>
          </figure>
          <figure class="qr">
            <img src="img/alipay.jpg" alt="${t('Donate.Alipay')}" />
            <figcaption>${t('Donate.Alipay')}</figcaption>
          </figure>
        </div>
        <p class="donate-note">${t('Donate.Note')}</p>
      </section>`;
  },
};
