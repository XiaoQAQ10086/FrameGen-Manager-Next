import 'package:material_ui/material_ui.dart';

import '../../bridge/core_library.dart';
import '../../design/app_tokens.dart';
import '../../l10n/app_localizations.dart';

/// First migrated page: mirror of the existing frame-generation workspace.
class FrameGenPage extends StatelessWidget {
  const FrameGenPage({super.key, this.core});

  final CoreLibrary? core;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);

    return Padding(
      padding: AppTokens.pagePadding,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Row(
            children: <Widget>[
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      l10n.navFrameGeneration,
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      l10n.rewriteTitle,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
              _PhaseChip(label: l10n.phaseBadge),
            ],
          ),
          const SizedBox(height: 20),
          Expanded(
            child: LayoutBuilder(
              builder: (BuildContext context, BoxConstraints constraints) {
                final bool wide = constraints.maxWidth >= 900;
                final Widget sidebar = Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    _CoreCard(core: core),
                    const SizedBox(height: AppTokens.gap),
                    _MigrationCard(l10n: l10n),
                  ],
                );

                if (!wide) {
                  return SingleChildScrollView(
                    child: Column(
                      children: <Widget>[
                        sidebar,
                        const SizedBox(height: AppTokens.gap),
                        SizedBox(
                          height: 360,
                          child: _LibraryCard(l10n: l10n),
                        ),
                      ],
                    ),
                  );
                }

                return Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: <Widget>[
                    SizedBox(width: 320, child: sidebar),
                    const SizedBox(width: AppTokens.gap),
                    Expanded(child: _LibraryCard(l10n: l10n)),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _CoreCard extends StatelessWidget {
  const _CoreCard({this.core});

  final CoreLibrary? core;

  @override
  Widget build(BuildContext context) {
    final AppLocalizations l10n = AppLocalizations.of(context);
    final bool online = core != null;
    final Color tone = online ? AppTokens.success : AppTokens.warning;
    final Color soft = online ? AppTokens.successSoft : AppTokens.warningSoft;

    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Row(
            children: <Widget>[
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: soft,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(
                  online ? Icons.check_circle_outline : Icons.cloud_off,
                  color: tone,
                  size: 21,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: <Widget>[
                    Text(
                      online ? l10n.coreConnected : l10n.coreOffline,
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 2),
                    Text(
                      online
                          ? l10n.coreVersion(core!.version)
                          : l10n.coreOfflineHint,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
            ],
          ),
          if (online) ...<Widget>[
            const SizedBox(height: 16),
            Text(
              l10n.rewriteBody,
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ],
      ),
    );
  }
}

class _MigrationCard extends StatelessWidget {
  const _MigrationCard({required this.l10n});

  final AppLocalizations l10n;

  @override
  Widget build(BuildContext context) {
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            l10n.migrationTitle,
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 14),
          _MigrationRow(
            label: l10n.migrationDomain,
            status: l10n.statusNext,
            tone: _ChipTone.accent,
          ),
          _MigrationRow(
            label: l10n.migrationCore,
            status: l10n.statusPlanned,
            tone: _ChipTone.neutral,
          ),
          _MigrationRow(
            label: l10n.migrationInfra,
            status: l10n.statusPlanned,
            tone: _ChipTone.neutral,
          ),
          _MigrationRow(
            label: l10n.migrationUi,
            status: l10n.statusDone,
            tone: _ChipTone.success,
            last: true,
          ),
        ],
      ),
    );
  }
}

class _MigrationRow extends StatelessWidget {
  const _MigrationRow({
    required this.label,
    required this.status,
    required this.tone,
    this.last = false,
  });

  final String label;
  final String status;
  final _ChipTone tone;
  final bool last;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: last ? 0 : 10),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Text(
              label,
              style: const TextStyle(
                color: AppTokens.textPrimary,
                fontSize: 13,
              ),
            ),
          ),
          _Chip(label: status, tone: tone),
        ],
      ),
    );
  }
}

class _LibraryCard extends StatelessWidget {
  const _LibraryCard({required this.l10n});

  final AppLocalizations l10n;

  @override
  Widget build(BuildContext context) {
    return _Panel(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Padding(
            padding: const EdgeInsets.fromLTRB(22, 18, 22, 16),
            child: Row(
              children: <Widget>[
                Expanded(
                  child: Text(
                    l10n.gameLibraryTitle,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                _PhaseChip(label: l10n.phaseLibraryBadge),
              ],
            ),
          ),
          const Divider(height: 1),
          Expanded(
            child: Center(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: <Widget>[
                    Container(
                      width: 64,
                      height: 64,
                      decoration: const BoxDecoration(
                        color: AppTokens.surfaceMuted,
                        shape: BoxShape.circle,
                      ),
                      child: const Icon(
                        Icons.sports_esports_outlined,
                        color: AppTokens.textSecondary,
                        size: 28,
                      ),
                    ),
                    const SizedBox(height: 16),
                    Text(
                      l10n.gameLibraryEmpty,
                      textAlign: TextAlign.center,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Panel extends StatelessWidget {
  const _Panel({required this.child, this.padding});

  final Widget child;
  final EdgeInsets? padding;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: padding ?? const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: AppTokens.surface,
        borderRadius: BorderRadius.circular(AppTokens.radiusLarge),
        border: Border.all(color: AppTokens.border),
        boxShadow: AppTokens.panelShadow,
      ),
      child: child,
    );
  }
}

class _PhaseChip extends StatelessWidget {
  const _PhaseChip({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: AppTokens.accentSoft,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: AppTokens.accent,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

enum _ChipTone { accent, success, neutral }

class _Chip extends StatelessWidget {
  const _Chip({required this.label, required this.tone});

  final String label;
  final _ChipTone tone;

  @override
  Widget build(BuildContext context) {
    final (Color foreground, Color background) = switch (tone) {
      _ChipTone.accent => (AppTokens.accent, AppTokens.accentSoft),
      _ChipTone.success => (AppTokens.success, AppTokens.successSoft),
      _ChipTone.neutral => (AppTokens.textSecondary, AppTokens.surfaceMuted),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 4),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: foreground,
          fontSize: 11,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}
