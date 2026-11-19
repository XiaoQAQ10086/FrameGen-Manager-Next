import 'dart:ui' show ImageFilter;

import 'package:flutter/services.dart' show LogicalKeyboardKey;

import 'package:material_ui/material_ui.dart';

/// Shows a modal dialog with the page behind it blurred and dimmed.
///
/// Mirrors `showDialog` for the common case and keeps the legacy focus effect:
/// the window content blurs back while the dialog fades and scales in.
Future<T?> showLegacyDialog<T>({
  required BuildContext context,
  required WidgetBuilder builder,
  bool barrierDismissible = true,
}) {
  return showGeneralDialog<T>(
    context: context,
    barrierDismissible: barrierDismissible,
    barrierLabel: MaterialLocalizations.of(context).modalBarrierDismissLabel,
    barrierColor: Colors.transparent,
    transitionDuration: const Duration(milliseconds: 220),
    pageBuilder:
        (
          BuildContext pageContext,
          Animation<double> animation,
          Animation<double> secondaryAnimation,
        ) {
          return Stack(
            fit: StackFit.expand,
            children: <Widget>[
              GestureDetector(
                behavior: HitTestBehavior.opaque,
                onTap: barrierDismissible
                    ? () => Navigator.of(pageContext).maybePop()
                    : null,
                child: BackdropFilter(
                  filter: ImageFilter.blur(sigmaX: 6, sigmaY: 6),
                  child: const ColoredBox(color: Color(0x2E101828)),
                ),
              ),
              Center(
                child: CallbackShortcuts(
                  bindings: barrierDismissible
                      ? <ShortcutActivator, VoidCallback>{
                          const SingleActivator(
                            LogicalKeyboardKey.escape,
                          ): () => Navigator.of(pageContext).maybePop(),
                        }
                      : const <ShortcutActivator, VoidCallback>{},
                  child: builder(pageContext),
                ),
              ),
            ],
          );
        },
    transitionBuilder:
        (
          BuildContext context,
          Animation<double> animation,
          Animation<double> secondaryAnimation,
          Widget child,
        ) {
          final Animation<double> curved = CurvedAnimation(
            parent: animation,
            curve: Curves.easeOutCubic,
            reverseCurve: Curves.easeInCubic,
          );
          return FadeTransition(
            opacity: curved,
            child: ScaleTransition(
              scale: Tween<double>(begin: 0.97, end: 1).animate(curved),
              child: child,
            ),
          );
        },
  );
}
