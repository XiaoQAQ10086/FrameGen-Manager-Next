import 'package:material_ui/material_ui.dart';

import 'app/app.dart';
import 'bridge/core_library.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(FgmApp(core: CoreLibrary.tryOpen()));
}
