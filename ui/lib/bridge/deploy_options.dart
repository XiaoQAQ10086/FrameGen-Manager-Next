/// Frame-generation deploy options mirrored into `dlssg_sm86.ini`.
final class DeployOptions {
  const DeployOptions({
    this.proxySlot = 'version.dll',
    this.engine = 'DLSS',
    this.maxGeneratedFrames = 3,
    this.optimized = 1,
    this.preset = 'A',
    this.superResolution = 'Off',
  });

  final String proxySlot;
  final String engine;
  final int maxGeneratedFrames;
  final int optimized;
  final String preset;
  final String superResolution;

  List<Map<String, String>> iniOverrides() => <Map<String, String>>[
    <String, String>{
      'section': 'FrameGeneration',
      'key': 'Engine',
      'value': engine,
    },
    <String, String>{
      'section': 'FrameGeneration',
      'key': 'MaxGeneratedFrames',
      'value': '$maxGeneratedFrames',
    },
    <String, String>{
      'section': 'DLSSG',
      'key': 'Optimized',
      'value': '$optimized',
    },
    <String, String>{'section': 'DLSSG', 'key': 'Preset', 'value': preset},
    <String, String>{
      'section': 'SuperResolution',
      'key': 'Mode',
      'value': superResolution,
    },
  ];

  DeployOptions copyWith({
    String? proxySlot,
    String? engine,
    int? maxGeneratedFrames,
    int? optimized,
    String? preset,
    String? superResolution,
  }) {
    return DeployOptions(
      proxySlot: proxySlot ?? this.proxySlot,
      engine: engine ?? this.engine,
      maxGeneratedFrames: maxGeneratedFrames ?? this.maxGeneratedFrames,
      optimized: optimized ?? this.optimized,
      preset: preset ?? this.preset,
      superResolution: superResolution ?? this.superResolution,
    );
  }
}
