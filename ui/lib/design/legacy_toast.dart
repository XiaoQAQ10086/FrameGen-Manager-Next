import 'dart:async';

import 'package:material_ui/material_ui.dart';

import 'motion_tokens.dart';

/// Dark rounded notice that slides in at the bottom-right.
///
/// Replaces `SnackBar`, which stretched across the bottom of the window and
/// covered the list the user was reading.
abstract final class LegacyToast {
  static OverlayEntry? _current;

  static void show(BuildContext context, String message) {
    final OverlayState? overlay = Overlay.maybeOf(context);
    if (overlay == null) {
      return;
    }
    _current?.remove();
    _current = null;
    late final OverlayEntry entry;
    entry = OverlayEntry(
      builder: (BuildContext context) => _ToastView(
        message: message,
        onDone: () {
          if (_current == entry) {
            _current = null;
          }
          entry.remove();
        },
      ),
    );
    _current = entry;
    overlay.insert(entry);
  }
}

class _ToastView extends StatefulWidget {
  const _ToastView({required this.message, required this.onDone});

  final String message;
  final VoidCallback onDone;

  @override
  State<_ToastView> createState() => _ToastViewState();
}

class _ToastViewState extends State<_ToastView> {
  static const Duration _lifetime = Duration(seconds: 3);
  Timer? _timer;
  bool _visible = false;

  @override
  void initState() {
    super.initState();
    _timer = Timer(_lifetime, _close);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        setState(() => _visible = true);
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  Future<void> _close() async {
    _timer?.cancel();
    if (!mounted) {
      return;
    }
    setState(() => _visible = false);
    await Future<void>.delayed(MotionTokens.standard);
    if (mounted) {
      widget.onDone();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Positioned(
      right: 24,
      bottom: 24,
      child: IgnorePointer(
        child: AnimatedSlide(
          duration: MotionTokens.standard,
          curve: MotionTokens.enterCurve,
          offset: _visible ? Offset.zero : const Offset(0.15, 0),
          child: AnimatedOpacity(
            duration: MotionTokens.standard,
            opacity: _visible ? 1 : 0,
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 320),
              child: Material(
                color: const Color(0xF21B2434),
                elevation: 8,
                shadowColor: const Color(0x40101828),
                borderRadius: BorderRadius.circular(12),
                child: Padding(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 18,
                    vertical: 12,
                  ),
                  child: Text(
                    widget.message,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 13,
                      height: 1.4,
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
