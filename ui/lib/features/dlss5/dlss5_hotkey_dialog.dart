import 'package:flutter/services.dart';
import 'package:material_ui/material_ui.dart';

import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';
import 'dlss5_hotkey.dart';

/// Records the ReShade overlay key and returns its Windows virtual key code.
class Dlss5HotkeyDialog extends StatefulWidget {
  const Dlss5HotkeyDialog({super.key, this.initialVirtualKey});

  final int? initialVirtualKey;

  @override
  State<Dlss5HotkeyDialog> createState() => _Dlss5HotkeyDialogState();
}

class _Dlss5HotkeyDialogState extends State<Dlss5HotkeyDialog> {
  final FocusNode _focus = FocusNode(debugLabel: 'dlss5-hotkey');
  late int? _virtualKey = widget.initialVirtualKey;

  @override
  void dispose() {
    _focus.dispose();
    super.dispose();
  }

  void _onKey(KeyEvent event) {
    if (event is! KeyDownEvent) {
      return;
    }
    if (event.logicalKey == LogicalKeyboardKey.escape) {
      Navigator.pop(context);
      return;
    }
    final int? virtualKey = Dlss5Hotkey.virtualKeyFor(event.physicalKey);
    if (virtualKey != null) {
      setState(() => _virtualKey = virtualKey);
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return AlertDialog(
      title: Text(l10n.dlss5Hotkey),
      content: KeyboardListener(
        focusNode: _focus,
        autofocus: true,
        onKeyEvent: _onKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Text(l10n.dlss5HotkeyHint),
            const SizedBox(height: 16),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(vertical: 14),
              alignment: Alignment.center,
              decoration: BoxDecoration(
                color: AppTokens.surfaceMuted,
                borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
                border: Border.all(color: AppTokens.border),
              ),
              child: Text(
                _virtualKey == null ? '—' : Dlss5Hotkey.label(_virtualKey!),
                style: const TextStyle(
                  color: AppTokens.textPrimary,
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
          ],
        ),
      ),
      actions: <Widget>[
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: Text(l10n.cancel),
        ),
        FilledButton(
          onPressed: _virtualKey == null
              ? null
              : () => Navigator.pop(context, _virtualKey),
          child: Text(l10n.save),
        ),
      ],
    );
  }
}
