import 'package:material_ui/material_ui.dart';

import '../l10n/app_localizations.dart';
import 'app_tokens.dart';

class LibraryListHead extends StatelessWidget {
  const LibraryListHead({
    super.key,
    required this.title,
    required this.status,
    required this.removedCount,
    required this.scanning,
    required this.adding,
    required this.onAdd,
    required this.onRemoved,
    required this.onScan,
    this.badge,
  });

  final String title;
  final String status;
  final int removedCount;
  final bool scanning;
  final bool adding;
  final VoidCallback onAdd;
  final VoidCallback onRemoved;
  final VoidCallback onScan;
  final Widget? badge;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.end,
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Text(title, style: Theme.of(context).textTheme.headlineSmall),
                  if (badge != null) ...<Widget>[
                    const SizedBox(width: 8),
                    badge!,
                  ],
                ],
              ),
              const SizedBox(height: 2),
              Text(
                status,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppTokens.textSecondary,
                  fontSize: 12.5,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 12),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: <Widget>[
            OutlinedButton(
              onPressed: scanning || adding ? null : onAdd,
              child: Text(l10n.addGame),
            ),
            OutlinedButton(
              onPressed: onRemoved,
              child: Text(
                removedCount > 0
                    ? '${l10n.removedTitle} ($removedCount)'
                    : l10n.removedTitle,
              ),
            ),
            SizedBox(
              height: 36,
              child: FilledButton(
                onPressed: scanning ? null : onScan,
                style: FilledButton.styleFrom(
                  backgroundColor: AppTokens.scanGreen,
                  padding: const EdgeInsets.symmetric(horizontal: 16),
                ),
                child: scanning
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(
                          strokeWidth: 2,
                          color: Colors.white,
                        ),
                      )
                    : Text(l10n.scanGames),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

