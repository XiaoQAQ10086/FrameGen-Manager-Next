import 'dart:io';

import 'package:material_ui/material_ui.dart';

import 'app_tokens.dart';

/// Title block shared by the deploy-style modals: optional square cover, a
/// 20px name, a muted subtitle and a circular close button.
class LegacyDialogHeader extends StatelessWidget {
  const LegacyDialogHeader({
    super.key,
    this.coverPath,
    required this.title,
    required this.subtitle,
    required this.onClose,
  });

  static const double _coverSize = 48;

  /// Omitted for dialogs that have no artwork, such as the DLSS5 detail.
  final String? coverPath;
  final String title;
  final String subtitle;
  final VoidCallback onClose;

  @override
  Widget build(BuildContext context) {
    final String? cover = coverPath;
    return Row(
      children: <Widget>[
        if (cover != null) ...<Widget>[
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: cover.isNotEmpty
                ? Image(
                    image: ResizeImage(FileImage(File(cover)), width: 144),
                    width: _coverSize,
                    height: _coverSize,
                    fit: BoxFit.cover,
                    errorBuilder:
                        (
                          BuildContext context,
                          Object error,
                          StackTrace? stack,
                        ) => _placeholder(),
                  )
                : _placeholder(),
          ),
          const SizedBox(width: 14),
        ],
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Text(
                title,
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
                subtitle,
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
        title.isEmpty ? '?' : title.substring(0, 1).toUpperCase(),
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
        shape: const CircleBorder(side: BorderSide(color: AppTokens.border)),
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
