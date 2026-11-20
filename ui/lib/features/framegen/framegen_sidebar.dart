import 'package:material_ui/material_ui.dart';

import '../../bridge/gpu_info.dart';
import '../../bridge/system_bridge.dart';
import '../../design/app_tokens.dart';
import 'assets_panel.dart';
import 'gpu_panel.dart';
import 'mode_source_panel.dart';
import 'spoof_panel.dart';

/// Left-hand GPU/asset column of the frame-generation workspace.
class FrameGenSidebar extends StatelessWidget {
  const FrameGenSidebar({super.key, this.gpu, required this.gpuLabel});

  final GpuInfo? gpu;
  final String gpuLabel;

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          GpuPanel(gpu: gpu, onOpenHags: () => SystemBridge.openHags()),
          const SizedBox(height: AppTokens.gap),
          const SpoofPanel(),
          const SizedBox(height: AppTokens.gap),
          const AssetsPanel(),
          const SizedBox(height: AppTokens.gap),
          ModeSourcePanel(
            gpuLabel: gpuLabel,
            sourceUrl: 'https://github.com/sdli1995/dlssg_for_sm86',
          ),
        ],
      ),
    );
  }
}
