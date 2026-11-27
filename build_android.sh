#!/usr/bin/env bash
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/build_common.sh"
run_unity_build "Android" "CostaLivre.Editor.CostaPlatformBuild.Android" "build_android.log"
echo "APK Android gerado em: $BUILDS_DIR/Android/CostaLivre2.apk"
