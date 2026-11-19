import 'dart:ui';

import 'package:fgm_ui/app/app.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:material_ui/material_ui.dart';

void main() {
  testWidgets('renders the legacy shell in English', (WidgetTester tester) async {
    tester.view.physicalSize = const Size(1280, 850);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const FgmApp(locale: Locale('en')),
    );
    await tester.pumpAndSettle();

    expect(find.text('FrameGen Manager-Next'), findsOneWidget);
    expect(find.text('DLSS-G Frame Generation'), findsOneWidget);
    expect(find.text('Add game'), findsOneWidget);
    expect(find.text('Scan games'), findsWidgets);
    expect(find.textContaining('Rust core'), findsNothing);
  });

  testWidgets('renders the legacy shell in Chinese', (WidgetTester tester) async {
    tester.view.physicalSize = const Size(1280, 850);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      const FgmApp(locale: Locale('zh')),
    );
    await tester.pumpAndSettle();

    expect(find.text('DLSS-G 帧生成'), findsWidgets);
    expect(find.text('添加游戏'), findsOneWidget);
    expect(find.textContaining('Rust 核心'), findsNothing);
  });
}
