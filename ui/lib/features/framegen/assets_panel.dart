import 'package:material_ui/material_ui.dart';

import '../../bridge/panel_cache.dart';
import '../../bridge/system_bridge.dart';
import '../../bridge/system_paths.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_panel.dart';
import '../../l10n/app_localizations.dart';

/// Sidebar card: bundled/downloaded asset folders with open buttons.
class AssetsPanel extends StatefulWidget {
  const AssetsPanel({super.key});

  @override
  State<AssetsPanel> createState() => _AssetsPanelState();
}

class _AssetsPanelState extends State<AssetsPanel> {
  SystemPaths _paths = PanelCache.paths ?? const SystemPaths();

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final SystemPaths paths = await SystemBridge.paths();
    PanelCache.paths = paths;
    if (mounted) {
      setState(() => _paths = paths);
    }
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return LegacyPanel(
      title: l10n.assetsFolderTitle,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          _PathBlock(
            label: l10n.assetsBundled,
            path: _paths.bundledPacksDirectory,
            onOpen: () => SystemBridge.openFolder('bundledPacks'),
          ),
          const SizedBox(height: 10),
          _PathBlock(
            label: l10n.assetsDownloaded,
            path: _paths.packsDirectory,
            onOpen: () => SystemBridge.openFolder('packs'),
          ),
        ],
      ),
    );
  }
}

class _PathBlock extends StatelessWidget {
  const _PathBlock({
    required this.label,
    required this.path,
    required this.onOpen,
  });

  final String label;
  final String path;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: const TextStyle(
            color: AppTokens.textPrimary,
            fontSize: 13,
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          path.isEmpty ? '-' : _prettyPath(path),
          style: const TextStyle(
            color: AppTokens.textSecondary,
            fontFamily: 'Consolas',
            fontSize: 12,
            height: 1.45,
          ),
        ),
        const SizedBox(height: 6),
        SizedBox(
          width: double.infinity,
          child: OutlinedButton(
            onPressed: onOpen,
            style: LegacyButtonStyles.tall(LegacyButtonStyles.standard),
            child: Text(l10n.openFolder),
          ),
        ),
      ],
    );
  }

  String _prettyPath(String value) {
    if (value.length >= 2 && value[1] == ':') {
      return '${value[0].toUpperCase()}${value.substring(1)}';
    }
    return value;
  }
}
