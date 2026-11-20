import 'package:fgm_ui/bridge/library_game.dart';
import 'package:fgm_ui/features/framegen/deploy_options_form.dart';
import 'package:fgm_ui/features/framegen/framegen_deploy_dialog.dart';
import 'package:fgm_ui/l10n/app_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';

/// Pins the legacy deploy dialog layout: one labelled row per option, with
/// every control starting at the same x.
void main() {
  const LibraryGame game = LibraryGame(
    id: 'steam:1',
    name: 'Test Game',
    source: 'steam',
    installDirectory: r'D:\Games\Test',
    targetDirectory: r'D:\Games\Test',
    graphicsApi: 'dx12',
    renderExe: r'D:\Games\Test\Game.exe',
  );

  Future<void> pumpDialog(WidgetTester tester) async {
    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const MaterialApp(
        locale: Locale('en'),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          body: Center(child: FrameGenDeployDialog(game: game)),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('shows the game header and every option', (
    WidgetTester tester,
  ) async {
    await pumpDialog(tester);

    expect(find.text('Test Game'), findsOneWidget);
    expect(find.text('Graphics API'), findsOneWidget);
    expect(find.byType(DeployOptionsForm), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('stacks the options in a single labelled column', (
    WidgetTester tester,
  ) async {
    await pumpDialog(tester);

    final Rect proxy = tester.getRect(find.text('Proxy entry'));
    final Rect engine = tester.getRect(find.text('Frame engine'));

    expect(proxy.left, engine.left);
    expect(proxy.top, lessThan(engine.top));
    expect(tester.takeException(), isNull);
  });
}
