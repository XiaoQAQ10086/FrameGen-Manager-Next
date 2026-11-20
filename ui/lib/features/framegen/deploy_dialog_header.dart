import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/library_game.dart';
import '../../bridge/path_display.dart';
import '../../design/app_tokens.dart';

class DeployDialogHeader extends StatelessWidget {
  const DeployDialogHeader({
    super.key,
    required this.game,
    required this.coverPath,
    required this.onClose,
  });

  final LibraryGame game;
  final String? coverPath;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    final String? cover = coverPath;
    return Row(
      children: <Widget>[
        ClipRRect(
          borderRadius: BorderRadius.circular(8),
          child: cover != null && cover.isNotEmpty
              ? Image.file(
                  File(cover),
                  width: 44,
                  height: 58,
                  fit: BoxFit.cover,
                  errorBuilder:
                      (BuildContext context, Object error, StackTrace? stack) =>
                          _placeholder(),
                )
              : _placeholder(),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                game.name,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 2),
              Text(
                displayPath(game.targetDirectory),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppTokens.textSecondary,
                  fontFamily: 'Consolas',
                  fontSize: 11.5,
                ),
              ),
            ],
          ),
        ),
        IconButton(
          onPressed: onClose,
          icon: const Icon(Icons.close, size: 18),
        ),
      ],
    );
  }

  Widget _placeholder() {
    return Container(
      width: 44,
      height: 58,
      color: AppTokens.accentSoft,
      alignment: Alignment.center,
      child: Text(
        game.name.isEmpty ? '?' : game.name.substring(0, 1).toUpperCase(),
        style: const TextStyle(
          color: Color(0xFF4E6F9D),
          fontSize: 18,
          fontWeight: FontWeight.w800,
        ),
      ),
    );
  }
}
