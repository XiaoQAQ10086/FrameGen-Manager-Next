/// NVIDIA GPU information returned by `fgm_gpu_json`.
final class GpuInfo {
  const GpuInfo({
    required this.name,
    required this.driverVersion,
    required this.route,
    required this.driverSupported,
  });

  factory GpuInfo.fromJson(Map<String, Object?> json) {
    return GpuInfo(
      name: json['name'] as String? ?? '',
      driverVersion: json['driverVersion'] as String? ?? '',
      route: json['route'] as String? ?? 'unknown',
      driverSupported: json['driverSupported'] == true,
    );
  }

  final String name;
  final String driverVersion;
  final String route;
  final bool driverSupported;
}
