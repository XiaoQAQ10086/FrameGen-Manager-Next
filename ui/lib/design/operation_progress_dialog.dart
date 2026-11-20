import 'dart:async';

import 'package:material_ui/material_ui.dart';

import '../bridge/core_library.dart';
import '../bridge/operation_progress.dart';
import '../l10n/app_localizations.dart';
import 'legacy_dialog.dart';

/// Modal progress dialog for long native operations, with cancel support.
final class OperationProgressDialog {
  const OperationProgressDialog._();

  static Future<T?> run<T>({
    required BuildContext context,
    required String title,
    required Future<T> Function() action,
    bool cancellable = true,
  }) {
    return showLegacyDialog<T>(
      context: context,
      barrierDismissible: false,
      builder: (BuildContext context) => _OperationDialog<T>(
        title: title,
        action: action,
        cancellable: cancellable,
      ),
    );
  }
}

class _OperationDialog<T> extends StatefulWidget {
  const _OperationDialog({
    required this.title,
    required this.action,
    required this.cancellable,
  });

  final String title;
  final Future<T> Function() action;
  final bool cancellable;

  @override
  State<_OperationDialog<T>> createState() => _OperationDialogState<T>();
}

class _OperationDialogState<T> extends State<_OperationDialog<T>> {
  Timer? _timer;
  OperationProgress _progress = const OperationProgress(active: true);
  bool _cancelling = false;
  int _lastDone = 0;
  Duration _lastSample = Duration.zero;
  double _bytesPerSecond = 0;

  /// Downloads count bytes; deploy-style operations count files.
  bool get _isDownload =>
      _progress.kind == 'anrDownload' || _progress.kind == 'updateDownload';

  @override
  void initState() {
    super.initState();
    _lastSample = _now();
    _timer = Timer.periodic(
      const Duration(milliseconds: 200),
      (_) => _poll(),
    );
    _run();
  }

  static Duration _now() =>
      Duration(milliseconds: DateTime.now().millisecondsSinceEpoch);

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
    if (!mounted) {
      return;
    }
    final Duration now = _now();
    final double seconds =
        (now - _lastSample).inMilliseconds / Duration.millisecondsPerSecond;
    if (seconds > 0.05) {
      final int delta = progress.done - _lastDone;
      _bytesPerSecond = delta >= 0 ? delta / seconds : 0;
      _lastDone = progress.done;
      _lastSample = now;
    }
    setState(() => _progress = progress);
  }

  Future<void> _run() async {
    T? result;
    try {
      result = await widget.action();
    } finally {
      _timer?.cancel();
      if (mounted) {
        Navigator.of(context).pop(result);
      }
    }
  }

  void _cancel() {
    if (_cancelling) {
      return;
    }
    final CoreLibrary? core = CoreLibrary.shared();
    core?.operationCancelJson();
    setState(() => _cancelling = true);
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final double? fraction = _progress.fraction;
    return AlertDialog(
      title: Text(widget.title),
      content: SizedBox(
        width: 360,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            LinearProgressIndicator(value: fraction, minHeight: 6),
            const SizedBox(height: 10),
            Text(
              _detail(),
              style: Theme.of(context).textTheme.labelMedium,
            ),
            if (_isDownload && _progress.done > 0) ...<Widget>[
              const SizedBox(height: 2),
              Text(
                '${_formatBytes(_bytesPerSecond.round())} / s',
                style: Theme.of(context).textTheme.labelSmall,
              ),
            ],
          ],
        ),
      ),
      actions: widget.cancellable
          ? <Widget>[
              TextButton(
                onPressed: _cancelling ? null : _cancel,
                child: Text(
                  _cancelling ? l10n.operationCancelling : l10n.cancel,
                ),
              ),
            ]
          : null,
    );
  }

  String _detail() {
    if (_progress.total <= 0) {
      return _progress.done > 0 ? _formatBytes(_progress.done) : '…';
    }
    // The first bytes can take twenty seconds to arrive while the connection
    // is negotiated; showing "0 B / 11.2 MB" the whole time reads as frozen.
    if (_isDownload && _progress.done <= 0) {
      return '…';
    }
    if (_isDownload) {
      return '${_formatBytes(_progress.done)} / ${_formatBytes(_progress.total)}';
    }
    return '${_progress.done} / ${_progress.total}';
  }
}

/// Byte counts for the download progress line; units are symbols, not prose.
String _formatBytes(int bytes) {
  const List<String> units = <String>['B', 'KB', 'MB', 'GB'];
  double value = bytes.toDouble();
  var index = 0;
  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index += 1;
  }
  return '${value.toStringAsFixed(index == 0 ? 0 : 1)} ${units[index]}';
}
