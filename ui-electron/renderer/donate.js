import { t } from './l10n.js';

export function renderDonate(root) {
  root.innerHTML = `
    <section class="page">
      <header class="page-head">
        <h1>${t('donateTitle')}</h1>
        <p>${t('donateSubtitle')}</p>
      </header>
      <div class="donate">
        <figure class="qr">
          <img src="wechat.png" alt="">
          <figcaption>${t('donateWechat')}</figcaption>
        </figure>
        <figure class="qr">
          <img src="alipay.jpg" alt="">
          <figcaption>${t('donateAlipay')}</figcaption>
        </figure>
      </div>
      <p class="donate-note">${t('donateNote')}</p>
    </section>`;
  root.dataset.ready = '1';
}
