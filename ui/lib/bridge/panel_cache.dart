import 'gpu_info.dart';
import 'gpu_spoof_state.dart';
import 'system_paths.dart';

/// Last successful sidebar lookups, kept for the life of the process.
///
/// Every navigation rebuilds the page from scratch, so without this the asset,
/// spoofer and GPU cards drop back to their loading state on each switch even
/// though nothing about them changed.
abstract final class PanelCache {
  static SystemPaths? paths;
  static GpuInfo? gpu;
  static GpuSpoofState? spoof;
}
