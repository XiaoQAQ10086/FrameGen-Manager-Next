import 'dart:convert';

import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../bridge/diagnostics_result.dart';
import '../../bridge/logs_bridge.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import '../../design/error_dialog.dart';
import '../../design/legacy_toast.dart';
import '../../l10n/app_localizations.dart';

/// Formats one JSON log line as `time LEVEL message key=value ...`.
String _formatLine(String line) {
  try {
    final Object? decoded = jsonDecode(line);
    if (decoded is! Map) {
      return line;
    }
    final int ts = (decoded['ts'] as num?)?.toInt() ?? 0;
    final DateTime time = DateTime.fromMillisecondsSinceEpoch(ts);
    final String stamp = '${time.hour.toString().padLeft(2, '0')}:'
        '${time.minute.toString().padLeft(2, '0')}:'
        '${time.second.toString().padLeft(2, '0')}';
    final String level = (decoded['level'] as String? ?? 'info').toUpperCase();
    final String message = decoded['message'] as String? ?? '';
    final Map<Object?, Object?> fields =
        (decoded['fields'] as Map<Object?, Object?>?) ?? <Object?, Object?>{};
    final String detail = fields.entries
        .map((MapEntry<Object?, Object?> entry) => '${entry.key}=${entry.value}')
        .join('  ');
    return '$stamp  ${level.padRight(5)}  $message'
        '${detail.isEmpty ? '' : '  $detail'}';
  } on FormatException {
    return line;
  }
}

/// Legacy logs page: page header with folder/clear/export and the log box.
class LogsPage extends StatefulWidget {
  const LogsPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  State<LogsPage> createState() => _LogsPageState();
}

class _LogsPageState extends State<LogsPage> {
  List<String> _lines = const <String>[];
  bool _exporting = false;
  bool _clearing = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  void _load() {
    final List<String> lines = LogsBridge.tail(widget.core, maxLines: 800);
    setState(() => _lines = lines);
  }

  Future<void> _clear() async {
    final AppLocalizations l10n = AppLocalizations.of(context);
    setState(() => _clearing = true);
    final int? removed = await LogsBridge.clear();
    if (!mounted) {
      return;
    }
    setState(() => _clearing = false);
    if (removed == null) {
      await ErrorDialog.show(
        context,
        message: l10n.diagnosticsFailed,
        code: 'LogsClearFailed',
      );
      return;
    }
    _load();
    LegacyToast.show(
      context,
      removed > 0 ? l10n.logsCleared(removed) : l10n.logsNothingCleared,
    );
  }

  Future<void> _export() async {
    setState(() => _exporting = true);
    final DiagnosticsResult result = await LogsBridge.exportDiagnostics();
    if (!mounted) {
      return;
    }
    setState(() => _exporting = false);
    if (result.cancelled) {
      return;
    }
    final AppLocalizations l10n = AppLocalizations.of(context);
    if (!result.ok || result.path == null) {
      await ErrorDialog.show(
        context,
        message: l10n.diagnosticsFailed,
        code: result.code ?? 'DiagnosticsExportFailed',
      );
      return;
    }
    LegacyToast.show(context, l10n.diagnosticsSaved(result.path!));
  }

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.navLogs,
                      style: const TextStyle(
                        color: AppTokens.textPrimary,
                        fontSize: 28,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 5),
                    Text(
                      l10n.logsSubtitle,
                      style: const TextStyle(
                        color: AppTokens.textSecondary,
                        fontSize: 14,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Wrap(
                spacing: 8,
                children: <Widget>[
                  OutlinedButton(
                    onPressed: () => SystemBridge.openFolder('logs'),
                    child: Text(l10n.logsOpenFolder),
                  ),
                  OutlinedButton(
                    onPressed: _clearing ? null : _clear,
                    child: Text(l10n.logsClear),
                  ),
                  FilledButton(
                    onPressed: _exporting ? null : _export,
                    child: Text(l10n.diagnosticsExport),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 20),
          Expanded(
            child: Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: AppTokens.surfaceSolid,
                borderRadius: BorderRadius.circular(AppTokens.radiusMedium),
                border: Border.all(color: AppTokens.border),
              ),
              child: _lines.isEmpty
                  ? Center(
                      child: Text(
                        l10n.logsEmpty,
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                    )
                  : Scrollbar(
                      child: SingleChildScrollView(
                        child: SelectableText(
                          _lines.map(_formatLine).join('\n'),
                          style: const TextStyle(
                            fontFamily: 'Consolas',
                            fontSize: 12,
                            height: 1.65,
                            color: AppTokens.textPrimary,
                          ),
                        ),
                      ),
                    ),
            ),
          ),
        ],
      ),
    );
  }
}
