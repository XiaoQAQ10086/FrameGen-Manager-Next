import 'dart:ui' show ImageFilter;

import 'package:flutter/services.dart' show LogicalKeyboardKey;
import 'package:material_ui/material_ui.dart';

import 'motion_tokens.dart';

/// Shows a modal dialog with the page behind it blurred and dimmed.
///
/// The backdrop blur is a fixed radius that fades in with the dialog, matching
/// the legacy WebView modal without re-filtering the page every frame.
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
    transitionDuration: MotionTokens.enter,
    pageBuilder:
        (
          BuildContext pageContext,
          Animation<double> animation,
          Animation<double> secondaryAnimation,
        ) {
          final Animation<double> curved = CurvedAnimation(
            parent: animation,
            curve: MotionTokens.enterCurve,
            reverseCurve: MotionTokens.exitCurve,
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

/// Backdrop that fades in already blurred.
///
/// The blur radius is fixed: animating sigma re-ran the filter every frame and
/// dropped frames on weaker GPUs. Fading a pre-blurred, repaint-isolated layer
/// costs one blur per dialog instead of one per frame.
class _BlurBackdrop extends StatelessWidget {
  const _BlurBackdrop({
    required this.animation,
    required this.dismissible,
    required this.onDismiss,
  });

  static const double _sigma = 6;

  final Animation<double> animation;
  final bool dismissible;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: dismissible ? onDismiss : null,
      child: RepaintBoundary(
        child: FadeTransition(
          opacity: animation,
          child: BackdropFilter(
            filter: ImageFilter.blur(sigmaX: _sigma, sigmaY: _sigma),
            child: const ColoredBox(color: Color.fromRGBO(16, 24, 40, 0.10)),
          ),
        ),
      ),
    );
  }
}
