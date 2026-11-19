/// One game's Smooth Motion driver state.
final class SmoothMotionStatus {
  const SmoothMotionStatus({
    this.enabled,
    this.profiles = const <String>[],
    this.ours = false,
    this.code,
    this.detail,
  });

  factory SmoothMotionStatus.fromJson(Map<String, Object?> json, {String? code}) {
    return SmoothMotionStatus(
      enabled: json['enabled'] as bool?,
      profiles: (json['profiles'] as List<Object?>? ?? <Object?>[])
          .map((Object? value) => value.toString())
          .toList(growable: false),
      ours: json['ours'] == true,
      code: code,
      detail: json['detail'] as String?,
    );
  }

  final bool? enabled;
  final List<String> profiles;
  final bool ours;
  final String? code;
  final String? detail;

  SmoothMotionStatus withCode(String value) => SmoothMotionStatus(
    enabled: enabled,
    profiles: profiles,
    ours: ours,
    code: value,
    detail: detail,
  );
}
