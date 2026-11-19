/// Persisted app settings mirrored from `config/app.json`.
final class AppSettings {
  const AppSettings({
    this.language = 'system',
    this.autoScanOnStartup = false,
    this.showCovers = true,
    this.autoInstallReframework = true,
  });

  factory AppSettings.fromJson(Map<String, Object?> json) {
    return AppSettings(
      language: json['language'] as String? ?? 'system',
      autoScanOnStartup: json['autoScanOnStartup'] == true,
      showCovers: json['showCovers'] != false,
      autoInstallReframework: json['autoInstallReframework'] != false,
    );
  }

  final String language;
  final bool autoScanOnStartup;
  final bool showCovers;
  final bool autoInstallReframework;

  Map<String, Object?> toJson() => <String, Object?>{
    'schemaVersion': 3,
    'language': language,
    'theme': 'light',
    'autoScanOnStartup': autoScanOnStartup,
    'showCovers': showCovers,
    'autoInstallReframework': autoInstallReframework,
  };

  AppSettings copyWith({
    String? language,
    bool? autoScanOnStartup,
    bool? showCovers,
    bool? autoInstallReframework,
  }) {
    return AppSettings(
      language: language ?? this.language,
      autoScanOnStartup: autoScanOnStartup ?? this.autoScanOnStartup,
      showCovers: showCovers ?? this.showCovers,
      autoInstallReframework:
          autoInstallReframework ?? this.autoInstallReframework,
    );
  }
}
