import 'dart:async';

import 'package:material_ui/material_ui.dart';

import '../bridge/core_library.dart';
import '../bridge/operation_progress.dart';
import '../l10n/app_localizations.dart';

/// Modal progress dialog for long native operations, with cancel support.
final class OperationProgressDialog {
  const OperationProgressDialog._();

  static Future<T?> run<T>({
    required BuildContext context,
    required String title,
    required Future<T> Function() action,
    bool cancellable = true,
  }) {
    return showDialog<T>(
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
    if (!mounted) {
      return;
    }
    setState(() => _progress = progress);
  }

  Future<void> _run() async {
    final T result = await widget.action();
    _timer?.cancel();
    if (!mounted) {
      return;
    }
    Navigator.of(context).pop(result);
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
            LinearProgressIndicator(value: fraction, minHeight: 4),
            const SizedBox(height: 10),
            Text(
              _progress.total > 0
                  ? '${_progress.done} / ${_progress.total}'
                  : '…',
              style: Theme.of(context).textTheme.labelMedium,
            ),
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
}
