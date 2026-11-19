import 'package:material_ui/material_ui.dart';

import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// Static donation page with the author's QR codes.
class DonatePage extends StatelessWidget {
  const DonatePage({super.key});

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return SingleChildScrollView(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            l10n.donateTitle,
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 6),
          Text(
            l10n.donateSubtitle,
            style: Theme.of(context).textTheme.bodyMedium,
          ),
          const SizedBox(height: 22),
          Wrap(
            alignment: WrapAlignment.center,
            spacing: 22,
            runSpacing: 18,
            children: <Widget>[
              _QrCard(image: 'assets/wechat.png', label: l10n.donateWechat),
              _QrCard(image: 'assets/alipay.jpg', label: l10n.donateAlipay),
            ],
          ),
          const SizedBox(height: 18),
          Text(
            l10n.donateNote,
            textAlign: TextAlign.center,
            style: Theme.of(context).textTheme.bodyMedium,
          ),
        ],
      ),
    );
  }
}

class _QrCard extends StatelessWidget {
  const _QrCard({required this.image, required this.label});

  final String image;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Container(
          width: 240,
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            color: AppTokens.surfaceSolid,
            borderRadius: BorderRadius.circular(AppTokens.radius),
            border: Border.all(color: AppTokens.border),
            boxShadow: AppTokens.panelShadow,
          ),
          child: AspectRatio(
            aspectRatio: 3 / 4,
            child: Image.asset(image, fit: BoxFit.contain),
          ),
        ),
        const SizedBox(height: 10),
        Text(label, style: Theme.of(context).textTheme.titleMedium),
      ],
    );
  }
}
