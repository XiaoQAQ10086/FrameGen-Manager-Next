import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/library_game.dart';
import '../../bridge/path_display.dart';
import '../../design/app_tokens.dart';

/// Cover, game name and install path shown at the top of the deploy modal.
class DeployDialogHeader extends StatelessWidget {
  const DeployDialogHeader({
    super.key,
    required this.game,
    required this.coverPath,
    required this.onClose,
  });

  static const double _coverSize = 48;

  final LibraryGame game;
  final String? coverPath;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    final String? cover = coverPath;
    return Row(
      children: <Widget>[
        ClipRRect(
          borderRadius: BorderRadius.circular(12),
          child: cover != null && cover.isNotEmpty
              ? Image(
                  image: ResizeImage(FileImage(File(cover)), width: 144),
                  width: _coverSize,
                  height: _coverSize,
                  fit: BoxFit.cover,
                  errorBuilder:
                      (BuildContext context, Object error, StackTrace? stack) =>
                          _placeholder(),
                )
              : _placeholder(),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                game.name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppTokens.textPrimary,
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                  height: 1.25,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                displayPath(game.targetDirectory),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppTokens.textSecondary,
                  fontSize: 12,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(width: 8),
        _CloseButton(onPressed: onClose),
      ],
    );
  }

  Widget _placeholder() {
    return Container(
      width: _coverSize,
      height: _coverSize,
      color: AppTokens.accentSoft,
      alignment: Alignment.center,
      child: Text(
        game.name.isEmpty ? '?' : game.name.substring(0, 1).toUpperCase(),
        style: const TextStyle(
          color: AppTokens.monogram,
          fontSize: 20,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

/// Circular close button matching the legacy modal's corner control.
class _CloseButton extends StatelessWidget {
  const _CloseButton({required this.onPressed});

  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 32,
      height: 32,
      child: Material(
        color: Colors.transparent,
        shape: const CircleBorder(
          side: BorderSide(color: AppTokens.border),
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onPressed,
          splashFactory: NoSplash.splashFactory,
          hoverColor: const Color(0x0F007AFF),
          mouseCursor: SystemMouseCursors.click,
          child: const Icon(
            Icons.close,
            size: 16,
            color: AppTokens.textSecondary,
          ),
        ),
      ),
    );
  }
}
