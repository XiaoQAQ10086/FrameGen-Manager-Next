import 'package:material_ui/material_ui.dart';

import '../../bridge/deploy_dialog_result.dart';
import '../../bridge/deploy_options.dart';
import '../../bridge/library_bridge.dart';
import '../../bridge/library_game.dart';
import '../../bridge/proxy_badge.dart';
import '../../design/app_tokens.dart';
import '../../design/legacy_button_styles.dart';
import '../../design/legacy_pill.dart';
import '../../l10n/app_localizations.dart';
import 'deploy_dialog_header.dart';
import 'deploy_options_form.dart';

/// Legacy frame-generation deploy modal.
class FrameGenDeployDialog extends StatefulWidget {
  const FrameGenDeployDialog({
    super.key,
    required this.game,
    this.deployed = false,
    this.proxies = const <ProxyBadge>[],
    this.coverPath,
    this.initial = const DeployOptions(),
  });

  final LibraryGame game;
  final bool deployed;
  final List<ProxyBadge> proxies;
  final String? coverPath;
  final DeployOptions initial;

  @override
  State<FrameGenDeployDialog> createState() => _FrameGenDeployDialogState();
}

class _FrameGenDeployDialogState extends State<FrameGenDeployDialog> {
  late DeployOptions _options = widget.initial;
  late String? _gameExe = widget.game.renderExe;
  bool _exeChanged = false;

  Future<void> _pickExe() async {
    final String? path = await LibraryBridge.pickExecutable();
    if (path == null || !mounted) {
      return;
    }
    setState(() {
      _gameExe = path;
      _exeChanged = true;
    });
  }

  void _update(DeployOptions options) => setState(() => _options = options);

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    return Dialog(
      backgroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(20),
        side: const BorderSide(color: Color(0x1F14233C)),
      ),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 660, maxHeight: 680),
        child: Padding(
          padding: const EdgeInsets.all(22),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: <Widget>[
              DeployDialogHeader(
                game: widget.game,
                coverPath: widget.coverPath,
                onClose: () => Navigator.pop(context),
              ),
              const SizedBox(height: 14),
              Flexible(
                child: SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: <Widget>[
                      _targetRow(context, l10n),
                      const SizedBox(height: 8),
                      Row(
                        children: <Widget>[
                          Text(
                            l10n.deployGraphicsApi,
                            style: const TextStyle(
                              color: AppTokens.textSecondary,
                              fontSize: 12.5,
                            ),
                          ),
                          const SizedBox(width: 8),
                          LegacyPill(label: widget.game.graphicsApi),
                        ],
                      ),
                      const SizedBox(height: 12),
                      Text(
                        l10n.deployConfirmBody(widget.game.name),
                        style: Theme.of(context).textTheme.bodyMedium,
                      ),
                      const SizedBox(height: 14),
                      DeployOptionsForm(
                        options: _options,
                        proxies: widget.proxies,
                        onChanged: _update,
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 14),
              Row(
                children: <Widget>[
                  Text(
                    l10n.deployCloseGameHint,
                    style: const TextStyle(
                      color: AppTokens.textSecondary,
                      fontSize: 12,
                    ),
                  ),
                  const Spacer(),
                  OutlinedButton(
                    onPressed: widget.deployed
                        ? () => Navigator.pop(
                            context,
                            const DeployDialogResult.restore(),
                          )
                        : null,
                    child: Text(l10n.restore),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    onPressed: () => Navigator.pop(
                      context,
                      DeployDialogResult.deploy(
                        _options.copyWith(
                          gameExe: _gameExe,
                          persistExe: _exeChanged,
                        ),
                      ),
                    ),
                    style: LegacyButtonStyles.success,
                    child: Text(l10n.deploy),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _targetRow(BuildContext context, AppLocalizations l10n) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            Text(
              l10n.deployTargetLabel,
              style: const TextStyle(
                color: AppTokens.textSecondary,
                fontSize: 12.5,
              ),
            ),
            const Spacer(),
            TextButton(
              onPressed: _pickExe,
              child: Text(l10n.deployChooseExe),
            ),
          ],
        ),
        Container(
          width: double.infinity,
          height: 34,
          alignment: Alignment.centerLeft,
          padding: const EdgeInsets.symmetric(horizontal: 11),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: <Color>[Color(0xFFFFFFFF), Color(0xFFF4F7FB)],
            ),
            borderRadius: BorderRadius.circular(AppTokens.radiusSmall),
            border: Border.all(color: const Color(0x2E14233C)),
          ),
          child: Text(
            _gameExe ?? widget.game.targetDirectory,
            style: const TextStyle(
              color: AppTokens.textPrimary,
              fontFamily: 'Consolas',
              fontSize: 12,
            ),
          ),
        ),
      ],
    );
  }
}
