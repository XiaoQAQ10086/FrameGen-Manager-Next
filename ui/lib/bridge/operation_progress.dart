import 'dart:convert';

/// Snapshot of the native operation progress counters.
final class OperationProgress {
  const OperationProgress({
    this.active = false,
    this.kind,
    this.done = 0,
    this.total = 0,
    this.cancelled = false,
  });

  factory OperationProgress.parse(String? reply) {
    if (reply == null) {
      return const OperationProgress();
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return const OperationProgress();
      }
      return OperationProgress(
        active: decoded['active'] == true,
        kind: decoded['kind'] as String?,
        done: decoded['done'] as int? ?? 0,
        total: decoded['total'] as int? ?? 0,
        cancelled: decoded['cancelled'] == true,
      );
    } on FormatException {
      return const OperationProgress();
    }
  }

  final bool active;
  final String? kind;
  final int done;
  final int total;
  final bool cancelled;

  double? get fraction =>
      total > 0 ? (done / total).clamp(0.0, 1.0).toDouble() : null;
}
