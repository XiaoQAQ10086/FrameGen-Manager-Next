import 'dart:ui' show ImageFilter;

import 'package:flutter/services.dart' show LogicalKeyboardKey;
import 'package:material_ui/material_ui.dart';

/// Shows a modal dialog with the page behind it smoothly blurred and dimmed.
///
/// The blur radius and dim alpha animate together with the dialog, matching the
/// legacy WebView modal instead of snapping to a pre-blurred backdrop.
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
          final Animation<double> curved = CurvedAnimation(
            parent: animation,
            curve: Curves.easeOutCubic,
            reverseCurve: Curves.easeInCubic,
          );
          return Stack(
            fit: StackFit.expand,
            children: <Widget>[
              _BlurBackdrop(
                animation: curved,
                dismissible: barrierDismissible,
                onDismiss: () => Navigator.of(pageContext).maybePop(),
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
                  child: FadeTransition(
                    opacity: curved,
                    child: ScaleTransition(
                      scale: Tween<double>(begin: 0.97, end: 1).animate(curved),
                      child: builder(pageContext),
                    ),
                  ),
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
        ) => child,
  );
}

/// Backdrop that ramps blur and dim from zero with the dialog animation.
class _BlurBackdrop extends StatelessWidget {
  const _BlurBackdrop({
    required this.animation,
    required this.dismissible,
    required this.onDismiss,
  });

  final Animation<double> animation;
  final bool dismissible;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: animation,
      builder: (BuildContext context, Widget? child) {
        final double t = animation.value.clamp(0.0, 1.0);
        return GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: dismissible ? onDismiss : null,
          child: BackdropFilter(
            filter: ImageFilter.blur(sigmaX: 5 * t, sigmaY: 5 * t),
            child: ColoredBox(
              color: Color.fromRGBO(16, 24, 40, 0.04 * t),
            ),
          ),
        );
      },
    );
  }
}
