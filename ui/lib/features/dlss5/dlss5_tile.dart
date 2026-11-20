import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/dlss5_state.dart';
import '../../bridge/graphics_api_label.dart';
import '../../bridge/library_game.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';

/// Cover tile in the legacy DLSS5 wall.
class Dlss5Tile extends StatelessWidget {
  const Dlss5Tile({
    super.key,
    required this.game,
    required this.state,
    required this.coverPath,
    required this.onOpen,
    required this.onRemove,
  });

  final LibraryGame game;
  final Dlss5State? state;
  final String? coverPath;
  final VoidCallback onOpen;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final Dlss5State? current = state;
    final bool deployed =
        current != null &&
        current.state != 'notDeployed' &&
        current.state != 'unknown' &&
        current.state != 'coreUnavailable';
    return ClipRRect(
      borderRadius: BorderRadius.circular(AppTokens.radius),
      child: Material(
        color: AppTokens.surfaceSolid,
        child: InkWell(
          onTap: onOpen,
          mouseCursor: SystemMouseCursors.click,
          child: AspectRatio(
            aspectRatio: 0.72,
            child: Stack(
              fit: StackFit.expand,
              children: <Widget>[
                _cover(),
                const DecoratedBox(
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      begin: Alignment.topCenter,
                      end: Alignment.bottomCenter,
                      colors: <Color>[Colors.transparent, Color(0xC7101A2E)],
                      stops: <double>[0.48, 1.0],
                    ),
                  ),
                ),
                if (current?.hasLegacyResidue == true)
                  Positioned(
                    left: 10,
                    top: 10,
                    child: LegacyPill(
                      label: l10n.dlss5LegacyRibbon,
                      foreground: AppTokens.warning,
                      background: AppTokens.warningSoft,
                      border: AppTokens.warning.withValues(alpha: 0.4),
                      fontSize: 11,
                    ),
                  ),
                Positioned(
                  left: 12,
                  right: 12,
                  bottom: 12,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        game.name,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: Colors.white,
                          fontSize: 14,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 3),
                      Text(
                        l10n.dlss5ApiLabel(
                          GraphicsApiLabel.of(game.graphicsApi),
                        ),
                        style: const TextStyle(
                          color: Color(0xD9FFFFFF),
                          fontSize: 12,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        deployed ? l10n.dlss5Deployed : l10n.dlss5NotDeployed,
                        style: TextStyle(
                          color: deployed
                              ? const Color(0xFF7BE3A9)
                              : const Color(0xD9FFFFFF),
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                ),
                Positioned(
                  right: 8,
                  bottom: 8,
                  child: Material(
                    color: AppTokens.danger,
                    shape: const CircleBorder(),
                    child: InkWell(
                      customBorder: const CircleBorder(),
                      onTap: onRemove,
                      mouseCursor: SystemMouseCursors.click,
                      child: const Padding(
                        padding: EdgeInsets.all(6),
                        child: Icon(
                          Icons.delete_outline,
                          size: 16,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _cover() {
    final String? path = coverPath;
    if (path != null && path.isNotEmpty) {
      return Image(
        image: ResizeImage(FileImage(File(path)), width: 336),
        fit: BoxFit.cover,
        errorBuilder:
            (BuildContext context, Object error, StackTrace? stack) =>
                _placeholder(),
      );
    }
    return _placeholder();
  }

  Widget _placeholder() {
    return Container(
      color: const Color(0xFFDCE9FB),
      alignment: Alignment.center,
      child: Text(
        game.name.isEmpty ? '?' : game.name.substring(0, 1).toUpperCase(),
        style: const TextStyle(
          color: AppTokens.monogram,
          fontSize: 44,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
