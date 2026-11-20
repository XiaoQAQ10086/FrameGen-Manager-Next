import 'package:material_ui/material_ui.dart';

import '../../bridge/library_game.dart';
import '../../bridge/path_display.dart';
import '../../bridge/proxy_badge.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_dialog.dart';
import '../../design/legacy_dialog_header.dart';
import '../../design/legacy_field_row.dart';
import '../../design/legacy_select.dart';
import '../../l10n/app_localizations.dart';

/// Legacy ANR deploy modal: choose the proxy slot, then confirm.
class AnrDeployDialog extends StatefulWidget {
  const AnrDeployDialog({
    super.key,
    required this.game,
    this.proxies = const <ProxyBadge>[],
    this.chooseProxy = true,
  });

  /// Sent back instead of a module name when the user leaves it on auto.
  static const String auto = 'auto';
  static const List<String> slots = <String>[
    'dxgi.dll',
    'winmm.dll',
    'version.dll',
  ];

  final LibraryGame game;
  final List<ProxyBadge> proxies;

  /// Addon packages carry no proxy DLL, so they skip the selector.
  final bool chooseProxy;

  /// Returns the chosen slot, [auto], or null when the dialog was dismissed.
  static Future<String?> show(
    BuildContext context,
    LibraryGame game,
    List<ProxyBadge> proxies, {
    bool chooseProxy = true,
  }) {
    return showLegacyDialog<String>(
      context: context,
      builder: (BuildContext context) => AnrDeployDialog(
        game: game,
        proxies: proxies,
        chooseProxy: chooseProxy,
      ),
    );
  }

  @override
  State<AnrDeployDialog> createState() => _AnrDeployDialogState();
}

class _AnrDeployDialogState extends State<AnrDeployDialog> {
  String _choice = AnrDeployDialog.auto;

  Set<String> get _occupied => widget.proxies
      .map((ProxyBadge proxy) => proxy.module.toLowerCase())
      .toSet();

  /// Mirrors the core's pick: the first candidate that is still free.
  String get _autoProxy => AnrDeployDialog.slots.firstWhere(
    (String slot) => !_occupied.contains(slot),
    orElse: () => '',
  );

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final List<String> available = <String>[
      AnrDeployDialog.auto,
      ...AnrDeployDialog.slots.where(
        (String slot) => !_occupied.contains(slot),
      ),
    ];
    return Dialog(
      backgroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(20),
        side: const BorderSide(color: Color(0x1F14233C)),
      ),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560, maxHeight: 520),
        child: Padding(
          padding: const EdgeInsets.all(22),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              LegacyDialogHeader(
                title: l10n.deployAnrTitle,
                subtitle: widget.game.name,
                onClose: () => Navigator.pop(context),
              ),
              const SizedBox(height: 12),
              Text(
                displayPath(widget.game.targetDirectory),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppTokens.textSecondary,
                  fontSize: 12,
                ),
              ),
              const SizedBox(height: 14),
              const Divider(height: 1, thickness: 1, color: AppTokens.border),
              const SizedBox(height: 14),
              if (widget.chooseProxy) ...<Widget>[
                LegacyFieldRow(
                  label: l10n.optionProxySlot,
                  child: LegacySelect<String>(
                    value: available.contains(_choice)
                        ? _choice
                        : AnrDeployDialog.auto,
                    items: available,
                    labelFor: (String item) => item == AnrDeployDialog.auto
                        ? (_autoProxy.isEmpty
                              ? l10n.optionOff
                              : l10n.dlss5ProxyAuto(_autoProxy))
                        : item,
                    onChanged: (String item) =>
                        setState(() => _choice = item),
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  l10n.anrProxyHint,
                  style: const TextStyle(
                    color: AppTokens.textSecondary,
                    fontSize: 12,
                  ),
                ),
              ],
              const SizedBox(height: 14),
              const Divider(height: 1, thickness: 1, color: AppTokens.border),
              const SizedBox(height: 12),
              Row(
                children: <Widget>[
                  const Spacer(),
                  TextButton(
                    onPressed: () => Navigator.pop(context),
                    child: Text(l10n.cancel),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    onPressed: () => Navigator.pop(context, _choice),
                    style: LegacyButtonStyles.tall(
                      LegacyButtonStyles.success,
                    ),
                    child: Text(l10n.deploy),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
