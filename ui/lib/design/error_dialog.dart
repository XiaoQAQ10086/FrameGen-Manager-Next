import 'dart:io';

import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';

import '../bridge/core_library.dart';
import '../bridge/logs_bridge.dart';
import '../l10n/app_localizations.dart';
import 'app_tokens.dart';
import 'legacy_dialog.dart';
import 'legacy_toast.dart';

/// Failure dialog with the error code and a diagnostics copy button.
final class ErrorDialog {
  const ErrorDialog._();

  static Future<void> show(
    BuildContext context, {
    required String message,
    required String code,
    String? detail,
  }) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return showLegacyDialog<void>(
      context: context,
      builder: (BuildContext dialogContext) => AlertDialog(
        title: Text(l10n.errorTitle),
        content: SizedBox(
          width: 420,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(message, style: Theme.of(context).textTheme.bodyMedium),
              const SizedBox(height: 12),
              Text(
                '${l10n.errorCodeLabel}: $code',
                style: const TextStyle(
                  color: AppTokens.textSecondary,
                  fontSize: 12,
                  fontFamily: 'Consolas',
                ),
              ),
              if (detail != null && detail.isNotEmpty) ...<Widget>[
                const SizedBox(height: 6),
                Text(
                  detail,
                  maxLines: 6,
                  overflow: TextOverflow.ellipsis,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ],
          ),
        ),
        actions: <Widget>[
          TextButton.icon(
            onPressed: () async {
              await Clipboard.setData(
                ClipboardData(
                  text: _diagnostics(
                    context,
                    message: message,
                    code: code,
                    detail: detail,
                  ),
                ),
              );
              if (!context.mounted) {
                return;
              }
              LegacyToast.show(context, l10n.errorCopied);
            },
            icon: const Icon(Icons.copy, size: 16),
            label: Text(l10n.errorCopy),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: Text(l10n.ok),
          ),
        ],
      ),
    );
  }

  static String _diagnostics(
    BuildContext context, {
    required String message,
    required String code,
    String? detail,
  }) {
    final CoreLibrary? core = CoreLibrary.tryOpen();
    final String logs = LogsBridge.tail(core, maxLines: 80).join('\n');
    return <String>[
      'message: $message',
      'code: $code',
      if (detail != null && detail.isNotEmpty) 'detail: $detail',
      'platform: ${Platform.operatingSystemVersion}',
      'core: ${core?.version ?? 'unavailable'}',
      '--- recent log ---',
      logs,
    ].join('\n');
  }
}
