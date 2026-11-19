import 'dart:convert';

import 'app_settings.dart';
import 'core_library.dart';

/// Loads and saves app settings through the FFI bridge.
final class SettingsBridge {
  const SettingsBridge._();

  static AppSettings load(CoreLibrary? core) {
    if (core == null) {
      return const AppSettings();
    }
    final String? reply = core.settingsGetJson();
    if (reply == null) {
      return const AppSettings();
    }
    try {
      final Object? decoded = jsonDecode(reply);
      if (decoded is! Map || decoded['ok'] != true) {
        return const AppSettings();
      }
      final Object? settings = decoded['settings'];
      if (settings is Map) {
        return AppSettings.fromJson(Map<String, Object?>.from(settings));
      }
    } on FormatException {
      return const AppSettings();
    }
    return const AppSettings();
  }

  static bool save(AppSettings settings, CoreLibrary? core) {
    if (core == null) {
      return false;
    }
    final String? reply = core.settingsSetJson(jsonEncode(settings.toJson()));
    if (reply == null) {
      return false;
    }
    try {
      final Object? decoded = jsonDecode(reply);
      return decoded is Map && decoded['ok'] == true;
    } on FormatException {
      return false;
    }
  }
}
