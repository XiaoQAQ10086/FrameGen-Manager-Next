import 'dart:async';

import 'package:material_ui/material_ui.dart';

import '../bridge/core_library.dart';
import '../bridge/operation_progress.dart';
import '../l10n/app_localizations.dart';

/// Non-blocking progress card that sits in the bottom-left corner, letting the
/// user keep interacting with the page while a native operation runs.
final class FloatingProgress {
  const FloatingProgress._();

  static Future<T?> run<T>({
    required BuildContext context,
    required String title,
    required Future<T> Function() action,
    bool cancellable = true,
  }) {
    final OverlayState? overlay = Overlay.maybeOf(context);
    if (overlay == null) {
      return action();
    }
    final Completer<T?> completer = Completer<T?>();
    late OverlayEntry entry;
    entry = OverlayEntry(
      builder: (BuildContext context) => _FloatingCard<T>(
        title: title,
        action: action,
        cancellable: cancellable,
        onDone: (T? value) {
          if (entry.mounted) {
            entry.remove();
          }
          if (!completer.isCompleted) {
            completer.complete(value);
          }
        },
      ),
    );
    overlay.insert(entry);
    return completer.future;
  }
}

class _FloatingCard<T> extends StatefulWidget {
  const _FloatingCard({
    required this.title,
    required this.action,
    required this.cancellable,
    required this.onDone,
  });

  final String title;
  final Future<T> Function() action;
  final bool cancellable;
  final ValueChanged<T?> onDone;

  @override
  State<_FloatingCard<T>> createState() => _FloatingCardState<T>();
}

class _FloatingCardState<T> extends State<_FloatingCard<T>> {
  Timer? _timer;
  OperationProgress _progress = const OperationProgress(active: true);
  bool _cancelling = false;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(
      const Duration(milliseconds: 200),
      (_) => _poll(),
    );
    _run();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  void _poll() {
    final CoreLibrary? core = CoreLibrary.shared();
    final OperationProgress progress = OperationProgress.parse(
      core?.operationProgressJson(),
    );
    if (mounted) {
      setState(() => _progress = progress);
    }
  }

  Future<void> _run() async {
    try {
      final T result = await widget.action();
      _timer?.cancel();
      widget.onDone(result);
    } catch (_) {
      _timer?.cancel();
      widget.onDone(null);
    }
  }

  void _cancel() {
    if (_cancelling) {
      return;
    }
    CoreLibrary.shared()?.operationCancelJson();
    setState(() => _cancelling = true);
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final double? fraction = _progress.fraction;
    return Positioned(
      left: 22,
      bottom: 22,
      width: 340,
      child: Material(
        color: Colors.white,
        elevation: 12,
        shadowColor: const Color(0x33101828),
        borderRadius: BorderRadius.circular(14),
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: const Color(0xFFDCE5F2)),
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  Expanded(
                    child: Text(
                      widget.title,
                      style: const TextStyle(
                        color: Color(0xFF172033),
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (widget.cancellable)
                    TextButton(
                      onPressed: _cancelling ? null : _cancel,
                      child: Text(
                        _cancelling
                            ? l10n.operationCancelling
                            : l10n.cancel,
                      ),
                    ),
                ],
              ),
              const SizedBox(height: 8),
              ClipRRect(
                borderRadius: BorderRadius.circular(3),
                child: LinearProgressIndicator(value: fraction, minHeight: 5),
              ),
              const SizedBox(height: 6),
              Text(
                _progress.total > 0
                    ? '${_progress.done} / ${_progress.total}'
                    : '…',
                style: const TextStyle(
                  color: Color(0xFF606B7D),
                  fontSize: 11.5,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
