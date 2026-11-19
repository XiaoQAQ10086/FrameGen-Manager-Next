import 'dart:io';

import 'package:material_ui/material_ui.dart';

import '../../bridge/library_game.dart';
import '../../design/app_tokens.dart';

/// Cover thumbnail with a game-pad placeholder when no image is cached.
class GameCover extends StatelessWidget {
  const GameCover({super.key, required this.game, required this.path});

  final LibraryGame game;
  final String? path;

  @override
  Widget build(BuildContext context) {
    final Widget placeholder = Container(
      width: 36,
      height: 36,
      decoration: BoxDecoration(
        color: AppTokens.accentSoft,
        borderRadius: BorderRadius.circular(10),
      ),
      child: const Icon(
        Icons.sports_esports,
        size: 18,
        color: AppTokens.accent,
      ),
    );
    final String? cover = path;
    if (cover == null || cover.isEmpty) {
      return placeholder;
    }
    return ClipRRect(
      borderRadius: BorderRadius.circular(10),
      child: Image.file(
        File(cover),
        width: 36,
        height: 36,
        fit: BoxFit.cover,
        errorBuilder: (BuildContext context, Object error, StackTrace? stack) =>
            placeholder,
      ),
    );
  }
}
